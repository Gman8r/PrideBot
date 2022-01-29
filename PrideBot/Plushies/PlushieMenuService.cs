using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Webhook;
using Discord.Audio;
using Discord.Net;
using Discord.Rest;

using System;
using System.Collections.Generic;
using System.Linq;

using Discord.Commands;
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
using System.Net;
using Microsoft.Data.SqlClient;
using PrideBot.Models;
using PrideBot.Repository;
using PrideBot.Quizzes;
using PrideBot.Game;
using PrideBot.Registration;

namespace PrideBot.Plushies
{
    public class PlushieMenuService
    {

        readonly IConfigurationRoot config;
        readonly ShipImageGenerator shipImageGenerator;
        readonly PlushieImageService imageService;
        public ModelRepository repo;

        public PlushieMenuService(IConfigurationRoot config, ShipImageGenerator shipImageGenerator, PlushieImageService imageService, ModelRepository repo)
        {
            this.config = config;
            this.shipImageGenerator = shipImageGenerator;
            this.imageService = imageService;
            this.repo = repo;
        }

        string GetCustomId(bool isButton, ulong userId, int selectedPlushieId, PlushieAction action, string imageState)
        {
            return $"PLUSHMENU.{(isButton ? "B" : "S")}:{userId},{selectedPlushieId},{(int)action},{imageState}";
        }

        public async Task<IUserMessage> PostPlushieMenuAsync(SqlConnection connection, IGuildUser user, IMessageChannel channel, bool viewingOther = false)
        {
            var userPlushies = await repo.GetOwnedUserPlushiesForUserAsync(connection, user.Id.ToString());
            var inEffectPlushies = await repo.GetInEffectUserPlushiesForUserAsync(connection, user.Id.ToString(), DateTime.Now);
            var components = await GenerateComponentsAsync(connection, userPlushies, user.Id, 0, string.Join(".", userPlushies.Select(a => a.UserPlushieId)));
            var embedData = await  GenerateEmbedAsync(user, userPlushies, inEffectPlushies, viewingOther: viewingOther);
            var embed = embedData.Item1;
            if (viewingOther)
                components = null;
            var file = embedData.Item2;
            if (file != null)
                return await channel.SendFileAsync(file.Stream, file.FileName, user.Mention, embed: embed.Build(), components: components?.Build());
            else
                return await channel.SendMessageAsync(user.Mention, embed: embed.Build(), components: components?.Build());
        }

        public async Task<(EmbedBuilder, MemoryFile)> GenerateEmbedAsync(IGuildUser user, IEnumerable<UserPlushie> userPlushies, IEnumerable<UserPlushie> inEffectPlushies, string overrideImageUrl = null, bool viewingOther = false)
        {
            var imageFile = userPlushies.Any() && overrideImageUrl == null
                ? await imageService.WritePlushieCollectionImageAsync(userPlushies)
                : null;
            var embed = EmbedHelper.GetEventEmbed(user, config)
                .WithTitle("Plushies! 🧸")
                .WithDescription(!viewingOther
                    ? DialogueDict.Get(userPlushies.Any() ? "PLUSHIE_MENU_DESCRIPTION" : "PLUSHIE_MENU_DESCRIPTION_EMPTY")
                    : DialogueDict.Get(userPlushies.Any() ? "PLUSHIE_MENU_DESCRIPTION_OTHER" : "PLUSHIE_MENU_DESCRIPTION_OTHER_EMPTY", user.Mention));
            if (overrideImageUrl != null)
                embed.WithImageUrl(overrideImageUrl);
            else
                embed.WithAttachedImageUrl(imageFile);
            foreach (var plushie in userPlushies)
            {
                embed.AddField($"{plushie.Name} ({plushie.CharacterName})", plushie.DecriptionUponUse());
            }
            var inEfectString = "";
            foreach (var plushie in inEffectPlushies)
            {
                inEfectString += $"\n**{plushie.Name}**";
                if (plushie.DurationHours > 0)
                {
                    var timeRemaining = plushie.ExpirationTimestamp - DateTime.Now;
                    inEfectString += $" - {(int)timeRemaining.TotalHours}h:{timeRemaining.Minutes}m Remaining";
                }
                if (plushie.Uses > 1)
                {
                    inEfectString += $" - {plushie.RemainingUses} {(plushie.RemainingUses == 1 ? "Use" : "Uses")} Remaining";
                }
                else if (plushie.Uses == 1)
                {
                    inEfectString += $" - Standby";
                }
                inEfectString += $"\n- *{plushie.Description}*";
            }
            if (!string.IsNullOrWhiteSpace(inEfectString))
                embed.AddField("Plushies In Effect:", inEfectString);
            return (embed, imageFile);
        }

