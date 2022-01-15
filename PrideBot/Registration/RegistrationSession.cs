using Discord;
using Discord.WebSocket;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PrideBot.Models;
using PrideBot.Repository;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PrideBot.Game;

namespace PrideBot.Registration
{
    public class RegistrationSession : DMSession
    {
        protected static IEmote DeleteEmote => new Emoji("🗑");
        protected static IEmote BackgroundEmote => new Emoji("🖼");
        protected static IEmote DoubleEmote => new Emoji("💕");

        readonly ShipImageGenerator shipImageGenerator;
        readonly ModelRepository repo;
        readonly ScoringService scoringService;
        UserRegisteredCache userRegs;

        bool userHasRegistered;
        User dbUser;
        UserShipCollection dbUserShips;

        public RegistrationSession(IDMChannel channel, SocketUser user, IConfigurationRoot config, ShipImageGenerator shipImageGenerator, ModelRepository repo, DiscordSocketClient client, TimeSpan timeout, SocketMessage originmessage, ScoringService scoringService, UserRegisteredCache userRegs) : base(channel, user, config, client, timeout, originmessage)
        {
            this.shipImageGenerator = shipImageGenerator;
            this.repo = repo;
            this.scoringService = scoringService;
            this.userRegs = userRegs;
        }

        public IDMChannel Channel { get; }

        protected override async Task PerformSessionInternalAsync()
        {

            using var connection = repo.GetDatabaseConnection();
            await connection.OpenAsync();

            dbUserShips = new UserShipCollection();
            dbUser = await repo.GetOrCreateUserAsync(connection, user.Id.ToString());
            if (dbUser != null)
                dbUserShips = await repo.GetUserShipsAsync(connection, dbUser);
            userHasRegistered = dbUser.ShipsSelected;

            if (!dbUser.ShipsSelected)
                await FirstTimeSetupAsync(connection, dbUserShips);
            else
                await EditRegistrationAsync(connection, dbUserShips);

        }

        async Task FirstTimeSetupAsync(SqlConnection connection, UserShipCollection dbUserShips)
        {

            var embed = GetEmbed()
                .WithTitle(userHasRegistered ? "Edit Your Registration!" : "Registration Time!")
                .WithDescription(userHasRegistered
                ? DialogueDict.Get("REGISTRATION_EDIT", user.Queen(client))
                : DialogueDict.Get("REGISTRATION_WELCOME", user.Queen(client), config.GetDefaultPrefix()));

            //embed.ImageUrl = config.GetRelativeHostPathWeb(await shipImageGenerator.GenerateBackgroundChoicesAsync(dbUser));

            var firstResponse = await SendAndAwaitYesNoResponseAsync(embed: embed);
            if (!firstResponse.IsYes)
            {
                await channel.SendMessageAsync(embed: GetUserCancelledEmbed().Build());
                return;
            }

            // Main loop for reg finalization
            while (true)
            {
                embed = GetEmbed()
                    .WithDescription("");
                for (int i = 0; i < 3; i++)
                {
                    embed.Description = await SetUpShip(connection, (UserShipTier)i, embed);
                }


                await SetUpBackgroundAsync(connection, embed.Description);

                var confirmImageFile = await GetShipsImageAsync(dbUser, dbUserShips);
                embed = GetEmbed()
                    .WithTitle("Confirm Please!")
                    .WithDescription(DialogueDict.Get(dbUser.ShipsSelected ? "REGISTRATION_CONFIRM_EDIT" : "REGISTRATION_CONFIRM"));
                var confirmChoices = new List<IEmote>() { YesEmote, new Emoji("↩") };
                if (!dbUser.ShipsSelected)
                    confirmChoices.Add(NoEmote);
                var result = await SendAndAwaitEmoteResponseAsync(file: confirmImageFile, embed: embed, emoteChoices: confirmChoices);

                if (result.IsYes)
                    break;
                else if (result.IsNo)
                {
                    await channel.SendMessageAsync(embed: GetUserCancelledEmbed().Build());
                    return;
                }
            }

            var key = "REGISTRATION_" + (userHasRegistered ? "EDITED" : "COMPLETE") + (GameHelper.IsEventOccuring(config) ? "" : "_PREREG");
            embed = GetEmbed()
                .WithTitle("Setup Complete!")
                .WithDescription(DialogueDict.Get(key, config.GetDefaultPrefix()));
            await channel.SendMessageAsync(embed: embed.Build());
            if (!userHasRegistered)
            {
                dbUser.ShipsSelected = true;
                await repo.UpdateUserAsync(connection, dbUser);
                userRegs[user.Id.ToString()] = true;

                var achievementId = GameHelper.IsEventOccuring(config) ? "REGISTER" : "PREREGISTER";
                var achievement = await repo.GetAchievementAsync(connection, achievementId);
                await scoringService.AddAndDisplayAchievementAsync(connection, user, achievement, client.CurrentUser);

                // Reward points from before user registered if needed
                var storedPoints = await repo.GetUserNonRegPoints(connection, dbUser.UserId);
                if (storedPoints > 0)
                {
                    var pointsAchievement = await repo.GetAchievementAsync(connection, "STORED");
                    await scoringService.AddAndDisplayAchievementAsync(connection, user, pointsAchievement, client.CurrentUser, overridePoints: storedPoints);
                }

                // Give registered role
                var gyn = client.GetGyn(config);
                var registeredRole = gyn.GetRoleFromConfig(config, "registeredrole");
                var guildUser = gyn.GetUser(user.Id);
                if (guildUser != null)
                {
                    try
                    {
                        await guildUser.AddRoleAsync(registeredRole);
                    }
                    catch (Exception e)
                    {
                        var modEmbed = EmbedHelper.GetEventErrorEmbed(null, $"OH NO! Sages I'm totes sorry to bug you all but I need my role to be higher so I can give people the registration role! And then give the {registeredRole.Mention} role to my bestie {guildUser.Mention}, pretty please?", client, showUser: false);
                        var modChannel = client.GetGyn(config).GetChannelFromConfig(config, "modchat") as SocketTextChannel;
                        await modChannel.SendMessageAsync(embed: modEmbed.Build());
                    }
                }
            }

        }

