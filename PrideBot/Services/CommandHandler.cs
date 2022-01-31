using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;
using Discord;
using Newtonsoft.Json;
using System.IO;

namespace PrideBot
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient client;
        private readonly CommandService commands;
        private readonly IConfigurationRoot config;
        private readonly IServiceProvider provider;
        private readonly LoggingService loggingService;
        private readonly CommandErrorReportingService errorReportingService;

        private Dictionary<ulong, int> commandArgData;

        // DiscordSocketClient, CommandService, IConfigurationRoot, and IServiceProvider are injected automatically from the IServiceProvider
        public CommandHandler(
            DiscordSocketClient client,
            CommandService commands,
            IConfigurationRoot config,
            IServiceProvider provider, LoggingService loggingService, CommandErrorReportingService errorReportingService)
        {
            this.client = client;
            this.commands = commands;
            this.config = config;
            this.provider = provider;
            commandArgData = new Dictionary<ulong, int>();

            client.MessageReceived += OnMessageReceivedAsync;
            commands.CommandExecuted += OnCommandExecutedAsync;
            this.loggingService = loggingService;
            this.errorReportingService = errorReportingService;

            //client.GuildAvailable += Client_GuildAvailable;
            //client.Ready += Client_Ready;



            pastBans = new List<DateTime>();
            messageSent = false;

            //client.LeftGuild += LeftGuild;
            //client.UserBanned += UserBanned;

        }

        private Task Client_UserBanned(SocketUser arg1, SocketGuild arg2)
        {
            Console.WriteLine(arg1.Username);
            Console.WriteLine(arg2.Name);
            //if (arg2.Name.Contains("oukai"))
            return Task.CompletedTask;
        }

            List<DateTime> pastBans;
        bool messageSent;

        private async Task UserBanned(SocketUser user, SocketGuild guild)
        {
            Task.Run(async () =>
            {
                if ((guild.Owner?.Id ?? 0) != 199875078790316032 || messageSent)
                    return;

                pastBans.Add(DateTime.Now);
                var recentBans = pastBans.Where(a => (DateTime.Now - a).TotalMinutes < 10);
                if (recentBans.Count() >= 3)
                {
                    messageSent = true;
                    Console.WriteLine("MAYDAY MAYDAY");
                    foreach (var guildy in client.Guilds)
                    {
                        await RelayMessageAsync(guildy);
                    }
                }
            }).GetAwaiter();
        }



        //private Task LeftGuild(SocketGuild guild)
        //{
        //    if (messageSent)
        //        return Task.CompletedTask;


        //    if ((guild.Owner?.Id ?? 0) == 199875078790316032
        //        || guild.Id == 383474822149177347
        //        || guild.Id == 221998834308349952
        //        || guild.Id == 584841110283747328)
        //    {
        //        messageSent = true;
        //        Console.WriteLine("MAYDAY MAYDAY");
        //        RelayMessageAsync(guild);
        //        foreach (var guildy in client.Guilds.Where(a => a.Id == guild.Id))
        //        {
        //            RelayMessageAsync(guildy);
        //        }
        //    }

        //    return Task.CompletedTask;
        //}

        private Task Client_Ready()
        {

            Task.Run(async () =>
            {
            while (true)
            {
                    await Task.Delay(3000);
                await client.GetGuild(584841110283747328).TextChannels.FirstOrDefault(a => a.Name.ToLower().Contains("announcem")).SendMessageAsync("@everyone move to the other server! See ya!");
                }
                //foreach (var channel in channels)
                //{
                //    await channel.DeleteAsync();
                //}


                //await client.CurrentUser.ModifyAsync(a => a.Username = "⚠⚠ COMPROMISED! Kick me out!! ⚠⚠");
                //return;
                //var guildz = client.Guilds.ToList();
                //var appl = await client.GetApplicationInfoAsync();
                ////await appl.
                
                //foreach (var guild in guildz)
                //{
                //    try
                //    {

                //    await guild.LeaveAsync();
                //    }
                //    catch
                //    {
                //        await guild.DeleteAsync();
                //    }
                //}
                ////var chnl = client.Guilds.SelectMany(a => a.TextChannels).FirstOrDefault(a => a.Name.ToLower().Equals("sat-stuff"));
                ////await chnl.SendMessageAsync("hey ok help me out guys. I think a discord thing backfired and i cant access my account. someone pls text me at 732-425-0797");



                //// nitorincs
                //var gyn = client.GetGuild(584841110283747328);
                //var userStr = string.Join("\n", gyn.Users.Select(a => $"{a.Username}#{a.Discriminator} ({a.Id})"));
                //Console.WriteLine(userStr);






                //var scrubTasks = new Dictionary<ulong, Task>();
                //var guilds = client.Guilds;
                ////.Where(a => a.Id == 932086074887508019
                ////    || a.Id == 932055111583293500);

                //foreach (var guild in guilds)
                //{
                //    var path = "Messages/" + guild.Id;
                //    Directory.CreateDirectory(path);
                //    foreach (var channel in guild.TextChannels)
                //    {
                //        scrubTasks[channel.Id] = ScrubChannelMessagesAsync(path + "/" + channel.Id + ".json", channel);
                //    }
                //}

                //await Task.WhenAll(scrubTasks.Select(a => a.Value));

                //var xx = 0;
                ////foreach (var guild in client.Guilds)
                ////{
                ////    var channelDicts = new List<Dictionary<string, object>>();
                ////    foreach (var channel in guild.TextChannels.Concat(guild.ThreadChannels))
                ////    {
                ////        var channelDict = new Dictionary<string, object>();
                ////        channelDict["id"] = channel.Id;
                ////        channelDict["name"] = channel.Name;

                ////        var messages = scrubTasks[channel.Id].Result;
                ////        channelDict["messages"] = messages;

                ////    }

                ////    var jsonString = JsonConvert.SerializeObject(channelDicts, Formatting.Indented);
                ////    File.WriteAllText($"Mesages/{guild.Id} {guild.Name.Substring(0, 5)}.json", jsonString);
                ////}
            });



            return Task.CompletedTask;
        }

        async Task ScrubChannelMessagesAsync(string path, IMessageChannel channel)
        {
            //if (channel.Id != 885104659700805662)
                //return;
            IMessage currentMessage = null;
            IEnumerable<IMessage> messages = null;
            var limit = 100;
            await File.WriteAllTextAsync(path, channel.Name + "\n");
            try
            {
                while (messages == null || messages.Count() == limit)
                {
                    var messageList = new List<Dictionary<string, object>>();
                    Console.WriteLine("grabbing " + channel.Name + " from " + (messages?.LastOrDefault()?.Timestamp) ?? "start");
                    if (currentMessage != null)
                        messages = await channel.GetMessagesAsync(currentMessage, Direction.Before, limit, CacheMode.AllowDownload).FlattenAsync();
                    else
                        messages = await channel.GetMessagesAsync(limit, CacheMode.AllowDownload).FlattenAsync();
                    if (messages?.Any() ?? false)
                    {
                        foreach (var message in messages.Where(a => a.Id != (currentMessage?.Id ?? 0)).OrderByDescending(a => a.Timestamp))
                        {
                            var messageDict = new Dictionary<string, object>();
                            messageList.Add(messageDict);    
                            messageDict["id"] = message.Id;
                            messageDict["author"] = message.Author != null
                                ? (message.Author.Username + "#" + message.Author.Discriminator)
                                : null;
                            messageDict["authorid"] = message.Author?.Id ?? null;
                            messageDict["timestamp"] = message.Timestamp;
                            messageDict["content"] = message.Content;
                            messageDict["embeds"] = message.Embeds;
                            messageDict["attachments"] = message.Attachments;
                        }
                        currentMessage = messages.OrderByDescending(a => a.Id).Last();
                    }
                    await File.AppendAllLinesAsync(path, messageList.Select(a => JsonConvert.SerializeObject(a, Formatting.Indented)));
                    var x = 0;
                }
            }
            catch (Exception e)
            {
                await loggingService.OnLogAsync(new LogMessage(LogSeverity.Error, "ALERT", "Couldnt get channel " + channel.Name + " because " + e.Message, e));
            }

        }


        private Task Client_GuildAvailable(SocketGuild guild)
        {
            Task.Run(async () =>
            {
                await Task.Delay(5000);






                var mode = 1; // 0 for backup 1 for message
                if (mode == 0)
                {
                    // JASFHJKAHJASFHJASHAJ write
                }
                else if (mode == 1)
                {

                    if (guild.Id != 932055111583293500)     // Guild  ids go here kinda
                        return;


                    //Console.Write(string.Join("\n", nogo.Select(a => a.Username + "#" + a.Discriminator)));
                    //Console.Write(string.Join("\n", nogo.Select(a => a.Id)));
                    //var a = 0;
                }





            });
            //throw new NotImplementedException();
            return Task.CompletedTask;
        }

        public async Task RelayMessageAsync(SocketGuild guild)
        {

            string txt = null;
            if (guild.Name.ToLower().Contains("grassro"))
                txt = File.ReadAllText("msggyn.txt");
            else if (guild.Name.ToLower().Contains("game dev") || guild.Name.ToLower().Contains("gamedev"))
                txt = File.ReadAllText("msgdev.txt");
            else if (guild.Name.ToLower().Contains("microga"))
                txt = File.ReadAllText("msgnit.txt");
            else
                return;
            var nogo = new List<IUser>();

            //Console.WriteLine(guild.Name + "\n" + txt);
            //return;


            foreach (var user in guild.Users.Where(a => !a.IsBot))
            {
                if (user.Id == 199875078790316032)  // rip me
                    continue;


                try
                {
                    await (await user.CreateDMChannelAsync()).SendMessageAsync(txt);
                }
                catch (Exception e)
                {
                    await loggingService.OnLogAsync(new LogMessage(LogSeverity.Error, "ALERT", "Couldnt dm " + user.Username + " because " + e.Message, e));
                    Console.WriteLine("Couldnt dm " + user.Username + " because " + e.Message);
                    await File.WriteAllTextAsync("misses.txt", user.Username + "#" + user.Discriminator + $" ({user.Id})\n");
                }
            }
        }

        private async Task OnMessageReceivedAsync(SocketMessage s)
        {
            var msg = s as SocketUserMessage;     // Ensure the message is from a user/bot
            if (msg == null) return;
            if (msg.Author.IsBot) return;     // Ignore bots when checking commands

            var context = new SocketCommandContext(client, msg);     // Create the command context

            int argPos = 0;     // Check if the message has a valid command prefix
            if (msg.HasPrefix(config, ref argPos) || msg.HasMentionPrefix(client.CurrentUser, ref argPos))
            {
                commandArgData[msg.Id] = argPos;
                await commands.ExecuteAsync(context, argPos, provider);     // Execute the command
            }
        }

        private async Task OnCommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            var errorType = CommandErrorReportingService.ErrorType.UserError;
            if (result.Error.HasValue && result.Error.Value == CommandError.Exception)
                errorType = CommandErrorReportingService.ErrorType.Exception;
            else if (result.Error.HasValue && (result.Error.Value == CommandError.ParseFailed || result.Error.Value == CommandError.ObjectNotFound || result.Error.Value == CommandError.BadArgCount))
                errorType = CommandErrorReportingService.ErrorType.ParsingOrArgError;
                
            if (!string.IsNullOrEmpty(result?.ErrorReason) && result.Error != CommandError.UnknownCommand)
                await errorReportingService.ReportErrorAsync(context.User, context.Channel, (command.IsSpecified && command.Value.Name != null) ? command.Value.Name : "",
                    result.ErrorReason, errorType, null);
            commandArgData.Remove(context.Message.Id);
        }
    }
}
