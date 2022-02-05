using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot.Models
{
    public class RpControl
    {
        [PrimaryKey]
        public string MessageId { get; set; }
        public string ChannelId { get; set; }

    }
}
