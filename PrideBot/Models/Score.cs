using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot.Models
{
    public class Score
    {
        [PrimaryKey]
        [DontPushToDatabase]
        public int ScoreId { get; set; }
        public string UserId { get; set; }
        public string AchievementId { get; set; }
        public int PointsEarned { get; set; }

        [DontPushToDatabase]
        public DateTime TimeStamp { get; set; }
        [DontPushToDatabase]
        public int ShipCount { get; set; }
        public string Approver { get; set; }
        public string PostGuildId { get; set; }
        public string PostChannelId { get; set; }
        public string PostMessageId { get; set; }

        public string GetMessageUrl() =>
            (PostMessageId == null || PostChannelId == null || PostGuildId == null)
            ? null
            : $"https://discord.com/channels/{PostGuildId}/{PostChannelId}/{PostMessageId}";
    }
}
