using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using PrideBot.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrideBot
{
    public abstract class DMSession
    {
        protected static IEmote SkipEmote => new Emoji("➡");
        protected static IEmote CancelEmote => new Emoji("❌");

        protected static readonly List<DMSession> activeSessions = new List<DMSession>();

        protected readonly DiscordSocketClient client;
        protected readonly IDMChannel channel;
        protected readonly SocketUser user;
        protected readonly IConfigurationRoot config;
        protected readonly SocketMessage originMessage;

        protected Prompt currentPrompt;

        protected class Prompt
        {
            public IMessage BotMessage { get; }
            public bool AcceptsText { get; }
            public List<IEmote> EmoteChoices { get; }

            public bool AcceptsEmote => EmoteChoices.Any();
            public bool IsEntered { get; set; }
            public SocketMessage MessageResponse { get; set; }
            public IEmote emoteResponse { get; set; }
            public bool IsSkipped => emoteResponse?.ToString().Equals(SkipEmote.ToString()) ?? false;
            public bool IsCancelled => emoteResponse?.ToString().Equals(CancelEmote.ToString()) ?? false;

            public Prompt(IMessage botMessage, bool acceptsText, List<IEmote> emoteChoies)
            {
                BotMessage = botMessage;
                AcceptsText = acceptsText;
                EmoteChoices = emoteChoies;
            }
        }

        public DMSession(IDMChannel channel, SocketUser user, IConfigurationRoot config, DiscordSocketClient client, SocketMessage originMessage = null)
        {
            this.channel = channel;
            this.user = user;
            this.config = config;
            this.originMessage = originMessage;

            client.MessageReceived += MesageReceived;
            client.ReactionAdded += ReactionAdded;
            this.client = client;
        }

        private async Task ReactionAdded(Cacheable<IUserMessage, ulong> msg, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (currentPrompt == null
                || reaction.Channel.Id != channel.Id
                || currentPrompt.IsEntered
                || !currentPrompt.AcceptsEmote
                || msg.Id != currentPrompt.BotMessage.Id
                || reaction.UserId != user.Id
                || !currentPrompt.EmoteChoices.Select(a => a.ToString()).Contains(reaction.Emote.ToString())) return;

            currentPrompt.IsEntered = true;
            currentPrompt.emoteResponse = reaction.Emote;
        }

        private Task MesageReceived(SocketMessage message)
        {
            if (currentPrompt == null
                || message.Channel.Id != channel.Id
                || currentPrompt.IsEntered
                || message.Author.Id != user.Id
                || !currentPrompt.AcceptsText)  return Task.CompletedTask;

            currentPrompt.MessageResponse = message;
            currentPrompt.IsEntered = true;
            return Task.CompletedTask;
        }

        public async Task PerformSessionAsync()
        {
            if (originMessage != null)
                originMessage.AddReactionAsync(new Emoji("✅")).GetAwaiter();
            if (activeSessions.Any(a => a.user.Id == user.Id))
            {
                await channel.SendMessageAsync("Hold up! We're already in the middle of a session! Let's finish up here before we do anything else, 'kaayyy?");
                return;
            }
            activeSessions.Add(this);
            try
            {
                await PerformSessionInternalAsync();
            }
            catch
            {
                await channel.SendMessageAsync("UH OH! Something went wrong! Sorry, you'll need to start this session up again, please contact a sage if this continues and blame them instead, 'kay?");
                throw;
            }
            finally
            {
                activeSessions.Remove(this);
            }
        }

        protected abstract Task PerformSessionInternalAsync();

        protected async Task<Prompt> SendAndAwaitEmoteResponseAsync(string text = null, EmbedBuilder embed = null, List<IEmote> emoteChoices = null, bool canSkip = false, bool canCancel = false)
            => await SendAndAwaitResponseAsync(text, embed, emoteChoices, false, canSkip, canCancel);

        protected async Task<Prompt> SendAndAwaitResponseAsync(string text = null, EmbedBuilder embed = null, List<IEmote> emoteChoices = null, bool acceptsText = true, bool canSkip = false, bool canCancel = false)
        {
            emoteChoices ??= new List<IEmote>();
            if (emoteChoices.Count >= 2)
            {
                if (embed != null)
                    embed.Description += "\n\n" + DialogueDict.Get("SESSION_EMOTE_FILLING");
                else
                    text += "\n\n" + DialogueDict.Get("SESSION_EMOTE_FILLING");
            }
            if (canSkip)
            {
                emoteChoices.Insert(0, SkipEmote);
                //if (embed != null)
                //    embed.Description += $"\n\nSelect {SkipEmote} to skip this step.";
                //else
                //    text += $"\n\nSelect {SkipEmote} to skip this step.";
            }
            else if (canCancel)
            {
                emoteChoices.Add(CancelEmote);
                //if (embed != null)
                //    embed.Description += $"\n\nSelect {CancelEmote} to cancel.";
                //else
                //    text += $"\n\nSelect {canCancel} to cancel.";
            }

            var message = await channel.SendMessageAsync(text, embed: embed.Build());
            currentPrompt = new Prompt(message, acceptsText, emoteChoices);
            AddReactions(message, emoteChoices).GetAwaiter();

            while(!currentPrompt.IsEntered)
            {
                await Task.Delay(100);
            }

            return currentPrompt;
        }

        async Task AddReactions(IMessage message, List<IEmote> emotes)
        {
            foreach (var emote in emotes)
            {
                if (currentPrompt != null && currentPrompt.IsEntered || currentPrompt.BotMessage.Id != message.Id)
                    break;
                await message.AddReactionAsync(emote);
            }
        }

    }
}
