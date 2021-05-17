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

        public async Task<string> WriteUserAvatarAsync(User dbUser, UserShipCollection dbShips, int highlightTier = -1, int highlightHeart = 0, int[] scores = null)
        {
            var path = $"ships/usericon-{dbUser.UserId}-{Math.Abs(DateTimeOffset.Now.GetHashCode())}.png";
            var fullPath = config.GetRelativeHostPathLocal(path);
            await (await GenerateUserAvatarAsync(dbUser, dbShips, highlightTier, highlightHeart, scores)).WriteToFileAsync(fullPath);
            return path;
        }

        public async Task<MagickImage> GenerateUserAvatarAsync(User dbUser, UserShipCollection userShips, int highlightTier = -1, int highlightHeart = 0, int[] scores = null)
        {
            scores ??= new int[3];
            var image = new MagickImage("Assets/CharacterSprites/shipbg1.png");
            //var image = new MagickImage(MagickColors.Transparent, 192, 128);
            MagickImage ship2Image = null;
            MagickImage ship3Image = null;
            using var primaryShipImage = await GenerateShipImageAsync(userShips.PrimaryShip, highlightTier == 0, highlightHeart);
            primaryShipImage.InterpolativeResize(128, 128, PixelInterpolateMethod.Nearest);
            //if (ships[1].IsEmpty() && ships[2].IsEmpty())
            //{
            //    return primaryShipImage;
            //    //image.Composite(primaryShipImage, Gravity.Northwest, 32, 32, CompositeOperator.Over);
            //}
            //else
            {
                image.Composite(primaryShipImage, Gravity.Northwest, 80, 0, CompositeOperator.Over);
                if (scores[0] > 0)
                {
                    using var numberImage = GenerateNumberText(scores[0]);
                    numberImage.InterpolativeResize(128, 128, PixelInterpolateMethod.Nearest);
                    image.Composite(numberImage, Gravity.Northwest, 80, 84, CompositeOperator.Over);
                }
                if (userShips.HasSecondaryShip || highlightTier == 1)
                {
                    var yLevel = scores[1] > 0 ? 90 : 112;
                    ship2Image = await GenerateShipImageAsync(userShips.SecondaryShip, highlightTier == 1, highlightHeart);
                    image.Composite(ship2Image, Gravity.Northwest, 8, yLevel, CompositeOperator.Over);
                    if (scores[1] > 0)
                    {
                        using var number2Image = GenerateNumberText(scores[1]);
                        number2Image.InterpolativeResize(64, 64, PixelInterpolateMethod.Nearest);
                        image.Composite(number2Image, Gravity.Northwest, 8, yLevel + 44, CompositeOperator.Over);
                    }
                }
                if (userShips.HasTertiaryShip || highlightTier == 2)
                {
                    var yLevel = scores[2] > 0 ? 90 : 112;
                    ship3Image = await GenerateShipImageAsync(userShips.TertiaryShip, highlightTier == 2, highlightHeart);
                    image.Composite(ship3Image, Gravity.Northwest, 216, yLevel, CompositeOperator.Over);
                    if (scores[2] > 0)
                    {
                        using var number3Image = GenerateNumberText(scores[2]);
                        number3Image.InterpolativeResize(64, 64, PixelInterpolateMethod.Nearest);
                        image.Composite(number3Image, Gravity.Northwest, 216, yLevel + 44, CompositeOperator.Over);
                    }
                }
            }
            
            ship2Image?.Dispose();
            ship3Image?.Dispose();

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
            using var heartImage1 = new MagickImage(await File.ReadAllBytesAsync($"Assets/CharacterSprites/{ship.Heart1}.png"));
            using var heartImage2 = new MagickImage(await File.ReadAllBytesAsync($"Assets/CharacterSprites/{ship.Heart2}.png"));
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
            image.Composite(heartImage1, Gravity.Northwest, 1, 5, CompositeOperator.Over);
            if (highlight && highlightHeart == 2)
            {
                using var smallHighlight = new MagickImage(await File.ReadAllBytesAsync($"Assets/CharacterSprites/BOXSMALL.png"));
                image.Composite(smallHighlight, Gravity.Northwest, 31, 4, CompositeOperator.Over);
            }
            image.Composite(heartImage2, Gravity.Northwest, 31, 5, CompositeOperator.Over);

            return image;
        }


        MagickImage GenerateNumberText(int num)
        {
            var numStr = "+" + num.ToString();
            var baseGeometry = new MagickGeometry(32, 32);
            var offsetGeometry = new MagickGeometry((baseGeometry.Width - (8 * numStr.Length)) / 2, (baseGeometry.Width - 12) / 2);
            if (numStr.Length < 4)
                offsetGeometry.Width -= 2;

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