        async Task EditRegistrationAsync(SqlConnection connection, UserShipCollection dbUserShips)
        {
            var descriptionPrefix = DialogueDict.Get("REGISTRATION_EDIT", user.Queen(client));
            EmbedBuilder embed = null;
            while(true)
            {
                var editImageFile = await GetShipsImageAsync(dbUser, dbUserShips);
                embed = GetEmbed()
                        .WithTitle("Edit Registration")
                        .WithDescription(descriptionPrefix
                        + "\n\n" + DialogueDict.Get("REGISTRATION_EDIT_INSTRUCTIONS"))
                        .WithAttachedImageUrl(editImageFile);
                var emoteChoices = new List<IEmote> {
                    EmoteHelper.GetNumberEmote(1), EmoteHelper.GetNumberEmote(2), EmoteHelper.GetNumberEmote(3), BackgroundEmote, YesEmote};
                var numberEmotes = emoteChoices
                    .Where(a => EmoteHelper.NumberEmotes.Contains(a.ToString()))
                    .ToList();
                var response = await SendAndAwaitEmoteResponseAsync(file: editImageFile, embed: embed, emoteChoices: emoteChoices);
                if (response.IsYes)
                    break;
                else if (numberEmotes.Contains(response.EmoteResponse))
                    descriptionPrefix = await SetUpShip(connection, (UserShipTier)(numberEmotes.IndexOf(response.EmoteResponse)));
                else if (response.EmoteResponse.Equals(BackgroundEmote))
                    await SetUpBackgroundAsync(connection);

                dbUserShips = await repo.GetUserShipsAsync(connection, dbUser);
            }
            var key = "REGISTRATION_EDITED" + (GameHelper.IsEventOccuring(config) ? "" : "_PREREG");
            embed = GetEmbed()
                .WithTitle("Setup Complete!")
                .WithDescription(DialogueDict.Get(key, config.GetDefaultPrefix()));
            await channel.SendMessageAsync(embed: embed.Build());
        }

