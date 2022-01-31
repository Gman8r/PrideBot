using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot
{
    public class PkMessage
    {
        public DateTimeOffset timestamp { get; set; }
        public string id { get; set; }
        public string original { get; set; }
        public string sender { get; set; }
        public string channel { get; set; }
    }
}
