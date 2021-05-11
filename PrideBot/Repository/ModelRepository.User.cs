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
        public async Task<IEnumerable<Character>> GetAllUsers(SqlConnection conn)
        => (await new SqlCommand($"select * from VI_USERS", conn).ExecuteReaderAsync()).As<Character>();

        public async Task<User> GetUser(SqlConnection conn, string id)
        => (await new SqlCommand($"select * from VI_USERS where USER_ID = '{id.ToString()}'", conn).ExecuteReaderAsync()).As<User>().FirstOrDefault();

        public async Task<int> AddUser(SqlConnection conn, User user)
            => await DatabaseHelper.GetInsertCommand(conn, user, "USERS").ExecuteNonQueryAsync();

        public async Task<int> UpdateUser(SqlConnection conn, User user)
            => await DatabaseHelper.GetUpdateCommand(conn, user, "USERS").ExecuteNonQueryAsync();
    }
}
