using PipServices.Aws.Queues;
using PipServices.Components.Build;
using PipServices.Commons.Refer;

namespace PipServices.Aws.Build
{
    public class DefaultAwsFactory: Factory
    {
        public static Descriptor Descriptor = new Descriptor("pip-services", "factory", "aws", "default", "1.0");
        public static Descriptor AmazonSqsMessageQueueFactoryDescriptor = new Descriptor("pip-services", "factory", "message-queue", "sqs", "1.0");
        public static Descriptor AmazonSqsMessageQueueDescriptor = new Descriptor("pip-services", "message-queue", "sqs", "*", "1.0");

        public DefaultAwsFactory()
        {
            RegisterAsType(AmazonSqsMessageQueueFactoryDescriptor, typeof(AmazonSqsMessageQueueFactory));
            RegisterAsType(AmazonSqsMessageQueueDescriptor, typeof(AmazonSqsMessageQueue));
        }
    }
}
