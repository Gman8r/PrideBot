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

        public async Task<IEnumerable<Character>> GetAllCharactersAsync(SqlConnection conn)
        => (await new SqlCommand($"select * from VI_CHARACTERS", conn).ExecuteReaderAsync()).As<Character>();

        public async Task<Character> GetCharacterAsync(SqlConnection conn, string id)
        => (await new SqlCommand($"select * from VI_CHARACTERS where CHARACTER_ID = '{id}'", conn).ExecuteReaderAsync()).As<Character>().FirstOrDefault();
    }
}