        // Returns prefix for next message
        async Task<string> SetUpShip(SqlConnection connection, UserShipTier tier, EmbedBuilder embed = null)
        {
            embed ??= GetEmbed();
            var isNewShip = !dbUserShips.Has(tier);

            var title = $"{tier} Pair Setup";
            var tierKey = userHasRegistered ? "EDIT" : tier.ToString().ToUpper();
            var pointPercent = GameHelper.GetPointPercent(await repo.GetScoreRatioForShipTierAsync(connection, tier));
            embed.Description += "\n\n" + DialogueDict.Get($"REGISTRATION_ENTER_{tierKey}", tier.ToString().ToLower(), pointPercent);

            var enterInstructions = DialogueDict.Get("REGISTRATION_SHIP_FORMAT");
            if (isNewShip && tier != UserShipTier.Primary)
                enterInstructions += "\n" + DialogueDict.Get($"REGISTRATION_SKIP_SHIP", SkipEmote.ToString(), tier.ToString().ToLower());
            else if (!isNewShip)
                enterInstructions += "\n" + DialogueDict.Get($"REGISTRATION_KEEP_SHIP", SkipEmote.ToString());
            embed.Description += "\n\n" + enterInstructions;

            // Determine what bypasses are possible
            var canSkip = tier != UserShipTier.Primary || userHasRegistered || !isNewShip;

            // Now register the ship
            var shipValidated = false;
            Prompt response;
            embed.Title = title;
            var skipped = false;
            UserShip selectedUserShip = null;
            while (!shipValidated)
            {
                embed.WithAttachedImageUrl(await GenerateShipImage(dbUser, dbUserShips, highlightTier: (int)tier));
                response = await SendAndAwaitResponseAsync(embed: embed, canSkip: canSkip);
                if (response.IsSkipped)
                {
                    skipped = true;
                    if (isNewShip)
                        return "";
                    else
                        break;
                }

                var inputResult = await ProcessPairingInputAsync(connection, response.MessageResponse.Content, tier);
                if (!inputResult.IsSuccess)
                {
                    if (!string.IsNullOrWhiteSpace(inputResult.ErrorMessage))
                    {
                        var errorEmbed = EmbedHelper.GetEventErrorEmbed(user, $"{inputResult.ErrorMessage}",
                            client, showUser: false);
                        await channel.SendMessageAsync(embed: errorEmbed.Build());
                    }
                    embed.Description = $"{DialogueDict.Get("SESSION_TRY_AGAIN")} {enterInstructions}";
                }
                else
                {
                    shipValidated = true;
                }
            }
            selectedUserShip ??= dbUserShips.Get(tier);

            if (!skipped)
            {
                var conirmationEmbed = GetEmbed()
                    .WithTitle(DialogueDict.Get("REGISTRATION_SHIP_ENTERED"))
                    .WithDescription(DialogueDict.Get("REGISTRATION_SHIP_REVIEW", selectedUserShip.GetDisplayName(), tier.ToString().ToLower()));
                await channel.SendMessageAsync(embed: conirmationEmbed.Build());
            }

            embed.Description = DialogueDict.Get("REGISTRATION_HEART_PROMPT");
            response = await SetUpHeartAsync(connection, selectedUserShip, embed, 1);
            //if (!response.IsSkipped)
            await SetUpHeartAsync(connection, selectedUserShip, GetEmbed(), 2);

            return DialogueDict.Get("REGISTRATION_FINISH_SHIP", user.Queen(client));
        }

