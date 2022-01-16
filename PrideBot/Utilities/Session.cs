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
    public abstract class Session
    {
        protected static IEmote SkipEmote => new Emoji("➡");
        protected static IEmote YesEmote => new Emoji("✅");
        protected static IEmote ThumbsUpEmote => new Emoji("👍");
        protected static IEmote NoEmote => new Emoji("❌");

        public static readonly List<Session> activeSessions = new List<Session>();

        protected readonly DiscordSocketClient client;
        protected readonly IMessageChannel channel;
        protected readonly SocketUser user;
        protected readonly IConfigurationRoot config;
        protected readonly IMessage originMessage;
        protected readonly TimeSpan timeout;

        protected Prompt currentPrompt;

        public IUser GetUser() => user;
        public bool IsCancelled { get; private set; }
        public void Cancel(string message)
        {
            IsCancelled = true;
            cancellationMessage = message;
            if (currentPrompt?.BotMessage.Components?.Any() ?? false)
            {
                var newComponents = currentPrompt.BotMessage.Components.ToBuilder().WithAllDisabled(true);
                currentPrompt.BotMessage.ModifyAsync(a => a.Components = newComponents?.Build()).GetAwaiter();
            }
        }

        string cancellationMessage;

        protected class Prompt
        {
            public IUserMessage BotMessage { get; }
            public bool AcceptsText { get; }
            public List<IEmote> EmoteChoices { get; }

            public bool AcceptsEmote => EmoteChoices.Any();
            public bool IsEntered { get; set; }
            public bool AlwaysPopulateEmotes { get; set; }
            public IMessage MessageResponse { get; set; }
            public IEmote EmoteResponse { get; set; }
            public SocketMessageComponent InteractionResponse { get; set; }
            public bool IsSkipped =>
                EmoteResponse?.ToString().Equals(SkipEmote.ToString()) ?? false
                || (InteractionResponse?.Data?.CustomId ?? "").Equals("SKIP");
            public bool IsYes =>
                EmoteResponse?.ToString().Equals(YesEmote.ToString()) ?? false
                || (InteractionResponse?.Data?.CustomId ?? "").Equals("YES");
            public bool IsNo =>
                EmoteResponse?.ToString().Equals(NoEmote.ToString()) ?? false
                || (InteractionResponse?.Data?.CustomId ?? "").Equals("NO");

            public Prompt(IUserMessage botMessage, bool acceptsText, List<IEmote> emoteChoies)
            {
                BotMessage = botMessage;
                AcceptsText = acceptsText;
                EmoteChoices = emoteChoies;
            }
        }

        public Session(IDMChannel channel, SocketUser user, IConfigurationRoot config, DiscordSocketClient client, TimeSpan timeout, IMessage originMessage = null)
        {
            this.channel = channel;
            this.user = user;
            this.config = config;
            this.originMessage = originMessage;
            this.timeout = timeout;

            this.client = client;
        }

        private async Task ReactionAdded(Cacheable<IUserMessage, ulong> msg, Cacheable<IMessageChannel, ulong> chnl, SocketReaction reaction)
        {
            if (currentPrompt == null
                || chnl.Id != channel.Id
                || currentPrompt.IsEntered
                || !currentPrompt.AcceptsEmote
                || msg.Id != currentPrompt.BotMessage.Id
                || reaction.UserId != user.Id
                || !currentPrompt.EmoteChoices.Select(a => a.ToString()).Contains(reaction.Emote.ToString())) return;

            currentPrompt.IsEntered = true;
            currentPrompt.EmoteResponse = reaction.Emote;
        }

        private Task MesageReceived(IMessage message)
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

        private Task InteractionCreated(SocketInteraction interaction)
        {
            if (currentPrompt == null
                || currentPrompt.IsEntered
                || !(interaction is SocketMessageComponent mInteraction)
                || mInteraction.Message.Id != currentPrompt.BotMessage.Id)
                return Task.CompletedTask;

            mInteraction.DeferAsync().GetAwaiter();

            if (mInteraction.User.Id != user.Id)
            {
                var errorEmbed = EmbedHelper.GetEventErrorEmbed(mInteraction.User, $"Only {user.Mention} can interact with this message.", client);
                mInteraction.RespondAsync(embed: errorEmbed.Build(), ephemeral: true).GetAwaiter();
                return Task.CompletedTask;
            }

            currentPrompt.InteractionResponse = mInteraction;
            currentPrompt.IsEntered = true;
            return Task.CompletedTask;
        }

        public async Task PerformSessionAsync()
        {
            if (originMessage != null && originMessage.Channel.GetType() != typeof(SocketDMChannel))
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
                client.InteractionCreated += InteractionCreated;
                await PerformSessionInternalAsync();
            }
            catch (OperationCanceledException e)
            {
                var embed = EmbedHelper.GetEventEmbed(user, config, showUser: false)
                    .WithTitle("Session Cancelled")
                    .WithDescription(cancellationMessage);
                await channel.SendMessageAsync(embed: embed.Build());
            }
            catch (Exception e)
            {
                if (e.Message.Contains("50007"))
                    throw new CommandException("HMM I can't seem to send you any DM's. Do you accept messages from people on this server?");
                var errorEmbed = EmbedHelper.GetEventErrorEmbed(user, DialogueDict.Get("SESSION_EXCEPTION"), client, showUser: false);
                await channel.SendMessageAsync(embed: errorEmbed.Build());
                throw;
            }
            finally
            {
                activeSessions.Remove(this);
                client.MessageReceived -= MesageReceived;
                client.ReactionAdded -= ReactionAdded;
                client.InteractionCreated -= InteractionCreated;
            }
        }

        protected abstract Task PerformSessionInternalAsync();

        protected async Task<Prompt> SendAndAwaitYesNoEmoteResponseAsync(string text = null, EmbedBuilder embed = null, ComponentBuilder components = null, bool canSkip = false, bool canCancel = false, bool alwaysPopulateEmotes = false, MemoryFile file = null, IDiscordInteraction interaction = null, bool disableComponents = true)
            => await SendAndAwaitNonTextResponseAsync(text, embed, components, new List<IEmote>() { YesEmote, NoEmote }, canSkip, canCancel, alwaysPopulateEmotes, file, interaction, disableComponents);

        protected async Task<Prompt> SendAndAwaitNonTextResponseAsync(string text = null, EmbedBuilder embed = null, ComponentBuilder components = null, List<IEmote> emoteChoices = null, bool canSkip = false, bool canCancel = false, bool alwaysPopulateEmotes = false, MemoryFile file = null, IDiscordInteraction interaction = null, bool disableComponents = true)
            => await SendAndAwaitResponseAsync(text, embed, components, emoteChoices, false, canSkip, canCancel, alwaysPopulateEmotes, file, interaction, disableComponents);

        protected async Task<Prompt> SendAndAwaitResponseAsync(string text = null, EmbedBuilder embed = null, ComponentBuilder components = null, List<IEmote> emoteChoices = null, bool acceptsText = true, bool canSkip = false, bool canCancel = false, bool alwaysPopulateEmotes = false, MemoryFile file = null, IDiscordInteraction interaction = null, bool disableComponents = true)
        {
            await SendResponseAsync(text, embed, components, emoteChoices, acceptsText, canSkip, canCancel, alwaysPopulateEmotes, file, interaction);
            var prompt = await AwaitCurrentResponseAsync();
            if (disableComponents && (prompt.BotMessage.Components?.Any() ?? false))
            {
                var newComponents = prompt.BotMessage.Components.ToBuilder().WithAllDisabled(true);
                prompt.BotMessage.ModifyAsync(a => a.Components = newComponents?.Build()).GetAwaiter();
            }
            return prompt;
        }

        protected async Task<Prompt> SendResponseAsync(string text = null, EmbedBuilder embed = null, ComponentBuilder components = null, List<IEmote> emoteChoices = null, bool acceptsText = true, bool canSkip = false, bool canCancel = false, bool alwaysPopulateEmotes = false, MemoryFile file = null, IDiscordInteraction interaction = null)
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

            IUserMessage message;
            if (interaction != null)
            {
                if (file == null)
                    message = await interaction.FollowupAsync(text, embed: embed?.Build(), components: components?.Build());
                else
                    message = await interaction.FollowupWithFileAsync(file.Stream, file.FileName, text, embed: embed?.Build(), components: components?.Build());
            }
            else
            {
                if (file == null)
                    message = await channel.SendMessageAsync(text, embed: embed?.Build(), components: components?.Build());
                else
                    message = await channel.SendFileAsync(file.Stream, file.FileName, text, embed: embed?.Build(), components: components?.Build());
            }
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
            .WithThumbnailUrl(client.CurrentUser.GetAvatarUrl());

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
