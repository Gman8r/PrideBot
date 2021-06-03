using PrideBot.Models;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace PrideBot.Repository
{
    public partial class ModelRepository
    {

        public async Task<IEnumerable<Bullshit>> GetAllBullshitsAsync(SqlConnection conn)
        => (await new SqlCommand($"select * from VI_BULLSHIT", conn).ExecuteReaderAsync()).As<Bullshit>();

        public async Task<Bullshit> GetBullshitAsync(SqlConnection conn, string id)
        => (await new SqlCommand($"select * from VI_BULLSHIT where BULLSHIT_ID = '{id}'", conn).ExecuteReaderAsync()).As<Bullshit>().FirstOrDefault();

        public async Task<int> UpdateBullshitAsync(SqlConnection conn, Bullshit value)
            => await DatabaseHelper.GetUpdateCommand(conn, value, "BULLSHIT").ExecuteNonQueryAsync();
    }
}
