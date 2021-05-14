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
        public async Task<UserShipCollection> GetUserShipsAsync(SqlConnection conn, User user)
        => new UserShipCollection((await new SqlCommand($"select * from VI_USER_SHIPS where USER_ID = '{user.UserId}'", conn).ExecuteReaderAsync()).As<UserShip>());
        public async Task<UserShipCollection> GetUserShipsAsync(SqlConnection conn, string userId)
        => new UserShipCollection((await new SqlCommand($"select * from VI_USER_SHIPS where USER_ID = '{userId}'", conn).ExecuteReaderAsync()).As<UserShip>());

        public async Task<UserShip> GetUserShipAsync(SqlConnection conn, string userId, int tier)
        => (await new SqlCommand($"select * from VI_USER_SHIPS where USER_ID = '{userId}' and TIER = {tier}", conn).ExecuteReaderAsync()).As<UserShip>().FirstOrDefault();

        public async Task<int> AddUserShipAsync(SqlConnection conn, UserShip value)
            => await DatabaseHelper.GetInsertCommand(conn, value, "USER_SHIPS").ExecuteNonQueryAsync();

        public async Task<int> UpdateUserShipAsync(SqlConnection conn, UserShip value)
            => await DatabaseHelper.GetUpdateCommand(conn, value, "USER_SHIPS").ExecuteNonQueryAsync();

        public async Task<int> DeleteUserShipAsync(SqlConnection conn, string userId, int tier)
            => await new SqlCommand($"delete from USER_SHIPS where USER_ID = '{userId}' and TIER = {tier}", conn).ExecuteNonQueryAsync();

        public async Task<int> ChangeUserShipTierAsync(SqlConnection conn, string userId, int oldTier, int newTier)
        {
            var b = $"update USER_SHIPS set tier = {newTier} where tier = {oldTier}";
            return await new SqlCommand($"update USER_SHIPS set tier = {newTier} where USER_ID = '{userId}' and tier = {oldTier}", conn).ExecuteNonQueryAsync();
        }
    }
}
