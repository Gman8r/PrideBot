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
        public async Task<IEnumerable<Dialogue>> GetAllDialogueAsync(SqlConnection conn)
        => (await new SqlCommand($"select * from VI_DIALOGUE", conn).ExecuteReaderAsync()).As<Dialogue>();

        public async Task<Dialogue> GetDialogueAsync(SqlConnection conn, string id)
        => (await new SqlCommand($"select * from VI_DIALOGUE where DIALOGUE_ID = '{id}'", conn).ExecuteReaderAsync()).As<Dialogue>().FirstOrDefault();
    }
}
