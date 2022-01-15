using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrideBot
{
    static class MessageHelper
    {
        public static async Task<IUserMessage> SendErrorMessageAsync(this IMessageChannel channel, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, bool toOwner = false, AllowedMentions allowedMentions = null, MessageReference messageReference = null)
        {
            return await channel.SendMessageAsync(ConvertToErrorMessage(text, toOwner),
                isTTS, embed, options, allowedMentions, messageReference);
        }
        public static async Task<IUserMessage> SendResultMessageAsync(this IMessageChannel channel, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, string honorific = "", AllowedMentions allowedMentions = null, MessageReference messageReference = null, bool sendEmote = true)
        {
            return await channel.SendMessageAsync(ConvertToResultMessage(text, honorific, sendEmote),
                isTTS, embed, options, allowedMentions, messageReference);
        }

        public static string ConvertToResultMessage(string message, string honorific, bool sendEmote = true)
        {
            var breakAt = message.IndexOfAny(new char[] { '.' , '!' });
            if (breakAt >= 0)
                message = message.Substring(0, breakAt) + $" {honorific}" + message.Substring(breakAt);
            else
                message += $" {honorific}!";

            if (sendEmote)
                message = $"{ProgressBar.SignatureEmote} {message}";
            message = DialogueDict.GenerateEmojiText(message);
            return message;
        }

        public static string ConvertToErrorMessage(string message, bool isBotOwner)
        {
            var errorPrefix = isBotOwner ? "Insufficient, my lady. " : "Insufficient, sorry. ";
            return errorPrefix + message;
        }
        
        const int MessageMaxChars = 1945;

        public static async Task<List<IUserMessage>> SendOverflowMessagesAsync(this IMessageChannel channel, string message = null, string delimiter = null,
            bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null)
        {
            var parts = delimiter == null ? message.Split() : message.Split(delimiter);
            return await SendOverflowMessagesAsync(channel, parts, delimiter, isTTS, embed, options, allowedMentions, messageReference);
        }

        public static async Task<List<IUserMessage>> SendOverflowMessagesAsync(this IMessageChannel channel, string[] parts, string delimiter = " ",
            bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null)
        {
            var returnMessages = new List<IUserMessage>();

            var currentMessage = "";
            var first = true;
            foreach (var part in parts)
            {
                if (part.Length > MessageMaxChars)
                    throw new CommandException("Chunk of message was too large to send.");
                if ((currentMessage + part).Length > MessageMaxChars)
                {
                    if (!string.IsNullOrWhiteSpace(currentMessage))
                        returnMessages.Add(await channel.SendMessageAsync(currentMessage,
                            isTTS, embed, options, allowedMentions, messageReference)
                            .ConfigureAwait(false));
                    currentMessage = part;
                }
                else
                    currentMessage += delimiter + part;
                first = false;
            }
            if (!string.IsNullOrWhiteSpace(currentMessage))
                returnMessages.Add(await channel.SendMessageAsync(currentMessage,
                    isTTS, embed, options, allowedMentions, messageReference)
                    .ConfigureAwait(false));
            return returnMessages;
        }

        public static List<EmbedFieldBuilder> WithMessageReference(this List<EmbedFieldBuilder> fields, IMessage message)
        {
            var newFields = new List<EmbedFieldBuilder>(fields);
            newFields.Add(new EmbedFieldBuilder()
                .WithName("Reference:")
                .WithValue(message.GetJumpUrl()));
            return newFields;
        }


        public enum LogSeverity
        {
            Info,
            Error,
            InternalError
        }

        public enum Logrecipient
        {
            LogChannel,
            UserDM
        }

        const string EmoteUrl = "https://cdn.discordapp.com/emojis/799928966403457074.gif?v=1";
        const string ErrorEmoteUrl = "https://cdn.discordapp.com/emojis/809543471449767946.png?v=1";


        public static EmbedBuilder GetLogEmbed(string title = null, string description = null, List<EmbedFieldBuilder> fields = null, LogSeverity severity = LogSeverity.Info, Logrecipient recipient = Logrecipient.LogChannel, string reason = null, string imageUrl = null)
        {
            var embed = new EmbedBuilder();
            embed.Description = description ?? "";
            embed.Author = new EmbedAuthorBuilder()
                .WithIconUrl(imageUrl ?? (severity == LogSeverity.Info ? EmoteUrl : ErrorEmoteUrl))
                .WithName(title ?? (severity == LogSeverity.InternalError ? "Internal Error" : severity.ToString()));
            if (fields != null)
                embed.Fields.AddRange(fields);
            if (reason != null)
                embed.AddField("Reason:", reason);
            if (recipient == Logrecipient.UserDM)
                embed.Footer = new EmbedFooterBuilder().WithText($"Please contact a sage if you think there's been a mistake.");
            if (severity == LogSeverity.InternalError)
                embed.Footer = new EmbedFooterBuilder().WithText($"Please check the logs.");
            embed.Color = severity == LogSeverity.Info ? Color.LightOrange : Color.Red;
            return embed;
        }

        public static async Task<IUserMessage> QuickLogAsync(this SocketGuild guild, IServiceProvider provider, string title = null, string description = null, List<EmbedFieldBuilder> fields = null, LogSeverity severity = LogSeverity.Info, Logrecipient recipient = Logrecipient.LogChannel, string reason = null, string imageUrl = null, string messageText = null)
        {
            return await SendLogMessageAsync(guild, provider, messageText,
                embed: GetLogEmbed(title, description, fields, severity, recipient, reason, imageUrl)
                .Build());
        }

        public static async Task<IUserMessage> QuickLogAsync(this SocketGuild guild, IServiceProvider provider, string title, string description, params (string, string)[] fieldValues)
            => await QuickLogAsync(guild, provider, title, description, LogSeverity.Info, fieldValues);

        public static async Task<IUserMessage> QuickLogAsync(this SocketGuild guild, IServiceProvider provider, string title, string description, LogSeverity severity, params (string, string)[] fieldValues)

        {
            fieldValues ??= new (string, string)[0];
            var fields = fieldValues
                .Select(a => new EmbedFieldBuilder()
                    .WithName(a.Item1)
                    .WithValue(a.Item2))
                .ToList();
            return await QuickLogAsync(guild, provider, title, description, fields, severity);
        }


        public static async Task<IUserMessage> AttemptSendDMAsync(this SocketUser user, IServiceProvider provider, string text = null, string overrideEmote = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null)
        {
            try
            {
                return await (await user.CreateDMChannelAsync()).SendMessageAsync(text, isTTS, embed, options, allowedMentions, messageReference);
            }
            catch
            {
                Console.WriteLine($"Couldn't DM {user?.Username} with a report.");
                return null;
            }
        }

        public static async Task<IUserMessage> SendLogMessageAsync(this SocketGuild guild, IServiceProvider provider, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null)
        {
            await provider.GetService<LoggingService>().OnLogAsync(new LogMessage(Discord.LogSeverity.Info, "ModLog",
                (embed?.Description ?? embed?.Title ?? text ?? "Unknown Log Message") + $" in {guild.Name} ({guild.Id})"));

            try
            {
                var config = provider.GetService<IConfigurationRoot>();
                var chat = guild.GetTextChannel(ulong.Parse(config["ids:logchat"]))
                    ?? guild.TextChannels.FirstOrDefault(a => a.Name.ToLower().Contains("hanilog"));
                return await chat.SendMessageAsync(text, isTTS, embed, options, allowedMentions, messageReference);
            }
            catch
            {
                Console.WriteLine($"Couldn't send server log message for {guild?.Name}.");
                return null;
            }
        }

        public static bool HasPrefix(this SocketUserMessage msg, IConfigurationRoot config, ref int argPos)
        {
            var prefixes = ConfigHelper.GetPrefixes(config)
                .OrderByDescending(a => a.Length)   // Find largest possible match;
                .ToList();
            //if (msg.Author.IsOwner())
            //    prefixes.Add("oh! ");
            foreach (var prefix in prefixes)
            {
                if (msg.HasStringPrefix(prefix, ref argPos, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static string ToBubbleText(string phrase)
        {
            string message = "";

            phrase = phrase.ToLower();
            char[] chars = phrase.ToCharArray();
            string[] letters = new string[chars.Length];

            for (int i = 0; i < letters.Length; i++)
            {
                if ((int)chars[i] >= 97 && (int)chars[i] <= 122)
                {
                    letters[i] = ":regional_indicator_" + chars[i].ToString() + ":";
                }
                else if ((int)chars[i] >= 48 && (int)chars[i] <= 57)
                {
                    letters[i] = EmoteHelper.NumberEmotes[chars[i] - 48];
                }
                else if (chars[i] == '!')
                {
                    letters[i] = ":exclamation:";
                }
                else if (chars[i] == '?')
                {
                    letters[i] = ":question:";
                }
                else if (chars[i] == ' ')
                {
                    letters[i] = "    ";
                }
                else
                {
                    letters[i] = chars[i].ToString();
                }

                message += letters[i];
            }
            return message;
        }
    }
}
