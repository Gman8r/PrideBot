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
    }
}
