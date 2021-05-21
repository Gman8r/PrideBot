using System;
using System.Collections.Generic;
using System.Text;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Discord.Commands.Builders;

namespace PrideBot
{
    public class PrideModuleBase : ModuleBase<SocketCommandContext>
    {
        private static Dictionary<string, PrideModuleBase> BuiltModules;
        private static Dictionary<ModuleInfo, PrideModuleBase> ModuleDictCache;

        public static Dictionary<ModuleInfo, PrideModuleBase> GetModuleClassDictionary(CommandService service)
        {
            if (ModuleDictCache == null)
            {
                ModuleDictCache = service.Modules
                    .Where(a => BuiltModules.ContainsKey(a.Name))
                    .Select(a => new KeyValuePair<ModuleInfo, PrideModuleBase>(a, BuiltModules[a.Name]))
                    .ToDictionary(t => t.Key, t => t.Value);
            }
            return ModuleDictCache;
        }

        public static KeyValuePair<ModuleInfo, PrideModuleBase> GetModule<T>(CommandService service)
            => GetModuleClassDictionary(service)
                .FirstOrDefault(a => a.Value.GetType() == typeof(T));
        public static KeyValuePair<ModuleInfo, PrideModuleBase> GetModule<T>(T type, CommandService service)
            => GetModule<T>(service);
        public static KeyValuePair<ModuleInfo, PrideModuleBase> GetModule(ModuleInfo moduleInfo, CommandService service)
            => GetModuleClassDictionary(service)
                .FirstOrDefault(a => a.Key.Equals(moduleInfo));


        protected virtual async Task<IUserMessage> ReplyErrorAsync(string message = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null)
        {
            message = MessageHelper.ConvertToErrorMessage(message, Context.User.IsOwner());
            return await ReplyAsync(message, isTTS, embed, options, allowedMentions, messageReference);
        }

        protected virtual async Task<IUserMessage> ReplyResultAsync(string message = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, AllowedMentions allowedMentions = null, MessageReference messageReference = null)
        {
            var honorific = Context.User.Queen(Context.Client);
            message = MessageHelper.ConvertToResultMessage(message, honorific);
            return await ReplyAsync(message, isTTS, embed, options, allowedMentions, messageReference);
        }

        protected override void OnModuleBuilding(CommandService commandService, ModuleBuilder builder)
        {
            if (BuiltModules == null)
                BuiltModules = new Dictionary<string, PrideModuleBase>();
            BuiltModules[builder.Name] = this;
        }


        public virtual int HelpSortOrder => 0;

        public virtual async Task<EmbedFieldBuilder> GetHelpLineAsync(ModuleInfo moduleInfo, IEnumerable<CommandInfo> allUsableCommands, SocketCommandContext context, IServiceProvider provider, IConfigurationRoot config)
        {
            var prefix = config.GetDefaultPrefix();
            var modulePath = moduleInfo.IsSubmodule ? moduleInfo.Name + " " : "";   // TODO support N-depth modules if needed
            var submodules = moduleInfo.Submodules
                .Where(a => allUsableCommands.Any(aa => aa.Module == a));
            var commandStrings = allUsableCommands
                .Where(a => a.Module.Equals(moduleInfo))
                .Select(a => $"`{prefix}{modulePath}{a.Name}`")
                .Distinct()
                .Concat(submodules
                    .Select(a => $"`{modulePath}{a.Name} ...`"));
            if (!commandStrings.Any())
                return null;

            var fieldName = !moduleInfo.IsSubmodule ? moduleInfo.Name : "Commands";
            var value = string.Join("\n", commandStrings);
            if (moduleInfo.IsSubmodule)
            {
                var aliases = moduleInfo.Aliases.Except(new string[] { moduleInfo.Name });
                if (aliases.Any())
                    value += $"\n**Aliases:**  {string.Join(",  ", aliases)}";
                if (!string.IsNullOrWhiteSpace(moduleInfo.Summary))
                    value += $"\n**Description:**  {moduleInfo.Summary}";
            }
            return new EmbedFieldBuilder().WithName(fieldName).WithValue(value).WithIsInline(true);
        }
    }
}

