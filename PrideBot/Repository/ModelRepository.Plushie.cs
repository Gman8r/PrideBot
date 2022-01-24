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

        public async Task<Plushie> GetPlushieAsync(SqlConnection conn, string plushieId)
        => (await new SqlCommand($"select * from VI_PLUSHIES where PLUSHIE_ID = '{plushieId}'", conn).ExecuteReaderAsync()).As<Plushie>().FirstOrDefault();

        public async Task<UserPlushie> GetUserPlushieAsync(SqlConnection conn, int userPlushieId)
        => (await new SqlCommand($"select * from VI_USER_PLUSHIES where USER_PLUSHIE_ID = {userPlushieId}", conn).ExecuteReaderAsync()).As<UserPlushie>().FirstOrDefault();

        public async Task<IEnumerable<UserPlushie>> GetAllUserPlushiesForUserAsync(SqlConnection conn, string userId)
        => (await new SqlCommand($"select * from VI_USER_PLUSHIES where USER_ID = '{userId}'", conn).ExecuteReaderAsync()).As<UserPlushie>();

        public async Task<IEnumerable<UserPlushie>> GetOwnedUserPlushiesForUserAsync(SqlConnection conn, string userId)
        => (await new SqlCommand($"select * from VI_USER_PLUSHIES where USER_ID = '{userId}' and FATE = {(int)PlushieTransaction.None}", conn).ExecuteReaderAsync()).As<UserPlushie>();

        public async Task<IEnumerable<UserPlushie>> GetActiveUserPlushiesForUserAsync(SqlConnection conn, string userId)
        => (await new SqlCommand($"select * from VI_USER_PLUSHIES where USER_ID = '{userId}' and FATE = {(int)PlushieTransaction.Using}", conn).ExecuteReaderAsync()).As<UserPlushie>();

        public async Task<IEnumerable<Plushie>> GetAllPlushiesAsync(SqlConnection conn)
        => (await new SqlCommand($"select * from VI_PLUSHIES", conn).ExecuteReaderAsync()).As<Plushie>();

        public async Task<IEnumerable<UserPlushieChoice>> GetPlushieChoicesForuserAsync(SqlConnection conn, string userId, int day)
        => (await new SqlCommand($"select * from VI_USER_PLUSHIE_CHOICES where USER_ID = '{userId}' and DAY = '{day}'", conn).ExecuteReaderAsync()).As<UserPlushieChoice>();

        public async Task<bool> CanUserDrawPlushieAsync(SqlConnection conn, string userId, int day)
        => (await new SqlCommand($"select dbo.fnUserCanDrawPlushie('{userId}', {day})", conn).ExecuteScalarAsync()).ToString().Equals("Y");

        public async Task<bool> CanUserReceivePlushieAsync(SqlConnection conn, string userId)
        => (await new SqlCommand($"select dbo.fnUserCanReceivePlushie('{userId}')", conn).ExecuteScalarAsync()).ToString().Equals("Y");

        public async Task UpdatePlushieChoicesForUserAsync(SqlConnection conn, string userId, int day, bool forceUpdate = false)
        {
            var command = new SqlCommand("SP_UPDATE_PLUSHIE_CHOICES_FOR_USER", conn);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@USER_ID", userId));
            command.Parameters.Add(new SqlParameter("@DAY", day));
            command.Parameters.Add(new SqlParameter("@FORCE_UPDATE", forceUpdate ? "Y" : "N"));

            await command.ExecuteNonQueryAsync();
        }

        public class AddPlushieResult
        {
            public int UserPlushieId { get; }
            public AddPlushieError Error { get; }

            public AddPlushieResult(int userPlushieId, AddPlushieError error)
            {
                UserPlushieId = userPlushieId;
                Error = error;
            }
        }

        public enum AddPlushieError
        {
            None = 0,
            CantReceivePlushies = 1,
            CantSelectPlushieChoice = 2,
            UnknownError = 99
        }

        public async Task<AddPlushieResult> AttemptAddUserPlushieAsync(SqlConnection conn, string userId, string senderId, string originalUserId, string plushieId, string characterId, int day, decimal rotation, PlushieTransaction source, int userPlushieChoiceId = 0)
        {
            var command = new SqlCommand("SP_ADD_USER_PLUSHIE", conn);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@USER_ID", userId));
            command.Parameters.Add(new SqlParameter("@SENDER_ID", (object)senderId ?? DBNull.Value));
            command.Parameters.Add(new SqlParameter("@ORIGINAL_USER_ID", originalUserId));
            command.Parameters.Add(new SqlParameter("@PLUSHIE_ID", plushieId));
            command.Parameters.Add(new SqlParameter("@CHARACTER_ID", characterId));
            command.Parameters.Add(new SqlParameter("@DAY", day));
            command.Parameters.Add(new SqlParameter("@ROTATION", rotation));
            command.Parameters.Add(new SqlParameter("@SOURCE", (int)source));
            command.Parameters.Add(new SqlParameter("@USER_PLUSHIE_CHOICE_ID", userPlushieChoiceId));

            var plushieIdParam = new SqlParameter();
            plushieIdParam.ParameterName = "@USER_PLUSHIE_ID";
            plushieIdParam.Direction = ParameterDirection.Output;
            plushieIdParam.DbType = DbType.Int32;
            command.Parameters.Add(plushieIdParam);

            var errorCodeParam = new SqlParameter();
            errorCodeParam.ParameterName = "@ERROR_CODE";
            errorCodeParam.Direction = ParameterDirection.Output;
            errorCodeParam.DbType = DbType.Int32;
            command.Parameters.Add(errorCodeParam);

            await command.ExecuteNonQueryAsync();
            return new AddPlushieResult((int)plushieIdParam.Value, (AddPlushieError)errorCodeParam.Value);
        }

        public class TradePlushiesResult
        {
            public int NewUserPlushieId1 { get; }
            public int NewUserPlushieId2 { get; }
            public TradePlushiesError Error { get; }

            public TradePlushiesResult(int newUserPlushieId1, int newUserPlushieId2, TradePlushiesError error)
            {
                NewUserPlushieId1 = newUserPlushieId1;
                NewUserPlushieId2 = newUserPlushieId2;
                Error = error;
            }
        }

        public enum TradePlushiesError
        {
            None = 0,
            OneOrBothPlushiesMissing = 1,
            UnknownError = 99
        }

        public async Task<TradePlushiesResult> AttemptTradeUserPlushiesAsync(SqlConnection conn, string userPlushieId1, string userPlushieId2, DateTime timestamp)
        {
            var command = new SqlCommand("SP_TRADE_USER_PLUSHIES", conn);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@USER_PLUSHIE_ID_1", userPlushieId1));
            command.Parameters.Add(new SqlParameter("@USER_PLUSHIE_ID_2", userPlushieId2));
            command.Parameters.Add(new SqlParameter("@TIMESTAMP", timestamp));

            var plushieId1Param = new SqlParameter();
            plushieId1Param.ParameterName = "@NEW_USER_PLUSHIE_ID_1";
            plushieId1Param.Direction = ParameterDirection.Output;
            plushieId1Param.DbType = DbType.Int32;
            command.Parameters.Add(plushieId1Param);

            var plushieId2Param = new SqlParameter();
            plushieId2Param.ParameterName = "@NEW_USER_PLUSHIE_ID_2";
            plushieId2Param.Direction = ParameterDirection.Output;
            plushieId2Param.DbType = DbType.Int32;
            command.Parameters.Add(plushieId2Param);

            var errorCodeParam = new SqlParameter();
            errorCodeParam.ParameterName = "@ERROR_CODE";
            errorCodeParam.Direction = ParameterDirection.Output;
            errorCodeParam.DbType = DbType.Int32;
            command.Parameters.Add(errorCodeParam);

            await command.ExecuteNonQueryAsync();
            return new TradePlushiesResult((int)plushieId1Param.Value, (int)plushieId2Param.Value, (TradePlushiesError)errorCodeParam.Value);
        }

        public async Task<StandardTransactionError> AttemptDepleteUserPlushieAsync(SqlConnection conn, string userPlushieId, PlushieTransaction fate)
        {
            var command = new SqlCommand("SP_DEPLETE_USER_PLUSHIE", conn);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@USER_PLUSHIE_ID", userPlushieId));
            command.Parameters.Add(new SqlParameter("@FATE", (int)fate));

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
