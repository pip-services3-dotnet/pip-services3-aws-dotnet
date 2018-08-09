using System;
using PipServices.Commons.Config;
using PipServices.Commons.Convert;
using Xunit;

namespace PipServices.Aws.Log
{
    public sealed class CloudWatchLoggerTest : IDisposable
    {
        private readonly bool _enabled;
        private readonly CloudWatchLogger _logger;
        private readonly LoggerFixture _fixture;

        public CloudWatchLoggerTest()
        {
            var AWS_ENABLED = Environment.GetEnvironmentVariable("AWS_ENABLED") ?? "true";
            var AWS_REGION = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";
            var AWS_ACCOUNT = Environment.GetEnvironmentVariable("AWS_ACCOUNT");
            var AWS_ACCESS_ID = Environment.GetEnvironmentVariable("AWS_ACCESS_ID") ?? "AKIAI2B3PGHEAAK4BPUQ";
            var AWS_ACCESS_KEY = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY") ?? "zQZGX0vGL6OD936fCcP1v6YmpiSdW28oUcezAnb7";
            var AWS_QUEUE_ARN = Environment.GetEnvironmentVariable("AWS_QUEUE_ARN");

            _enabled = BooleanConverter.ToBoolean(AWS_ENABLED);

            if (_enabled)
            {
                _logger = new CloudWatchLogger();
                _logger.Configure(ConfigParams.FromTuples(
                    "group", "TestGroup",
                    "stream", "TestStream",
                    "connection.uri", AWS_QUEUE_ARN,
                    "connection.region", AWS_REGION,
                    "connection.account", AWS_ACCOUNT,
                    "credential.access_id", AWS_ACCESS_ID,
                    "credential.access_key", AWS_ACCESS_KEY
                ));

                _fixture = new LoggerFixture(_logger);

                try
                {                    
                    _logger.OpenAsync(null).Wait();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    throw ex;
                }
            }
        }

        public void Dispose()
        {
            if (_logger != null)
            {
                _logger.CloseAsync(null).Wait();
            }
        }

        [Fact]
        public void TestSimpleLogging()
        {
            if (_enabled)
            {
                _fixture.TestSimpleLogging();
            }
        }

        [Fact]
        public void TestErrorLogging()
        {
            if (_enabled)
            {
                _fixture.TestErrorLogging();
            }
        }
    }
}
