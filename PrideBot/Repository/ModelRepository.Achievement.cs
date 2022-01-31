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

        public async Task<IEnumerable<Achievement>> GetAllAchievementsAsync(SqlConnection conn)
        => (await new SqlCommand($"select * from VI_ACHIEVEMENTS", conn).ExecuteReaderAsync()).As<Achievement>();

        public async Task<Achievement> GetAchievementAsync(SqlConnection conn, string id)
        => (await new SqlCommand($"select * from VI_ACHIEVEMENTS where ACHIEVEMENT_ID = '{id}'", conn).ExecuteReaderAsync()).As<Achievement>().FirstOrDefault();

        public async Task<Achievement> GetAchievementFromEmojiAsync(SqlConnection conn, string emojiStr)
        => (await new SqlCommand($"select * from VI_ACHIEVEMENTS where EMOJI = N'{emojiStr}' collate Latin1_General_100_CI_AS_SC", conn).ExecuteReaderAsync()).As<Achievement>().FirstOrDefault();

        public async Task<IEnumerable<Achievement>> GetAchievementsWithEmojiAsync(SqlConnection conn, string emojiStr)
        => (await new SqlCommand($"select * from VI_ACHIEVEMENTS where EMOJI = '{emojiStr}'", conn).ExecuteReaderAsync()).As<Achievement>();

        public async Task<StarboardPost> GetStarboardPostAsync(SqlConnection conn, string messageId)
        => (await new SqlCommand($"select * from VI_STARBOARD_POSTS where MESSAGE_ID = '{messageId}'", conn).ExecuteReaderAsync()).As<StarboardPost>().FirstOrDefault();

        public async Task<IEnumerable<StarboardAchievement>> GetAllStarboardAchievementAsync(SqlConnection conn)
        => (await new SqlCommand($"select * from VI_STARBOARD_ACHIEVEMENTS", conn).ExecuteReaderAsync()).As<StarboardAchievement>();

        public async Task<StarboardAchievement> GetStarboardAchievementAsync(SqlConnection conn, int starCount)
        => (await new SqlCommand($"select * from VI_STARBOARD_ACHIEVEMENTS where STAR_COUNT = {starCount}", conn).ExecuteReaderAsync()).As<StarboardAchievement>().FirstOrDefault();
    }
}
