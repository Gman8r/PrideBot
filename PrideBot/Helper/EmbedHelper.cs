using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PrideBot
{
    public static class EmbedHelper
    {
        //public static Color GetEventColor(IConfigurationRoot config) => new Color(uint.Parse(config["eventcolor"]));
        public static Color GetEventColor(IConfigurationRoot config) => new Color(0xDECE17);

        public static EmbedBuilder GetEventEmbed(IUser user, IConfigurationRoot config, string id = "", bool showUser = true, bool showDate = false, bool userInThumbnail = false)
        {
            var embed = new EmbedBuilder().WithColor(GetEventColor(config))
            .WithFooter(new EmbedFooterBuilder()
                .WithText(id));
            if (showDate)
                embed = embed.WithCurrentTimestamp();
            if (showUser && user != null)
                embed = embed.WithAuthor(new EmbedAuthorBuilder()
                    .WithIconUrl(user.GetServerAvatarUrlOrDefault().Split('?')[0])
                    .WithName((user as IGuildUser)?.Nickname ?? user.Username));
            if (userInThumbnail)
            {
                embed.ThumbnailUrl = embed.Author?.IconUrl;
                //embed.Author = null;
            }
            return embed;
        }


        public static EmbedBuilder GetEventErrorEmbed(IUser user, string description, DiscordSocketClient client, bool showUser = true)
        {
            var title = "Watch It! 🖐";
            //if (client != null && user != null && user is SocketUser sUser)
            //    title = $"Hold Up {sUser.Queen(client)}";
            var embed = new EmbedBuilder()
                .WithTitle(title)
                .WithDescription(description)
                .WithColor(Color.Red);
            if (showUser && user != null)
                embed = embed
                    .WithAuthor(new EmbedAuthorBuilder()
                    .WithName((user as IGuildUser)?.Nickname ?? user.Username)
                    .WithIconUrl(user.GetServerAvatarUrlOrDefault().Split('?')[0]));
            return embed;
        }

        public static EmbedBuilder WithAttachedImageUrl(this EmbedBuilder builder, MemoryFile file)
            => builder.WithImageUrl(file?.FileName == null ? null : $"attachment://{file.FileName}");

        public static EmbedBuilder WithAttachedThumbnailUrl(this EmbedBuilder builder, MemoryFile file)
            => builder.WithThumbnailUrl(file?.FileName == null ? null : $"attachment://{file.FileName}");

        public static List<EmbedFieldBuilder> GetOverflowFields(IEnumerable<string> values, string separator, string title, string subsequentTitle = "\u200B")
        {
            var embeds = new List<EmbedFieldBuilder>();
            var str = "";
            foreach (var value in values)
            {
                if ((str + separator + value).Length >= 1024)
                {
                    embeds.Add(new EmbedFieldBuilder().WithName(subsequentTitle).WithValue(str));
                    str = "";
                }
                str += separator + value;
            }
            if (!string.IsNullOrWhiteSpace(str))
                embeds.Add(new EmbedFieldBuilder().WithName(subsequentTitle).WithValue(str));
            embeds.FirstOrDefault().Name = title;
            return embeds;
        }
    }
}