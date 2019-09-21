using PipServices3.Commons.Config;
using PipServices3.Commons.Convert;
using System;
using Xunit;

namespace PipServices3.Aws.Queues
{
    public class SqsMessageQueueTest : IDisposable
    {
        private bool _enabled;
        private SqsMessageQueue _queue;
        MessageQueueFixture _fixture;

        private string AWS_ENABLED;
        private string AWS_REGION;
        private string AWS_ACCOUNT;
        private string AWS_ACCESS_ID;
        private string AWS_ACCESS_KEY;
        private string AWS_QUEUE_ARN;
        private string AWS_QUEUE;
        private string AWS_DEAD_QUEUE;

        public SqsMessageQueueTest()
        {
            AWS_ENABLED = Environment.GetEnvironmentVariable("AWS_ENABLED") ?? "true";
            AWS_REGION = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";
            AWS_ACCOUNT = Environment.GetEnvironmentVariable("AWS_ACCOUNT");
            AWS_ACCESS_ID = Environment.GetEnvironmentVariable("AWS_ACCESS_ID");
            AWS_ACCESS_KEY = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY");

            _enabled = BooleanConverter.ToBoolean(AWS_ENABLED);
        }

        public void Dispose()
        {
            if (_queue != null)
                _queue.CloseAsync(null).Wait();
        }

        private void ConfigureStandardQueue()
        {
            AWS_QUEUE_ARN = Environment.GetEnvironmentVariable("AWS_QUEUE_ARN");
            AWS_QUEUE = Environment.GetEnvironmentVariable("AWS_QUEUE") ?? "TestQueue";
            AWS_DEAD_QUEUE = Environment.GetEnvironmentVariable("AWS_DEAD_QUEUE") ?? "TestQueueDLQ";

            if (_enabled)
            {
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
        }

        private void ConfigureFifoTest()
        {
            AWS_QUEUE_ARN = Environment.GetEnvironmentVariable("AWS_FIFO_QUEUE_ARN");
            AWS_QUEUE = Environment.GetEnvironmentVariable("AWS_FIFO_QUEUE") ?? "TestQueue.fifo";
            AWS_DEAD_QUEUE = Environment.GetEnvironmentVariable("AWS_DEAD_FIFO_QUEUE") ?? "TestQueueDLQ.fifo";

            if (_enabled)
            {
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

                _fixture = new MessageQueueFixture(_queue, AWS_QUEUE.EndsWith(".fifo"));
            }
        }

        [Fact]
        public void TestAmazonSqsFifoQueue()
        {
            if (_enabled)
            {
                ConfigureFifoTest();

                _fixture.TestMoveToDeadMessageAsync().Wait();
                _fixture.TestReceiveAndCompleteMessageAsync().Wait();
                _fixture.TestPeekNoMessageAsync().Wait();

                _fixture.TestSendReceiveMessageAsync().Wait();
                _fixture.TestReceiveSendMessageAsync().Wait();
                _fixture.TestReceiveAndAbandonMessageAsync().Wait();
                _fixture.TestSendPeekMessageAsync().Wait();
                _fixture.TestMessageCountAsync().Wait();
                _fixture.TestOnMessageAsync().Wait();
            }
        }

        [Fact]
        public void TestAmazonSqsSendReceiveMessage()
        {
            if (_enabled)
            {
                ConfigureStandardQueue();
                _fixture.TestSendReceiveMessageAsync().Wait();
            }
        }

        [Fact]
        public void TestAmazonSqsReceiveSendMessage()
        {
            if (_enabled)
            {
                ConfigureStandardQueue();
                _fixture.TestReceiveSendMessageAsync().Wait();
            }
        }

        [Fact]
        public void TestAmazonSqsReceiveAndComplete()
        {
            if (_enabled)
            {
                ConfigureStandardQueue();
                _fixture.TestReceiveAndCompleteMessageAsync().Wait();
            }
        }

        [Fact]
        public void TestAmazonSqsReceiveAndAbandon()
        {
            if (_enabled)
            {
                ConfigureStandardQueue();
                _fixture.TestReceiveAndAbandonMessageAsync().Wait();
            }
        }

        [Fact]
        public void TestAmazonSqsSendPeekMessage()
        {
            if (_enabled)
            {
                ConfigureStandardQueue();
                _fixture.TestSendPeekMessageAsync().Wait();
            }
        }

        [Fact]
        public void TestAmazonSqsPeekNoMessage()
        {
            if (_enabled)
            {
                ConfigureStandardQueue();
                _fixture.TestPeekNoMessageAsync().Wait();
            }
        }

        [Fact]
        public void TestAmazonSqsOnMessage()
        {
            if (_enabled)
            {
                ConfigureStandardQueue();
                _fixture.TestOnMessageAsync().Wait();
            }
        }

        [Fact]
        public void TestAmazonSqsMoveToDeadMessage()
        {
            if (_enabled)
            {
                ConfigureStandardQueue();
                _fixture.TestMoveToDeadMessageAsync().Wait();
            }
        }

        [Fact]
        public void TestAmazonSqsMessageCount()
        {
            if (_enabled)
            {
                ConfigureStandardQueue();
                _fixture.TestMessageCountAsync().Wait();
            }
        }
    }
}
