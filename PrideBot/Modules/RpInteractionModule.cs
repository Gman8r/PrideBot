using Discord;
using Discord.WebSocket;
using Discord.Webhook;
using Discord.Audio;
using Discord.Net;
using Discord.Rest;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using Newtonsoft.Json;
using PrideBot.Repository;
using PrideBot.Registration;
using PrideBot.Models;
using PrideBot.Game;
using PrideBot.Quizzes;
using PrideBot.Events;
using Discord.Interactions;
using PrideBot.Plushies;
using Microsoft.Data.SqlClient;

namespace PrideBot.Modules
{
    public class RpInteractionModule : InteractionModuleBase
    {
        private readonly IConfigurationRoot config;
        private readonly IServiceProvider provider;
        private readonly ModelRepository repo;
        private readonly DiscordSocketClient client;
        private readonly RpControlMenuService rpControlMenuService;

        public RpInteractionModule(IConfigurationRoot config, IServiceProvider provider, ModelRepository repo, DiscordSocketClient client, RpControlMenuService rpControlMenuService)
        {
            this.config = config;
            this.provider = provider;
            this.repo = repo;
            this.client = client;
            this.rpControlMenuService = rpControlMenuService;
        }

        [ComponentInteraction("RPMENU.C")]
        public async Task ChangeChannelAsync()
        {
            await DeferAsync();

            var sUser = client.GetGyn(config).GetUser(Context.User.Id);
            var message = (Context.Interaction as SocketMessageComponent).Message;
            var channel = message.Channel as ITextChannel;
            var data = rpControlMenuService.ParseEmbedDataString(message.Embeds.FirstOrDefault().Description);
            using var connection = await repo.GetAndOpenDatabaseConnectionAsync();

            data["enter"] = RpControlMenuService.Action.Channel.ToString();
            await rpControlMenuService.ModifyPostRpMenuAsync(connection, message, data);
        }

        [ComponentInteraction("RPMENU.A")]
        public async Task ChangeAttachmentAsync()
        {
            await DeferAsync();

            var sUser = client.GetGyn(config).GetUser(Context.User.Id);
            var message = (Context.Interaction as SocketMessageComponent).Message;
            var channel = message.Channel as ITextChannel;
            var data = rpControlMenuService.ParseEmbedDataString(message.Embeds.FirstOrDefault().Description);
            using var connection = await repo.GetAndOpenDatabaseConnectionAsync();

            data["enter"] = RpControlMenuService.Action.Attachment.ToString();
            await rpControlMenuService.ModifyPostRpMenuAsync(connection, message, data);
        }

        [ComponentInteraction("RPMENU.Y")]
        public async Task ChangeYellowTextAsync()
        {
            await DeferAsync();

            var sUser = client.GetGyn(config).GetUser(Context.User.Id);
            var message = (Context.Interaction as SocketMessageComponent).Message;
            var channel = message.Channel as ITextChannel;
            var data = rpControlMenuService.ParseEmbedDataString(message.Embeds.FirstOrDefault().Description);
            using var connection = await repo.GetAndOpenDatabaseConnectionAsync();

            data["enter"] = RpControlMenuService.Action.YellowText.ToString();
            await rpControlMenuService.ModifyPostRpMenuAsync(connection, message, data);
        }

        [ComponentInteraction("RPMENU.RY")]
        public async Task ResetYellowTextAsync()
        {
            await DeferAsync();

            var sUser = client.GetGyn(config).GetUser(Context.User.Id);
            var message = (Context.Interaction as SocketMessageComponent).Message;
            var channel = message.Channel as ITextChannel;
            var data = rpControlMenuService.ParseEmbedDataString(message.Embeds.FirstOrDefault().Description);
            using var connection = await repo.GetAndOpenDatabaseConnectionAsync();

            data["yellowtext"] = "";
            await rpControlMenuService.ModifyPostRpMenuAsync(connection, message, data);
        }

        [ComponentInteraction("RPMENU.U")]
        public async Task ChangeUserAsync(string[] values)
        {
            await DeferAsync();

            var sUser = client.GetGyn(config).GetUser(Context.User.Id);
            var mInteraction = (Context.Interaction as SocketMessageComponent);
            var message = mInteraction.Message;
            var channel = message.Channel as SocketTextChannel;
            var data = rpControlMenuService.ParseEmbedDataString(message.Embeds.FirstOrDefault().Description);
            using var connection = await repo.GetAndOpenDatabaseConnectionAsync();

            var user = (channel as SocketGuildChannel).GetUser(ulong.Parse(values.FirstOrDefault()));

            data["user"] = user.Mention;
            await rpControlMenuService.ModifyPostRpMenuAsync(connection, message, data);
        }
    }
}