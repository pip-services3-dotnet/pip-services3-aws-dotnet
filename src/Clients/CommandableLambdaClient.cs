using System;
using System.Threading.Tasks;
using PipServices3.Commons.Commands;

namespace PipServices3.Aws.Clients
{
    /// <summary>
    /// Abstract client that calls commandable Lambda Functions.
    /// 
    /// Commandable services are generated automatically for <see cref="ICommandable"/> objects.
    /// Each command is exposed as action determined by "cmd" parameter.
    /// 
    /// ### Configuration parameters ###
    /// 
    ///     - connections:                   
    ///         - discovery_key:               (optional) a key to retrieve the connection from <see cref="IDiscovery">
    ///         - region:                      (optional) AWS region
    ///     - credentials:    
    ///         - store_key:                   (optional) a key to retrieve the credentials from <see cref="ICredentialStore">
    ///         - access_id:                   AWS access/client id
    ///         - access_key:                  AWS access/client id
    ///     - options:
    ///         - connect_timeout:             (optional) connection timeout in milliseconds(default: 10 sec)
    /// 
    /// ### References ###
    ///     - *:logger:*:*:1.0         (optional) <see cref="ILogger"> components to pass log messages
    ///     - *:counters:*:*:1.0         (optional) <see cref="ICounters"> components to pass collected measurements
    ///     - *:discovery:*:*:1.0        (optional) <see cref="IDiscovery"> services to resolve connection
    ///     - *:credential-store:*:*:1.0  (optional) Credential stores to resolve credentials
    ///     
    /// See <see cref="LambdaFunction"/>
    /// </summary>
    /// 
    /// <example>
    /// <code>
    /// 
    /// class MyCommandableLambdaClient : CommandableLambdaClient, IMyClient
    /// {
    /// ...
    /// 
    ///     public async Task<MyData> GetDataAsync(string correlationId, string id) {
    ///         return await this.CallCommandAsync<MyData>("get_data", correlationId, new { id=id });
    ///     }
    ///     ...
    /// 
    ///     public async Task Main()
    ///     {
    ///         var client = new MyCommandableLambdaClient();
    ///         client.Configure(ConfigParams.FromTuples(
    ///             "connection.region", "us-east-1",
    ///             "connection.access_id", "XXXXXXXXXXX",
    ///             "connection.access_key", "XXXXXXXXXXX",
    ///             "connection.arn", "YYYYYYYYYYYYY"
    ///         ));
    /// 
    ///         var  result = await client.GetDataAsync("123", "1");
    ///     }
    /// }
    /// 
    /// 
    /// </code>
    /// </example>
    public class CommandableLambdaClient : LambdaClient
    {
        private readonly string _name;

        /// <summary>
        /// Creates a new instance of this client.
        /// </summary>
        /// <param name="name">a service name.</param>
        public CommandableLambdaClient(string name) : base()
        {
            _name = name;
        }

        /// <summary>
        /// Calls a remote action in AWS Lambda function.
        /// The name of the action is added as "cmd" parameter
        /// to the action parameters. 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="cmd">an action name</param>
        /// <param name="correlationId">(optional) transaction id to trace execution through call chain.</param>
        /// <param name="args">command parameters.</param>
        /// <returns>action result.</returns>
        public async Task<T> CallCommandAsync<T>(string cmd, string correlationId, object args)
            where T : class
        {
            var timing = Instrument(correlationId, _name + '.' + cmd);
            try
            {
                return await CallAsync<T>(correlationId, correlationId, args);
            }
            catch (Exception ex)
            {
                InstrumentError(correlationId, _name + '.' + cmd, ex);
                throw;
            }
            finally
            {
                timing.EndTiming();
            }
        }
    }
}