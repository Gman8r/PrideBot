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

        public async Task<IEnumerable<Score>> GetScoresFromGroupAsync(SqlConnection conn, string groupId)
        => (await new SqlCommand($"select * from VI_SCORES where SCORE_GROUP_ID = '{groupId}'", conn).ExecuteReaderAsync()).As<Score>();

        public async Task<string> AddScoreAsync(SqlConnection conn, string userId, string achievementId, int pointsEarned)
        {
            var command = new SqlCommand("SP_ADD_SCORE", conn);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@USER_ID", userId));
            command.Parameters.Add(new SqlParameter("@ACHIEVEMENT_ID", achievementId));
            command.Parameters.Add(new SqlParameter("@POINTS_EARNED", pointsEarned));
            var scoreGroupIdParam = new SqlParameter();
            scoreGroupIdParam.ParameterName = "@SCORE_GROUP_ID";
            scoreGroupIdParam.Direction = ParameterDirection.Output;
            scoreGroupIdParam.DbType = DbType.Int32;
            //scoreGroupIdParam.Size = 50;
            command.Parameters.Add(scoreGroupIdParam);
            await command.ExecuteNonQueryAsync();
            return scoreGroupIdParam.Value.ToString();
        }

        public async Task<int> DeleteScoreAsync(SqlConnection conn, string groupId)
            => await new SqlCommand($"delete from VI_SCORES where SCORE_GROUP_ID = {groupId}", conn).ExecuteNonQueryAsync();
    }
}
