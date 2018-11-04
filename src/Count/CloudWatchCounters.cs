using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using PipServices3.Aws.Connect;
using PipServices3.Commons.Config;
using PipServices3.Commons.Convert;
using PipServices3.Commons.Refer;
using PipServices3.Commons.Run;
using PipServices3.Components.Count;
using PipServices3.Components.Info;
using PipServices3.Components.Log;

namespace PipServices3.Aws.Count
{
    /// <summary>
    /// Performance counters that periodically dumps counters to AWS Cloud Watch Metrics.
    /// 
    /// ### Configuration parameters ###
    /// 
    /// connections:
    /// - discovery_key:         (optional) a key to retrieve the connection from <a href="https://rawgit.com/pip-services3-dotnet/pip-services3-components-dotnet/master/doc/api/interface_pip_services_1_1_components_1_1_connect_1_1_i_discovery.html">IDiscovery</a>
    /// - region:                (optional) AWS region
    /// - credentials:    
    /// - store_key:             (optional) a key to retrieve the credentials from <a href="https://rawgit.com/pip-services3-dotnet/pip-services3-components-dotnet/master/doc/api/interface_pip_services_1_1_components_1_1_auth_1_1_i_credential_store.html">ICredentialStore</a>
    /// - access_id:             AWS access/client id
    /// - access_key:            AWS access/client id
    /// 
    /// options:
    /// - interval:              interval in milliseconds to save current counters measurements(default: 5 mins)
    /// - reset_timeout:         timeout in milliseconds to reset the counters. 0 disables the reset(default: 0)
    /// 
    /// ### References ###
    /// 
    /// - *:context-info:*:*:1.0      (optional) <a href="https://rawgit.com/pip-services3-dotnet/pip-services3-components-dotnet/master/doc/api/class_pip_services_1_1_components_1_1_info_1_1_context_info.html">ContextInfo</a> to detect the context id and specify counters source
    /// - *:discovery:*:*:1.0         (optional) <a href="https://rawgit.com/pip-services3-dotnet/pip-services3-components-dotnet/master/doc/api/interface_pip_services_1_1_components_1_1_connect_1_1_i_discovery.html">IDiscovery</a> services to resolve connections
    /// - *:credential-store:*:*:1.0  (optional) Credential stores to resolve credentials
    /// </summary>
    /// <example>
    /// <code>
    /// var counters = new CloudWatchCounters();
    /// counters.Configure(ConfigParams.FromTuples(
    /// "connection.region", "us-east-1",
    /// "connection.access_id", "XXXXXXXXXXX",
    /// "connection.access_key", "XXXXXXXXXXX"  ));
    /// 
    /// counters.SetReferences(References.fromTuples(
    /// new Descriptor("pip-services3", "logger", "console", "default", "1.0"), 
    /// new ConsoleLogger() ));
    /// counters.Open("123");
    /// 
    /// counters.Increment("mycomponent.mymethod.calls");
    /// var timing = counters.BeginTiming("mycomponent.mymethod.exec_time");
    /// try {
    /// ...
    /// } finally {
    /// timing.EndTiming();
    /// }
    /// counters.Dump();
    /// </code>
    /// </example>
    /// See <see cref="Counter"/>, <see cref="CachedCounters"/>, <see cref="CompositeCounters"/>
    public class CloudWatchCounters : CachedCounters, IReferenceable, IOpenable
    {
        private CompositeLogger _logger = new CompositeLogger();
        private AwsConnectionResolver _connectionResolver = new AwsConnectionResolver();
        private bool _opened;
        private string _source;
        private string _instance;
        private AmazonCloudWatchClient _client;

        /// <summary>
        /// Creates a new instance of this counters.
        /// </summary>
        public CloudWatchCounters()
        { }

        /// <summary>
        /// Configures component by passing configuration parameters.
        /// </summary>
        /// <param name="config">configuration parameters to be set.</param>
        public override void Configure(ConfigParams config)
        {
            base.Configure(config);

            _connectionResolver.Configure(config);
            _source = config.GetAsStringWithDefault("source", _source);
            _instance = config.GetAsStringWithDefault("instance", _instance);
        }

        /// <summary>
        /// Sets references to dependent components.
        /// </summary>
        /// <param name="references">references to locate the component dependencies.</param>
        public virtual void SetReferences(IReferences references)
        {
            _logger.SetReferences(references);
            _connectionResolver.SetReferences(references);

            var contextInfo = references.GetOneOptional<ContextInfo>(
                new Descriptor("pip-services3", "context-info", "default", "*", "1.0"));
            if (contextInfo != null && string.IsNullOrEmpty(_source))
                _source = contextInfo.Name;
            if (contextInfo != null && string.IsNullOrEmpty(_instance))
                _instance = contextInfo.ContextId;
        }

        /// <summary>
        /// Checks if the component is opened.
        /// </summary>
        /// <returns>true if the component has been opened and false otherwise.</returns>
        public bool IsOpen()
        {
            return _opened;
        }

        /// <summary>
        /// Opens the component.
        /// </summary>
        /// <param name="correlationId">(optional) transaction id to trace execution through call chain.</param>
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

        /// <summary>
        /// Closes component and frees used resources.
        /// </summary>
        /// <param name="correlationId">(optional) transaction id to trace execution through call chain.</param>
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

        /// <summary>
        /// Saves the current counters measurements.
        /// </summary>
        /// <param name="counters">current counters measurements to be saves.</param>
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
