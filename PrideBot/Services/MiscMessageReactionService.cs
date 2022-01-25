using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using System.IO;

namespace PrideBot
{
    class MiscMessageReactionService
    {
        private readonly DiscordSocketClient client;

        public MiscMessageReactionService(DiscordSocketClient client)
        {
            this.client = client;
            client.MessageReceived += MessageReceived;
        }

        private Task MessageReceived(SocketMessage msg)
        {
            if (!(msg is SocketMessage message))
                return Task.CompletedTask;

            // react to hi-fives
            var hiFive = "<:YuukaFive:933356068904525904>";
            var hifiveFlipped = "<:YuukaFiveFlipped:933356068451520564>";
            if (message.Content.Equals(hiFive))
                msg.AddReactionAsync(Emote.Parse(hifiveFlipped)).GetAwaiter();
            else if (message.Content.Equals(hifiveFlipped))
                msg.AddReactionAsync(Emote.Parse(hiFive)).GetAwaiter();

            return Task.CompletedTask;
        }
    }
}
