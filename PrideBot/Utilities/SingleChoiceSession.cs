using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace PrideBot
{
    public class SingleChoiceSession : Session
    {
        public IMessage MessageResponse { get; private set; }
        public IEmote EmoteResponse { get; private set; }
        public SocketMessageComponent InteractionResponse { get; private set; }

        private readonly bool acceptsText;
        private readonly List<IEmote> emoteChoices;
        private readonly IUserMessage botMessage;

        public SingleChoiceSession(IMessageChannel channel, SocketUser user, IConfigurationRoot config, DiscordSocketClient client, TimeSpan timeout,
            bool acceptsText, List<IEmote> emoteChoices, IUserMessage botMessage,
            SocketMessage originMessage = null)
                : base(channel, user, config, client, timeout, originMessage)
        {
            this.acceptsText = acceptsText;
            this.emoteChoices = emoteChoices;
            this.botMessage = botMessage;
        }

        protected override async Task PerformSessionInternalAsync()
        {
            // TODO
            //await ca(botMessage, emoteChoices: emoteChoices, acceptsText: acceptsText);
            //MessageResponse = currentPrompt.MessageResponse;
            //EmoteResponse = currentPrompt.EmoteResponse;
            //InteractionResponse = currentPrompt.InteractionResponse;
        }
    }
}