        async Task<Prompt> SetUpHeartAsync(SqlConnection connection, UserShip userShip, EmbedBuilder embed, int heart)
        {
            embed ??= GetEmbed();
            var heartIsValid = true;
            Prompt response = null;
            do
            {
                heartIsValid = true;
                var heartEmotes = client.GetGuild(776510593443430401).Emotes.Where(a => a.Name.StartsWith("shipheart"))
                    .Select(a => (IEmote)a)
                    .ToList();
                // Make sure we have heart all emote images
                foreach (var heartEmote in heartEmotes)
                {
                    var heartFile = $"Assets/Hearts/{heartEmote.Name}.png";
                    if (!File.Exists(heartFile))
                    {
                        var data = await WebHelper.DownloadWebFileDataAsync((heartEmote as Emote).Url);
                        var stream = new FileStream(heartFile, FileMode.Create, FileAccess.Write);
                        stream.Seek(0, SeekOrigin.Begin);
                        await stream.WriteAsync(data);
                        stream.Close();
                    }
                }
                var heartChoicesStr = string.Join(" ", heartEmotes);
                var registrationHeartImageFile = await GenerateShipImage(dbUser, dbUserShips, highlightTier: userShip.Tier, highlightHeart: heart);
                embed = embed.WithDescription((embed.Description ?? "")
                        + "\n\n" + DialogueDict.Get("REGISTRATION_HEART_CHOOSE",
                        heart == 1 ? userShip.Character1First : userShip.Character2First, SkipEmote, heartChoicesStr, DoubleEmote))
                    .WithAttachedImageUrl(registrationHeartImageFile)
                    .WithTitle($"{(UserShipTier)userShip.Tier} Pair Heart Setup");
                var choiceEmotes = new List<IEmote>(heartEmotes);
                choiceEmotes.Insert(0, DoubleEmote);
                response = await SendAndAwaitEmoteResponseAsync(file: registrationHeartImageFile, embed: embed, emoteChoices: choiceEmotes, canSkip: true);
                if (!response.IsSkipped)
                {
                    if (response.EmoteResponse.Equals(DoubleEmote))
                    {
                        if (heart == 1)
                        {
                            userShip.Heart1 = "shipheart";
                            userShip.Heart1Right = "shipheart";
                        }
                        else
                        {
                            userShip.Heart2 = "shipheart";
                            userShip.Heart2Right = "shipheart";
                        }

                        await SetUpHeartHalf(connection, userShip, heart, false, heartEmotes);
                        await SetUpHeartHalf(connection, userShip, heart, true, heartEmotes);

                        // check for bi and pan lesbians rip
                        var bothHearts = heart == 1
                            ? new List<string> { userShip.Heart1, userShip.Heart1Right }
                            : new List<string> { userShip.Heart2, userShip.Heart2Right };

                        if ((bothHearts.Select(a => a.ToLower()).Contains("shipheartbi") || bothHearts.Select(a => a.ToLower()).Contains("shipheartpan"))
                            && bothHearts.Select(a => a.ToLower()).Contains("shipheartlesbian"))
                        {
                            var errorEmbed = EmbedHelper.GetEventErrorEmbed(null,
                                DialogueDict.GetNoBrainRot("REGISTRATION_HEART_INVALID"), client, false);
                            await channel.SendMessageAsync(embed: errorEmbed.Build());
                            heartIsValid = false;

                            if (heart == 1)
                            {
                                userShip.Heart1 = "shipheart";
                                userShip.Heart1Right = "shipheart";
                            }
                            else
                            {
                                userShip.Heart2 = "shipheart";
                                userShip.Heart2Right = "shipheart";
                            }

                            continue;
                        }

                    }
                    else if (heart == 1)
                    {
                        userShip.Heart1 = ((Emote)response.EmoteResponse).Name;
                        userShip.Heart1Right = null;
                    }
                    else
                    {
                        userShip.Heart2 = ((Emote)response.EmoteResponse).Name;
                        userShip.Heart2Right = null;
                    }
                }
            }
            while (!heartIsValid);
            await repo.UpdateUserShipAsync(connection, userShip);
            return response;
        }

        async Task SetUpHeartHalf(SqlConnection connection, UserShip userShip, int heart, bool isRightHalf, List<IEmote> heartEmotes)
        {
            var choiceEmotes = new List<IEmote>(heartEmotes);
            choiceEmotes.RemoveAt(0);
            var heartChoicesStr = string.Join(" ", choiceEmotes);
            var heartHalfBannerFile = await GenerateShipImage(dbUser, dbUserShips, highlightTier: userShip.Tier, highlightHeart: heart);
            var embed = GetEmbed()
                .WithDescription(DialogueDict.Get("REGISTRATION_HEART_CHOOSE_SIDE",
                heart == 1 ? userShip.Character1First : userShip.Character2First, isRightHalf ? "right" : "left", heartChoicesStr))
                .WithAttachedImageUrl(heartHalfBannerFile)
                .WithTitle($"{(UserShipTier)userShip.Tier} Pair Heart Setup");
            var response = await SendAndAwaitEmoteResponseAsync(file: heartHalfBannerFile, embed: embed, emoteChoices: choiceEmotes);
            if (heart == 1)
            {
                if (!isRightHalf)
                    userShip.Heart1 = ((Emote)response.EmoteResponse).Name;
                else
                    userShip.Heart1Right = ((Emote)response.EmoteResponse).Name;
            }
            else
            {
                if (!isRightHalf)
                    userShip.Heart2 = ((Emote)response.EmoteResponse).Name;
                else
                    userShip.Heart2Right = ((Emote)response.EmoteResponse).Name;
            }
        }

