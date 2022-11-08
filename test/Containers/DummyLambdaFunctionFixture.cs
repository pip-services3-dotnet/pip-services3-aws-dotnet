using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

using PipServices3.Commons.Validate;
using PipServices3.Commons.Convert;
using PipServices3.Aws.Example;
using PipServices3.Aws.Containers;

namespace PipServices3.Aws.Test.Containers
{
    public class DummyLambdaFunctionFixture
    {
        protected LambdaFunction _lambda;

        public DummyLambdaFunctionFixture(LambdaFunction lambda)
        {
            _lambda = lambda;
        }

        public async Task TestCrudOperations()
        {
            var DUMMY1 = new Dummy(null, "Key 1", "Content 1");
            var DUMMY2 = new Dummy(null, "Key 2", "Content 2");

            // Create one dummy
            var body = new Dictionary<string, object>() {
                { "cmd", "create_dummy" },
                { "dummy", DUMMY1 }
            };

            Dummy dummy1 = await ExecuteRequest<Dummy>(body);

            Assert.Equal(dummy1.Content, DUMMY1.Content);
            Assert.Equal(dummy1.Key, DUMMY1.Key);

            // Create another dummy
            body = new Dictionary<string, object>() {
                 { "cmd", "create_dummy" },
                 { "dummy", DUMMY2 }
            };

            Dummy dummy2 = await ExecuteRequest<Dummy>(body);
            Assert.Equal(dummy2.Content, DUMMY2.Content);
            Assert.Equal(dummy2.Key, DUMMY2.Key);

            // Update the dummy
            dummy1.Content = "Updated Content 1";
            body = new Dictionary<string, object>() {
                { "cmd", "update_dummy" },
                { "dummy", dummy1 }
            };

            Dummy updatedDummy1 = await ExecuteRequest<Dummy>(body);
            Assert.Equal(updatedDummy1.Id, dummy1.Id);
            Assert.Equal(updatedDummy1.Content, dummy1.Content);
            Assert.Equal(updatedDummy1.Key, dummy1.Key);
            dummy1 = updatedDummy1;

            // Delete dummy
            body = new Dictionary<string, object>() {
                { "cmd", "delete_dummy" },
                { "dummy_id", dummy1.Id }
            };

            Dummy deleted = await ExecuteRequest<Dummy>(body);

            Assert.Equal(deleted.Id, dummy1.Id);
            Assert.Equal(deleted.Content, dummy1.Content);
            Assert.Equal(deleted.Key, dummy1.Key);

            // Try to get deleted dummy
            body = new Dictionary<string, object>() {
                { "cmd", "get_dummy_by_id" },
                { "dummy_id", dummy1.Id }
            };

            var textResponse = await ExecuteRequest<string>(body);

            Assert.True(string.IsNullOrEmpty(textResponse));

            // Failed validation
            body = new Dictionary<string, object>() {
                { "cmd", "create_dummy" },
                { "dummy", null }
            };

            var exception = await ExecuteRequest<ValidationException>(body, true);

            Assert.Contains("INVALID_DATA", exception.Code);
        }

        private async Task<T> ExecuteRequest<T>(Dictionary<string, object> data, bool errResponse = false)
        {
            var json = JsonConverter.ToJson(data);

            var response = await _lambda.ActAsync(json);

            if (string.IsNullOrEmpty(response))
                return default;

            return JsonConverter.FromJson<T>(response);
        }
    }
}
