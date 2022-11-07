using PipServices3.Aws.Containers;
using PipServices3.Aws.Utils;
using PipServices3.Commons.Convert;
using PipServices3.Commons.Data;
using PipServices3.Commons.Refer;
using PipServices3.Commons.Validate;
using System;
using System.Threading.Tasks;
using TypeCode = PipServices3.Commons.Convert.TypeCode;

namespace PipServices3.Aws.Example.Containers
{
    public class DummyLambdaFunction : LambdaFunction
    {
        private IDummyController _controller;

        public DummyLambdaFunction() : base("dummy", "Dummy lambda function")
        {
            this._dependencyResolver.Put("controller", new Descriptor("pip-services-dummies", "controller", "default", "*", "*"));
            this._factories.Add(new DummyFactory());
        }

        public override void SetReferences(IReferences references)
        {
            base.SetReferences(references);
            _controller = this._dependencyResolver.GetOneRequired<IDummyController>("controller");
        }

        private async Task<string> GetPageByFilterAsync(string input)
        {
            var body = AwsLambdaHelper.GetParameters(input);
            var page = await _controller.GetPageByFilterAsync(
                GetCorrelationId(input),
                FilterParams.FromString(body.GetAsNullableString("filter")),
                PagingParams.FromTuples(
                    "total", body.GetAsBoolean("total"),
                    "skip", body.GetAsNullableLong("skip"),
                    "take", body.GetAsNullableLong("take")
                )
           );

            return JsonConverter.ToJson(page);
        }

        private async Task<string> GetOneByIdAsync(string input)
        {
            var body = AwsLambdaHelper.GetParameters(input);
            var dummy = await this._controller.GetOneByIdAsync(
            GetCorrelationId(input),
                body.GetAsNullableString("dummy_id")
            );

            if (dummy != null)
                return JsonConverter.ToJson(dummy);
            else
                return string.Empty;
        }

        private async Task<string> CreateAsync(string input)
        {
            var body = AwsLambdaHelper.GetParameters(input);
            var dummy = await _controller.CreateAsync(
            GetCorrelationId(input),
                JsonConverter.FromJson<Dummy>(JsonConverter.ToJson(body.GetAsObject("dummy")))
            );

            return JsonConverter.ToJson(dummy);
        }

        private async Task<string> UpdateAsync(string request)
        {
            var body = AwsLambdaHelper.GetParameters(request);
            var dummy = await this._controller.UpdateAsync(
            GetCorrelationId(request),
                JsonConverter.FromJson<Dummy>(JsonConverter.ToJson(body.GetAsObject("dummy")))
            );

            return JsonConverter.ToJson(dummy);
        }

        private async Task<string> DeleteByIdAsync(string request)
        {
            var body = AwsLambdaHelper.GetParameters(request);
            var dummy = await this._controller.DeleteByIdAsync(
            GetCorrelationId(request),
                body.GetAsNullableString("dummy_id")
            );

            return JsonConverter.ToJson(dummy);
        }

        [Obsolete("Overloading of this method has been deprecated. Use CloudFunctionService instead.", false)]
        protected override void Register()
        {
            RegisterAction("get_dummies", new ObjectSchema()
                        .WithOptionalProperty("filter", new FilterParamsSchema())
                        .WithOptionalProperty("paging", new PagingParamsSchema())
                        .WithRequiredProperty("cmd", TypeCode.String),
                GetPageByFilterAsync
            );

            RegisterAction("get_dummy_by_id", new ObjectSchema()
                        .WithRequiredProperty("dummy_id", TypeCode.String)
                        .WithRequiredProperty("cmd", TypeCode.String),
                GetOneByIdAsync
            );

            RegisterAction("create_dummy", new ObjectSchema()
                        .WithRequiredProperty("dummy", new DummySchema())
                        .WithRequiredProperty("cmd", TypeCode.String),
                CreateAsync
            );

            RegisterAction("update_dummy", new ObjectSchema()
                        .WithRequiredProperty("dummy", new DummySchema())
                        .WithRequiredProperty("cmd", TypeCode.String),
                UpdateAsync
            );

            RegisterAction("delete_dummy", new ObjectSchema()
                        .WithRequiredProperty("dummy_id", TypeCode.String)
                        .WithRequiredProperty("cmd", TypeCode.String),
                DeleteByIdAsync
            );
        }
    }
}
