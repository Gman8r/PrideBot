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

        public async Task<Quiz> GetQuizAsync(SqlConnection conn, string quizId)
        => (await new SqlCommand($"select * from VI_QUIZZES where QUIZ_ID = '{quizId}'", conn).ExecuteReaderAsync()).As<Quiz>().FirstOrDefault();

        public async Task<IEnumerable<Quiz>> GetQuizzesForDayAsync(SqlConnection conn, string day)
        => (await new SqlCommand($"select * from VI_QUIZZES where DAY = '{day}'", conn).ExecuteReaderAsync()).As<Quiz>();

        public async Task<QuizLog> GetOrCreateQuizLogAsync(SqlConnection conn, string userId, string day)
        {
            var command = new SqlCommand("SP_GET_OR_CREATE_QUIZ_LOG", conn);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@USER_ID", userId));
            command.Parameters.Add(new SqlParameter("@DAY", day));
            await command.ExecuteNonQueryAsync();

            return (await new SqlCommand($"select * from VI_USER_QUIZ_LOGS where USER_ID = '{userId}' and DAY = '{day}'", conn)
                .ExecuteReaderAsync()).As<QuizLog>().FirstOrDefault();
        }

        public async Task<int> UpdateQuizLogAsync(SqlConnection conn, QuizLog value)
            => await DatabaseHelper.GetUpdateCommand(conn, value, "USER_QUIZ_LOGS").ExecuteNonQueryAsync();

        public async Task<QuizLog> GetLastQuizLogForUserAsync(SqlConnection conn, string userId, string beforeDay)
        => (await new SqlCommand($"select top 1 * from VI_USER_QUIZ_LOGS where USER_ID = '{userId}' and DAY < '{beforeDay}' order by DAY desc", conn).ExecuteReaderAsync()).As<QuizLog>().FirstOrDefault();

        public async Task<IEnumerable<QuizLog>> GetQuizLogsForUserAsync(SqlConnection conn, string userId)
        => (await new SqlCommand($"select * from VI_USER_QUIZ_LOGS where USER_ID = '{userId}'", conn).ExecuteReaderAsync()).As<QuizLog>();
    }
}
