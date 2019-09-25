using PipServices3.Components.Build;
using PipServices3.Commons.Config;
using PipServices3.Commons.Refer;

namespace PipServices3.Aws.Queues
{
    public class SqsMessageQueueFactory : Factory, IConfigurable
    {
        public static readonly Descriptor Descriptor = new Descriptor("pip-services3-aws", "factory", "message-queue", "sqs", "1.0");
        public static readonly Descriptor SqsMessageQueueDescriptor = new Descriptor("pip-services3-aws", "message-queue", "sqs", "*", "*");

        private ConfigParams _config;

        public SqsMessageQueueFactory()
        {
            Register(SqsMessageQueueDescriptor, (locator) => {
                Descriptor descriptor = (Descriptor)locator;
                var queue = new SqsMessageQueue(descriptor.Name);
                if (_config != null)
                    queue.Configure(_config);
                return queue;
            });
        }

        public void Configure(ConfigParams config)
        {
            _config = config;
        }
    }
}
