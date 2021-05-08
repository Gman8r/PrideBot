using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Immutable;
using System.Linq;

namespace PrideBot
{
    public class MessageUrl
    {
        public IMessage Value { get; }

        public MessageUrl(IMessage message)
        {
            this.Value = message;
        }
    }
}
