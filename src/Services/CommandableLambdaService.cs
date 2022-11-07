using PipServices3.Commons.Commands;
using PipServices3.Commons.Convert;
using PipServices3.Aws.Clients;

using System;
using PipServices3.Commons.Refer;

namespace PipServices3.Aws.Services
{
    /// <summary>
    /// Abstract service that receives commands via AWS Lambda protocol
    /// to operations automatically generated for commands defined in <see cref="ICommandable"/> components.
    /// Each command is exposed as invoke method that receives command name and parameters.
    /// 
    /// Commandable services require only 3 lines of code to implement a robust external
    /// Lambda-based remote interface.
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
    /// 
    ///     - *:logger:*:*:1.0               (optional) <see cref="ILogger"> components to pass log messages
    ///     - *:counters:*:*:1.0             (optional) <see cref="ICounters"> components to pass collected measurements
    ///     
    /// <see cref="CommandableLambdaClient"/>
    /// <see cref="LambdaService"/>
    /// 
    /// <example>
    /// <code>
    /// class MyCommandableLambdaService: CommandableLambdaService {
    ///     public MyCommandableLambdaService()
    ///     {
    ///         this._dependencyResolver.Put(
    ///             "controller",
    ///             new Descriptor("mygroup", "controller", "*", "*", "1.0")
    ///         );
    ///     }
    /// }
    /// var service = new MyCommandableLambdaService();
    /// service.SetReferences(References.fromTuples(
    ///    new Descriptor("mygroup", "controller", "default", "default", "1.0"), controller
    /// ));
    /// await service.OpenAsync("123");
    /// Console.WriteLine("The AWS Lambda service is running");
    /// </code>
    /// </example>
    /// </summary>
    public abstract class CommandableLambdaService: LambdaService
    {
        private CommandSet _commandSet;

        /// <summary>
        /// Creates a new instance of the service.
        /// </summary>
        public CommandableLambdaService(): this(null) { }

        /// <summary>
        /// Creates a new instance of the service.
        /// </summary>
        /// <param name="name">a service name.</param>
        public CommandableLambdaService(string name): base(name)
        {
            _dependencyResolver.Put("controller", "none");
        }

        /// <summary>
        /// Registers all actions in AWS Lambda function.
        /// </summary>
        protected override void Register()
        {
            ICommandable controller = _dependencyResolver.GetOneRequired<ICommandable>("controller");
            _commandSet = controller.GetCommandSet();

            var commands = _commandSet.Commands;
            for (var index = 0; index < commands.Count; index++)
            {
                var command = commands[index];
                var name = command.Name;

                this.RegisterAction(name, null, async (request) => {
                    var correlationId = GetCorrelationId(request);
                    var args = GetParameters(request);
                    args.Remove("correlation_id");

                    try
                    {
                        using (var timing = Instrument(correlationId, name))
                        {
                            var result = await command.ExecuteAsync(correlationId, args);
                            return JsonConverter.ToJson(result);
                        }
                    }
                    catch (Exception ex)
                    {
                        InstrumentError(correlationId, name, ex);
                        return JsonConverter.ToJson(ex);
                    }
                });
            }
        }
    }
}
