using PipServices3.Aws.Containers;
using PipServices3.Commons.Refer;

namespace PipServices3.Aws.Example.Containers
{
    public class DummyCommandableLambdaFunction: CommandableLambdaFunction
    {
        public DummyCommandableLambdaFunction() : base("dummy", "Dummy lambda function")
        {
            _dependencyResolver.Put("controller", new Descriptor("pip-services-dummies", "controller", "default", "*", "*"));
            _factories.Add(new DummyFactory());
        }
    }
}
