using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using PipServices.Aws.Connect;
using PipServices.Commons.Config;
using PipServices.Commons.Convert;
using PipServices.Commons.Refer;
using PipServices.Commons.Run;
using PipServices.Components.Count;
using PipServices.Components.Info;
using PipServices.Components.Log;

namespace PipServices.Aws.Count
{
    public class CloudWatchCounters : CachedCounters, IReferenceable, IOpenable
    {
        private CompositeLogger _logger = new CompositeLogger();
        private AwsConnectionResolver _connectionResolver = new AwsConnectionResolver();
        private bool _opened;
        private string _source;
        private string _instance;
        private AmazonCloudWatchClient _client;

        public CloudWatchCounters()
        { }

        public override void Configure(ConfigParams config)
        {
            base.Configure(config);

            _connectionResolver.Configure(config);
            _source = config.GetAsStringWithDefault("source", _source);
            _instance = config.GetAsStringWithDefault("instance", _instance);
        }

        public virtual void SetReferences(IReferences references)
        {
            _logger.SetReferences(references);
            _connectionResolver.SetReferences(references);

            var contextInfo = references.GetOneOptional<ContextInfo>(
                new Descriptor("pip-services", "context-info", "default", "*", "1.0"));
            if (contextInfo != null && string.IsNullOrEmpty(_source))
                _source = contextInfo.Name;
            if (contextInfo != null && string.IsNullOrEmpty(_instance))
                _instance = contextInfo.ContextId;
        }

        public bool IsOpen()
        {
            return _opened;
        }

        public async Task OpenAsync(string correlationId)
        {
            if (_opened) return;

            var awsConnection = await _connectionResolver.ResolveAsync(correlationId);

            // Validate connection params
            var err = awsConnection.Validate(correlationId);
            if (err != null) throw err;

            // Create client
            var region = RegionEndpoint.GetBySystemName(awsConnection.Region);
            var config = new AmazonCloudWatchConfig()
            {
                RegionEndpoint = region
            };

            _client = new AmazonCloudWatchClient(awsConnection.AccessId, awsConnection.AccessKey, config);
            _opened = true;

            await Task.Delay(0);
        }

        public async Task CloseAsync(string correlationId)
        {
            _opened = false;
            _client = null;

            await Task.Delay(0);
        }

        private MetricDatum GetCounterData(Counter counter, DateTime time, List<Dimension> dimensions)
        {
            var value = new MetricDatum
            {
                MetricName = counter.Name,
                Timestamp = time,
                Dimensions = dimensions,
                Unit = StandardUnit.None
            };

            switch (counter.Type)
            {
                case CounterType.Increment:
                    value.Value = counter.Count.Value;
                    value.Unit = StandardUnit.Count;
                    break;
                case CounterType.Interval:
                    value.Unit = StandardUnit.Milliseconds;
                    //value.Value = counter.Average.Value;
                    value.StatisticValues = new StatisticSet
                    {
                        SampleCount = counter.Count.Value,
                        Maximum = counter.Max.Value,
                        Minimum = counter.Min.Value,
                        Sum = counter.Count.Value * counter.Average.Value
                    };
                    break;
                case CounterType.Statistics:
                    //value.Value = counter.Average.Value;
                    value.StatisticValues = new StatisticSet
                    {
                        SampleCount = counter.Count.Value,
                        Maximum = counter.Max.Value,
                        Minimum = counter.Min.Value,
                        Sum = counter.Count.Value * counter.Average.Value
                    };
                    break;
                case CounterType.LastValue:
                    value.Value = counter.Last.Value;
                    break;
                case CounterType.Timestamp:
                    value.Value = counter.Time.Value.Ticks;
                    break;
            }

            return value;
        }

        protected override void Save(IEnumerable<Counter> counters)
        {
            if (_client == null) return;

            try
            {
                var dimensions = new List<Dimension>();
                dimensions.Add(new Dimension
                {
                    Name = "InstanceID",
                    Value = _instance
                });

                var data = new List<MetricDatum>();
                var now = DateTime.UtcNow;

                foreach (var counter in counters)
                {
                    data.Add(GetCounterData(counter, now, dimensions));  

                    if (data.Count >= 20)
                    {
                        _client.PutMetricDataAsync(
                            new PutMetricDataRequest
                            {
                                Namespace = _source,
                                MetricData = data
                            }
                        ).Wait();
                        data.Clear();
                    }
                }

                if (data.Count > 0)
                {
                    _client.PutMetricDataAsync(
                        new PutMetricDataRequest
                        { 
                            Namespace = _source,
                            MetricData = data
                        }
                    ).Wait();
                }

            }
            catch (Exception ex)
            {
                _logger.Error("cloudwatch-counters", ex, "Failed to push metrics to prometheus");
            }
        }
    }
}
