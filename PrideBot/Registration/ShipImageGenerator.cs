using ImageMagick;
using Microsoft.Extensions.Configuration;
using PrideBot.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrideBot.Registration
{
    public class ShipImageGenerator
    {
        readonly IConfigurationRoot config;

        public ShipImageGenerator(IConfigurationRoot config)
        {
            this.config = config;
        }

        public string GetShipAvatarPath(User user, IConfigurationRoot config)
            => config.GetRelativeHostPathLocal("ships/" + user.UserId + ".png");

        public async Task<MemoryFile> WriteUserCardAsync(User dbUser, UserShipCollection dbShips, int highlightTier = -1, int highlightHeart = 0, string[] scoreTexts = null,
            bool blackOutHeartRight = false)
        {
            return
                await (await GenerateUserCardAsync(dbUser, dbShips, highlightTier, highlightHeart, scoreTexts, blackOutHeartRight))
                .WriteToMemoryFileAsync("ships");
        }

        public async Task<MemoryFile> WriteShipImageAsync(Ship ship)
        {
            return
                await (await GenerateShipImageAsync(ship))
                .WriteToMemoryFileAsync("ships");
        }

        public async Task<MagickImage> GenerateUserCardAsync(User dbUser, UserShipCollection userShips, int highlightTier = -1, int highlightHeart = 0, string[] scoreTexts = null, bool blackOutHeartRight = false)
        {
            scoreTexts ??= new string[3];
            var image = new MagickImage(await File.ReadAllBytesAsync($"Assets/Backgrounds/shipbg{dbUser.CardBackground}.png"));
            //var image = new MagickImage(MagickColors.Transparent, 288, 192);
            using var primaryShipImage = await GenerateUserShipImageAsync(userShips.PrimaryShip, highlightTier == 0, highlightHeart, blackOutHeartRight);
            var yLevel = !string.IsNullOrWhiteSpace(scoreTexts[0]) ? 96 : 106;
            primaryShipImage.InterpolativeResize(128, 128, PixelInterpolateMethod.Nearest);
            //if (ships[1].IsEmpty() && ships[2].IsEmpty())
            //{
            //    return primaryShipImage;
            //    //image.Composite(primaryShipImage, Gravity.Northwest, 32, 32, CompositeOperator.Over);
            //}
            //else
            {
                if (userShips.HasSecondaryShip || highlightTier == 1)
                {
                    //var yLevel = scores[1] > 0 ? 90 : 90;
                    using var ship2Image = await GenerateUserShipImageAsync(userShips.SecondaryShip, highlightTier == 1, highlightHeart, blackOutHeartRight);
                    image.Composite(ship2Image, Gravity.Northwest, 8, yLevel, CompositeOperator.Over);
                    if (!string.IsNullOrWhiteSpace(scoreTexts[1]))
                    {
                        using var number2Image = await GenerateScoreTextAsync(scoreTexts[1]);
                        number2Image.InterpolativeResize(128, 128, PixelInterpolateMethod.Nearest);
                        image.Composite(number2Image, Gravity.Northwest, 8 - 32, yLevel + 37 - 32, CompositeOperator.Over);
                    }
                }
                if (userShips.HasTertiaryShip || highlightTier == 2)
                {
                    using var ship3Image = await GenerateUserShipImageAsync(userShips.TertiaryShip, highlightTier == 2, highlightHeart, blackOutHeartRight);
                    image.Composite(ship3Image, Gravity.Northwest, 216, yLevel, CompositeOperator.Over);
                    if (!string.IsNullOrWhiteSpace(scoreTexts[2]))
                    {
                        using var number3Image = await GenerateScoreTextAsync(scoreTexts[2]);
                        number3Image.InterpolativeResize(128, 128, PixelInterpolateMethod.Nearest);
                        image.Composite(number3Image, Gravity.Northwest, 216 - 32, yLevel + 37 - 32, CompositeOperator.Over);
                    }
                }
                image.Composite(primaryShipImage, Gravity.Northwest, 80, yLevel - 90, CompositeOperator.Over);
                if (!string.IsNullOrWhiteSpace(scoreTexts[0]))
                {
                    using var numberImage = await GenerateScoreTextAsync(scoreTexts[0]);
                    numberImage.InterpolativeResize(256, 256, PixelInterpolateMethod.Nearest);
                    image.Composite(numberImage, Gravity.Northwest, 80 - 64, yLevel - 16 - 64, CompositeOperator.Over);
                }
            }

            //image.Resize(new Percentage(150));
            return image;
        }

        public async Task<MagickImage> GenerateUserShipImageAsync(UserShip ship, bool highlight, int highlightHeart, bool blackOutHeartRight)
        {
            ship ??= new UserShip();
            var image = new MagickImage(MagickColors.Transparent, 64, 64);
            if (highlight && highlightHeart <= 0)
                image.Composite(new MagickImage(await File.ReadAllBytesAsync($"Assets/CharacterSprites/BOX.png")), Gravity.Northwest, 0, 0, CompositeOperator.Over);

            ship.Heart1 = string.IsNullOrWhiteSpace(ship.Heart1) ? "shipheart" : ship.Heart1;
            ship.Heart2 = string.IsNullOrWhiteSpace(ship.Heart2) ? "shipheart" : ship.Heart2;
            using var heartImage1 = await GenerateHeartImageAsync(ship, 1, blackOutHeartRight && highlightHeart == 1);
            using var heartImage2 = await GenerateHeartImageAsync(ship, 2, blackOutHeartRight && highlightHeart == 2);

            using var shipImage = await GenerateShipImageAsync(ship as Ship);
            image.Composite(shipImage, Gravity.Northwest, 0, 30, CompositeOperator.Over);

            if (highlight && highlightHeart == 1)
            {
                using var smallHighlight = new MagickImage(await File.ReadAllBytesAsync($"Assets/CharacterSprites/BOXSMALL.png"));
                image.Composite(smallHighlight, Gravity.Northwest, 1, 4, CompositeOperator.Over);
            }
            image.Composite(heartImage1, Gravity.Northwest, 5, 5, CompositeOperator.Over);
            if (highlight && highlightHeart == 2)
            {
                using var smallHighlight = new MagickImage(await File.ReadAllBytesAsync($"Assets/CharacterSprites/BOXSMALL.png"));
                image.Composite(smallHighlight, Gravity.Northwest, 31, 4, CompositeOperator.Over);
            }
            image.Composite(heartImage2, Gravity.Northwest, 35, 5, CompositeOperator.Over);


            return image;
        }

        public async Task<MagickImage> GenerateHeartImageAsync(UserShip userShip, int heartIndex, bool blackOutRight)
        {
            var heart = heartIndex == 1 ? userShip.Heart1 : userShip.Heart2;
            heart = string.IsNullOrWhiteSpace(heart) ? "shipheart" :  heart;
            var baseHeartImage = new MagickImage(await File.ReadAllBytesAsync($"Assets/Hearts/{heart}.png"));

            var heartRight = heartIndex == 1 ? userShip.Heart1Right : userShip.Heart2Right;
            if (blackOutRight)
                heartRight = "shipheartblack";
            if (!string.IsNullOrWhiteSpace(heartRight) )
            {
                using var rightHeartImage = new MagickImage(await File.ReadAllBytesAsync($"Assets/Hearts/{heartRight}.png"));
                var cropGeo = new MagickGeometry(rightHeartImage.Width / 2, 0, rightHeartImage.Width / 2, 0);
                rightHeartImage.Crop(cropGeo);
                rightHeartImage.RePage();
                baseHeartImage.Composite(rightHeartImage, Gravity.Northwest, baseHeartImage.Width / 2, 0, CompositeOperator.Over);
            }

            return baseHeartImage;
        }

        public async Task<MagickImage> GenerateShipImageAsync(Ship ship)
        {
            var image = new MagickImage(MagickColors.Transparent, 64, 32);
            var file1 = $"Assets/CharacterSprites/{ship.CharacterId1}.png";
            var file2 = $"Assets/CharacterSprites/{ship.CharacterId2}.png";
            if (!File.Exists(file1))
                file1 = $"Assets/CharacterSprites/DEFAULT.png";
            if (!File.Exists(file2))
                file2 = $"Assets/CharacterSprites/DEFAULT.png";

            using var char1Image = new MagickImage(await File.ReadAllBytesAsync(file1));
            using var char2Image = new MagickImage(await File.ReadAllBytesAsync(file2));

            char2Image.Flop();

            image.Composite(char1Image, Gravity.Northwest, 1, 0, CompositeOperator.Over);
            image.Composite(char2Image, Gravity.Northwest, 31, 0, CompositeOperator.Over);
            return image;
        }

        public async Task<MemoryFile> GenerateBackgroundChoicesAsync(User dbUser)
        {
            var backgroundFiles = Directory.GetFiles("Assets/Backgrounds")
                .OrderBy(a => int.Parse(Path.GetFileNameWithoutExtension(a).Substring(6)))
                .ToArray();
            //var rows = backgroundFiles.Length > 2 ? 2 : 1;
            var columns = 1;
            if (backgroundFiles.Length == 2)
                columns = 2;
            else if (backgroundFiles.Length > 2)
            {
                while (true)
                {
                    if (backgroundFiles.Length <= 2)
                        break;
                    if (backgroundFiles.Length > (columns * (columns - 1)))
                        columns++;
                    else
                        break;
                }
            }
            var rows = (int)Math.Ceiling((double)(backgroundFiles.Length) / columns);
            var backgroundImages = backgroundFiles
                .Select(a => new MagickImage(a))
                .ToList();
            var bgWidth = backgroundImages.First().Width;
            var bgHeight = backgroundImages.First().Height;
            using var image = new MagickImage(MagickColors.Transparent, bgWidth * columns, bgHeight * rows);

            var i = 0;
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    if (i >= backgroundImages.Count)
                        break;
                    using var numberImage = await GenerateScoreTextAsync((i + 1).ToString());
                    numberImage.InterpolativeResize(numberImage.Width * 3, numberImage.Height * 3, PixelInterpolateMethod.Nearest);
                    backgroundImages[i].Composite(numberImage, Gravity.Northwest, -67, -67, CompositeOperator.Over);
                    image.Composite(backgroundImages[i], Gravity.Northwest, column * bgWidth, row * bgHeight, CompositeOperator.Over);
                    i++;
                }
            }

            var file = await image.WriteToMemoryFileAsync("backgrounds");
            backgroundImages.ForEach(a => a.Dispose());
            return file;
        }


        async Task<MagickImage> GenerateScoreTextAsync(string numStr)
        {
            numStr = numStr.Replace("?", "Q");
            var baseGeometry = new MagickGeometry(64, 32);
            var offsetGeometry = new MagickGeometry((baseGeometry.Width - (8 * numStr.Length)) / 2, (baseGeometry.Width - 12) / 2);
            //if (numStr.Length < 4)
            offsetGeometry.Width -= numStr.StartsWith("+") ? 2 : 0;

            var digitChars = numStr
                .ToCharArray()
                .Distinct();

            var digitImages = new Dictionary<char, MagickImage>();
            foreach (var ch in digitChars)
            {
                digitImages[ch] = new MagickImage(await File.ReadAllBytesAsync($"Assets/NumberSprites/{ch}.png"));
            }

            var image = new MagickImage(MagickColors.Transparent, baseGeometry.Width, baseGeometry.Width);
            for (int i = 0; i < numStr.Length; i++)
            {
                image.Composite(digitImages[numStr[i]], Gravity.Northwest, offsetGeometry.Width + (i * 8), offsetGeometry.Height, CompositeOperator.Over);
            }

            foreach (var digitImage in digitImages)
            {
                digitImage.Value.Dispose();
            }

            return image;
        }
    }
}
    