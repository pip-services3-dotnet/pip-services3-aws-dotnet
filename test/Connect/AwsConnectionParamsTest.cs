using PipServices3.Commons.Config;
using Xunit;

namespace PipServices3.Aws.Connect
{
    public class AwsConnectionParamsTest
    {
        [Fact]
        public void TestEmptyConnection()
        {
            var connection = new AwsConnectionParams();
            Assert.Equal("arn:aws::::", connection.Arn);
        }

        [Fact]
        public void TestParseArn()
        {
            var connection = new AwsConnectionParams();

            connection.Arn = "arn:aws:lambda:us-east-1:12342342332:function:pip-services3-dummies";
            Assert.Equal("lambda", connection.Service);
            Assert.Equal("us-east-1", connection.Region);
            Assert.Equal("12342342332", connection.Account);
            Assert.Equal("function", connection.ResourceType);
            Assert.Equal("pip-services3-dummies", connection.Resource);

            connection.Arn = "arn:aws:s3:us-east-1:12342342332:pip-services3-dummies";
            Assert.Equal("s3", connection.Service);
            Assert.Equal("us-east-1", connection.Region);
            Assert.Equal("12342342332", connection.Account);
            Assert.Equal(null, connection.ResourceType);
            Assert.Equal("pip-services3-dummies", connection.Resource);

            connection.Arn = "arn:aws:lambda:us-east-1:12342342332:function/pip-services3-dummies";
            Assert.Equal("lambda", connection.Service);
            Assert.Equal("us-east-1", connection.Region);
            Assert.Equal("12342342332", connection.Account);
            Assert.Equal("function", connection.ResourceType);
            Assert.Equal("pip-services3-dummies", connection.Resource);
        }

        [Fact]
        public void TestComposeArn()
        {
            var connection = AwsConnectionParams.FromConfig(
                ConfigParams.FromTuples(
                    "connection.service", "lambda",
                    "connection.region", "us-east-1",
                    "connection.account", "12342342332",
                    "connection.resource_type", "function",
                    "connection.resource", "pip-services3-dummies",
                    "credential.access_id", "1234",
                    "credential.access_key", "ABCDEF"
                )
            );

            Assert.Equal("arn:aws:lambda:us-east-1:12342342332:function:pip-services3-dummies", connection.Arn);
            Assert.Equal("1234", connection.AccessId);
            Assert.Equal("ABCDEF", connection.AccessKey);
        }
        
        [Fact]
        public void TestComposeArnWithAnotherAccessKeyName()
        {
            var connection = AwsConnectionParams.FromConfig(
                ConfigParams.FromTuples(
                    "connection.service", "lambda",
                    "connection.region", "us-east-1",
                    "connection.account", "12342342332",
                    "connection.resource_type", "function",
                    "connection.resource", "pip-services3-dummies",
                    "credential.access_id", "1234",
                    "credential.secret_key", "ABCDEF"
                )
            );

            Assert.Equal("arn:aws:lambda:us-east-1:12342342332:function:pip-services3-dummies", connection.Arn);
            Assert.Equal("1234", connection.AccessId);
            Assert.Equal("ABCDEF", connection.AccessKey);
        }
        
        [Fact]
        public void TestComposeArnWithAnotherCredentialsParamNames()
        {
            var connection = AwsConnectionParams.FromConfig(
                ConfigParams.FromTuples(
                    "connection.service", "lambda",
                    "connection.region", "us-east-1",
                    "connection.account", "12342342332",
                    "connection.resource_type", "function",
                    "connection.resource", "pip-services3-dummies",
                    "credential.client_id", "1234",
                    "credential.client_key", "ABCDEF"
                )
            );

            Assert.Equal("arn:aws:lambda:us-east-1:12342342332:function:pip-services3-dummies", connection.Arn);
            Assert.Equal("1234", connection.AccessId);
            Assert.Equal("ABCDEF", connection.AccessKey);
        }
    }
}
