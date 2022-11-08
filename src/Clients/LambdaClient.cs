using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Amazon;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon.Runtime;

using PipServices3.Aws.Connect;
using PipServices3.Commons.Config;
using PipServices3.Commons.Convert;
using PipServices3.Commons.Errors;
using PipServices3.Commons.Refer;
using PipServices3.Commons.Run;
using PipServices3.Components.Count;
using PipServices3.Components.Log;
using PipServices3.Components.Trace;

namespace PipServices3.Aws.Clients
{
    /// <summary>
    /// Abstract client that calls Lambda function.
    /// 
    /// When making calls "cmd" parameter determines which what action shall be called, while
    /// other parameters are passed to the action itself.
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
    /// 
    /// ### References ###
    ///     - *:logger:*:*:1.0         (optional) <see cref="ILogger"> components to pass log messages
    ///     - *:counters:*:*:1.0         (optional) <see cref="ICounters"> components to pass collected measurements
    ///     - *:discovery:*:*:1.0        (optional) <see cref="IDiscovery"> services to resolve connection
    ///     - *:credential-store:*:*:1.0  (optional) Credential stores to resolve credentials
    ///     
    /// See <see cref="CommandableLambdaClient"/>, <see cref="LambdaFunction"/> 
    /// </summary>
    /// <example>
    /// <code>
    /// 
    /// class MyLambdaClient: LambdaClient, IMyClient
    /// {
    /// ...
    /// 
    ///     public async Task<MyData> GetDataAsync(string correlationId, string id) {
    ///         var timing = this.Instrument(correlationId, "myclient.get_data");
    ///         var result = await this.CallAsync<MyData>("get_data", correlationId, new { id=id });
    ///         timing.EndTiming();
    ///         return result;
    ///     }
    ///     ...
    /// 
    ///     public async Task Main()
    ///     {
    ///         var client = new MyLambdaClient();
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
    /// </code>
    /// </example>
    public abstract class LambdaClient : IOpenable, IConfigurable, IReferenceable
    {
        /// <summary>
        /// The reference to AWS Lambda Function.
        /// </summary>
        protected AmazonLambdaClient _lambda;

        /// <summary>
        /// The opened flag.
        /// </summary>
        protected bool _opened = false;

        /// <summary>
        /// The AWS connection parameters
        /// </summary>
        protected AwsConnectionParams _connection;

        /// <summary>
        /// The connection timeout in milliseconds.
        /// </summary>
        protected int _connectTimeout = 10000;

        /// <summary>
        /// The remote service uri which is calculated on open.
        /// </summary>
        protected string _uri;

        /// <summary>
        /// The dependencies resolver.
        /// </summary>
        protected DependencyResolver _dependencyResolver = new();

        /// <summary>
        /// The connection resolver.
        /// </summary>
        protected AwsConnectionResolver _connectionResolver = new();

        /// <summary>
        /// The logger.
        /// </summary>
        protected CompositeLogger _logger = new();

        /// <summary>
        /// The performance counters.
        /// </summary>
        protected CompositeCounters _counters = new();

        /// <summary>
        /// The tracer.
        /// </summary>
        protected CompositeTracer _tracer = new();

        /// <summary>
        /// Configures component by passing configuration parameters.
        /// </summary>
        /// <param name="config">configuration parameters to be set.</param>
        public void Configure(ConfigParams config)
        {
            _connectionResolver.Configure(config);
            _dependencyResolver.Configure(config);

            _connectTimeout = config.GetAsIntegerWithDefault("options.connect_timeout", _connectTimeout);
        }

        /// <summary>
        /// Sets references to dependent components.
        /// </summary>
        /// <param name="references">references to locate the component dependencies.</param>
        public void SetReferences(IReferences references)
        {
            _logger.SetReferences(references);
            _counters.SetReferences(references);
            _connectionResolver.SetReferences(references);
            _dependencyResolver.SetReferences(references);
        }

        /// <summary>
        /// Adds instrumentation to log calls and measure call time. It returns a CounterTiming
        /// object that is used to end the time measurement.
        /// </summary>
        /// <param name="correlationId">(optional) transaction id to trace execution through call chain.</param>
        /// <param name="methodName">a method name.</param>
        /// <returns>CounterTiming object to end the time measurement.</returns>
        protected CounterTiming Instrument(string correlationId, string methodName)
        {
            _logger.Trace(correlationId, "Executing {0} method", methodName);
            _counters.IncrementOne(methodName + ".exec_count");
            return _counters.BeginTiming(methodName + ".exec_time");
        }

