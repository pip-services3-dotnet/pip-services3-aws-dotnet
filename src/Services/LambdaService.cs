using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PipServices3.Aws.Utils;
using PipServices3.Commons.Config;
using PipServices3.Commons.Convert;
using PipServices3.Commons.Errors;
using PipServices3.Commons.Refer;
using PipServices3.Commons.Run;
using PipServices3.Commons.Validate;
using PipServices3.Components.Count;
using PipServices3.Components.Log;
using PipServices3.Components.Trace;
using PipServices3.Aws.Clients;

namespace PipServices3.Aws.Services
{
    /// <summary>
    /// Abstract service that receives remove calls via AWS Lambda protocol.
    /// 
    /// This service is intended to work inside LambdaFunction container that
    /// exploses registered actions externally.
    /// 
    /// ### Configuration parameters ###
    /// 
    /// - dependencies:
    ///     - controller:            override for Controller dependency
    ///     
    /// ### References ###
    ///     - *:logger:*:*:1.0      (optional) <see cref="ILogger"> components to pass log messages
    ///     - *:counters:*:*:1.0    (optional) <see cref="ICounters"> components to pass collected measurements
    ///     
    /// <see cref="LambdaClient"/>
    /// 
    /// <example>
    /// <code>
    /// public class MyLambdaService : LambdaService
    /// {
    ///     private IMyController _controller;
    ///     /// <summary>
    ///     ///  ...
    ///     /// </summary>
    ///     public MyLambdaService() : base("v1.myservice")
    ///     {
    ///         _dependencyResolver.Put(
    ///             "controller",
    ///             new Descriptor("mygroup", "controller", "*", "*", "1.0")
    ///         );
    ///     }
    ///     public override void SetReferences(IReferences references)
    ///     {
    ///         base.SetReferences(references);
    ///         this._controller = _dependencyResolver.GetRequired<IMyController>("controller");
    ///     }
    ///     protected override void Register()
    ///     {
    ///         RegisterAction("get_mydata", null, async (input) =>
    ///         {
    ///             var body = AwsLambdaHelper.GetParameters(input);
    ///             var data = await this._controller.GetMyDataAsync(
    ///             GetCorrelationId(input),
    ///                 body.GetAsNullableString("id")
    ///             );
    ///             return JsonConverter.ToJson(data);
    ///         });
    ///     }
    /// }
    /// 
    /// var service = new MyLambdaService();
    /// service.Configure(ConfigParams.FromTuples(
    ///     "connection.protocol", "http",
    ///     "connection.host", "localhost",
    ///     "connection.port", 8080
    /// ));
    /// service.SetReferences(References.FromTuples(
    ///    new Descriptor("mygroup", "controller", "default", "default", "1.0"), controller
    /// ));
    /// await service.OpenAsync("123");
    /// Console.WriteLine("The GRPC service is running on port 8080");
    /// 
    /// </code>
    /// </example>
    /// </summary>
    public abstract class LambdaService : ILambdaService, IOpenable, IConfigurable,
    IReferenceable
    {

        private string _name;
        private List<LambdaAction> _actions = new();
        private List<Func<string, Func<string, Task<string>>, Task<string>>> _interceptors = new();
        private bool _opened = false;

        /// <summary>
        /// The dependency resolver.
        /// </summary>
        protected DependencyResolver _dependencyResolver = new DependencyResolver();

        /// <summary>
        /// The logger.
        /// </summary>
        protected CompositeLogger _logger = new CompositeLogger();

        /// <summary>
        /// The performance counters.
        /// </summary>
        protected CompositeCounters _counters = new CompositeCounters();

        /// <summary>
        /// The tracer.
        /// </summary>
        protected CompositeTracer _tracer = new CompositeTracer();

        /// <summary>
        /// Creates an instance of this service.
        /// </summary>
        public LambdaService() : this(null) { }

        /// <summary>
        /// Creates an instance of this service.
        /// </summary>
        /// <param name="name">a service name to generate action cmd.</param>
        public LambdaService(string name)
        {
            _name = name;
        }

        /// <summary>
        /// Configures component by passing configuration parameters.
        /// </summary>
        /// <param name="config">configuration parameters to be set.</param>
        public virtual void Configure(ConfigParams config)
        {
            this._dependencyResolver.Configure(config);
        }

        /// <summary>
        /// Sets references to dependent components.
        /// </summary>
        /// <param name="references">references to locate the component dependencies. </param>
        public virtual void SetReferences(IReferences references)
        {
            _logger.SetReferences(references);
            _counters.SetReferences(references);
            _tracer.SetReferences(references);
            _dependencyResolver.SetReferences(references);
        }

        /// <summary>
        /// Get all actions supported by the service.
        /// </summary>
        /// <returns>an array with supported actions.</returns>
        public IList<LambdaAction> GetActions()
        {
            return _actions;
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
        public Task OpenAsync(string correlationId)
        {
            if (_opened)
                return Task.CompletedTask;


            Register();

            _opened = true;

            return Task.CompletedTask;
        }

        /// <summary>
        /// Closes component and frees used resources.
        /// </summary>
        /// <param name="correlationId">(optional) transaction id to trace execution through call chain.</param>
        public Task CloseAsync(string correlationId)
        {
            if (!_opened)
            {
                return Task.CompletedTask;
            }

            _opened = false;
            _actions = new();
            _interceptors = new();

            return Task.CompletedTask;
        }

        /// <summary>
        /// Registers all service routes in HTTP endpoint.
        /// 
        /// This method is called by the service and must be overridden
        /// in child classes.
        /// </summary>
        protected abstract void Register();

        protected Func<string, Task<string>> ApplyValidation(Schema schema, Func<string, Task<string>> action)
        {
            // Create an action function
            async Task<string> actionWrapper(string input)
            {
                // Validate object
                if (schema != null)
                {
                    // Perform validation
                    var correlationId = GetCorrelationId(input);
                    var parameters = GetParameters(input);
                    var err = schema.ValidateAndReturnException(correlationId, parameters, false);
                    if (err != null)
                        return JsonConverter.ToJson(err);
                }

                return await action(input);
            };

            return actionWrapper;
        }
        protected Func<string, Task<string>> ApplyInterceptors(Func<string, Task<string>> action)
        {
            var actionWrapper = action;

            for (var index = _interceptors.Count - 1; index >= 0; index--)
            {
                var interceptor = _interceptors[index];

                Func<string, Task<string>> wrapper(Func<string, Task<string>> action)
                {
                    return async (string input) => await interceptor(input, action);
                }

                actionWrapper = wrapper(actionWrapper);
            }

            return actionWrapper;
        }

        public string GenerateActionCmd(string name)
        {
            var cmd = name;
            if (_name != null)
                cmd = _name + "." + cmd;

            return cmd;
        }

        /// <summary>
        /// Registers a action in Lambda function function.
        /// </summary>
        /// <param name="name">an action name</param>
        /// <param name="schema">a validation schema to validate received parameters.</param>
        /// <param name="action">an action function that is called when operation is invoked.</param>
        protected void RegisterAction(string name, Schema schema, Func<string, Task<string>> action)
        {
            var actionWrapper = ApplyValidation(schema, action);
            actionWrapper = ApplyInterceptors(action);

            var registeredAction = new LambdaAction()
            {
                Cmd = GenerateActionCmd(name),
                Schema = schema,
                Action = async (input) => await actionWrapper(input)
            };

            _actions.Add(registeredAction);
        }

        /// <summary>
        /// Registers a action in Lambda function with authorizer.
        /// </summary>
        /// <param name="name">an action name</param>
        /// <param name="schema">a validation schema to validate received parameters.</param>
        /// <param name="authorize">an action function that authorize user before calling action.</param>
        /// <param name="action">an action function that is called when operation is invoked.</param>
        protected void RegisterActionWithAuth(string name, Schema schema,
        Func<string, Func<string, Task<string>>, Task<string>> authorize,
            Func<string, Task<string>> action)
        {
            var actionWrapper = ApplyValidation(schema, action);

            // Add authorization just before validation
            actionWrapper = (req) =>
            {
                return authorize(req, actionWrapper);
            };
            actionWrapper = this.ApplyInterceptors(actionWrapper);

            var self = this;
            var registeredAction = new LambdaAction()
            {
                Cmd = GenerateActionCmd(name),
                Schema = schema,
                Action = async (input) => { return await actionWrapper(input); }
            };

            _actions.Add(registeredAction);
        }

        /// <summary>
        /// Registers a middleware for actions in Google Function service.
        /// </summary>
        /// <param name="action">an action function that is called when middleware is invoked.</param>
        protected void RegisterInterceptor(string cmd,
            Func<string, Func<string, Task<string>>, Task<string>> action)
        {
            // Match by cmd pattern
            //async Task<string> interceptorWrapper(string input, Func<string, Task<string>> next)
            //{
            //    var currCmd = GetCommand(input);
            //    var match = Regex.Match(currCmd, cmd, RegexOptions.IgnoreCase).Success;

            //    if (cmd != null && cmd != "" && !match)
            //        return await next(input);
            //    else
            //        return await action(input, next);
            //}

            _interceptors.Add(action);
        }

        /// <summary>
        /// Calls registered action in this lambda function.
        /// "cmd" parameter in the action parameters determin
        /// what action shall be called.
        /// 
        /// This method shall only be used in testing.
        /// </summary>
        /// <param name="input">action parameters.</param>
        /// <returns>action result</returns>
        /// <exception cref="BadRequestException"></exception>
        public async Task<string> ActAsync(string input)
        {
            var cmd = GetCommand(input);
            var correlationId = GetCorrelationId(input);

            if (string.IsNullOrEmpty(cmd))
            {
                throw new BadRequestException(
                    correlationId,
                    "NO_COMMAND",
                    "Cmd parameter is missing"
                );
            }

            LambdaAction action = this._actions.Find(a => a.Cmd == cmd);

            if (action == null)
            {
                throw new BadRequestException(
                    correlationId,
                    "NO_ACTION",
                    "Action " + cmd + " was not found"
                ).WithDetails("command", cmd);
            }

            return await action.Action(input);
        }

        /// <summary>
        /// Returns command from Lambda function context.
        /// This method can be overloaded in child classes
        /// </summary>
        /// <param name="request">the context request</param>
        /// <returns>returns command from request/returns>
        protected string GetCommand(string input)
        {
            return AwsLambdaHelper.GetCommand(input);
        }

        /// <summary>
        /// Returns correlationId from Lambda function context.
        /// This method can be overloaded in child classes
        /// </summary>
        /// <param name="request">the context request</param>
        /// <returns>returns correlationId from request</returns>
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
