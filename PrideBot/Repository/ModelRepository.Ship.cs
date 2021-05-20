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
        public async Task<IEnumerable<Ship>> GetAllShipsAsync(SqlConnection conn)
        => (await new SqlCommand($"select * from VI_SHIPS", conn).ExecuteReaderAsync()).As<Ship>();

        public async Task<Ship> GetShipAsync(SqlConnection conn, string shipId)
        => (await new SqlCommand($"select * from VI_SHIPS where SHIP_ID = '{shipId}'", conn).ExecuteReaderAsync()).As<Ship>().FirstOrDefault();

        public async Task<Ship> GetShipAsync(SqlConnection conn, string char1Id, string char2Id)
        => (await new SqlCommand($"select * from VI_SHIPS where CHARACTER_ID_1 = '{char1Id}' and CHARACTER_ID_2 = '{char2Id}'", conn).ExecuteReaderAsync()).As<Ship>().FirstOrDefault();

        public async Task<string> GetOrCreateShipAsync(SqlConnection conn, string char1Id, string char2Id)
        {
            var command = new SqlCommand("SP_GET_OR_CREATE_SHIP", conn);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@CHARACTER_ID_1", char1Id));
            command.Parameters.Add(new SqlParameter("@CHARACTER_ID_2", char2Id));
            var shipIdParam = new SqlParameter();
            shipIdParam.ParameterName = "@SHIP_ID";
            shipIdParam.Direction = ParameterDirection.Output;
            shipIdParam.Size = 50;
            command.Parameters.Add(shipIdParam);
            await command.ExecuteNonQueryAsync();
            return shipIdParam.Value.ToString();
        }

        public async Task<int> AddShipAsync(SqlConnection conn, Ship value)
            => await DatabaseHelper.GetInsertCommand(conn, value, "SHIPS").ExecuteNonQueryAsync();

        public async Task<int> UpdateShipAsync(SqlConnection conn, Ship value)
            => await DatabaseHelper.GetUpdateCommand(conn, value, "SHIPS").ExecuteNonQueryAsync();

        public async Task<decimal> GetScoreRatioForShipTierAsync(SqlConnection conn, UserShipTier tier)
            => (decimal)(await new SqlCommand($"select dbo.fnGetScoreRatioForTier({(int)tier})", conn).ExecuteScalarAsync());

        public async Task SwapShipTiersAsync(SqlConnection conn, string userId, UserShipTier tier1, UserShipTier tier2)
        {
            var command = new SqlCommand("SP_SWAP_SHIP_TIERS", conn);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@USER_ID", userId));
            command.Parameters.Add(new SqlParameter("@TIER_1", (int)tier1));
            command.Parameters.Add(new SqlParameter("@TIER_2", (int)tier2));
            await command.ExecuteNonQueryAsync();
        }
    }
}
