using PipServices.Aws.Queues;
using PipServices.Components.Build;
using PipServices.Commons.Refer;
using PipServices.Aws.Log;

namespace PipServices.Aws.Build
{
    public class DefaultAwsFactory: Factory
    {
        public static Descriptor Descriptor = new Descriptor("pip-services", "factory", "aws", "default", "1.0");
        public static Descriptor SqsMessageQueueFactoryDescriptor = new Descriptor("pip-services", "factory", "message-queue", "sqs", "1.0");
        public static Descriptor SqsMessageQueueDescriptor = new Descriptor("pip-services", "message-queue", "sqs", "*", "1.0");
        public static Descriptor CloudWatchLoggerDescriptor = new Descriptor("pip-services", "logger", "cloudwatch", "*", "1.0");

        public DefaultAwsFactory()
        {
            RegisterAsType(SqsMessageQueueFactoryDescriptor, typeof(SqsMessageQueueFactory));
            RegisterAsType(SqsMessageQueueDescriptor, typeof(SqsMessageQueue));
            RegisterAsType(CloudWatchLoggerDescriptor, typeof(CloudWatchLogger));
        }
    }
}