        /// <summary>
        /// Adds instrumentation to error handling.
        /// </summary>
        /// <param name="correlationId">(optional) transaction id to trace execution through call chain.</param>
        /// <param name="methodName">a method name.</param>
        /// <param name="ex">Error that occured during the method call</param>
        /// <param name="rethrow">True to throw the exception</param>
        protected void InstrumentError(string correlationId, string methodName, Exception ex, bool rethrow = false)
        {
            _logger.Error(correlationId, ex, "Failed to execute {0} method", methodName);
            _counters.IncrementOne(methodName + ".exec_errors");

            if (rethrow)
                throw ex;
        }

        /// <summary>
        /// Checks if the component is opened.
        /// </summary>
        /// <returns>true if the component has been opened and false otherwise.</returns>
        public bool IsOpen()
        {
            return _opened;
        }

        /// <summary>
        /// Opens the component.
        /// </summary>
        /// <param name="correlationId">(optional) transaction id to trace execution through call chain.</param>
        /// <returns></returns>
        public async Task OpenAsync(string correlationId)
        {
            if (IsOpen())
                return;

            _connection = await _connectionResolver.ResolveAsync(correlationId);

            BasicAWSCredentials awsCredentials = new BasicAWSCredentials(_connection.AccessId, _connection.AccessKey);
            AmazonLambdaConfig lambdaConfig = new AmazonLambdaConfig() { RegionEndpoint = RegionEndpoint.GetBySystemName(_connection.Region) };
            lambdaConfig.Timeout = TimeSpan.FromMilliseconds(_connectTimeout);

            _lambda = new AmazonLambdaClient(awsCredentials, lambdaConfig);

            _opened = true;
            _logger.Debug(correlationId, "Lambda client connected to %s", _connection.Arn);
        }

        /// <summary>
        /// Closes component and frees used resources.
        /// </summary>
        /// <param name="correlationId">(optional) transaction id to trace execution through call chain.</param>
        /// <returns></returns>
        public Task CloseAsync(string correlationId)
        {
            if (!IsOpen())
                return Task.CompletedTask;

            _opened = false;

            return Task.CompletedTask;
        }

        /// <summary>
        /// Performs AWS Lambda Function invocation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="invocationType">an invocation type: "RequestResponse" or "Event"</param>
        /// <param name="cmd">>an action name to be called.</param>
        /// <param name="correlationId">(optional) transaction id to trace execution through call chain.</param>
        /// <param name="args">action arguments</param>
        /// <returns>action result.</returns>
        protected async Task<T> InvokeAsync<T>(string invocationType, string cmd, string correlationId, object args)
        {
            if (string.IsNullOrEmpty(cmd))
                throw new UnknownException(null, "NO_COMMAND", "Missing command: " + cmd);

            // TODO: optimize this conversion
            var data = JsonConverter.ToMap(JsonConverter.ToJson(args));
            var parameters = JsonConverter.ToMap(JsonConverter.ToJson(new { cmd, correlationId }));

            foreach(var param in parameters)
                data.Add(param.Key, param.Value);

            var lambdaRequest = new InvokeRequest
            {
                InvocationType = invocationType,
                LogType = "None",
                FunctionName = _connection.Arn,
                Payload = JsonConverter.ToJson(data)
            };

            try
            {
                var response = await _lambda.InvokeAsync(lambdaRequest);
                if (response != null)
                {
                    using (var sr = new StreamReader(response.Payload))
                    {
                        var result = await sr.ReadToEndAsync();
                        try
                        {
                            return JsonConverter.FromJson<T>(result);
                        }
                        catch (Exception ex)
                        {
                            throw new InvocationException(
                                correlationId,
                                "DESERIALIZATION_FAILED",
                                "Failed to deserialize result"
                            ).WithCause(ex);
                        }
                    }
                } else
                {
                    return default;
                }
            }
            catch (Exception ex)
            {
                throw new InvocationException(
                    correlationId,
                    "CALL_FAILED",
                    "Failed to invoke lambda function"
                ).WithCause(ex);
            }
        }

        /// <summary>
        /// Calls a AWS Lambda Function action.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="cmd">an action name to be called.</param>
        /// <param name="correlationId">(optional) transaction id to trace execution through call chain.</param>
        /// <param name="args">(optional) action parameters.</param>
        /// <returns>action result.</returns>
        protected async Task<T> CallAsync<T>(string cmd, string correlationId, object args)
        {
            return await InvokeAsync<T>("RequestResponse", cmd, correlationId, args);
        }

        /// <summary>
        /// Calls a AWS Lambda Function action asynchronously without waiting for response.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="cmd">an action name to be called.</param>
        /// <param name="correlationId">(optional) transaction id to trace execution through call chain.</param>
        /// <param name="data">(optional) action parameters.</param>
        /// <returns>action result</returns>
        protected async Task<T> CallOneWayAsync<T>(string cmd, string correlationId, object data)
        {
            return await InvokeAsync<T>("Event", cmd, correlationId, data);
        }
    }
}