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

        public PlushieMenuService(IConfigurationRoot config, ShipImageGenerator shipImageGenerator)
        {
            this.config = config;
            this.shipImageGenerator = shipImageGenerator;
        }

        string GetCustomId(bool isButton, ulong userId, int selectedPlushieId, PlushieAction action, string imageState)
        {
            return $"PLUSHMENU.{(isButton ? "B" : "S")}:{userId},{selectedPlushieId},{(int)action},{imageState}";
        }

        public async Task<IUserMessage> PostPlushieMenuAsync(IGuildUser user, IMessageChannel channel, IEnumerable<UserPlushie> userPlushies)
        {
            var components = GenerateComponents(user.Id, 0, "0.1.2.3.4.5");
            return await channel.SendMessageAsync(user.Mention, components: components?.Build());
        }

        //public async Task<EmbedBuilder> GenerateEmbedAsync(IGuildUser user)
        //{
        //    var embed = EmbedHelper.GetEventEmbed(user, config)
        //        .WithTitle("Plushies 🧸")
        //        .WithDescription(DialogueDict.Get("PLUSHIE_MENU_DESCRIPTION"))

        //}

        public ComponentBuilder GenerateComponents(ulong userId, int selectedPlushieId, string imageState)
        {
            var cBuilder = new ComponentBuilder();
            cBuilder.ActionRows = new List<ActionRowBuilder>();

            // Dropdown
            var dropdownBuilder = new ActionRowBuilder();
            if (true)   // If any plushies available
            {
                var chooseMenu = new SelectMenuBuilder()
                {
                    Placeholder = DialogueDict.GenerateEmojiText("Examine a plushie for options!"),
                    CustomId = GetCustomId(false, userId, selectedPlushieId, PlushieAction.Select, imageState),
                    MinValues = 0
                };

                // Options
                for (int i = 1; i <= 6; i++)
                {
                    chooseMenu.AddOption(new SelectMenuOptionBuilder()
                    {
                        Label = "Your Plushie #" + i.ToString(),
                        Description = "Reimu Hakurei",
                        Emote = EmoteHelper.GetNumberEmote(i),
                        IsDefault = selectedPlushieId == i,
                        Value = i.ToString()
                    });
                }
                dropdownBuilder.AddComponent(chooseMenu.Build());
            }

            // Plushie option row
            var plushieOptionRowBuilder = new ActionRowBuilder();
            if (selectedPlushieId > 0)
            {
                // Use button

                plushieOptionRowBuilder.AddComponent(new ButtonBuilder()
                {
                    Style = ButtonStyle.Primary,
                    Emote = new Emoji("⚡"),
                    Label = "Use Now",
                    CustomId = GetCustomId(true, userId, selectedPlushieId, PlushieAction.Use, imageState)
                }.Build());
                // Sell button
                plushieOptionRowBuilder.AddComponent(new ButtonBuilder()
                {
                    Style = ButtonStyle.Primary,
                    Emote = new Emoji("💗"),
                    Label = "Pawn",
                    CustomId = GetCustomId(true, userId, selectedPlushieId, PlushieAction.Pawn, imageState)
                }.Build());
                // Give button
                plushieOptionRowBuilder.AddComponent(new ButtonBuilder()
                {
                    Style = ButtonStyle.Secondary,
                    Emote = new Emoji("🎁"),
                    Label = "Give Away",
                    CustomId = GetCustomId(true, userId, selectedPlushieId, PlushieAction.Give, imageState)
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
            var isCoolownOver = true;
            var hasRoom = true;
            navigationRowBuilder.AddComponent(new ButtonBuilder()
            {
                Style = ButtonStyle.Success,
                Emote = new Emoji("🧸"),
                Label = isCoolownOver
                    ? (!hasRoom
                        ? "Free Some Room To Get More Plushies!"
                        : "Get A New Plushie!")
                    : "Get Another Plushie Tomorrow!",
                CustomId = GetCustomId(true, userId, selectedPlushieId, PlushieAction.Draw, imageState)
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
