using System;
using PipServices.Commons.Config;
using PipServices.Commons.Convert;
using PipServices.Commons.Refer;
using PipServices.Components.Info;
using Xunit;

namespace PipServices.Aws.Count
{
    public sealed class CloudWatchCountersTest : IDisposable
    {
        private readonly bool _enabled;
        private readonly CloudWatchCounters _counters;
        private readonly CountersFixture _fixture;

        public CloudWatchCountersTest()
        {
            var AWS_ENABLED = Environment.GetEnvironmentVariable("AWS_ENABLED") ?? "true";
            var AWS_REGION = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";
            var AWS_ACCOUNT = Environment.GetEnvironmentVariable("AWS_ACCOUNT");
            var AWS_ACCESS_ID = Environment.GetEnvironmentVariable("AWS_ACCESS_ID") ?? "AKIAI2B3PGHEAAK4BPUQ";
            var AWS_ACCESS_KEY = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY") ?? "zQZGX0vGL6OD936fCcP1v6YmpiSdW28oUcezAnb7";

            _enabled = BooleanConverter.ToBoolean(AWS_ENABLED);

            if (_enabled)
            {
                _counters = new CloudWatchCounters();
                _counters.Configure(ConfigParams.FromTuples(
                    "interval", "5000",
                    "connection.region", AWS_REGION,
                    "connection.account", AWS_ACCOUNT,
                    "credential.access_id", AWS_ACCESS_ID,
                    "credential.access_key", AWS_ACCESS_KEY
                ));

                var contextInfo = new ContextInfo();
                contextInfo.Name = "Test";
                contextInfo.Description = "This is a test container";

                var references = References.FromTuples(
                    new Descriptor("pip-services", "context-info", "default", "default", "1.0"), contextInfo,
                    new Descriptor("pip-services", "counters", "prometheus", "default", "1.0"), _counters
                );
                _counters.SetReferences(references);

                _fixture = new CountersFixture(_counters);

                _counters.OpenAsync(null).Wait();
            }
        }

        public void Dispose()
        {
            if (_counters != null)
                _counters.CloseAsync(null).Wait();
        }

        [Fact]
        public void TestSimpleCounters()
        {
            if (_enabled)
                _fixture.TestSimpleCounters();
        }

        [Fact]
        public void TestMeasureElapsedTime()
        {
            if (_enabled)
                _fixture.TestMeasureElapsedTime();
        }
    }
}
