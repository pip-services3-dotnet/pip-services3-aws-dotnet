using Amazon.Lambda.Core;
using PipServices3.Aws.Example.Containers;
using PipServices3.Commons.Config;
using System.Text;

namespace PipServices3.Aws.Example;

public class Function
{
    static DummyCommandableLambdaFunction func = new DummyCommandableLambdaFunction();
    static Func<string, Task<string>> handler;
    /// <summary>
    /// A simple function that takes a string and does a ToUpper
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task<Stream> FunctionHandler(Stream stream, ILambdaContext context)
    {
        if (handler == null)
        {
            var config = ConfigParams.FromTuples(
                "logger.descriptor", "pip-services:logger:console:default:1.0"
            );
            func.Configure(config);
            await func.OpenAsync(null);

            handler = func.GetHandlerAsync();
        }

        using (var reader = new StreamReader(stream))
        {
            var res = reader.ReadToEnd();
            var response = await handler(res);
            return GenerateStreamFromString(response);
        }
    }

    public static MemoryStream GenerateStreamFromString(string value)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(value ?? ""));
    }
}
