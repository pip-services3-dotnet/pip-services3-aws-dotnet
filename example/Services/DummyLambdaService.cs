using PipServices3.Aws.Services;
using PipServices3.Aws.Utils;
using PipServices3.Commons.Convert;
using PipServices3.Commons.Data;
using PipServices3.Commons.Refer;
using PipServices3.Commons.Validate;
using System.Threading.Tasks;
using TypeCode = PipServices3.Commons.Convert.TypeCode;

namespace PipServices3.Aws.Example.Services
{
    public class DummyLambdaService : LambdaService
    {
        private IDummyController _controller;

        public DummyLambdaService() : base("dummies")
        {
            _dependencyResolver.Put("controller", new Descriptor("pip-services-dummies", "controller", "default", "*", "*"));
        }

        public override void SetReferences(IReferences references)
        {
            base.SetReferences(references);
            _controller = _dependencyResolver.GetOneRequired<IDummyController>("controller");
        }

        private async Task<string> GetPageByFilterAsync(string input)
        {
            var body = GetParameters(input);
            var page = await _controller.GetPageByFilterAsync(
                GetCorrelationId(input),
                FilterParams.FromString(body.GetAsNullableString("filter")),
                PagingParams.FromTuples(
                    "total", AwsLambdaHelper.GetPropertyByName("total", input),
                    "skip", AwsLambdaHelper.GetPropertyByName("skip", input),
                    "take", AwsLambdaHelper.GetPropertyByName("take", input)
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

        private async Task<string> UpdateAsync(string input)
        {
            var body = AwsLambdaHelper.GetParameters(input);
            var dummy = await this._controller.UpdateAsync(
                GetCorrelationId(input),
                JsonConverter.FromJson<Dummy>(JsonConverter.ToJson(body.GetAsObject("dummy")))
            );

            return JsonConverter.ToJson(dummy);
        }

        private async Task<string> DeleteByIdAsync(string input)
        {
            var body = AwsLambdaHelper.GetParameters(input);
            var dummy = await this._controller.DeleteByIdAsync(
            GetCorrelationId(input),
                body.GetAsNullableString("dummy_id")
            );

            return JsonConverter.ToJson(dummy);
        }

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
