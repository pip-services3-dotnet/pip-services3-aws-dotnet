using System.Threading.Tasks;
using PipServices3.Commons.Config;
using PipServices3.Commons.Refer;
using PipServices3.Components.Auth;
using PipServices3.Components.Connect;

namespace PipServices3.Aws.Connect
{
    /// <summary>
    /// Helper class to retrieve AWS connection and credential parameters,
    /// validate them and compose a AwsConnectionParams value.
    /// 
    /// ### Configuration parameters ###
    /// 
    /// connections:
    /// - discovery_key:               (optional) a key to retrieve the connection from <a href="https://rawgit.com/pip-services3-dotnet/pip-services3-components-dotnet/master/doc/api/interface_pip_services_1_1_components_1_1_connect_1_1_i_discovery.html">IDiscovery</a>
    /// - region:                      (optional) AWS region
    /// - partition:                   (optional) AWS partition
    /// - service:                     (optional) AWS service
    /// - resource_type:               (optional) AWS resource type
    /// - resource:                    (optional) AWS resource id
    /// - arn:                         (optional) AWS resource ARN
    /// 
    /// credentials:
    /// - store_key:                   (optional) a key to retrieve the credentials from <a href="https://rawgit.com/pip-services3-dotnet/pip-services3-components-dotnet/master/doc/api/interface_pip_services_1_1_components_1_1_auth_1_1_i_credential_store.html">ICredentialStore</a>
    /// - access_id:                   AWS access/client id
    /// - access_key:                  AWS access/client id
    /// 
    /// ### References ###
    /// - *:discovery:*:*:1.0         (optional) <a href="https://rawgit.com/pip-services3-dotnet/pip-services3-components-dotnet/master/doc/api/interface_pip_services_1_1_components_1_1_connect_1_1_i_discovery.html">IDiscovery</a> services to resolve connections
    /// - *:credential-store:*:*:1.0  (optional) Credential stores to resolve credentials
    /// </summary>
    /// <example>
    /// <code>
    /// var config = ConfigParams.FromTuples(
    /// "connection.region", "us-east1",
    /// "connection.service", "s3",
    /// "connection.bucket", "mybucket",
    /// "credential.access_id", "XXXXXXXXXX",
    /// "credential.access_key", "XXXXXXXXXX"  );
    /// 
    /// let connectionResolver = new AwsConnectionResolver();
    /// connectionResolver.Configure(config);
    /// connectionResolver.SetReferences(references);
    /// 
    /// connectionResolver.Resolve("123");
    /// </code>
    /// </example>
    /// See <see cref="ConnectionParams"/>, <see cref="IDiscovery"/>
    public class AwsConnectionResolver : IConfigurable, IReferenceable
    {
        /// <summary>
        /// The connection resolver.
        /// </summary>
        protected ConnectionResolver _connectionResolver = new ConnectionResolver();
        /// <summary>
        /// The credential resolver.
        /// </summary>
        protected CredentialResolver _credentialResolver = new CredentialResolver();

        /// <summary>
        /// Configures component by passing configuration parameters.
        /// </summary>
        /// <param name="config">configuration parameters to be set.</param>
        public void Configure(ConfigParams config)
        {
            _connectionResolver.Configure(config);
            _credentialResolver.Configure(config);
        }

        /// <summary>
        /// Sets references to dependent components.
        /// </summary>
        /// <param name="references">references to locate the component dependencies.</param>
        public void SetReferences(IReferences references)
        {
            _connectionResolver.SetReferences(references);
            _credentialResolver.SetReferences(references);
        }

        /// <summary>
        /// Resolves connection and credental parameters and generates a single AWSConnectionParams value.
        /// </summary>
        /// <param name="correlationId">(optional) transaction id to trace execution through call chain.</param>
        /// <returns>AWSConnectionParams value</returns>
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
