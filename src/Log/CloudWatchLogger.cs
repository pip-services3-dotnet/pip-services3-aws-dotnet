using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using PipServices.Aws.Connect;
using PipServices.Commons.Config;
using PipServices.Commons.Convert;
using PipServices.Commons.Data;
using PipServices.Commons.Errors;
using PipServices.Commons.Refer;
using PipServices.Commons.Run;
using PipServices.Components.Auth;
using PipServices.Components.Connect;
using PipServices.Components.Log;

namespace PipServices.Aws.Log
{
    public class CloudWatchLogger : CachedLogger, IReferenceable, IOpenable
    {
        private FixedRateTimer _timer;
        private AwsConnectionResolver _connectionResolver = new AwsConnectionResolver();
        private AmazonCloudWatchLogsClient _client;
        private string _group = "undefined";
        private string _stream = null;
        private string _lastToken = null;

        public CloudWatchLogger()
        { }

        public override void Configure(ConfigParams config)
        {
            base.Configure(config);
            _connectionResolver.Configure(config);

            _group = config.GetAsStringWithDefault("group", _group);
            _stream = config.GetAsStringWithDefault("stream", _stream);
        }

        public override void SetReferences(IReferences references)
        {
            base.SetReferences(references);
            _connectionResolver.SetReferences(references);
        }

        protected override void Write(LogLevel level, string correlationId, Exception error, string message)
        {
            if (Level < level)
            {
                return;
            }

            base.Write(level, correlationId, error, message);
        }

        public bool IsOpen()
        {
            return _timer != null;
        }

        public async Task OpenAsync(string correlationId)
        {
            if (IsOpen()) return;

            var awsConnection = await _connectionResolver.ResolveAsync(correlationId);

            // Assign service name
            awsConnection.Service = "logs";
            awsConnection.ResourceType = "log-group";

            if (!string.IsNullOrEmpty(awsConnection.Resource))
                _group = awsConnection.Resource;

            // Undefined stream creates a random stream on every connect
            if (string.IsNullOrEmpty(_stream))
                _stream = IdGenerator.NextLong();

            // Validate connection params
            var err = awsConnection.Validate(correlationId);
            if (err != null) throw err;

            // Create client
            var region = RegionEndpoint.GetBySystemName(awsConnection.Region);
            var config = new AmazonCloudWatchLogsConfig()
            {
                RegionEndpoint = region
            };
            _client = new AmazonCloudWatchLogsClient(awsConnection.AccessId, awsConnection.AccessKey, config);

            // Create a log group if needed
            try
            {
                await _client.CreateLogGroupAsync(
                    new CreateLogGroupRequest 
                    { 
                        LogGroupName = _group
                    }
                );
            }
            catch (ResourceAlreadyExistsException)
            {
                // Ignore. Everything is ok
            }

            // Create or read log stream
            try
            {
                await _client.CreateLogStreamAsync(
                    new CreateLogStreamRequest
                    {
                        LogGroupName = _group,
                        LogStreamName = _stream
                    }
                );

                _lastToken = null;
            }
            catch (ResourceAlreadyExistsException)
            {
                var response = await _client.DescribeLogStreamsAsync(
                    new DescribeLogStreamsRequest
                    {
                        LogGroupName = _group,
                        LogStreamNamePrefix = _stream
                    }
                );

                if (response.LogStreams.Count > 0)
                {
                    _lastToken = response.LogStreams[0].UploadSequenceToken;
                }
            }

            if (_timer == null)
            {
                _timer = new FixedRateTimer(OnTimer, _interval, _interval);
                _timer.Start();
            }
        }

        public async Task CloseAsync(string correlationId)
        {
            // Log all remaining messages before closing
            Dump();

            if (_timer != null)
            {
                _timer.Stop();
                _timer = null;
            }

            _client = null;

            await Task.Delay(0);
        }

        private void OnTimer()
        {
            Dump();
        }

        private string FormatMessageText(LogMessage message)
        {
            var build = new StringBuilder();
            build.Append('[');
            build.Append(message.Source ?? "---");
            build.Append(':');
            build.Append(message.CorrelationId ?? "---");
            build.Append(':');
            build.Append(message.Level.ToString());
            build.Append("] ");

            build.Append(message.Message);

            if (message.Error != null)
            {
                if (string.IsNullOrEmpty(message.Message))
                    build.Append("Error: ");
                else
                    build.Append(": ");

                build.Append(message.Error.Message);

                if (!string.IsNullOrEmpty(message.Error.StackTrace))
                {
                    build.Append(" StackTrace: ");
                    build.Append(message.Error.StackTrace);
                }
            }

            return build.ToString();
        }
    
        protected override void Save(List<LogMessage> messages)
        {
            if (messages == null || messages.Count == 0) return;

            if (_client == null)
            {
                throw new InvalidStateException(
                    "cloudwatch_logger", "NOT_OPENED", "CloudWatchLogger is not opened"
                );
            }

            lock (_lock)
            {
                var events = new List<InputLogEvent>();
                foreach (var message in messages)
                {
                    events.Add(new InputLogEvent
                    {
                        Timestamp = message.Time,
                        Message = FormatMessageText(message)
                    });
                }

                try
                {
                    var result = _client.PutLogEventsAsync(new PutLogEventsRequest
                        {
                            LogGroupName = _group,
                            LogStreamName = _stream,
                            SequenceToken = _lastToken,
                            LogEvents = events
                        }
                    ).Result;

                    _lastToken = result.NextSequenceToken;
                }
                catch
                {
                    // Do nothing if elastic search client was not enable to process bulk of messages
                }
            }
        }
    }}
