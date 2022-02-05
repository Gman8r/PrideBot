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
        public async Task<IEnumerable<RpControl>> GetAlRpControlsAsync(SqlConnection conn)
        => (await new SqlCommand($"select * from VI_RP_CONTROLS", conn).ExecuteReaderAsync()).As<RpControl>();

        public async Task<IEnumerable<RpControl>> DeleteRpControlsInChannelAsync(SqlConnection conn, string channelId)
        => (await new SqlCommand($"delete from VI_RP_CONTROLS where CHANNEL_ID = '{channelId}'", conn).ExecuteReaderAsync()).As<RpControl>();

        public async Task<RpControl> GetRpControlAsync(SqlConnection conn, string messageId)
        => (await new SqlCommand($"select * from VI_RP_CONTROLS where MESSAGE_ID = '{messageId}'", conn).ExecuteReaderAsync()).As<RpControl>().FirstOrDefault();
    }
}