        async Task SetUpBackgroundAsync(SqlConnection connection, string descriptionPrefix = "")
        {
            var embed = GetEmbed()
                .WithTitle("Choose a Background")
                .WithDescription(descriptionPrefix
                + "\n\n" + DialogueDict.Get("REGISTRATION_CUSTOMIZE_BG" + (dbUser.ShipsSelected ? "_EDIT" : ""), config.GetDefaultPrefix())
                + "\n\n" + DialogueDict.Get("REGISTRATION_CHOOSE_BG"));

            var bgImagesFile = await shipImageGenerator.GenerateBackgroundChoicesAsync(dbUser);
            var emotes = Enumerable.Range(1, Directory.GetFiles("Assets/Backgrounds").Length)
                .Select(a => new Emoji(EmoteHelper.NumberEmotes[a]) as IEmote)
                .ToList();

            while (true)
            {
                emotes.Insert(0, YesEmote);
                embed.WithAttachedImageUrl(bgImagesFile);
                var bgResponse = await SendAndAwaitEmoteResponseAsync(file: bgImagesFile, embed: embed, emoteChoices: emotes);

                if (bgResponse.IsYes)
                    break;
                dbUser.CardBackground = emotes.FindIndex(a => a.ToString().Equals(bgResponse.EmoteResponse.ToString()));
                var bgImageFile = await GetShipsImageAsync(dbUser, dbUserShips);
                embed = GetEmbed()
                    .WithTitle("Background Config")
                    .WithDescription(DialogueDict.Get("REGISTRATION_BG_CHANGED", user.Queen(client)));
                await repo.UpdateUserAsync(connection, dbUser);
                await channel.SendFileAsync(bgImageFile.Stream, bgImageFile.FileName, embed: embed.Build());

                embed = GetEmbed()
                    .WithTitle("Choose a Background")
                    .WithDescription(DialogueDict.Get("REGISTRATION_CHOOSE_BG"));
            }
        }


        async Task<Result> ProcessPairingInputAsync(SqlConnection connection, string shipStr, UserShipTier tier)
        {
            if (shipStr.ToLower().Contains("drop table") || shipStr.Contains(";--"))
                return Result.Error("HAHAHAHA THAT'S SO FUNNY I HOPE YOU KNOW HOW FUNNY YOU ARE !!!");
            using var typingState = channel.EnterTypingState();
            var dbCharacters = await repo.GetAllCharactersAsync(connection);
            var parseResult = await ParseShipAsync(connection, repo, shipStr, dbCharacters);
            if (!parseResult.IsSuccess)
                return parseResult.ToResult();
            var ship = parseResult.Value;

            // Don't recreate ship if they just inputed the same one (to preserve ship hearts)
            if (((dbUserShips.Get(tier)?.ShipId) ?? "").Equals(ship.ShipId))
                return Result.Success();

            var validateResult = await ValidateShipAsync(connection, ship, dbCharacters);
            if (!validateResult.IsSuccess)
                return validateResult;

            typingState.Dispose();
            var duplicateShip = dbUserShips.FirstOrDefault(a => a.Tier != (int)tier && a.ShipId.Equals(ship.ShipId));
            if (duplicateShip != null)
            {
                var result = await HandleDuplicateShipAsync(connection, duplicateShip, tier);
                if (!result.IsSuccess)
                    return result.ToResult();
                if (result.Value)
                    return Result.Success();
            }
            await repo.CreateOrReplaceUserShip(connection, dbUser.UserId, tier, ship.ShipId);
            dbUserShips = await repo.GetUserShipsAsync(connection, dbUser.UserId);
            return Result.Success();
        }

        public static async Task<ValueResult<Ship>> ParseShipAsync(SqlConnection connection, ModelRepository repo, string shipStr, IEnumerable<Character> dbCharacters)
        {
            var split = shipStr.Replace(" x ", " X ").Split(" X ");
            if (split.Length != 2)
                return ValueResult<Ship>.Error(DialogueDict.Get("REGISTRATION_ERROR_FORMAT"));

            var char1 = FindMatch(split[0], dbCharacters);
            var char2 = FindMatch(split[1], dbCharacters);

            if (char1 == null)
                return ValueResult<Ship>.Error(DialogueDict.Get("REGISTRATION_ERROR_NOT_FOUND", split[0]));
            if (char2 == null)
                return ValueResult<Ship>.Error(DialogueDict.Get("REGISTRATION_ERROR_NOT_FOUND", split[1]));

            var shipKey = await repo.GetOrCreateShipAsync(connection, char1.CharacterId, char2.CharacterId);
            return ValueResult<Ship>.Success(await repo.GetShipAsync(connection, shipKey));
        }

