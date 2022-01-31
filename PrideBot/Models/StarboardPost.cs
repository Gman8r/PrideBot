using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot.Models
{
    public class StarboardPost
    {
        [PrimaryKey]
        public string MessageId { get; set; }
        public string UserId { get; set; }
        public int StarCount { get; set; }
    }
}
