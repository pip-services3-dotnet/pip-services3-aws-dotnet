using PipServices3.Aws.Clients;
using PipServices3.Aws.Example;
using PipServices3.Commons.Data;

using System.Threading.Tasks;

namespace PipServices3.Aws.Example.Clients
{
    public class DummyLambdaClient : LambdaClient, IDummyClient
    {
        public DummyLambdaClient() : base() { }
        public async Task<Dummy> CreateDummyAsync(string correlationId, Dummy dummy)
        {
            return await CallAsync<Dummy>("create_dummy", correlationId, new { dummy });
        }

        public async Task<Dummy> DeleteDummyAsync(string correlationId, string dummyId)
        {
            return await CallAsync<Dummy>("delete_dummy", correlationId, new { dummy_id = dummyId });
        }

        public async Task<DataPage<Dummy>> GetDummiesAsync(string correlationId, FilterParams filter, PagingParams paging)
        {
            return await CallAsync<DataPage<Dummy>>("get_dummies", correlationId, new
            {
                filter,
                paging
            });
        }

        public async Task<Dummy> GetDummyByIdAsync(string correlationId, string dummyId)
        {
            return await CallAsync<Dummy>("get_dummy_by_id", correlationId, new
            {
                dummy_id = dummyId
            });
        }

        public async Task<Dummy> UpdateDummyAsync(string correlationId, Dummy dummy)
        {
            return await CallAsync<Dummy>("update_dummy", correlationId, new { dummy });
        }
    }
}
