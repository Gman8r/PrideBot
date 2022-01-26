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
    public class PlushieInteractionModule : InteractionModuleBase
    {
        private readonly IConfigurationRoot config;
        private readonly IServiceProvider provider;
        private readonly ModelRepository repo;
        private readonly PlushieMenuService plushieMenuService;
        private readonly PlushieService plushieService;
        private readonly DiscordSocketClient client;
        private readonly PlushieImageService imageService;
        private readonly PlushieEffectService plushieEffectService;
        private readonly CommandErrorReportingService errorReportingService;

        public PlushieInteractionModule(IConfigurationRoot config, IServiceProvider provider, ModelRepository repo, PlushieMenuService plushieMenuService, PlushieService plushieService, DiscordSocketClient client, PlushieImageService imageService, PlushieEffectService plushieEffectService, CommandErrorReportingService errorReportingService)
        {
            this.config = config;
            this.provider = provider;
            this.repo = repo;
            this.plushieMenuService = plushieMenuService;
            this.plushieService = plushieService;
            this.client = client;
            this.imageService = imageService;
            this.plushieEffectService = plushieEffectService;
            this.errorReportingService = errorReportingService;
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

            var message = (Context.Interaction as SocketMessageComponent).Message;
            var repostAction = RepostAction.Edit;
            using var connection = await repo.GetAndOpenDatabaseConnectionAsync();
            switch (action)
            {
                case PlushieAction.Use:
                    var userPlushie = await repo.GetUserPlushieAsync(connection, selectedPlushieId);
                    try
                    {
                        await plushieEffectService.ActivatePlushie(connection, Context.Interaction.User as IGuildUser, userPlushie, Context.Channel, Context.Interaction);
                    }
                    catch (Exception e)
                    {
                        await errorReportingService.ReportErrorAsync(Context.User, Context.Channel, "plushies", e.Message, e is CommandException, Context.Interaction);
                    }
                    selectedPlushieId = 0;
                    break;
                case PlushieAction.Draw:
                    message.ModifyAsync(a => a.Components = message.Components.ToBuilder().WithAllDisabled(true).Build()).GetAwaiter();
                    await plushieService.DrawPlushie(connection, Context.Channel, Context.Interaction.User as SocketUser, Context.Interaction);
                    break;
                case PlushieAction.Trade:
                    message.ModifyAsync(a => a.Components = message.Components.ToBuilder().WithAllDisabled(true).Build()).GetAwaiter();
                    await plushieService.TradePlushieInSession(connection, Context.Channel, Context.Interaction.User as SocketUser, selectedPlushieId, provider, Context.Interaction);
                    selectedPlushieId = 0;
                    break;
                case PlushieAction.Pawn:
                    // TODO
                    //var session = new 
                    //var dbCharacters = await repo.GetAllCharactersAsync(connection);
                    //var shipResult = await RegistrationSession.ParseShipAsync(connection, repo, shipName, dbCharacters);
                    //if (!shipResult.IsSuccess)
                    //    throw new CommandException(shipResult.ErrorMessage);
                    //var ship = shipResult.Value;
                    //var validationResult = await RegistrationSession.ValidateShipAsync(connection, shipResult.Value, dbCharacters);
                    //if (!validationResult.IsSuccess)
                    //    throw new CommandException(DialogueDict.Get("SHIP_SCORES_INVALID"));

                    //await Context.Interaction.FollowupAsync("bababa u used it");
                    //selectedPlushieId = 0;
                    break;
                case PlushieAction.BringToBottom:
                case PlushieAction.Close:
                    (Context.Interaction as SocketMessageComponent).Message.DeleteAsync().GetAwaiter();
                    repostAction = action == PlushieAction.BringToBottom ? RepostAction.Post : RepostAction.DontPost;
                    break;
                case PlushieAction.ClearSelection:
                    selectedPlushieId = 0;
                    break;
                default:
                    break;
            }

            var userPlushies = await repo.GetOwnedUserPlushiesForUserAsync(connection, Context.User.Id.ToString());
            var inEffectPlushies = await repo.GetInEffectUserPlushiesForUserAsync(connection, Context.User.Id.ToString(), DateTime.Now);
            await HandleUpdateAsync(connection, userPlushies, inEffectPlushies, selectedPlushieId, imageState, repostAction);
            connection?.Close();
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

            using var connection = await repo.GetAndOpenDatabaseConnectionAsync();
            var userPlushies = await repo.GetOwnedUserPlushiesForUserAsync(connection, Context.User.Id.ToString());
            if (!userPlushies.Any(a => a.UserPlushieId == selectedPlushieId))
                selectedPlushieId = 0;

            var inEffectPlushies = await repo.GetInEffectUserPlushiesForUserAsync(connection, Context.User.Id.ToString(), DateTime.Now);

            await HandleUpdateAsync(connection, userPlushies, inEffectPlushies, selectedPlushieId, imageState, RepostAction.Edit);
        }

        async Task HandleUpdateAsync(SqlConnection connection, IEnumerable<UserPlushie> userPlushies, IEnumerable<UserPlushie> inEffectPlushies, int selectedPlushieId, string imageState, RepostAction repostAction)
        {
            if (repostAction != RepostAction.DontPost)
            {
                var message = (Context.Interaction as SocketMessageComponent).Message;

                // TODO check image state and determine whether to edit image, then replace image state
                var newImageState = string.Join(".", userPlushies.Select(a => a.UserPlushieId));
                var overrideImageFile = newImageState.Equals(imageState)
                    ? (message.Embeds.FirstOrDefault().Image.HasValue ? message.Embeds.FirstOrDefault().Image.Value.Url : null)
                    : null;
                var embedData = await plushieMenuService.GenerateEmbedAsync(Context.User as IGuildUser, userPlushies, inEffectPlushies, overrideImageFile);
                var embed = embedData.Item1;
                var file = embedData.Item2;
                imageState = newImageState;
                
                
                var components = await plushieMenuService.GenerateComponentsAsync(connection, userPlushies, Context.User.Id, selectedPlushieId, imageState);
                if (repostAction == RepostAction.Edit)
                {
                    if (file != null)
                    {
                        var attachment = new FileAttachment(file.Stream, file.FileName);
                        await message.ModifyAsync(a =>
                        {
                            a.Components = components?.Build();
                            a.Embed = embed.Build();
                            a.Attachments = new List<FileAttachment>() { attachment };
                        });
                    }
                    else
                    {
                        await message.ModifyAsync(a =>
                        {
                            a.Components = components?.Build();
                            a.Embed = embed.Build();
                            a.Attachments = new List<FileAttachment>();
                        }); 
                    }
                }
                else if (repostAction == RepostAction.Post)
                {
                    if (file != null)
                        await Context.Channel.SendFileAsync(file.Stream, file.FileName, message.Content, embed: embed.Build(), components: components?.Build());
                    else
                        await Context.Channel.SendMessageAsync(message.Content, embed: embed.Build(), components: components?.Build());
                }
            }
        }

        void VerifyUser(string idStr)
        {
            var id = ulong.Parse(idStr);
            if (id != Context.User.Id)
                throw new CommandException("That's not for you! Be fair to everyone and use your own buttons please!", ephemeral: true);
        }
    }
}