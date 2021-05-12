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
        public async Task<IEnumerable<Ship>> GetAllShipsAsync(SqlConnection conn)
        => (await new SqlCommand($"select * from VI_SHIPS", conn).ExecuteReaderAsync()).As<Ship>();
        
        public async Task<Ship> GetShipAsync(SqlConnection conn, string char1Id, string char2Id)
        => (await new SqlCommand($"select * from VI_SHIPS where CHARACTER_ID_1 = '{char1Id}' and CHARACTER_ID_2 = '{char2Id}'", conn).ExecuteReaderAsync()).As<Ship>().FirstOrDefault();

        public async Task<int> AddShipAsync(SqlConnection conn, Ship value)
            => await DatabaseHelper.GetInsertCommand(conn, value, "SHIPS").ExecuteNonQueryAsync();

        public async Task<int> UpdateShipAsync(SqlConnection conn, Ship value)
            => await DatabaseHelper.GetUpdateCommand(conn, value, "SHIPS").ExecuteNonQueryAsync();
    }
}
