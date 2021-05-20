using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot
{
    public static class EmbedHelper
    {
        //public static Color GetEventColor(IConfigurationRoot config) => new Color(uint.Parse(config["eventcolor"]));
        public static Color GetEventColor(IConfigurationRoot config) => new Color(0xB00b69);

        public static EmbedBuilder GetEventEmbed(IUser user, IConfigurationRoot config, string id = "", bool showUser = true, bool showDate = false, bool userInThumbnail = false)
        {
            var embed = new EmbedBuilder().WithColor(GetEventColor(config))
            .WithFooter(new EmbedFooterBuilder()
                .WithText(id));
            if (showDate)
                embed = embed.WithCurrentTimestamp();
            if (showUser)
                embed = embed.WithAuthor(new EmbedAuthorBuilder()
                    .WithIconUrl(user.GetAvatarUrlOrDefault().Split('?')[0])
                    .WithName((user as IGuildUser)?.Username ?? user.Username));
            if (userInThumbnail)
            {
                embed.ThumbnailUrl = embed.Author?.IconUrl;
                embed.Author = null;
            }
            return embed;
    }


        public static EmbedBuilder GetEventErrorEmbed(IUser user, string description, DiscordSocketClient client, bool showUser = true)
        {
            var title = "Hold Up";
            if (client != null && user != null && user is SocketUser sUser)
                title = $"Hold Up {sUser.Queen(client)}";
            var embed = new EmbedBuilder()
                .WithTitle(title)
                .WithDescription(description)
                .WithColor(Color.Red);
            if (showUser && user != null)
                embed = embed
                    .WithAuthor(new EmbedAuthorBuilder()
                    .WithName((user as IGuildUser)?.Nickname ?? user.Username)
                    .WithIconUrl(user.GetAvatarUrlOrDefault().Split('?')[0]));
            return embed;
        }
    }
}