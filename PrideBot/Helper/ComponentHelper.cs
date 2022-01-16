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
using Microsoft.Data.SqlClient;

namespace PrideBot
{
    static class ComponentHelper
    {
        public static ComponentBuilder ToBuilder(this MessageComponent component)
            => component.Components.ToBuilder();

        public static ComponentBuilder ToBuilder(this IEnumerable<IMessageComponent> actionRows)
        {
            var builder = new ComponentBuilder();
            var actionRowArray = actionRows
                .Select(a => (ActionRowComponent)a)
                .ToArray();
            for (int i = 0; i < actionRowArray.Length; i++)
            {
                var actionRow = actionRowArray[i];
                foreach (var item in actionRow.Components)
                {
                    switch (item)
                    {
                        case ButtonComponent b:
                            builder.WithButton(b.ToBuilder(), i);
                            break;
                        case SelectMenuComponent s:
                            builder.WithSelectMenu(s.ToBuilder(), i);
                            break;
                        default:
                            break;
                    }
                }
            }
            return builder;
        }

        public static ComponentBuilder WithAllDisabled(this ComponentBuilder builder, bool disabled)
        {
            foreach (var actionRow in builder.ActionRows)
            {
                for (int i = 0; i < actionRow.Components.Count; i++)
                {
                    var item = actionRow.Components[i];
                    switch (item)
                    {
                        case ButtonComponent b:
                            actionRow.Components[i] = b.ToBuilder().WithDisabled(disabled).Build();
                            break;
                        case SelectMenuComponent s:
                            actionRow.Components[i] = s.ToBuilder().WithDisabled(disabled).Build();
                            break;
                        default:
                            break;
                    }
                }
            }
            return builder;
        }
    }
}