        public async Task<ComponentBuilder> GenerateComponentsAsync(SqlConnection connection, IEnumerable<UserPlushie> userPlushies, ulong userId, int selectedPlushieId, string imageState)
        {
            var cBuilder = new ComponentBuilder();
            cBuilder.ActionRows = new List<ActionRowBuilder>();
            var selectedPlushie = userPlushies
                .FirstOrDefault(a => a.UserPlushieId == selectedPlushieId);

            // Dropdown
            var dropdownBuilder = new ActionRowBuilder();
            if (userPlushies.Any())   // If any plushies available
            {
                var chooseMenu = new SelectMenuBuilder()
                {
                    Placeholder = DialogueDict.GenerateEmojiText("Examine a plushie for options!"),
                    CustomId = GetCustomId(false, userId, selectedPlushieId, PlushieAction.Select, imageState),
                    MinValues = 0
                };

                // Options
                foreach (var plushie in userPlushies)
                {
                    chooseMenu.AddOption(new SelectMenuOptionBuilder()
                    {
                        Label = plushie.CharacterName,
                        Description = plushie.Name,
                        IsDefault = selectedPlushieId == plushie.UserPlushieId,
                        Value = plushie.UserPlushieId.ToString()
                    });
                }
                dropdownBuilder.AddComponent(chooseMenu.Build());
            }

            // Plushie option row
            var plushieOptionRowBuilder = new ActionRowBuilder();
            if (selectedPlushieId > 0)
            {
                // Use button

                var canUse = GameHelper.IsEventOccuring(config) && selectedPlushie.Context.Equals("CARD_MENU");
                var label = GameHelper.IsEventOccuring(config)
                    ? (selectedPlushie.Context.Equals("CARD_MENU")
                        ? "Activate Now"
                        : "Can't Activate Here")
                    : "Activate (Locked)";
                plushieOptionRowBuilder.AddComponent(new ButtonBuilder()
                {
                    Style = ButtonStyle.Primary,
                    Emote = new Emoji("⚡"),
                    Label = label,
                    CustomId = GetCustomId(true, userId, selectedPlushieId, PlushieAction.Use, imageState),
                    IsDisabled = !canUse
                }.Build());

                canUse = GameHelper.IsEventOccuring(config);
                // Sell button
                plushieOptionRowBuilder.AddComponent(new ButtonBuilder()
                {
                    Style = ButtonStyle.Primary,
                    Emote = new Emoji("💗"),
                    Label = canUse ? "Pawn" : "Pawn (Locked)",
                    CustomId = GetCustomId(true, userId, selectedPlushieId, PlushieAction.Pawn, imageState),
                    IsDisabled = !canUse
                }.Build());
                // Trade button
                plushieOptionRowBuilder.AddComponent(new ButtonBuilder()
                {
                    Style = ButtonStyle.Primary,
                    Emote = new Emoji("♻"),
                    Label = "Trade",
                    CustomId = GetCustomId(true, userId, selectedPlushieId, PlushieAction.Trade, imageState)
                }.Build());
                // Clear selection
                plushieOptionRowBuilder.AddComponent(new ButtonBuilder()
                {
                    Style = ButtonStyle.Secondary,
                    Label = $"Clear Selection",
                    CustomId = GetCustomId(true, userId, selectedPlushieId, PlushieAction.ClearSelection, imageState)
                }.Build());
            }

            // Draw and misc buttons
            var navigationRowBuilder = new ActionRowBuilder();
            // Draw
            var isCoolownOver = await repo.CanUserDrawPlushieAsync(connection, userId.ToString(), GameHelper.IsEventOccuring(config) ? GameHelper.GetEventDay() : 0); ;
            var hasRoom = isCoolownOver
               ? await repo.CanUserReceivePlushieAsync(connection, userId.ToString())
               : false;
            navigationRowBuilder.AddComponent(new ButtonBuilder()
            {
                Style = ButtonStyle.Success,
                Emote = new Emoji("🧸"),
                Label = isCoolownOver
                    ? (!hasRoom
                        ? "Free Some Room To Get More Plushies!"
                        : "Get A New Plushie!")
                    : (GameHelper.IsEventOccuring(config) ? "Get Another Plushie Tomorrow!" : "Get More In February!"),
                CustomId = GetCustomId(true, userId, selectedPlushieId, PlushieAction.Draw, imageState),
                IsDisabled = !isCoolownOver || !hasRoom
            }.Build());
            // Bring to bottom
            navigationRowBuilder.AddComponent(new ButtonBuilder()
            {
                Style = ButtonStyle.Secondary,
                Emote = new Emoji("⏬"),
                Label = " ",
                CustomId = GetCustomId(true, userId, selectedPlushieId, PlushieAction.BringToBottom, imageState)
            }.Build());
            // Close
            navigationRowBuilder.AddComponent(new ButtonBuilder()
            {
                Style = ButtonStyle.Secondary,
                Emote = new Emoji("❌"),
                Label = " ",
                CustomId = GetCustomId(true, userId, selectedPlushieId, PlushieAction.Close, imageState)
            }.Build());

            // Add components
            if (dropdownBuilder.Components.Any())
                cBuilder.ActionRows.Add(dropdownBuilder);
            if (plushieOptionRowBuilder.Components.Any())
                cBuilder.ActionRows.Add(plushieOptionRowBuilder);
            if (navigationRowBuilder.Components.Any())
                cBuilder.ActionRows.Add(navigationRowBuilder);

            if (!cBuilder.ActionRows.Any())
                return null;
            else
                return cBuilder;
        }

    }
}
