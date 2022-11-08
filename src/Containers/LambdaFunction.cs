using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using PipContainer = PipServices3.Container;
using PipServices3.Components.Count;
using PipServices3.Components.Log;
using PipServices3.Commons.Config;
using PipServices3.Commons.Errors;
using PipServices3.Aws.Services;
using PipServices3.Commons.Run;
using PipServices3.Commons.Convert;
using PipServices3.Commons.Refer;
using PipServices3.Commons.Validate;
using PipServices3.Components.Trace;
using PipServices3.Aws.Utils;

namespace PipServices3.Aws.Containers
{
    /// <summary>
    /// Abstract AWS Lambda function, that acts as a container to instantiate and run components
    /// and expose them via external entry point.
    /// 
    /// When handling calls "cmd" parameter determines which what action shall be called, while
    /// other parameters are passed to the action itself.
    /// 
    /// Container configuration for this Lambda function is stored in <code>"./config/config.yml"</code> file.
    /// But this path can be overriden by <code>CONFIG_PATH</code> environment variable.
    /// 
    /// ### References ###
    ///     - *:logger:*:*:1.0                      (optional) <see cref="ILogger"> components to pass log messages
    ///     - *:counters:*:*:1.0                    (optional) <see cref="ICounters"> components to pass collected measurements
    ///     - *:service:lambda:*:1.0                (optional) services to handle action requests
    ///     - *:service:commandable-lambda:*:1.0    (optional) Credential stores to resolve credentials
    /// 
    /// <see cref="LambdaClient"/>
    /// <example>
    /// <code>
    /// class MyLambdaFunction extends LambdaFunction {
    ///     public MyLambdaFunction(): base("mygroup", "MyGroup lambda function") { }
    /// }
    /// var lambda = new MyLambdaFunction();
    /// await service.RunAsync();
    /// Console.WriteLine("MyLambdaFunction is started");
    /// </code>
    /// </example>
    /// </summary>
    public abstract class LambdaFunction : PipContainer.Container
    {
        private readonly ManualResetEvent _exitEvent = new ManualResetEvent(false);

        /// <summary>
        /// The performanc counters.
        /// </summary>
        protected CompositeCounters _counters = new CompositeCounters();

        /// <summary>
        /// The tracer.
        /// </summary>
        protected CompositeTracer _tracer = new CompositeTracer();

        /// <summary>
        /// The dependency resolver.
        /// </summary>
        protected DependencyResolver _dependencyResolver = new DependencyResolver();

        /// <summary>
        /// The map of registred validation schemas.
        /// </summary>
        protected Dictionary<string, Schema> _schemas = new();

        /// <summary>
        /// The map of registered actions.
        /// </summary>
        protected Dictionary<string, Func<string, Task<string>>> _actions = new();

        /// <summary>
        /// The default path to config file.
        /// </summary>
        protected string _configPath = "../config/config.yml";

        /// <summary>
        /// Creates a new instance of this Lambda function.
        /// </summary>
        /// <param name="name">(optional) a container name (accessible via ContextInfo)</param>
        /// <param name="descriptor">(optional) a container description (accessible via ContextInfo)</param>
        public LambdaFunction(string name, string descriptor) : base(name, descriptor)
        {
            _logger = new ConsoleLogger();
        }

        private string GetConfigPath()
        {
            return Environment.GetEnvironmentVariable("CONFIG_PATH") ?? this._configPath;
        }

        private ConfigParams GetParameters()
        {
            return ConfigParams.FromValue(Environment.GetEnvironmentVariables());
        }

        private void CaptureErrors(string correlationId)
        {
            AppDomain.CurrentDomain.UnhandledException += (obj, e) =>
            {
                _logger.Fatal(correlationId, e.ExceptionObject.ToString(), "Process is terminated");
                _exitEvent.Set();
            };
        }

        private void CaptureExit(string correlationId)
        {
            _logger.Info(correlationId, "Press Control-C to stop the microservice...");

            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                _logger.Info(correlationId, "Goodbye!");

                eventArgs.Cancel = true;
                _exitEvent.Set();

                Environment.Exit(1);
            };

            // Wait and close
            _exitEvent.WaitOne();
        }

        /// <summary>
        /// Sets references to dependent components.
        /// </summary>
        /// <param name="references">references to locate the component dependencies. </param>
        public override void SetReferences(IReferences references)
        {
            base.SetReferences(references);
            _counters.SetReferences(references);
            _dependencyResolver.SetReferences(references);

            Register();
        }