        public static async Task<Result> ValidateShipAsync(SqlConnection connection, Ship ship, IEnumerable<Character> dbCharacters)
        {
            var char1 = dbCharacters
                .FirstOrDefault(a => a.CharacterId == ship.CharacterId1);
            var char2 = dbCharacters
                .FirstOrDefault(a => a.CharacterId == ship.CharacterId2);

            if (ship.CharacterId1.Equals("YURIKO") || ship.CharacterId2.Equals("YURIKO"))
            {
                //if (ship.CharacterId1.Equals("JOON") || ship.CharacterId2.Equals("JOON"))
                //    return Result.Error("HAha, what? Noo, no I can't just. Please we're like so uh, different, in ways, y'know? 💦 I'm married to my work, yknow? Can't just do that haha... (Damn)");
                //else
                    return Result.Error(DialogueDict.Get("REGISTRATION_ERROR_YURIKO"));
            }
            if (char1.CharacterId.Equals(char2.CharacterId) && !char1.CharacterId.Equals("TSUCHINOKO"))
                return Result.Error(DialogueDict.Get("REGISTRATION_ERROR_SELF"));
            if (ship.IsBlacklisted)
                return Result.Error(DialogueDict.Get("REGISTRATION_ERROR_INVALID"));

            var categories = new string[] { char1.Category, char2.Category };
            if (categories.Contains("AMBIGUOUS"))
            {
                var compatibleFields = new string[] { "AMBIGUOUS", "ADULT", "CHILD" };
                if (!compatibleFields.Contains(char1.Category) || !compatibleFields.Contains(char2.Category))
                    return Result.Error(DialogueDict.Get("REGISTRATION_ERROR_INVALID"));
            }
            else if (!char1.Category.Equals(char2.Category))
                return Result.Error(DialogueDict.Get("REGISTRATION_ERROR_INVALID"));

            if (!string.IsNullOrWhiteSpace(char1.Family) && char1.Family.Equals(char2.Family))
                return Result.Error(DialogueDict.Get("REGISTRATION_ERROR_INVALID"));



            return Result.Success();
        }
        async Task<ValueResult<bool>> HandleDuplicateShipAsync(SqlConnection connection, UserShip duplicateShip, UserShipTier currentTier)
        {
            // Handle tier duplicates and swapping
            if (duplicateShip.Tier == (int)UserShipTier.Primary && !dbUserShips.Has(currentTier))
                return ValueResult<bool>.Error(DialogueDict.Get("REGISTRATION_ERROR_PRIMARY_DUPE"));
            var embed = GetEmbed()
                .WithTitle("Pairing Conflict")
                .WithDescription(DialogueDict.Get("REGISTRATION_ERROR_SHIP_DUPE",
                ((UserShipTier)duplicateShip.Tier).ToString().ToLower(), duplicateShip.GetDisplayName(), currentTier.ToString().ToLower()));
            if (dbUserShips.Has(currentTier))
            {
                var currentShip = dbUserShips.Get(currentTier);
                embed.Description += " " + DialogueDict.Get("REGISTRATION_ERROR_SHIP_DUPE_SWAP",
                currentTier.ToString().ToLower(), currentShip.GetDisplayName());
            }
            var response = await SendAndAwaitYesNoResponseAsync(embed: embed);
            if (response.IsYes)
            {
                await repo.SwapShipTiersAsync(connection, dbUser.UserId, (UserShipTier)duplicateShip.Tier, currentTier);
                dbUserShips = await repo.GetUserShipsAsync(connection, dbUser);
                return ValueResult<bool>.Success(true);
            }
            else
                return ValueResult<bool>.Error("");
        }

        static Character FindMatch(string inputName, IEnumerable<Character> characters)
        {
            var words = inputName
                .ToUpper()
                .Split()
                .Where(a => a.Any())
                .Select(a => ZeroPunctuation(a))
                .ToArray();
            return characters
                .FirstOrDefault(a => !words.Except(ZeroPunctuation(a.Name).ToUpper().Split()).Any());   // All words in input name appear in character name
        }

        static string ZeroPunctuation(string str)
            => new string(str.Where(a => !char.IsPunctuation(a)).ToArray());

        async Task<MemoryFile> GetShipsImageAsync(User dbUser, UserShipCollection dbShips, int highlightTier = -1, int highlightHeart = 0)
        => await GenerateShipImage(dbUser, dbShips, highlightTier, highlightHeart);

        public async Task<MemoryFile> GenerateShipImage(User dbUser, UserShipCollection dbShips, int highlightTier = -1, int highlightHeart = 0, bool blackOutHeartRight = false)
        {
            return await shipImageGenerator.WriteUserCardAsync(dbUser, dbShips, highlightTier, highlightHeart, blackOutHeartRight: blackOutHeartRight);
        }

        protected override string GetTimeoutMessage() => base.GetTimeoutMessage() + "\n\n" + DialogueDict.Get("SESSION_TIMEOUT_SAVED");
    }
}
