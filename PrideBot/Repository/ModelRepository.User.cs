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
        public async Task<IEnumerable<Character>> GetAllUsersAsync(SqlConnection conn)
        => (await new SqlCommand($"select * from VI_USERS", conn).ExecuteReaderAsync()).As<Character>();

        public async Task<User> GetUserAsync(SqlConnection conn, string id)
        => (await new SqlCommand($"select * from VI_USERS where USER_ID = '{id.ToString()}'", conn).ExecuteReaderAsync()).As<User>().FirstOrDefault();

        public async Task<User> GetOrCreateUserAsync(SqlConnection conn, string userId)
        {
            var command = new SqlCommand("SP_GET_OR_CREATE_USER", conn);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@USER_ID", userId));
            await command.ExecuteNonQueryAsync();

            return (await new SqlCommand($"select * from VI_USERS where USER_ID = '{userId}'", conn)
                .ExecuteReaderAsync()).As<User>().FirstOrDefault();
        }

        public async Task<int> AddUserAsync(SqlConnection conn, User value)
            => await DatabaseHelper.GetInsertCommand(conn, value, "USERS").ExecuteNonQueryAsync();

        public async Task<int> UpdateUserAsync(SqlConnection conn, User value)
            => await DatabaseHelper.GetUpdateCommand(conn, value, "USERS").ExecuteNonQueryAsync();

        public async Task<int> GetUserNonRegPoints(SqlConnection conn, string userId)
            => (int)(await new SqlCommand($"select dbo.fnGetUserNonRegPoints({userId})", conn).ExecuteScalarAsync());
    }
}
