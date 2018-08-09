using System;
using PipServices.Components.Log;
using Xunit;

namespace PipServices.Aws.Log
{
    public sealed class LoggerFixture
    {
        private readonly CachedLogger Logger;

        public LoggerFixture(CachedLogger logger)
        {
            Logger = logger;
        }

        public void TestLogLevel()
        {
            Assert.True(Logger.Level >= LogLevel.None);
            Assert.True(Logger.Level <= LogLevel.Trace);
        }

        public void TestSimpleLogging()
        {
            Logger.Level = LogLevel.Trace;

            Logger.Fatal(null, "Fatal error message");
            Logger.Error(null, "Error message");
            Logger.Warn(null, "Warning message");
            Logger.Info(null, "Information message");
            Logger.Debug(null, "Debug message");
            Logger.Trace(null, "Trace message");

            Logger.Dump();
        }

        public void TestErrorLogging()
        {
            try
            {
                // Raise an exception
                throw new Exception();
            }
            catch (Exception ex)
            {
                Logger.Fatal("123", ex, "Fatal error");
                Logger.Error("123", ex, "Recoverable error");
            }

            Logger.Dump();
        }
    }
}
