using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrideBot.Models
{
    public class Character
    {
        [PrimaryKey]
        public string CharacterId { get; set; }
        public string Name { get; set; }
        public string FirstName { get; set; }
        public string Category { get; set; }
        public string Family { get; set; }
        public string PlushieId { get; set; }

        // Debug fields
        [DontPushToDatabase]
        public int PointsEarned { get; set; }
        [DontPushToDatabase]
        public int Supporters { get; set; }
    }
}
