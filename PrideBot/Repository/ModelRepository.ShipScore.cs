using PrideBot.Models;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Data;

namespace PrideBot.Repository
{
    public partial class ModelRepository
    {

        public async Task<IEnumerable<ShipScore>> GetShipScoresAsync(SqlConnection conn, string scoreId)
        => (await new SqlCommand($"select * from VI_SHIP_SCORES where SCORE_ID = '{scoreId}'", conn).ExecuteReaderAsync()).As<ShipScore>();
    }
}
