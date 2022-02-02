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
            if (!(msg is SocketUserMessage message))
                return Task.CompletedTask;

            // react to hi-fives
            var hiFive = "<:YuukaFive:933356068904525904>";
            var hifiveFlipped = "<:YuukaFiveFlipped:933356068451520564>";
            if (message.Content.Equals(hiFive))
                msg.AddReactionAsync(Emote.Parse(hifiveFlipped)).GetAwaiter();
            else if (message.Content.Equals(hifiveFlipped))
                msg.AddReactionAsync(Emote.Parse(hiFive)).GetAwaiter();

            else if (message.Content.ToLower().Equals("gay takequiz"))
                message.ReplyAsync("Huh? ❓ What are you trying to say here? 😕 Are you taking the quiz or not? 📝").GetAwaiter();
            else if (message.Content.ToLower().Equals("gay ships"))
                message.ReplyAsync("Why are you looking at me while saying that? 🃏 Yeah they're gay, I guess, so what? 🎁").GetAwaiter();
            else if (message.Content.ToLower().Equals("gay scores"))
                message.ReplyAsync("?? Is this some part of human-world language that I'm missing? 📪 I mean I'm trying to get adapted here but I'm seeing weird cult phrases here all over the place. 🗺 Hello?? 📱").GetAwaiter();

            return Task.CompletedTask;
        }
    }
}
