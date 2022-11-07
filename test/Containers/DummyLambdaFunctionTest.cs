using System.Threading.Tasks;
using Xunit;
using System;

using PipServices3.Commons.Config;
using PipServices3.Aws.Example.Containers;

namespace PipServices3.Aws.Test.Containers
{
    public class DummyLambdaFunctionTest: IDisposable
    {
        private DummyLambdaFunctionFixture fixture;
        private DummyLambdaFunction lambda;

        public DummyLambdaFunctionTest()
        {
            var config = ConfigParams.FromTuples(
                "logger.descriptor", "pip-services:logger:console:default:1.0",
                "controller.descriptor", "pip-services-dummies:controller:default:default:1.0"
            );

            lambda = new DummyLambdaFunction();
            lambda.Configure(config);
            lambda.OpenAsync(null).Wait();

            fixture = new DummyLambdaFunctionFixture(lambda);
        }

        public void Dispose()
        {
            lambda.CloseAsync(null).Wait();
        }

        [Fact]
        public async Task TestCrudOperations()
        {
            await fixture.TestCrudOperations();
        }
    }
}
