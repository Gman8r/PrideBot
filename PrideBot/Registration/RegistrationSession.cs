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
using PrideBot.Graphics;

namespace PrideBot.Registration
{
    public class RegistrationSession : Session
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
            try
            {

                dbUser = await repo.GetOrCreateUserAsync(connection, user.Id.ToString());
            }
            catch (Exception e)
            {
                var b = 0;
            }
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

            try
            {
                var embed = GetEmbed()
                    .WithTitle(userHasRegistered ? "Edit Your Registration!" : "Registration Time!")
                    .WithDescription(userHasRegistered
                    ? DialogueDict.Get("REGISTRATION_EDIT")
                    : DialogueDict.Get("REGISTRATION_WELCOME", config.GetDefaultPrefix()));

                var components = new ComponentBuilder()
                        .WithButton("Sounds Gay I'm In", "YES", ButtonStyle.Success, ThumbsUpEmote)
                        .WithButton("Not Right Now Actually", "NO", ButtonStyle.Secondary, NoEmote);
                //.WithButton("Stop Doing That Emote Thing First (coming soon)", "YES", ButtonStyle.Secondary, new Emoji("😖"));

                //embed.ImageUrl = config.GetRelativeHostPathWeb(await shipImageGenerator.GenerateBackgroundChoicesAsync(dbUser));

                var firstResponse = await SendAndAwaitResponseAsync(embed: embed, components: components);
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

                    dbUserShips = await repo.GetUserShipsAsync(connection, dbUser);
                    var confirmImageFile = await GetShipsImageAsync(dbUser, dbUserShips);
                    embed = GetEmbed()
                        .WithTitle("Confirm Please!")
                        .WithDescription(DialogueDict.Get(dbUser.ShipsSelected ? "REGISTRATION_CONFIRM_EDIT" : "REGISTRATION_CONFIRM"))
                        .WithAttachedImageUrl(confirmImageFile);

                    var confirmComponents = new ComponentBuilder()
                        .WithButton("All Set!", "YES", ButtonStyle.Success, ThumbsUpEmote)
                        .WithButton("Let Me Redo Some Stuff", "REDO", ButtonStyle.Secondary, new Emoji("↩"))
                        .WithButton("Actually, Cancel For Now", "NO", ButtonStyle.Secondary, NoEmote);

                    var result = await SendAndAwaitNonTextResponseAsync(file: confirmImageFile, embed: embed, components: confirmComponents);

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
                    dbUser = await repo.GetUserAsync(connection, dbUser.UserId);
                    dbUser.ShipsSelected = true;
                    await repo.UpdateUserAsync(connection, dbUser);
                    userRegs[user.Id.ToString()] = true;

                    var achievementId = GameHelper.IsEventOccuring(config) ? "REGISTER" : "PREREGISTER";
                    var achievement = await repo.GetAchievementAsync(connection, achievementId);
                    await scoringService.AddAndDisplayAchievementAsync(connection, user, achievement, client.CurrentUser, DateTime.Now, null);

                    // Reward points from before user registered if needed
                    var storedPoints = await repo.GetUserNonRegPoints(connection, dbUser.UserId);
                    if (storedPoints > 0)
                    {
                        var pointsAchievement = await repo.GetAchievementAsync(connection, "STORED");
                        await scoringService.AddAndDisplayAchievementAsync(connection, user, pointsAchievement, client.CurrentUser, DateTime.Now, null, overridePoints: storedPoints);
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

                // plushie bonus
                if (!userHasRegistered)
                {
                    var plushieEmbed = GetEmbed()
                        .WithTitle("Here's Your Surprise! 🛎");
                    if (GameHelper.IsEventOccuring(config))
                        plushieEmbed.Description = DialogueDict.Get("REGISTRATION_PLUSHIE", config.GetDefaultPrefix());
                    else
                        plushieEmbed.Description = DialogueDict.Get("REGISTRATION_PLUSHIE_PREREG", config.GetDefaultPrefix());

                    var image = await new YellowTextGenerator(config).WriteYellowTextAsync(plushieEmbed.ThumbnailUrl, "Plushies !!");
                    plushieEmbed.WithAttachedThumbnailUrl(image);

                    var plushieComponents = new ComponentBuilder()
                        .WithButton("Get A Free Plushie!", $"PLUSHIEREG.{user.Id}", ButtonStyle.Success, new Emoji("🧸"));
                    var msg = await channel.SendFileAsync(image.Stream, image.FileName, null, embed: plushieEmbed.Build(), components: plushieComponents.Build());
                    await msg.PinAsync();
                }


            }
            catch (Exception e)
            {

            }
        }

