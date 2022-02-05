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

        public async Task<int> NullifyAchievementCoooldowns(SqlConnection conn, DateTime since, bool includeChatAchievement)
        => (int)(await new SqlCommand($"update scores set COOLDOWN_NULLIFIED  = 'Y' where TIMESTAMP > '{since}'" +
            (!includeChatAchievement ? "and ACHIEVEMENT_ID not in ('CHAT')"  : ""), conn).ExecuteNonQueryAsync());

        public async Task<StandardTransactionError> ResetActiveUserPlushiesAsync(SqlConnection conn, string userId, DateTime timestamp)
        {
            var command = new SqlCommand("SP_RESET_ACTIVE_USER_PLUSHIES", conn);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@USER_ID", userId));
            command.Parameters.Add(new SqlParameter("@TIMESTAMP", timestamp));

            var errorCodeParam = new SqlParameter();
            errorCodeParam.ParameterName = "@ERROR_CODE";
            errorCodeParam.Direction = ParameterDirection.Output;
            errorCodeParam.DbType = DbType.Int32;
            command.Parameters.Add(errorCodeParam);

            await command.ExecuteNonQueryAsync();
            return (StandardTransactionError)errorCodeParam.Value;
        }
    }
}
