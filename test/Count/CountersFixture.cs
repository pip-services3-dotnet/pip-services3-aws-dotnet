using System;
using System.Threading;
using PipServices3.Components.Count;
using Xunit;

namespace PipServices3.Aws.Test.Count
{
    public sealed class CountersFixture
    {
        private readonly CachedCounters _counters;

        public CountersFixture(CachedCounters counters)
        {
            _counters = counters;
        }

        public void TestSimpleCounters()
        {
            _counters.Last("Test.LastValue", 123);
            _counters.Last("Test.LastValue", 123456);

            var counter = _counters.Get("Test.LastValue", CounterType.LastValue);
            Assert.NotNull(counter);
            Assert.True(counter.Last.HasValue);
            Assert.Equal(counter.Last.Value, (double)123456, 3);

            _counters.IncrementOne("Test.Increment");
            _counters.Increment("Test.Increment", 3);

            counter = _counters.Get("Test.Increment", CounterType.Increment);
            Assert.NotNull(counter);
            Assert.Equal(4, counter.Count);

            _counters.TimestampNow("Test.Timestamp");
            _counters.TimestampNow("Test.Timestamp");

            counter = _counters.Get("Test.Timestamp", CounterType.Timestamp);
            Assert.NotNull(counter);
            Assert.True(counter.Time.HasValue);

            _counters.Stats("Test.Statistics", 1);
            _counters.Stats("Test.Statistics", 2);
            _counters.Stats("Test.Statistics", 3);

            counter = _counters.Get("Test.Statistics", CounterType.Statistics);
            Assert.NotNull(counter);
            Assert.Equal(counter.Average.Value, (double)2, 3);

            _counters.Dump();
        }

        public void TestMeasureElapsedTime()
        {
            using (var timing = _counters.BeginTiming("Test.Elapsed"))
            {
                Thread.Sleep(100);

                timing.EndTiming();

                var counter = _counters.Get("Test.Elapsed", CounterType.Interval);
                Assert.NotNull(counter);
                Assert.True(counter.Last > 50);
                Assert.True(counter.Last < 5000);

                _counters.Dump();
            }
        }
    }
}
