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
        public async Task<Character> GetCharacter(SqlConnection conn, string id)
        => (await new SqlCommand($"select * from CHARACTERS where CHARACTER_ID = '{id}'", conn).ExecuteReaderAsync()).As<Character>().FirstOrDefault();
    }
}
