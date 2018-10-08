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
    /// See <a href="https://rawgit.com/pip-services-dotnet/pip-services-aws-dotnet/master/doc/api/class_pip_services_1_1_aws_1_1_count_1_1_cloud_watch_counters.html">CloudWatchCounters</a>, 
    /// <a href="https://rawgit.com/pip-services-dotnet/pip-services-aws-dotnet/master/doc/api/class_pip_services_1_1_aws_1_1_log_1_1_cloud_watch_logger.html">CloudWatchLogger</a>, 
    /// <a href="https://rawgit.com/pip-services-dotnet/pip-services-aws-dotnet/master/doc/api/class_pip_services_1_1_aws_1_1_queues_1_1_sqs_message_queue.html">SqsMessageQueue</a>, 
    /// <a href="https://rawgit.com/pip-services-dotnet/pip-services-aws-dotnet/master/doc/api/class_pip_services_1_1_aws_1_1_queues_1_1_sqs_message_queue_factory.html">SqsMessageQueueFactory</a>
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
