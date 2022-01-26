using PrideBot.Plushies;
using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot.Models
{
    public class UserPlushie : Plushie
    {
        [PrimaryKey]
        [DontPushToDatabase]
        public int UserPlushieId { get; set; }
        public string CharacterId { get; set; }
        new public string PlushieId { get; set; }
        [DontPushToDatabase]
        public string CharacterName { get; set; }
        public decimal Rotation { get; set; }
        public string UserId { get; set; }
        public PlushieTransaction Source { get; set; }
        public PlushieTransaction Fate { get; set; }
        public int DrawnDay { get; set; }
        public string OriginalUserId{ get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime RemovedTimestamp { get; set; }  // Removed is when the card is no longer in the player's card menu

        //fields for after use
        [DontPushToDatabase]
        public DateTime ExpirationTimestamp { get; set; }
        public int RemainingUses { get; set; }
    }
}
