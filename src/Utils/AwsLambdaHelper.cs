using PipServices3.Commons.Run;


namespace PipServices3.Aws.Utils
{
    /// <summary>
    /// Class that helps to prepare function input
    /// </summary>
    public class AwsLambdaHelper
    {
        /// <summary>
        /// Returns correlationId from Lambda function input.
        /// </summary>
        /// <param name="input">the Lambda function input</param>
        /// <returns>returns correlationId from input</returns>
        public static string GetCorrelationId(string input)
        {
            return GetPropertyByName(input, "correlation_id");
        }

        /// <summary>
        /// Returns command from Lambda function context.
        /// </summary>
        /// <param name="input">the Lambda function input</param>
        /// <returns>returns command from input</returns>
        public static string GetCommand(string input)
        {
            return GetPropertyByName(input, "cmd");
        }

        /// <summary>
        /// Get input as Parameters object from input
        /// </summary>
        /// <param name="input">the Lambda function input</param>
        /// <returns>Parameters object</returns>
        public static Parameters GetParameters(string input)
        {
            return input != "" ? Parameters.FromJson(input) : new Parameters();
        }

        /// <summary>
        /// Extract property from input by name
        /// </summary>
        /// <param name="input">Lambda function input object</param>
        /// <param name="name">parameter name</param>
        /// <returns>parameter value as string or null</returns>
        public static string GetPropertyByName(string input, string name)
        {
            var parameters = input != "" ? Parameters.FromJson(input) : new Parameters();

            parameters.TryGetValue(name, out object res);

            // try with lower
            if (res == null)
                parameters.TryGetValue(name.ToLower(), out res);

            if (res != null)
                return res.ToString();
            else
                return string.Empty;
        }
    }
}