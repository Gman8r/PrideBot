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

        public async Task<string> WriteUserCardAsync(User dbUser, UserShipCollection dbShips, int highlightTier = -1, int highlightHeart = 0, int[] scores = null)
        {
            var path =
                await (await GenerateUserCardAsync(dbUser, dbShips, highlightTier, highlightHeart, scores))
                .WriteToWebFileAsync(config, "ships");
            return path;
        }

        public async Task<MagickImage> GenerateUserCardAsync(User dbUser, UserShipCollection userShips, int highlightTier = -1, int highlightHeart = 0, int[] scores = null)
        {
            scores ??= new int[3];
            var image = new MagickImage($"Assets/Backgrounds/shipbg{dbUser.CardBackground}.png");
            //var image = new MagickImage(MagickColors.Transparent, 288, 192);
            using var primaryShipImage = await GenerateShipImageAsync(userShips.PrimaryShip, highlightTier == 0, highlightHeart);
            var yLevel = scores[0] > 0 ? 96 : 106;
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
                    using var ship2Image = await GenerateShipImageAsync(userShips.SecondaryShip, highlightTier == 1, highlightHeart);
                    image.Composite(ship2Image, Gravity.Northwest, 8, yLevel, CompositeOperator.Over);
                    if (scores[1] > 0)
                    {
                        using var number2Image = GenerateScoreText(scores[1]);
                        number2Image.InterpolativeResize(128, 128, PixelInterpolateMethod.Nearest);
                        image.Composite(number2Image, Gravity.Northwest, 8 - 32, yLevel + 37 - 32, CompositeOperator.Over);
                    }
                }
                if (userShips.HasTertiaryShip || highlightTier == 2)
                {
                    using var ship3Image = await GenerateShipImageAsync(userShips.TertiaryShip, highlightTier == 2, highlightHeart);
                    image.Composite(ship3Image, Gravity.Northwest, 216, yLevel, CompositeOperator.Over);
                    if (scores[2] > 0)
                    {
                        using var number3Image = GenerateScoreText(scores[2]);
                        number3Image.InterpolativeResize(128, 128, PixelInterpolateMethod.Nearest);
                        image.Composite(number3Image, Gravity.Northwest, 216 - 32, yLevel + 37 - 32, CompositeOperator.Over);
                    }
                }
                image.Composite(primaryShipImage, Gravity.Northwest, 80, yLevel - 90, CompositeOperator.Over);
                if (scores[0] > 0)
                {
                    using var numberImage = GenerateScoreText(scores[0]);
                    numberImage.InterpolativeResize(256, 256, PixelInterpolateMethod.Nearest);
                    image.Composite(numberImage, Gravity.Northwest, 80 - 64, yLevel - 16 - 64, CompositeOperator.Over);
                }
            }

            //image.Resize(new Percentage(150));
            return image;
        }

        public async Task<MagickImage> GenerateShipImageAsync(UserShip ship, bool highlight, int highlightHeart)
        {
            ship ??= new UserShip();
            var image = new MagickImage(MagickColors.Transparent, 64, 64);
            if (highlight && highlightHeart <= 0)
                image.Composite(new MagickImage($"Assets/CharacterSprites/BOX.png"), Gravity.Northwest, 0, 0, CompositeOperator.Over);
            var file1 = $"Assets/CharacterSprites/{ship.CharacterId1}.png";
            var file2 = $"Assets/CharacterSprites/{ship.CharacterId2}.png";
            if (!File.Exists(file1))
                file1 = $"Assets/CharacterSprites/DEFAULT.png";
            if (!File.Exists(file2))
                file2 = $"Assets/CharacterSprites/DEFAULT.png";

            ship.Heart1 = string.IsNullOrWhiteSpace(ship.Heart1) ? "shipheart" : ship.Heart1;
            ship.Heart2 = string.IsNullOrWhiteSpace(ship.Heart2) ? "shipheart" : ship.Heart2;
            using var heartImage1 = new MagickImage(await File.ReadAllBytesAsync($"Assets/Hearts/{ship.Heart1}.png"));
            using var heartImage2 = new MagickImage(await File.ReadAllBytesAsync($"Assets/Hearts/{ship.Heart2}.png"));
            using var char1Image = new MagickImage(await File.ReadAllBytesAsync(file1));
            using var char2Image = new MagickImage(await File.ReadAllBytesAsync(file2));

            //char1Image.InterpolativeResize(32, 32, PixelInterpolateMethod.Nearest);
            //char2Image.InterpolativeResize(32, 32, PixelInterpolateMethod.Nearest);
            char2Image.Flop();
            //heartImage2.InterpolativeResize(32, 32, PixelInterpolateMethod.Nearest);

            image.Composite(char1Image, Gravity.Northwest, 1, 30, CompositeOperator.Over);
            image.Composite(char2Image, Gravity.Northwest, 31, 30, CompositeOperator.Over);
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
        public async Task<string> GenerateBackgroundChoicesAsync(User dbUser)
        {
            var backgroundFiles = Directory.GetFiles("Assets/Backgrounds");
            var rows = backgroundFiles.Length > 2 ? 2 : 1;
            var columns = (int)Math.Ceiling((double)(backgroundFiles.Length) / rows);
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
                    using var numberImage = new MagickImage($"Assets/NumberSprites/{i + 1}.png");
                    numberImage.InterpolativeResize(24, 36, PixelInterpolateMethod.Nearest);
                    backgroundImages[i].Composite(numberImage, Gravity.Northwest, 8, 8, CompositeOperator.Over);
                    image.Composite(backgroundImages[i], Gravity.Northwest, column * bgWidth, row * bgHeight, CompositeOperator.Over);
                    i++;
                }
            }

            var path = await image.WriteToWebFileAsync(config, "backgrounds");
            backgroundImages.ForEach(a => a.Dispose());
            return path;
        }


        MagickImage GenerateScoreText(int num)
        {
            var numStr = "+" + num.ToString();
            var baseGeometry = new MagickGeometry(64, 32);
            var offsetGeometry = new MagickGeometry((baseGeometry.Width - (8 * numStr.Length)) / 2, (baseGeometry.Width - 12) / 2);
            //if (numStr.Length < 4)
            offsetGeometry.Width -= 3;

            var digitImages = numStr
                .ToCharArray()
                .Distinct()
                .ToDictionary(k => k, v => new MagickImage($"Assets/NumberSprites/{v}.png"));

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
    