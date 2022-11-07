using PipServices3.Aws.Services;
using PipServices3.Commons.Refer;

namespace PipServices3.Aws.Example.Services
{
    public class DummyCommandableLambdaService: CommandableLambdaService
    {
        public DummyCommandableLambdaService() : base("dummies")
        {
            _dependencyResolver.Put("controller", new Descriptor("pip-services-dummies", "controller", "default", "*", "*"));
        }
    }
}
