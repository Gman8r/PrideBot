using PrideBot.Models;
using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Data;
using Discord;

namespace PrideBot.Repository
{
    public partial class ModelRepository
    {

        public async Task<decimal> GetMysteryMedicineMultAsync(SqlConnection conn)
        => (decimal)(await new SqlCommand($"select dbo.fnPlGetMysteryMedicineMult()", conn).ExecuteScalarAsync());

        public async Task<int> GetClearanceSaleCardValueAsync(SqlConnection conn)
        => (int)(await new SqlCommand($"select dbo.fnPlGetClearanceSaleCardValue()", conn).ExecuteScalarAsync());
    }
}
