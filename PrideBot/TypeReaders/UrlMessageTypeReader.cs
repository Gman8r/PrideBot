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
    class UrlMessageTypeReader : TypeReader
    {
        public override async Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            try
            {
                var split = input.Split("/");
                split = split.Skip(split.Length - 2).ToArray();
                var message = await (context as SocketCommandContext).Guild.GetTextChannel(ulong.Parse(split[0]))
                    .GetMessageAsync(ulong.Parse(split[1]));
                if (message != null)
                    return TypeReaderResult.FromSuccess(new MessageUrl(message));
            }
            catch
            {
                
            }

            return await Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed,
                $"Message URL is invalid or destination cannot be found."));
        }
    }
}