        /// <summary>
        /// Opens the component.
        /// </summary>
        /// <param name="correlationId">(optional) transaction id to trace execution through call chain.</param>
        public new async Task OpenAsync(string correlationId)
        {
            if (IsOpen()) return;

            await base.OpenAsync(correlationId);
            RegisterServices();
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
        /// Runs this Lambda function, loads container configuration,
        /// instantiate components and manage their lifecycle,
        /// makes this function ready to access action calls.
        /// </summary>
        public async Task RunAsync()
        {
            var correlationId = _info.Name;
            var path = GetConfigPath();
            var parameters = GetParameters();
            ReadConfigFromFile(correlationId, path, parameters);

            CaptureErrors(correlationId);
            await OpenAsync(correlationId);
            CaptureExit(correlationId);
            await CloseAsync(correlationId);
        }

        /// <summary>
        /// Registers all actions in this Lambda function.
        /// 
        /// Note: Overloading of this method has been deprecated. Use <see cref="LambdaFunction"/> instead.
        /// </summary>
        [Obsolete("Overloading of this method has been deprecated. Use LambdaFunction instead.", false)]
        protected virtual void Register() { }

        /// <summary>
        /// Registers all Lambda function services in the container.
        /// </summary>
        protected void RegisterServices()
        {
            // Extract regular and commandable lambda function services from references
            var services = _references.GetOptional<ILambdaService>(
                new Descriptor("*", "service", "lambda", "*", "*")
            );
            var cmdServices = _references.GetOptional<ILambdaService>(
                new Descriptor("*", "service", "commandable-lambda", "*", "*")
            );

            services.AddRange(cmdServices);

            // Register actions defined in those services
            foreach (var service in services)
            {

                var actions = service.GetActions();
                foreach (var action in actions)
                {
                    RegisterAction(action.Cmd, action.Schema, action.Action);
                }
            }
        }

        /// <summary>
        /// Registers an action in this Lambda function.
        /// 
        /// Note: This method has been deprecated. Use <see cref="LambdaFunction"/> instead.
        /// </summary>
        /// <param name="cmd">a action/command name.</param>
        /// <param name="schema">a validation schema to validate received parameters.</param>
        /// <param name="action">an action function that is called when action is invoked.</param>
        /// <exception cref="UnknownException"></exception>
        [Obsolete("This method has been deprecated. Use LambdaFunction instead.", false)]
        protected void RegisterAction(string cmd, Schema schema, Func<string, Task<string>> action)
        {
            if (string.IsNullOrEmpty(cmd))
                throw new UnknownException(null, "NO_COMMAND", "Missing command");

            if (action == null)
                throw new UnknownException(null, "NO_ACTION", "Missing action");

            if (this._actions.ContainsKey(cmd))
                throw new UnknownException(null, "DUPLICATED_ACTION", cmd + "action already exists");

            Func<string, Task<string>> actionCurl = async (req) =>
            {
                // Perform validation
                if (schema != null)
                {
                    var param = GetParameters(req);
                    var correlationId = GetCorrelationId(req);
                    var err = schema.ValidateAndReturnException(correlationId, param, false);
                    if (err != null)
                        return JsonConverter.ToJson(err);
                }

                return await action(req);
            };

            _actions[cmd] = actionCurl;
        }

        /// <summary>
        /// Executes this Lambda function and returns the result.
        /// This method can be overloaded in child classes
        /// if they need to change the default behavior
        /// </summary>
        /// <param name="input">the request function</param>
        /// <returns>task</returns>
        /// <exception cref="BadRequestException"></exception>
        protected async Task<string> ExecuteAsync(string input)
        {
            string cmd = GetCommand(input);
            string correlationId = GetCorrelationId(input);

            if (string.IsNullOrEmpty(cmd))
            {
                return JsonConverter.ToJson(new BadRequestException(
                    correlationId,
                    "NO_COMMAND",
                    "Cmd parameter is missing"
                ));
            }

            var action = this._actions[cmd];
            if (action == null)
            {
                return JsonConverter.ToJson(new BadRequestException(
                    correlationId,
                    "NO_ACTION",
                    "Action " + cmd + " was not found"
                )
                .WithDetails("command", cmd));
            }

            return await action(input);
        }

        private async Task<string> Handler(string input)
        {
            // If already started then execute
            if (IsOpen())
                return await ExecuteAsync(input);
            // Start before execute
            await RunAsync();
            return await ExecuteAsync(input);
        }

        public async Task<string> ActAsync(string input)
        {
            return await GetHandlerAsync()(input);
        }

        /// <summary>
        /// Gets entry point into this Lambda function.
        /// </summary>
        /// <returns>Returns plugin function</returns>
        public Func<string, Task<string>> GetHandlerAsync()
        {
            return Handler;
        }

        protected string GetCommand(string input)
        {
            return AwsLambdaHelper.GetCommand(input);
        }

        protected string GetCorrelationId(string input)
        {
            return AwsLambdaHelper.GetCorrelationId(input);
        }

        protected Parameters GetParameters(string input)
        {
            return AwsLambdaHelper.GetParameters(input);
        }
    }
}