        async Task EditRegistrationAsync(SqlConnection connection, UserShipCollection dbUserShips)
        {
            var descriptionPrefix = DialogueDict.Get("REGISTRATION_EDIT");
            EmbedBuilder embed = null;
            while(true)
            {
                var editImageFile = await GetShipsImageAsync(dbUser, dbUserShips);
                embed = GetEmbed()
                        .WithTitle("Edit Registration")
                        .WithDescription(descriptionPrefix
                        + "\n\n" + DialogueDict.Get("REGISTRATION_EDIT_INSTRUCTIONS"))
                        .WithAttachedImageUrl(editImageFile);

                var messageComponents = new ComponentBuilder()
                    //.WithButton("Edit Primary Ship", "SHIP 0",
                    //    style: ButtonStyle.Primary, emote: new Emoji("💗"))
                    //.WithButton("Edit Secondary Ship", "SHIP 1",
                    //    style: ButtonStyle.Primary, emote: new Emoji("💖"))
                    //.WithButton("Edit Tertiary Ship", "SHIP 2",
                    //    style: ButtonStyle.Primary, emote: new Emoji("♥"))
                    .WithSelectMenu("CHOICE", new List<SelectMenuOptionBuilder>()
                    {
                        new SelectMenuOptionBuilder("Configure Primary Pairing", "SHIP 0", dbUserShips.Get(UserShipTier.Primary)?.GetDisplayName() ?? "None Set", new Emoji("💗")),
                        new SelectMenuOptionBuilder("Configure Secondary Pairing", "SHIP 1", dbUserShips.Get(UserShipTier.Secondary)?.GetDisplayName() ?? "None Set", new Emoji("💖")),
                        new SelectMenuOptionBuilder("Configure Tertiary Pairing", "SHIP 2", dbUserShips.Get(UserShipTier.Tertiary)?.GetDisplayName() ?? "None Set", new Emoji("♥"))
                    },
                        placeholder: "Configure a Pairing!")
                    .WithButton("Edit Card Background", "BG",
                        style: ButtonStyle.Primary, emote: new Emoji("🖼"), row: 1)
                    .WithButton("All Good!", "YES",
                        style: ButtonStyle.Success, emote: new Emoji("👍"), row: 1);

                var response = await SendAndAwaitNonTextResponseAsync(file: editImageFile, embed: embed, components: messageComponents);
                if (response.IsYes)
                    break;
                else if (response.InteractionResponse.Data.Values?.FirstOrDefault()?.StartsWith("SHIP ") ?? false)
                {
                    var tierNo = int.Parse(response.InteractionResponse.Data.Values.First().Split()[1]);
                    descriptionPrefix = await SetUpShip(connection, (UserShipTier)tierNo);
                }
                else if (response.InteractionResponse.Data.CustomId.Equals("BG"))
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

            ComponentBuilder shipChooseComponents = new ComponentBuilder()
                .WithButton("Type Your Pairing as [Character One X Character Two]",
                emote: new Emoji("ℹ"), style: ButtonStyle.Secondary, customId: "TEXTINSTRUCTIONS", disabled: true);

            if (canSkip)
            {
                if (!isNewShip)
                    shipChooseComponents.WithButton($"Just Configure The Hearts", "SKIP",
                        style: ButtonStyle.Secondary, emote: SkipEmote);
                shipChooseComponents.WithButton(isNewShip ? $"Skip Adding a {tier} Pair For Now" : $"Leave This Pairing As Is", "SKIPSHIP",
                    style: ButtonStyle.Secondary, emote: new Emoji("⏩"));
            }

            // Now register the ship
            var shipValidated = false;
            Prompt response;
            embed.Title = title;
            var skipped = false;
            UserShip selectedUserShip = null;
            while (!shipValidated)
            {
                var imageFile = await GenerateShipImage(dbUser, dbUserShips, highlightTier: (int)tier);
                embed.WithAttachedImageUrl(imageFile);
                response = await SendAndAwaitResponseAsync(file: imageFile, embed: embed, components: shipChooseComponents);

                // Determine if skipping (whole ship or just to heart config)
                var interactionId = response.InteractionResponse?.Data.CustomId ?? "";
                if (interactionId.StartsWith("SKIP"))
                {
                    skipped = true;
                    if (interactionId.Equals("SKIPSHIP"))
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
            if (!response.InteractionResponse.Data.CustomId.Equals("SKIPSHIP"))
                await SetUpHeartAsync(connection, selectedUserShip, GetEmbed(), 2);

            return DialogueDict.Get("REGISTRATION_FINISH_SHIP");
        }

        async Task<Prompt> SetUpHeartAsync(SqlConnection connection, UserShip userShip, EmbedBuilder embed, int heart)
        {
            embed ??= GetEmbed();
            var heartIsValid = true;
            Prompt response = null;
            do
            {
                heartIsValid = true;
                var heartEmotes = client.GetGuild(932086074887508019).Emotes.Where(a => a.Name.StartsWith("shipheart"))
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
                embed.WithDescription((embed.Description ?? "")
                        + "\n\n" + DialogueDict.Get("REGISTRATION_HEART_CHOOSE",
                        heart == 1 ? userShip.Character1First : userShip.Character2First, SkipEmote, heartChoicesStr, DoubleEmote))
                    .WithAttachedImageUrl(registrationHeartImageFile)
                    .WithTitle($"{(UserShipTier)userShip.Tier} Pair Heart Setup");

                var components = CreateHeartChoiceComponents(heartEmotes, true, true, heart == 1, heart == 1 ? userShip.Character1First : userShip.Character2First);

                response = await SendAndAwaitNonTextResponseAsync(file: registrationHeartImageFile, embed: embed, components: components);
                if (!response.InteractionResponse.Data.CustomId.StartsWith("SKIP"))
                {
                    if (response.InteractionResponse.Data.CustomId.Equals("DUAL"))
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
                        userShip.Heart1 = response.InteractionResponse.Data.Values.FirstOrDefault();
                        userShip.Heart1Right = null;
                    }
                    else
                    {
                        userShip.Heart2 = response.InteractionResponse.Data.Values.FirstOrDefault();


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
            var components = CreateHeartChoiceComponents(heartEmotes, false, false, false, "");
            var heartChoicesStr = string.Join(" ", heartEmotes);
            var heartHalfBannerFile = await GenerateShipImage(dbUser, dbUserShips, highlightTier: userShip.Tier, highlightHeart: heart);
            var embed = GetEmbed()
                .WithDescription(DialogueDict.Get("REGISTRATION_HEART_CHOOSE_SIDE",
                heart == 1 ? userShip.Character1First : userShip.Character2First, isRightHalf ? "right" : "left", heartChoicesStr))
                .WithAttachedImageUrl(heartHalfBannerFile)
                .WithTitle($"{(UserShipTier)userShip.Tier} Pair Heart Setup");
            var response = await SendAndAwaitNonTextResponseAsync(file: heartHalfBannerFile, embed: embed, components: components);
            var choice = response.InteractionResponse.Data.Values.FirstOrDefault();
            if (heart == 1)
            {
                if (!isRightHalf)
                    userShip.Heart1 = choice;
                else
                    userShip.Heart1Right = choice;
            }
            else
            {
                if (!isRightHalf)
                    userShip.Heart2 = choice;
                else
                    userShip.Heart2Right = choice;
            }
        }

        ComponentBuilder CreateHeartChoiceComponents(IEnumerable<IEmote> heartEmotes, bool canSkip, bool withDualOption, bool isFirstChar, string charName)
        {
            heartEmotes = heartEmotes
                .OrderBy(a => a.Name)
                .ToList();
            var component = new ComponentBuilder();
            var options = new List<SelectMenuOptionBuilder>();
            foreach (var emote in heartEmotes)
            {
                var name = emote.Name.Length > 9
                    ? emote.Name.Substring(9).CapitalizeFirst()
                    : "Default";
                options.Add(new SelectMenuOptionBuilder(name, emote.Name, emote: emote));
            }
            component.WithSelectMenu("CHOICE", options, "Select a Heart");
            if (withDualOption)
                component.WithButton("Dual Heart", "DUAL", ButtonStyle.Primary, emote: DoubleEmote);
            if (canSkip)
            {
                component.WithButton($"Skip Editing {charName}'s Heart", "SKIP", ButtonStyle.Secondary,
                    emote: SkipEmote);
                if (isFirstChar)
                    component.WithButton($"Skip Editing Both Hearts for Pairing", "SKIPSHIP", ButtonStyle.Secondary,
                        emote: new Emoji("⏩"));
            }
            return component;
        }

        async Task SetUpBackgroundAsync(SqlConnection connection, string descriptionPrefix = "")
        {
            var embed = GetEmbed()
                .WithTitle("Choose a Background")
                .WithDescription(descriptionPrefix
                + "\n\n" + DialogueDict.Get("REGISTRATION_CUSTOMIZE_BG" + (dbUser.ShipsSelected ? "_EDIT" : ""), config.GetDefaultPrefix())
                + "\n\n" + DialogueDict.Get("REGISTRATION_CHOOSE_BG"));


            var emotes = Enumerable.Range(1, Directory.GetFiles("Assets/Backgrounds").Length)
                .Select(a => EmoteHelper.GetNumberEmote(a))
                .ToList();

            var bgImageFile = await GetShipsImageAsync(dbUser, dbUserShips);

            await channel.SendFileAsync(bgImageFile.Stream, bgImageFile.FileName);

            while (true)
            {
                var bgImagesFile = await shipImageGenerator.GenerateBackgroundChoicesAsync(dbUser);
                embed.WithAttachedImageUrl(bgImagesFile);

                var selectMenuList = new List<SelectMenuOptionBuilder>();
                for (int i = 0; i < emotes.Count; i++)
                {
                    var emote = emotes[i];
                    selectMenuList.Add(new SelectMenuOptionBuilder($"Preview Background {i + 1}", i.ToString(),
                        emote: emote));
                }
                var components = new ComponentBuilder()
                   .WithSelectMenu("CHOICE", selectMenuList, "Change Your Background")
                   .WithButton("Looks Good!", "YES",  ButtonStyle.Primary, ThumbsUpEmote);

                var bgResponse = await SendAndAwaitNonTextResponseAsync(file: bgImagesFile, embed: embed, components: components);

                if (bgResponse.IsYes)
                    break;
                dbUser = await repo.GetUserAsync(connection, dbUser.UserId);
                dbUser.CardBackground = int.Parse(bgResponse.InteractionResponse.Data.Values.FirstOrDefault()) + 1;
                await repo.UpdateUserAsync(connection, dbUser);
                bgImageFile = await GetShipsImageAsync(dbUser, dbUserShips);
                embed = GetEmbed()
                    .WithTitle("Background Config")
                    .WithDescription(DialogueDict.Get("REGISTRATION_BG_CHANGED"))
                    .WithAttachedImageUrl(bgImageFile);
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
            var response = await SendAndAwaitYesNoEmoteResponseAsync(embed: embed);
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
