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
        public async Task<GuildSettings> GetGuildSettings(SqlConnection conn, string guildId)
        => (await new SqlCommand($"select * from VI_GUILD_SETTINGS where GUILD_ID = '{guildId}'", conn).ExecuteReaderAsync()).As<GuildSettings>().FirstOrDefault();

        public async Task<GuildSettings> GetOrCreateGuildSettingsAsync(SqlConnection conn, string guildId)
        {
            var command = new SqlCommand("SP_GET_OR_CREATE_GUILD_SETTINGS", conn);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@GUILD_ID", guildId));
            await command.ExecuteNonQueryAsync();

            return (await new SqlCommand($"select * from VI_GUILD_SETTINGS where GUILD_ID = '{guildId}'", conn)
                .ExecuteReaderAsync()).As<GuildSettings>().FirstOrDefault();
        }

        public async Task<int> UpdateGuildSettingsAsync(SqlConnection conn, GuildSettings value)
            => await DatabaseHelper.GetUpdateCommand(conn, value, "GUILD_SETTINGS").ExecuteNonQueryAsync();
    }
}
