using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Webhook;
using Discord.Audio;
using Discord.Net;
using Discord.Rest;
using System.Net;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using Microsoft.Extensions.Configuration;

namespace PrideBot.Events
{
    class RPChatSession : IAsyncDisposable
    {
        readonly SocketCommandContext context;
        readonly IConfigurationRoot config;

        public ISocketMessageChannel OriginChannel => context.Channel;
        public SocketTextChannel Channel { get; private set; }
        public IDisposable TypingState { get; private set; }
        public IWebhook Webhook { get; private set; }
        public DiscordWebhookClient WebhookClient { get; private set; }
        public string WebhookName { get; private set; }
        public string WebhookAvatarUrl { get; private set; }

        public RPChatSession(SocketCommandContext context, IConfigurationRoot config, SocketTextChannel channel, IWebhook webhook)
        {
            this.context = context;
            this.config = config;
            Webhook = webhook;
            WebhookClient = new DiscordWebhookClient(webhook);
            Channel = channel;

            context.Client.MessageReceived += MessageRecieved;
            context.Client.UserIsTyping += UserIsTyping;
        }

        private async Task UserIsTyping(Cacheable<IUser, ulong> usr, Cacheable<IMessageChannel, ulong> chnl)
        {
            if (chnl.Id != OriginChannel.Id || usr.Id != context.User.Id || !string.IsNullOrEmpty(WebhookName)) return;
            var channel = await chnl.GetOrDownloadAsync();
            await Channel.TriggerTypingAsync();
        }

        private async Task MessageRecieved(SocketMessage s) 
        {
            var msg = s as SocketUserMessage;
            if (s == null) return;
            if (msg.Channel.Id != OriginChannel.Id || msg.Author.Id != context.User.Id) return;
            var argPos = 0;
            if (msg.HasPrefix(config, ref argPos)) return;

            //if (msg.Content.Equals("t", StringComparison.OrdinalIgnoreCase))
            //{
            //    TypingState = Channel.EnterTypingState();
            //    return;
            //}

            var attachmentFile = msg.Attachments.Any()
                ? await new FileDownloader(config.GetRelativeFilePath("temp"))
                    .DownloadFileAsync(msg.Attachments.First().Url)
                : null;
            await SendMessageAsync(msg.Content, attachmentFile);
        }

        public async Task SendMessageAsync(string message, string attachmentFile = null)
        {
            message = DialogueDict.RollBrainrot(message);
            if (string.IsNullOrEmpty(WebhookName))
            {
                if (attachmentFile == null)
                    await Channel.SendMessageAsync(message);
                else
                    await Channel.SendFileAsync(attachmentFile, message);
            }
            else
            {
                if (attachmentFile == null)
                    await WebhookClient.SendMessageAsync(message, username: WebhookName, avatarUrl: WebhookAvatarUrl);
                else
                    await WebhookClient.SendFileAsync(attachmentFile, message, username: WebhookName, avatarUrl: WebhookAvatarUrl);
            }

            if (TypingState != null)
            {
                TypingState.Dispose();
                TypingState = null;
            }
        }

        public async Task SetChannelAsync(SocketTextChannel channel)
        {
            if (TypingState != null)
            {
                TypingState.Dispose();
                TypingState = null;
            }
            if (Webhook != null && Webhook.Channel.Id != channel.Id)
                await Webhook.ModifyAsync(a => a.ChannelId = channel.Id);

            Channel = channel;
        }

        public void SetWebhookData(string avatarUrl, string name)
        {
            WebhookName = name;
            WebhookAvatarUrl = avatarUrl;
        }

        public async ValueTask DisposeAsync()
        {
            if (TypingState != null)
                TypingState.Dispose();
            if (Webhook != null)
                await Webhook.DeleteAsync();
            context.Client.MessageReceived -= MessageRecieved;
            context.Client.UserIsTyping -= UserIsTyping;
        }
    }
}