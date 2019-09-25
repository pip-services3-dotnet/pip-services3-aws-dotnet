using PipServices3.Commons.Config;
using PipServices3.Commons.Convert;
using System;
using Xunit;

namespace PipServices3.Aws.Queues
{
    public class SqsMessageQueueTest : IDisposable
    {
        protected bool _enabled;
        protected SqsMessageQueue _queue;
        protected MessageQueueFixture _fixture;

        protected string AWS_ENABLED;
        protected string AWS_REGION;
        protected string AWS_ACCOUNT;
        protected string AWS_ACCESS_ID;
        protected string AWS_ACCESS_KEY;
        protected string AWS_QUEUE_ARN;
        protected string AWS_QUEUE;
        protected string AWS_DEAD_QUEUE;

        public SqsMessageQueueTest()
        {
            AWS_ENABLED = Environment.GetEnvironmentVariable("AWS_ENABLED") ?? "true";
            AWS_REGION = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";
            AWS_ACCOUNT = Environment.GetEnvironmentVariable("AWS_ACCOUNT");
            AWS_ACCESS_ID = Environment.GetEnvironmentVariable("AWS_ACCESS_ID") ?? "AKIAI2B3PGHEAAK4BPUQ";
            AWS_ACCESS_KEY = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY") ?? "zQZGX0vGL6OD936fCcP1v6YmpiSdW28oUcezAnb7";

            _enabled = BooleanConverter.ToBoolean(AWS_ENABLED);

            if (_enabled)
                Setup();
        }

        public virtual void Setup()
        {
            AWS_QUEUE_ARN = Environment.GetEnvironmentVariable("AWS_QUEUE_ARN");
            AWS_QUEUE = Environment.GetEnvironmentVariable("AWS_QUEUE") ?? "TestQueue";
            AWS_DEAD_QUEUE = Environment.GetEnvironmentVariable("AWS_DEAD_QUEUE") ?? "TestQueueDLQ";

            _queue = new SqsMessageQueue(AWS_QUEUE);

            _queue.Configure(ConfigParams.FromTuples(
                    "connection.uri", AWS_QUEUE_ARN,
                    "connection.region", AWS_REGION,
                    "connection.account", AWS_ACCOUNT,
                    "credential.access_id", AWS_ACCESS_ID,
                    "credential.access_key", AWS_ACCESS_KEY,
                    "connection.queue", AWS_QUEUE,
                    "connection.dead_queue", AWS_DEAD_QUEUE
                ));

            _queue.OpenAsync(null).Wait();
            _queue.ClearAsync(null).Wait();

            _fixture = new MessageQueueFixture(_queue, AWS_QUEUE.EndsWith(".fifo"));
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
