using PrideBot.Plushies;
using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot.Models
{
    public class UserPlushieChoice : Plushie
    {
        [PrimaryKey]
        [DontPushToDatabase]
        public int UserPlushieChoiceId { get; set; }
        public string CharacterName { get; set; }
        public string UserId { get; set; }
        public int PlushieIndex{ get; set; }
        public string CharacterId { get; set; }
        public decimal Rotation { get; set; }
        public int Day { get; set; }

        [DontPushToDatabase]
        // I am NOT adding this in everywhere so here
        public bool Flip => new Random((int)Rotation).Next() % 2 == 0;
    }
}
