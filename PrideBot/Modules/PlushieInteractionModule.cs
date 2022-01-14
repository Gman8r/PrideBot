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
using PrideBot.Plushie;

namespace PrideBot.Modules
{
    public class PlushieInteractionModule : InteractionModuleBase
    {
        private readonly IConfigurationRoot config;
        private readonly IServiceProvider provider;
        private readonly ModelRepository repo;
        private readonly PlushieMenuService plushieService;

        public PlushieInteractionModule(IConfigurationRoot config, IServiceProvider provider, ModelRepository repo, PlushieMenuService plushieService)
        {
            this.config = config;
            this.provider = provider;
            this.repo = repo;
            this.plushieService = plushieService;
        }

        enum RepostAction
        {
            DontPost,
            Edit,
            Post
        }

        // Handle buttons in plushie menu
        [ComponentInteraction("PLUSHMENU.B:*,*,*,*")]
        public async Task PlushieMenuButton(string userIdStr, string selectedIdStr, string actionStr, string imageState)
        {
            await DeferAsync();
            VerifyUser(userIdStr);

            var action = (PlushieAction)int.Parse(actionStr);
            var selectedPlushieId = int.Parse(selectedIdStr);

            var repostAction = RepostAction.Edit;
            switch (action)
            {
                case PlushieAction.BringToBottom:
                case PlushieAction.Close:
                    (Context.Interaction as SocketMessageComponent).Message.DeleteAsync().GetAwaiter();
                    repostAction = action == PlushieAction.BringToBottom ? RepostAction.Post : RepostAction.DontPost;
                    break;
                default:
                    break;
            }

            await HandleUpdateAsync(selectedPlushieId, imageState, repostAction);
        }

        // Handle select menu in plushie menu
        [ComponentInteraction("PLUSHMENU.S:*,*,*,*")]
        public async Task PlushieMenuSelect(string userIdStr, string oldSelectedIdStr, string actionStr, string imageState, string[] selectedPlushieIds)
        {
            await DeferAsync();
            VerifyUser(userIdStr);

            var action = (PlushieAction)int.Parse(actionStr);
            var oldSelectedPlushieId = int.Parse(oldSelectedIdStr);
            var selectedPlushieId = int.Parse(selectedPlushieIds.FirstOrDefault() ?? "0");

            await HandleUpdateAsync(selectedPlushieId, imageState, RepostAction.Edit);
        }

        async Task HandleUpdateAsync(int selectedId, string imageState, RepostAction repostAction)
        {
            // TODO check image state sand determine whether to edit image, then replace image state

            var message = (Context.Interaction as SocketMessageComponent).Message;
            if (repostAction != RepostAction.DontPost)
            {
                var components = plushieService.GenerateComponents(Context.User.Id, selectedId, imageState);
                if (repostAction == RepostAction.Edit)
                    await message.ModifyAsync(a => a.Components = components?.Build());
                else if (repostAction == RepostAction.Post)
                    await Context.Channel.SendMessageAsync(message.Content, components: components?.Build());
            }
        }

        void VerifyUser(string idStr)
        {
            var id = ulong.Parse(idStr);
            if (id != Context.User.Id)
                throw new CommandException("That's not for you! Be fair to everyone and use your own buttons please!");
        }
    }
}