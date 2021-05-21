using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using PrideBot.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PrideBot
{
    public abstract class DMSession
    {
        protected static IEmote SkipEmote => new Emoji("➡");
        protected static IEmote YesEmote => new Emoji("✅");
        protected static IEmote NoEmote => new Emoji("❌");

        public static readonly List<DMSession> activeSessions = new List<DMSession>();

        protected readonly DiscordSocketClient client;
        protected readonly IDMChannel channel;
        protected readonly SocketUser user;
        protected readonly IConfigurationRoot config;
        protected readonly SocketMessage originMessage;
        protected readonly TimeSpan timeout;

        protected Prompt currentPrompt;

        public IUser GetUser() => user;
        public bool IsCancelled { get; private set; }
        public void Cancel(string message)
        {
            IsCancelled = true;
            cancellationMessage = message;
        }

        string cancellationMessage;

        protected class Prompt
        {
            public IMessage BotMessage { get; }
            public bool AcceptsText { get; }
            public List<IEmote> EmoteChoices { get; }

            public bool AcceptsEmote => EmoteChoices.Any();
            public bool IsEntered { get; set; }
            public bool AlwaysPopulateEmotes { get; set; }
            public SocketMessage MessageResponse { get; set; }
            public IEmote EmoteResponse { get; set; }
            public bool IsSkipped => EmoteResponse?.ToString().Equals(SkipEmote.ToString()) ?? false;
            public bool IsYes => EmoteResponse?.ToString().Equals(YesEmote.ToString()) ?? false;
            public bool IsNo => EmoteResponse?.ToString().Equals(NoEmote.ToString()) ?? false;

            public Prompt(IMessage botMessage, bool acceptsText, List<IEmote> emoteChoies)
            {
                BotMessage = botMessage;
                AcceptsText = acceptsText;
                EmoteChoices = emoteChoies;
            }
        }

        public DMSession(IDMChannel channel, SocketUser user, IConfigurationRoot config, DiscordSocketClient client, TimeSpan timeout, SocketMessage originMessage = null)
        {
            this.channel = channel;
            this.user = user;
            this.config = config;
            this.originMessage = originMessage;
            this.timeout = timeout;

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
            currentPrompt.EmoteResponse = reaction.Emote;
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
                var errorEmbed = EmbedHelper.GetEventErrorEmbed(user, DialogueDict.Get("SESSION_DUPE"), client, showUser: false);
                await channel.SendMessageAsync(embed: errorEmbed.Build());
                return;
            }
            activeSessions.Add(this);
            try
            {
                client.MessageReceived += MesageReceived;
                client.ReactionAdded += ReactionAdded;
                await PerformSessionInternalAsync();
            }
            catch (OperationCanceledException e)
            {
                var embed = EmbedHelper.GetEventEmbed(user, config, showUser: false)
                    .WithTitle("Session Cancelled")
                    .WithDescription(cancellationMessage);
                await channel.SendMessageAsync(embed: embed.Build());
            }
            catch
            {
                var errorEmbed = EmbedHelper.GetEventErrorEmbed(user, DialogueDict.Get("SESSION_EXCEPTION"), client, showUser: false);
                await channel.SendMessageAsync(embed: errorEmbed.Build());
                throw;
            }
            finally
            {
                activeSessions.Remove(this);
                client.MessageReceived -= MesageReceived;
                client.ReactionAdded -= ReactionAdded;
            }
        }

        protected abstract Task PerformSessionInternalAsync();

        protected async Task<Prompt> SendAndAwaitYesNoResponseAsync(string text = null, EmbedBuilder embed = null, bool canSkip = false, bool canCancel = false)
            => await SendAndAwaitEmoteResponseAsync(text, embed, new List<IEmote>() { YesEmote, NoEmote }, canSkip, canCancel);

        protected async Task<Prompt> SendAndAwaitEmoteResponseAsync(string text = null, EmbedBuilder embed = null, List<IEmote> emoteChoices = null, bool canSkip = false, bool canCancel = false)
            => await SendAndAwaitResponseAsync(text, embed, emoteChoices, false, canSkip, canCancel);

        protected async Task<Prompt> SendAndAwaitResponseAsync(string text = null, EmbedBuilder embed = null, List<IEmote> emoteChoices = null, bool acceptsText = true, bool canSkip = false, bool canCancel = false)
        {
            await SendResponseAsync(text, embed, emoteChoices, acceptsText, canSkip, canCancel);
            return await AwaitCurrentResponseAsync();
        }

        protected async Task<Prompt> SendResponseAsync(string text = null, EmbedBuilder embed = null, List<IEmote> emoteChoices = null, bool acceptsText = true, bool canSkip = false, bool canCancel = false, bool alwaysPopulateEmotes = false)
        {
            emoteChoices ??= new List<IEmote>();
            if (emoteChoices.Count >= 3)
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
                emoteChoices.Add(NoEmote);
                //if (embed != null)
                //    embed.Description += $"\n\nSelect {CancelEmote} to cancel.";
                //else
                //    text += $"\n\nSelect {canCancel} to cancel.";
            }

            var message = await channel.SendMessageAsync(text, embed: embed.Build());
            currentPrompt = new Prompt(message, acceptsText, emoteChoices);
            currentPrompt.AlwaysPopulateEmotes = alwaysPopulateEmotes;
            AddReactions(message, emoteChoices).GetAwaiter();
            return currentPrompt;
        }
        protected async Task<Prompt> AwaitCurrentResponseAsync()
        {
            currentPrompt.EmoteResponse = null;
            currentPrompt.MessageResponse = null;
            currentPrompt.IsEntered = false;
            var startTime = DateTime.Now;
            while (!currentPrompt.IsEntered)
            {
                await Task.Delay(100);
                if (DateTime.Now - startTime > timeout)
                    Cancel(GetTimeoutMessage());
                if (IsCancelled)
                {
                    throw new OperationCanceledException(cancellationMessage);
                }
            }
            return currentPrompt;
        }

        protected virtual string GetTimeoutMessage() => DialogueDict.Get("SESSION_TIMEOUT");

        protected EmbedBuilder GetUserCancelledEmbed()
        {
            //var goodbyeKeys = DialogueDict.GetDict()
            //    .Where(a => a.Key.StartsWith("SESSION_CANCEL"))
            //    .Select(a => a.Key)
            //    .ToList();
            //var goodbyeKey = goodbyeKeys[new Random().Next() % goodbyeKeys.Count];
            return GetEmbed()
                    .WithTitle("'Kay, Laters Then")
                    .WithDescription(DialogueDict.Get("SESSION_CANCEL"))
                    .WithImageUrl(null)
                    .WithThumbnailUrl(null);
        }

        protected EmbedBuilder GetEmbed()
            => EmbedHelper.GetEventEmbed(user, config, showUser: false)
            .WithThumbnailUrl("https://cdn.discordapp.com/attachments/419187329706491905/843048501458108436/unknown.png");

        async Task AddReactions(IMessage message, List<IEmote> emotes)
        {
            foreach (var emote in emotes)
            {
                if (currentPrompt != null &&
                    (currentPrompt.IsEntered || currentPrompt.BotMessage.Id != message.Id)
                    && !currentPrompt.AlwaysPopulateEmotes)
                    break;
                await message.AddReactionAsync(emote);
            }
        }

    }
}
