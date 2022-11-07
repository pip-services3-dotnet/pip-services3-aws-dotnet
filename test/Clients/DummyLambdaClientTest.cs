using System;
using System.Threading.Tasks;
using Xunit;

using PipServices3.Aws.Example.Clients;
using PipServices3.Commons.Config;

namespace PipServices3.Aws.Test.Clients
{
    [Collection("Sequential")]
    public class DummyLambdaClientTest : IDisposable
    {
        protected DummyLambdaClient client;
        protected DummyClientFixture fixture;

        private bool skip = false;

        public DummyLambdaClientTest()
        {
            var AWS_ACCESS_ID = Environment.GetEnvironmentVariable("AWS_ACCESS_ID");
            var AWS_ACCESS_KEY = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY");
            var AWS_ARN = Environment.GetEnvironmentVariable("LAMBDA_ARN");

            if (string.IsNullOrEmpty(AWS_ACCESS_ID) || string.IsNullOrEmpty(AWS_ACCESS_KEY) || string.IsNullOrEmpty(AWS_ARN))
            {
                skip = true;
                return;
            }

            var config = ConfigParams.FromTuples(
                "connection.protocol", "aws",
                "connection.arn", AWS_ARN,
                "credential.access_id", AWS_ACCESS_ID,
                "credential.access_key", AWS_ACCESS_KEY,
                "options.connection_timeout", 30000
            );

            client = new DummyLambdaClient();
            client.Configure(config);

            fixture = new DummyClientFixture(client);

            client.OpenAsync(null).Wait();
        }

        [Fact]
        public async Task TestCrudOperations()
        {
            if (!skip)
                await fixture.TestCrudOperations();
        }

        public void Dispose()
        {
            if (!skip)
                client.CloseAsync(null).Wait();
        }
    }
}
