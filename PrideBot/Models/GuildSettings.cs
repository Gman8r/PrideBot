using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot.Models
{
    public class GuildSettings
    {
        [PrimaryKey]
        public string GuildId { get; set; }
        public int QuizDay { get; set; }
        public bool QuizOpen { get; set; }
        public int LastSnakeDay { get; set; }
        public int SnakeMinutes { get; set; }
        public bool LeaderboardAvailable { get; set; }
    }
}
