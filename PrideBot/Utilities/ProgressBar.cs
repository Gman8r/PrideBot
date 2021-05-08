using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace PrideBot
{
    public class ProgressBar
    {
        public const string HaniwaEmote = "<a:haniwadance:799928966403457074>";
        public const string TransparentEmote = "<:transparent:805664623318925403>";
        public const string ErrorEmote = "<:haniWhat:809543471449767946>";
        private const decimal ProgressPerHaniwa = .1m;

        private SocketCommandContext context;
        private string description;
        private IUserMessage message;
        private ulong messageId;
        private decimal progress;
        private decimal progressInterval;

        public ProgressBar(SocketCommandContext context, string description, decimal progressInterval = .1m)
        {
            this.context = context;
            this.description = description;
            this.progressInterval = progressInterval;
        }

        public async Task<IUserMessage> GenerateMessageAsync()
        {
            message = (await context.Channel.SendMessageAsync(GenerateMessageText())) as IUserMessage;

            while (message == null)
            {
                await Task.Delay(25);
            }
            return message;
        }

        public decimal GetProgress() => progress;

        public async Task UpdateProgressAsync(decimal progress)
        {
            progress = Math.Clamp(progress, 0m, 1m);
            progress -= progress % progressInterval;
            this.progress = progress;
            var text = GenerateMessageText();
            if (!message.Content.Equals(text))
            {
                await message.ModifyAsync(a => a.Content = text);
            }
        }

        int ToPercent(decimal progress) => (int)(progress * 100);

        public string GenerateMessageText() => GetProgressText(progress);
        public string GetProgressText(decimal progress)
        {
            var text = "***GOOOOOOO!!!!***\n";
            text += description;
            text += $" ({ToPercent(progress)}%)";

            text += "\n[";
            for (decimal d = 0; d <= 1; d+= ProgressPerHaniwa)
            {
                if (progress >= d)
                    text += HaniwaEmote;
                else
                    text += TransparentEmote;
            }
            text += "]";

            return text;
        }

    }
}
