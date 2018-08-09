using PipServices.Components.Build;
using PipServices.Commons.Config;
using PipServices.Commons.Refer;

namespace PipServices.Aws.Queues
{
    public class SqsMessageQueueFactory : Factory, IConfigurable
    {
        public static readonly Descriptor Descriptor = new Descriptor("pip-services-aws", "factory", "message-queue", "sqs", "1.0");
        public static readonly Descriptor MemoryQueueDescriptor = new Descriptor("pip-services-aws", "message-queue", "sqs", "*", "*");

        private ConfigParams _config;

        public SqsMessageQueueFactory()
        {
            Register(MemoryQueueDescriptor, (locator) => {
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
