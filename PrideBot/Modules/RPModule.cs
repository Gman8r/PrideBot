﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Webhook;
using Discord.Audio;
using Discord.Net;
using Discord.Rest;
using Discord.Webhook;
using System.Net;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using PrideBot.Events;
using PrideBot.Graphics;

namespace PrideBot.Modules
{
    //[RequireSage]
    [Hidden]
    [Name("RP")]
    public class RPModule : PrideModuleBase
    {
        private readonly IConfigurationRoot config;
        
        public RPModule(IConfigurationRoot config)
        {
            this.config = config;
        }


        private static Dictionary<ulong, RPChatSession> chatSessions = new Dictionary<ulong, RPChatSession>();

        [Command("rpstart")]
        [Alias("startrp")]
        public async Task Chat(SocketTextChannel channel)
        {
            var webhook = (await channel.GetWebhooksAsync()).FirstOrDefault(a => a.Creator.Id == Context.Client.CurrentUser.Id)
                ?? await channel.CreateWebhookAsync("RP Chat Agent");
            if (!chatSessions.ContainsKey(Context.User.Id))
                chatSessions[Context.User.Id] = new RPChatSession(Context, config, channel, webhook);
            else
                await chatSessions[Context.User.Id].SetChannelAsync(channel);
            //else
            //{
            //    await chatSessions[Context.User.Id].DisposeAsync();
            //    chatSessions[Context.User.Id] = new RPChatSession(Context, config, channel, webhook);
            //}
            await ReplyResultAsync($"Connected to {Context.Guild.Name}/{channel.Name}.");
        }

        [Command("rpstop")]
        [Alias("stoprp", "rpend", "endrp")]
        public async Task StopChat()
        {
            await chatSessions[Context.User.Id].DisposeAsync();
            chatSessions.Remove(Context.User.Id);
            await ReplyResultAsync("Chat session ended.");
        }


        [Command("text")]
        public async Task YellowText([Remainder]string phrase = null)
        {
            var imageService = new YellowTextGenerator(config);
            var file = await imageService.WriteYellowTextAsync(Context.Client.CurrentUser.GetAvatarUrl(size: 128), phrase);
            if (!string.IsNullOrWhiteSpace(phrase))
            {
                var embed = EmbedHelper.GetEventEmbed(null, config)
                    .WithDescription("blablabla")
                    //.WithThumbnailUrl(Context.Client.CurrentUser.GetAvatarUrl())
                    .WithAttachedImageUrl(file)
                    .WithAttachedThumbnailUrl(file);
                await Context.Channel.SendFileAsync(file.Stream, file.FileName, embed: embed.Build());
            }
            else
            {
                var embed = EmbedHelper.GetEventEmbed(null, config)
                    .WithDescription("blablabla")
                    .WithThumbnailUrl(Context.Client.CurrentUser.GetAvatarUrl());
                    //.WithAttachedImageUrl(file);
                await Context.Channel.SendMessageAsync(embed: embed.Build());
            }
        }

    }
}