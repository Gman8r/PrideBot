using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot.Models
{
    public class Dialogue
    {
        [PrimaryKey]
        public string DialogueId { get; set; }
        public string Content { get; set; }
    }
}
