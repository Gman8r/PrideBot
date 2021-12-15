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

        public async Task<Score> GetScoreAsync(SqlConnection conn, string scoreId)
        => (await new SqlCommand($"select * from VI_SCORES where SCORE_ID = '{scoreId}'", conn).ExecuteReaderAsync()).As<Score>().FirstOrDefault();

        public async Task<IEnumerable<RecentScore>> GetRecentScoresForUserAsync(SqlConnection conn, string userId)
        => (await new SqlCommand($"select * from dbo.fnRecentScoresForUser('{userId}') order by LAST_TIME desc", conn).ExecuteReaderAsync()).As<RecentScore>();

        public async Task<IEnumerable<Score>> GetAllScoresAsync(SqlConnection conn)
        => (await new SqlCommand($"select * from VI_SCORES", conn).ExecuteReaderAsync()).As<Score>();

        public async Task<IEnumerable<Score>> GetScoresForUserAsync(SqlConnection conn, string userId)
        => (await new SqlCommand($"select * from VI_SCORES where USER_ID = '{userId}'", conn).ExecuteReaderAsync()).As<Score>();

        public async Task<IEnumerable<ScoreCategoryBreakdown>> GetScoreCategoryBreakdownsForUserAsync(SqlConnection conn, string userId)
        => (await new SqlCommand($"select * from dbo.fnScoreCategoriesForUser('{userId}') order by POINTS_EARNED desc", conn).ExecuteReaderAsync()).As<ScoreCategoryBreakdown>();

        public async Task<Score> GetLastScoreFromUserAndAchievementAsync(SqlConnection conn, string userId, string achievementId)
        => (await new SqlCommand($"select top 1 * from VI_SCORES where USER_ID = '{userId}' and ACHIEVEMENT_ID = '{achievementId}'" +
            $" order by TIMESTAMP desc", conn).ExecuteReaderAsync()).As<Score>().FirstOrDefault();

        public async Task<Score> GetLastScoreFromAchievementAsync(SqlConnection conn, string achievementId)
        => (await new SqlCommand($"select top 1 * from VI_SCORES where ACHIEVEMENT_ID = '{achievementId}'" +
            $" order by TIMESTAMP desc", conn).ExecuteReaderAsync()).As<Score>().FirstOrDefault();

        public async Task<int> UpdateScoreAsync(SqlConnection conn, Score value)
            => await DatabaseHelper.GetUpdateCommand(conn, value, "SCORES").ExecuteNonQueryAsync();


        public class AddScoreResult
        {
            public string ScoreId { get; }
            public AddScoreError errorCode { get; }
            public DateTime CooldownExpires { get; }

            public AddScoreResult(string scoreId, AddScoreError errorCode, DateTime cooldownExpires)
            {
                this.ScoreId = scoreId;
                this.errorCode = errorCode;
                this.CooldownExpires = cooldownExpires;
            }
        }

        public enum AddScoreError
        {
            None = 0,
            UserNotRegistered = 1,
            CooldownViolated = 2
        }

        public async Task<AddScoreResult> AttemptAddScoreAsync(SqlConnection conn, string userId, string achievementId, int pointsEarned, string approverId, bool ignoreCooldown)
        {
            var command = new SqlCommand("SP_ADD_SCORE", conn);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@USER_ID", userId));
            command.Parameters.Add(new SqlParameter("@ACHIEVEMENT_ID", achievementId));
            command.Parameters.Add(new SqlParameter("@POINTS_EARNED", pointsEarned));
            command.Parameters.Add(new SqlParameter("@APPROVER", approverId));
            command.Parameters.Add(new SqlParameter("@IGNORE_COOLDOWN", ignoreCooldown ? "Y" : "N"));
            var scoreIdParam = new SqlParameter();
            scoreIdParam.ParameterName = "@SCORE_ID";
            scoreIdParam.Direction = ParameterDirection.Output;
            scoreIdParam.DbType = DbType.Int32;
            command.Parameters.Add(scoreIdParam);
            var errorCodeParam = new SqlParameter();
            errorCodeParam.ParameterName = "@ERROR_CODE";
            errorCodeParam.Direction = ParameterDirection.Output;
            errorCodeParam.DbType = DbType.Int32;
            command.Parameters.Add(errorCodeParam);
            var cooldownExpiresParam = new SqlParameter();
            cooldownExpiresParam.ParameterName = "@COOLDOWN_EXPIRES";
            cooldownExpiresParam.Direction = ParameterDirection.Output;
            cooldownExpiresParam.DbType = DbType.Date;
            command.Parameters.Add(cooldownExpiresParam);
            await command.ExecuteNonQueryAsync();
            return new AddScoreResult(scoreIdParam.Value.ToString(), (AddScoreError)errorCodeParam.Value,
                cooldownExpiresParam.Value is DBNull nullCdParam ? DateTime.Now : (DateTime)cooldownExpiresParam.Value);
        }

        public async Task<int> DeleteScoreAsync(SqlConnection conn, string scoreId)
        {
            var command = new SqlCommand("SP_DELETE_SCORE", conn);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@SCORE_ID", scoreId));
            return await command.ExecuteNonQueryAsync();
        }
    }
}
