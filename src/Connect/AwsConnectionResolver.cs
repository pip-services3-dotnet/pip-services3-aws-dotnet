using System.Threading.Tasks;
using PipServices.Commons.Config;
using PipServices.Commons.Refer;
using PipServices.Components.Auth;
using PipServices.Components.Connect;

namespace PipServices.Aws.Connect
{
    public class AwsConnectionResolver : IConfigurable, IReferenceable
    {
        protected ConnectionResolver _connectionResolver = new ConnectionResolver();
        protected CredentialResolver _credentialResolver = new CredentialResolver();

        public void Configure(ConfigParams config)
        {
            _connectionResolver.Configure(config);
            _credentialResolver.Configure(config);
        }

        public void SetReferences(IReferences references)
        {
            _connectionResolver.SetReferences(references);
            _credentialResolver.SetReferences(references);
        }

        public async Task<AwsConnectionParams> ResolveAsync(string correlationId)
        {
            var result = new AwsConnectionParams();

            var connection = await _connectionResolver.ResolveAsync(correlationId);
            result.Append(connection);

            var credential = await _credentialResolver.LookupAsync(correlationId);
            result.Append(credential);

            // Force ARN parsing
            result.Arn = result.Arn;

            // Perform validation
            var err = result.Validate(correlationId);
            if (err != null) throw err;

            return result;
        }
    }
}
