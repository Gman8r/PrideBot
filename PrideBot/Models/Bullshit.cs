using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot.Models
{
    public class Bullshit
    {
        [PrimaryKey]
        public int BullshitId { get; set; }
        public string Content { get; set; }
        public DateTime AnnounceTime { get; set; }
        public bool Announced { get; set; }
    }
}
