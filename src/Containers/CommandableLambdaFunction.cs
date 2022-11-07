using System;
using PipServices3.Aws.Containers;
using PipServices3.Commons.Commands;

using JsonConverter = PipServices3.Commons.Convert.JsonConverter;

namespace PipServices3.Aws.Containers
{
    /// <summary>
    /// Abstract AWS Lambda function, that acts as a container to instantiate and run components
    /// and expose them via external entry point. All actions are automatically generated for commands
    /// defined in <see cref="ICommandable"> components. Each command is exposed as an action defined by "cmd" parameter.
    /// 
    /// Container configuration for this Lambda function is stored in "./config/config.yml" file.
    /// But this path can be overriden by CONFIG_PATH environment variable.
    /// 
    /// ### References ###
    ///     - *:logger:*:*:1.0                      (optional) <see cref="ILogger"> components to pass log messages
    ///     - *:counters:*:*:1.0                    (optional) <see cref="ICounters"> components to pass collected measurements
    ///     - *:service:lambda:*:1.0                (optional) services to handle action requests
    ///     - *:service:commandable-lambda:*:1.0    (optional) Credential stores to resolve credentials
    ///     
    /// <see cref="LambdaClient"/>
    /// 
    /// <example>
    /// <code>
    /// class MyLambdaFunction: CommandableLambdaFunction {
    ///     private IMyController _controller;
    ///     ...
    ///     public MyLambdaFunction() : base("mygroup", "MyGroup lambda function")
    ///     {
    ///         this._dependencyResolver.Put(
    ///             "controller",
    ///             new Descriptor("mygroup", "controller", "*", "*", "1.0")
    ///         );
    ///     }
    /// }
    /// var lambda = new MyLambdaFunction();
    /// 
    /// await service.RunAsync();
    /// Console.WriteLine("MyLambdaFunction is started");
    /// 
    /// </code>
    /// </example>
    /// </summary>
    [Obsolete("This component has been deprecated. Use LambdaService instead.", false)]
    public abstract class CommandableLambdaFunction : LambdaFunction
    {
        /// <summary>
        /// Creates a new instance of this Lambda function.
        /// </summary>
        /// <param name="name">(optional) a container name (accessible via ContextInfo)</param>
        /// <param name="description">(optional) a container description (accessible via ContextInfo)</param>
        public CommandableLambdaFunction(string name, string description) : base(name, description)
        {
            _dependencyResolver.Put("controller", "none");
        }

        private void RegisterCommandSet(CommandSet commandSet)
        {
            var commands = commandSet.Commands;

            for (var index = 0; index < commands.Count; index++)
            {
                var command = commands[index];

                RegisterAction(command.Name, null, async (context) => {
                    var correlationId = GetCorrelationId(context);
                    var args = GetParameters(context);

                    try
                    {
                        using var timing = this.Instrument(correlationId, _info.Name + '.' + command.Name);
                        var result = await command.ExecuteAsync(correlationId, args);
                        if (result is string)
                            return result as string;
                        else 
                            return JsonConverter.ToJson(result);
                    }
                    catch (Exception ex)
                    {
                        InstrumentError(correlationId, _info.Name + '.' + command.Name, ex);
                        return JsonConverter.ToJson(ex);
                    }
                });
            }
        }

        /// <summary>
        /// Registers all actions in this Lambda function.
        /// </summary>
        [Obsolete("Overloading of this method has been deprecated. Use LambdaService instead.", false)]
        protected override void Register()
        {
            ICommandable controller = _dependencyResolver.GetOneRequired<ICommandable>("controller");
            var commandSet = controller.GetCommandSet();
            this.RegisterCommandSet(commandSet);
        }
    }
}
