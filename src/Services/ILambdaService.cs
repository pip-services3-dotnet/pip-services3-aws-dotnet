using System.Collections.Generic;

namespace PipServices3.Aws.Services
{
    /// <summary>
    /// An interface that allows to integrate lambda services into lambda function containers
    /// and connect their actions to the function calls.
    /// </summary>
    public interface ILambdaService
    {
        /// <summary>
        /// Get all actions supported by the service.
        /// </summary>
        /// <returns>an array with supported actions.</returns>
        IList<LambdaAction> GetActions();
    }
}
