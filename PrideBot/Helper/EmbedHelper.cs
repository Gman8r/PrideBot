using Discord;
using Discord.Commands;
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

        public static EmbedBuilder GetEventEmbed(IUser user, IConfigurationRoot config, string id = "", bool showUser = true, bool showDate = false)
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
            return embed;
    }
            

        public static EmbedBuilder GetEventErrorEmbed(SocketCommandContext context, string title, string description) => new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(Color.Red)
            .WithAuthor(new EmbedAuthorBuilder()
                .WithIconUrl(context.User.GetAvatarUrlOrDefault().Split('?')[0])
                .WithName(context.Guild == null ? context.User.Username : context.Guild.GetUser(context.User.Id).Nickname ?? context.User.Username))
            .WithCurrentTimestamp();
    }
}