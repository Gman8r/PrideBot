using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Immutable;
using System.Linq;

namespace PrideBot.TypeReaders
{
    class IEmoteTypeReader<T> : TypeReader where T : IEmote
    {
        public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            var results = new Dictionary<string, TypeReaderValue>();

            // As emote (1.0)
            if (typeof(T) != typeof(Emoji))
            {
                Emote emote;
                if (Emote.TryParse(input, out emote))
                {
                    results.Add(emote.ToString(), new TypeReaderValue(emote, 1.0f));
                }
            }
            // As emoji (0.9)
            if (typeof(T) != typeof(Emote))
            {
                Emoji emoji = null;
                if (EmoteHelper.TryParseEmoji(input, out emoji))
                {
                    results.Add(emoji.ToString(), new TypeReaderValue(emoji, 0.9f));
                }
            }

            if (results.Any())
                return TypeReaderResult.FromSuccess(results.Values.ToImmutableArray());

            return await Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed,
                $"Input could not be parsed as an emote or emoji ({input})."));

        }
    }
}
