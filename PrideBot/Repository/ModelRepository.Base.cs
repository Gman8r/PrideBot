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
        readonly TokenConfig tokenConfig;

        public ModelRepository(TokenConfig tokenConfig)
        {
            this.tokenConfig = tokenConfig;
        }

        public string GetConnectionString()
        {
            return tokenConfig["connectionstring"];
        }

        public string GetAltConnectionString()
        {
            return tokenConfig["altconnectionstring"];
        }

        public SqlConnection GetDatabaseConnection()
            => new SqlConnection(GetConnectionString());

        public SqlConnection GetAltDatabaseConnection()
            => new SqlConnection(GetAltConnectionString());

        public async Task<SqlConnection> GetAndOpenDatabaseConnectionAsync()
        {
            var conn = GetDatabaseConnection();
            await conn.OpenAsync();
            return conn;
        }

        public async Task<SqlConnection> GetAndOpenAltDatabaseConnectionAsync()
        {
            var conn = GetAltDatabaseConnection();
            await conn.OpenAsync();
            return conn;
        }
    }
}
