using PipServices3.Aws.Containers;

namespace PipServices3.Aws.Example.Services
{
    public class DummyLambdaFunction: LambdaFunction
    {
        public DummyLambdaFunction() : base("dummy", "Dummy lambda function")
        {
            _factories.Add(new DummyFactory());
        }
    }
}
