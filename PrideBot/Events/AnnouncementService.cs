using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Webhook;
using Discord.Audio;
using Discord.Net;
using Discord.Rest;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using Microsoft.Extensions.Configuration;
using System.Net;
using Microsoft.Data.SqlClient;
using PrideBot.Models;
using PrideBot.Repository;
using PrideBot.Quizzes;
using PrideBot.Game;
using PrideBot.Registration;

namespace PrideBot.Events
{
    public class AnnouncementService
    {
        private readonly IConfigurationRoot config;
        private readonly DiscordSocketClient client;
        private readonly LoggingService loggingService;

        public AnnouncementService(IConfigurationRoot config, DiscordSocketClient client, LoggingService loggingService)
        {
            this.config = config;
            this.client = client;
            this.loggingService = loggingService;

            client.JoinedGuild += JoinedGuild;
        }

        private Task JoinedGuild(SocketGuild guild)
        {
            IntroAnnouncementAsync(guild).GetAwaiter();
            return Task.CompletedTask;
        }

        public async Task IntroAnnouncementAsync(SocketGuild guild)
        {
            try
            {
                if (guild.Id != client.GetGyn(config).Id)
                    return;

                var announcementsChannel = guild.GetChannelFromConfig(config, "announcementschannel") as SocketTextChannel;
                var announcementId = "INTRO";

                // Get purple role
                var newRole = await guild.CreateRoleAsync("Hold Up I Need This Color", null, EmbedHelper.GetEventColor(config), false, null);
                await guild.GetUser(client.CurrentUser.Id).AddRoleAsync(newRole);

                await PostAnnouncementMessageAsync(announcementsChannel, announcementId, 1, false);
                var typing = announcementsChannel.EnterTypingState();
                await Task.Delay(35000);
                await PostAnnouncementMessageAsync(announcementsChannel, announcementId, 2, false);
                typing.Dispose();
                typing = announcementsChannel.EnterTypingState();
                await Task.Delay(35000);
                await PostAnnouncementMessageAsync(announcementsChannel, announcementId, 3, true, config.GetDefaultPrefix());
                typing.Dispose();
                typing = announcementsChannel.EnterTypingState();

                // Rules and perms and crap
                var startTime = DateTime.Now;
                var rulesChannel = guild.GetChannelFromConfig(config, "ruleschannel") as SocketTextChannel;
                var youkaiPerms = new OverwritePermissions(viewChannel: PermValue.Allow);
                var youkaiOverwrite = new Overwrite(ulong.Parse(config["ids:youkai"]), PermissionTarget.Role, youkaiPerms);

                await Task.Delay(Math.Max(0, (int)(startTime.AddSeconds(35) - DateTime.Now).TotalMilliseconds));
                await rulesChannel.Category.ModifyAsync(a => a.PermissionOverwrites = new List<Overwrite>() { youkaiOverwrite });
                await PostAnnouncementMessageAsync(announcementsChannel, announcementId, 4, true, rulesChannel.Mention, config.GetDefaultPrefix());
                typing.Dispose();


                await rulesChannel.SendMessageAsync(DialogueDict.GetNoBullshit("RULES_1", config.GetDefaultPrefix(), guild.GetChannelFromConfig(config, "quizchannel")));
                await rulesChannel.SendMessageAsync(DialogueDict.GetNoBullshit("RULES_2"));
                await rulesChannel.SendMessageAsync(DialogueDict.GetNoBullshit("RULES_3"));
                await rulesChannel.SendMessageAsync(DialogueDict.GetNoBullshit("RULES_4", config.GetDefaultPrefix(), guild.GetChannelFromConfig(config, "scorereportchannel")));
            }
            catch (Exception e)
            {
                await loggingService.OnLogAsync(new LogMessage(LogSeverity.Error, this.GetType().Name, e.Message, e));
                var embed = EmbedHelper.GetEventErrorEmbed(null, DialogueDict.Get("EXCEPTION"), client, showUser: false);
                var modChannel = client.GetGyn(config).GetChannelFromConfig(config, "modchat") as SocketTextChannel;
                await modChannel.SendMessageAsync(embed: embed.Build());

                var announcementsChannel = guild.GetChannelFromConfig(config, "announcementschannel") as SocketTextChannel;
                embed = EmbedHelper.GetEventErrorEmbed(null, "OH NOOO did I really get an internal error during my grand intro? Ahaha, em-BARRASSING right? Isn't public speaking like, the hardest thing? Hold on juuuust a bit for me~" , client, showUser: false);
                await announcementsChannel.SendMessageAsync(embed: embed.Build());
                throw e;
            }
        }

        async Task<IMessage> PostAnnouncementMessageAsync(ITextChannel channel, string announcementId, int messageIndex, bool bullshit, params object[] dialogueArgs)
        {
            var key = $"ANNOUNCEMENT_{announcementId}_{messageIndex}";
            var content = bullshit ? DialogueDict.Get(key, dialogueArgs) : DialogueDict.GetNoBullshit(key, dialogueArgs);
            var folder = $"Assets/AnnouncementFiles/{key}/";
            if (!Directory.Exists(folder))
                return await channel.SendMessageAsync(content);
            else
                return await channel.SendFileAsync(Directory.GetFiles(folder).FirstOrDefault(), content);
        }
    }   
}
