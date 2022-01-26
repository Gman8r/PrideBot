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

        public async Task<Plushie> GetPlushieFromCharaterAsync(SqlConnection conn, string characterId)
        => (await new SqlCommand($"select p.PLUSHIE_ID from VI_CHARACTERS c" +
            $" left join VI_PLUSHIES p on c.PLUSHIE_ID = p.PLUSHIE_ID" +
            $" where c.CHARACTER_ID = '{characterId}'", conn).ExecuteReaderAsync()).As<Plushie>().FirstOrDefault();

        public async Task<UserPlushie> GetUserPlushieAsync(SqlConnection conn, int userPlushieId)
        => (await new SqlCommand($"select * from VI_USER_PLUSHIES where USER_PLUSHIE_ID = {userPlushieId}", conn).ExecuteReaderAsync()).As<UserPlushie>().FirstOrDefault();

        public async Task<IEnumerable<UserPlushie>> GetAllUserPlushiesForUserAsync(SqlConnection conn, string userId)
        => (await new SqlCommand($"select * from VI_USER_PLUSHIES where USER_ID = '{userId}'", conn).ExecuteReaderAsync()).As<UserPlushie>();

        public async Task<IEnumerable<UserPlushie>> GetOwnedUserPlushiesForUserAsync(SqlConnection conn, string userId)
        => (await new SqlCommand($"select * from VI_USER_PLUSHIES where USER_ID = '{userId}' and FATE = {(int)PlushieTransaction.None}", conn).ExecuteReaderAsync()).As<UserPlushie>();

        public async Task<IEnumerable<UserPlushie>> GetInEffectUserPlushiesForUserAsync(SqlConnection conn, string userId, DateTime timestamp)
        => (await new SqlCommand($"select * from VI_USER_PLUSHIES where USER_ID = '{userId}' and FATE = {(int)PlushieTransaction.Using}" +
            $" and dbo.fnUserCardInEffect(USER_PLUSHIE_ID, '{timestamp}') = 'Y'", conn).ExecuteReaderAsync()).As<UserPlushie>();

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

        public async Task<int> AddPlushieUseLog(SqlConnection conn, int userPlushieId, PlushieUseContext contextType, string context, DateTime timestamp)
        => await new SqlCommand($"insert into PLUSHIE_EFFECT_LOG (USER_PLUSHIE_ID, CONTEXT_TYPE, CONTEXT, TIMESTAMP)" +
            $" values({userPlushieId}, {(int)contextType}, '{context}', '{timestamp}')", conn).ExecuteNonQueryAsync();

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

            // forgive the discrepancy of this being here, i just had the idea of putting them in this class rn an dont have time to change the rest kjaskfjska
            public Result CheckErrors(bool throwCommandExceptions = true)
            {
                string errorMessage = null;
                switch (Error)
                {
                    case ModelRepository.AddPlushieError.CantReceivePlushies:
                        errorMessage = DialogueDict.Get("PLUSHIE_DRAWN_ALREADY");
                        break;
                    case ModelRepository.AddPlushieError.CantSelectPlushieChoice:
                        errorMessage = DialogueDict.Get("PLUSHIE_CANT_CHOOSE");
                        break;
                    case ModelRepository.AddPlushieError.UnknownError:
                        errorMessage = DialogueDict.Get("EXCEPTION");
                        break;
                    default:
                        break;
                }

                if (errorMessage != null)
                {
                    if (throwCommandExceptions)
                        throw new CommandException(errorMessage);
                    else
                        return Result.Error(errorMessage);
                }
                else
                    return Result.Success();
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

        public async Task<StandardTransactionError> AttemptRemoveUserPlushieAsync(SqlConnection conn, int userPlushieId, PlushieTransaction fate, DateTime timestamp)
        {
            var command = new SqlCommand("SP_REMOVE_USER_PLUSHIE", conn);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@USER_PLUSHIE_ID", userPlushieId));
            command.Parameters.Add(new SqlParameter("@FATE", (int)fate));
            command.Parameters.Add(new SqlParameter("@TIMESTAMP", timestamp));

            var errorCodeParam = new SqlParameter();
            errorCodeParam.ParameterName = "@ERROR_CODE";
            errorCodeParam.Direction = ParameterDirection.Output;
            errorCodeParam.DbType = DbType.Int32;
            command.Parameters.Add(errorCodeParam);

            await command.ExecuteNonQueryAsync();
            return (StandardTransactionError)errorCodeParam.Value;
        }

        public async Task<StandardTransactionError> ActivateUserPlushieAsync(SqlConnection conn, int userPlushieId, DateTime timestamp)
        {
            var command = new SqlCommand("SP_ACTIVATE_USER_PLUSHIE", conn);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@USER_PLUSHIE_ID", userPlushieId));
            command.Parameters.Add(new SqlParameter("@TIMESTAMP", timestamp));

            var errorCodeParam = new SqlParameter();
            errorCodeParam.ParameterName = "@ERROR_CODE";
            errorCodeParam.Direction = ParameterDirection.Output;
            errorCodeParam.DbType = DbType.Int32;
            command.Parameters.Add(errorCodeParam);

            await command.ExecuteNonQueryAsync();
            return (StandardTransactionError)errorCodeParam.Value;
        }

        public class DepletePlushieResult
        {
            public bool FullyDepleted { get; }
            public StandardTransactionError Error { get; }
            public DepletePlushieResult(bool fullyDepleted, StandardTransactionError error)
            {
                FullyDepleted = fullyDepleted;
                Error = error;
            }
        }

        public async Task<DepletePlushieResult> DepleteUserPlushieAsync(SqlConnection conn, int userPlushieId, DateTime timestamp, bool forceRemove, PlushieUseContext contextType, string context, bool logUse = true)
        {
            var command = new SqlCommand("SP_DEPLETE_USER_PLUSHIE", conn);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@USER_PLUSHIE_ID", userPlushieId));
            command.Parameters.Add(new SqlParameter("@TIMESTAMP", timestamp));
            command.Parameters.Add(new SqlParameter("@FORCE_REMOVE", forceRemove));

            var fullyDepletedParam = new SqlParameter();
            fullyDepletedParam.ParameterName = "@FULLY_DEPLETED";
            fullyDepletedParam.Direction = ParameterDirection.Output;
            fullyDepletedParam.DbType = DbType.String;
            fullyDepletedParam.Size = 1;
            command.Parameters.Add(fullyDepletedParam);

            var errorCodeParam = new SqlParameter();
            errorCodeParam.ParameterName = "@ERROR_CODE";
            errorCodeParam.Direction = ParameterDirection.Output;
            errorCodeParam.DbType = DbType.Int32;
            command.Parameters.Add(errorCodeParam);

            await command.ExecuteNonQueryAsync();

            if (logUse)
                await AddPlushieUseLog(conn, userPlushieId, contextType, context, timestamp);

            return new DepletePlushieResult(fullyDepletedParam.Value.ToString().Equals("Y"), (StandardTransactionError)errorCodeParam.Value);
        }

        public async Task<string> GetRandomPlushieCharacterAsync(SqlConnection conn, string userId)
        {
            var command = new SqlCommand("SP_GET_RANDOM_PLUSHIE_CHARACTER", conn);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@USER_ID", userId));

            var characterIdParam = new SqlParameter();
            characterIdParam.ParameterName = "@CHARACTER_ID";
            characterIdParam.Direction = ParameterDirection.Output;
            characterIdParam.DbType = DbType.String;
            characterIdParam.Size = 50;
            command.Parameters.Add(characterIdParam);

            await command.ExecuteNonQueryAsync();
            return characterIdParam.Value.ToString();
        }
    }
}
