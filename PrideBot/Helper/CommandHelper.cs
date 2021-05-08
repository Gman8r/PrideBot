using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot
{
    static class CommandHelper
    {
        public static string GetModulePathPrefix(this ModuleInfo module)
            => module.IsSubmodule ? $"{GetModulePathPrefix(module.Parent)}{module.Name} " : "";

        public static string GetFullCommandPath(this CommandInfo command)
            => command.Module.GetModulePathPrefix() + command.Name;
    }
}
