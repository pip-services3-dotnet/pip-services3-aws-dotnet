using System;
using PipServices3.Aws.Log;
using PipServices3.Commons.Config;
using Xunit;

namespace PipServices3.Aws.Test.Log
{
    public sealed class CloudWatchLoggerTest : IDisposable
    {
        private readonly bool _enabled;
        private readonly CloudWatchLogger _logger;
        private readonly LoggerFixture _fixture;

        public CloudWatchLoggerTest()
        {
            var AWS_REGION = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";
            var AWS_ACCOUNT = Environment.GetEnvironmentVariable("AWS_ACCOUNT");
            var AWS_ACCESS_ID = Environment.GetEnvironmentVariable("AWS_ACCESS_ID");
            var AWS_ACCESS_KEY = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY");

            _enabled = !(string.IsNullOrEmpty(AWS_REGION) || string.IsNullOrEmpty(AWS_ACCESS_ID) || string.IsNullOrEmpty(AWS_ACCESS_KEY));

            if (_enabled)
            {
                _logger = new CloudWatchLogger();
                _logger.Configure(ConfigParams.FromTuples(
                    "group", "TestGroup",
                    "stream", "TestStream",
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
