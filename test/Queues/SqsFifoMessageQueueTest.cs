using PipServices3.Commons.Config;
using System;

namespace PipServices3.Aws.Queues
{
    public class SqsFifoMessageQueueTest : SqsMessageQueueTest
    {
        public SqsFifoMessageQueueTest() : base()
        {
        }

        protected override void Setup()
        {
            AWS_QUEUE_ARN = Environment.GetEnvironmentVariable("AWS_FIFO_QUEUE_ARN");
            AWS_QUEUE = Environment.GetEnvironmentVariable("AWS_FIFO_QUEUE") ?? "TestQueue.fifo";
            AWS_DEAD_QUEUE = Environment.GetEnvironmentVariable("AWS_DEAD_FIFO_QUEUE") ?? "TestQueueDLQ.fifo";

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
}
