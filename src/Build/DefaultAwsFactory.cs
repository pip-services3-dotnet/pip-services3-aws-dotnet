using PipServices.Aws.Count;
using PipServices.Aws.Log;
using PipServices.Aws.Queues;
using PipServices.Commons.Refer;
using PipServices.Components.Build;

namespace PipServices.Aws.Build
{
    /// <summary>
    /// Creates AWS components by their descriptors.
    /// </summary>
    /// See <see cref="CloudWatchCounters"/>, <see cref="CloudWatchLogger"/>, 
    /// <see cref="SqsMessageQueue"/>, <see cref="SqsMessageQueueFactory"/>
    public class DefaultAwsFactory: Factory
    {
        public static Descriptor Descriptor = new Descriptor("pip-services", "factory", "aws", "default", "1.0");
        public static Descriptor SqsMessageQueueFactoryDescriptor = new Descriptor("pip-services", "factory", "message-queue", "sqs", "1.0");
        public static Descriptor SqsMessageQueueDescriptor = new Descriptor("pip-services", "message-queue", "sqs", "*", "1.0");
        public static Descriptor CloudWatchLoggerDescriptor = new Descriptor("pip-services", "logger", "cloudwatch", "*", "1.0");
        public static Descriptor CloudWatchCountersDescriptor = new Descriptor("pip-services", "counters", "cloudwatch", "*", "1.0");

        /// <summary>
        /// Create a new instance of the factory.
        /// </summary>
        public DefaultAwsFactory()
        {
            RegisterAsType(SqsMessageQueueFactoryDescriptor, typeof(SqsMessageQueueFactory));
            RegisterAsType(SqsMessageQueueDescriptor, typeof(SqsMessageQueue));
            RegisterAsType(CloudWatchLoggerDescriptor, typeof(CloudWatchLogger));
            RegisterAsType(CloudWatchCountersDescriptor, typeof(CloudWatchCounters));
        }
    }
}
