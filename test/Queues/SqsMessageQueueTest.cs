using PipServices.Commons.Config;
using PipServices.Commons.Convert;
using System;
using Xunit;

namespace PipServices.Aws.Queues
{
    public class SqsMessageQueueTest: IDisposable
    {
        private bool _enabled;
        private SqsMessageQueue _queue;
        MessageQueueFixture _fixture;

        public SqsMessageQueueTest()
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
                _queue = new SqsMessageQueue("TestQueue");
                _queue.Configure(ConfigParams.FromTuples(
                    "connection.uri", AWS_QUEUE_ARN,
                    "connection.region", AWS_REGION,
                    "connection.account", AWS_ACCOUNT,
                    "credential.access_id", AWS_ACCESS_ID,
                    "credential.access_key", AWS_ACCESS_KEY
                ));

                _queue.OpenAsync(null).Wait();
                _queue.ClearAsync(null).Wait();

                _fixture = new MessageQueueFixture(_queue);
            }
        }

        public void Dispose()
        {
            if (_queue != null)
                _queue.CloseAsync(null).Wait();
        }

        [Fact]
        public void TestAmazonSqsSendReceiveMessage()
        {
            if (_enabled)
                _fixture.TestSendReceiveMessageAsync().Wait();
        }

        [Fact]
        public void TestAmazonSqsReceiveSendMessage()
        {
            if (_enabled)
                _fixture.TestReceiveSendMessageAsync().Wait();
        }

        [Fact]
        public void TestAmazonSqsReceiveAndComplete()
        {
            if (_enabled)
                _fixture.TestReceiveAndCompleteMessageAsync().Wait();
        }

        [Fact]
        public void TestAmazonSqsReceiveAndAbandon()
        {
            if (_enabled)
                _fixture.TestReceiveAndAbandonMessageAsync().Wait();
        }

        [Fact]
        public void TestAmazonSqsSendPeekMessage()
        {
            if (_enabled)
                _fixture.TestSendPeekMessageAsync().Wait();
        }

        [Fact]
        public void TestAmazonSqsPeekNoMessage()
        {
            if (_enabled)
                _fixture.TestPeekNoMessageAsync().Wait();
        }

        [Fact]
        public void TestAmazonSqsOnMessage()
        {
            if (_enabled)
                _fixture.TestOnMessageAsync().Wait();
        }

        [Fact]
        public void TestAmazonSqsMoveToDeadMessage()
        {
            if (_enabled)
                _fixture.TestMoveToDeadMessageAsync().Wait();
        }

        [Fact]
        public void TestAmazonSqsMessageCount()
        {
            if (_enabled)
                _fixture.TestMessageCountAsync().Wait();
        }
    }
}
