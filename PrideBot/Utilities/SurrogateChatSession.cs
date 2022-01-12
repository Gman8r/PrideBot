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

namespace PrideBot
{
    class SurrogateChatSession : IAsyncDisposable
    {
        readonly DiscordSocketClient client;
        readonly IConfigurationRoot config;

        public ISocketMessageChannel OriginChannel { get; private set; }
        public SocketTextChannel Channel { get; private set; }
        public IDisposable TypingState { get; private set; }
        public IWebhook Webhook { get; private set; }
        public DiscordWebhookClient WebhookClient { get; private set; }
        public string WebhookName { get; private set; }
        public string WebhookAvatarUrl { get; private set; }

        public SurrogateChatSession(DiscordSocketClient client, IConfigurationRoot config, SocketTextChannel channel, SocketCommandContext context, IWebhook webhook)
        {
            this.client = client;
            this.config = config;
            Webhook = webhook;
            WebhookClient = new DiscordWebhookClient(webhook);
            Channel = channel;
            OriginChannel = context.Channel;

            client.MessageReceived += MessageRecieved;
            client.UserIsTyping += UserIsTyping;
        }

        private async Task UserIsTyping(Cacheable<IUser, ulong> usr, Cacheable<IMessageChannel, ulong> chnl)
        {
            var ownerId = config.ParseUlongField("ids:owner");
            if (chnl.Id != OriginChannel.Id || usr.Id != ownerId || !string.IsNullOrEmpty(WebhookName)) return;
            using var typing = Channel.EnterTypingState();
        }

        private async Task MessageRecieved(SocketMessage s)
        {
            var msg = s as SocketUserMessage;
            if (s == null) return;
            if (msg.Channel.Id != OriginChannel.Id || !msg.Author.IsOwner(config)) return;
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
            client.MessageReceived -= MessageRecieved;
        }
    }
}