using System;
using System.Collections.Generic;
using System.Linq;

using PrideBot.Models;

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
using ImageMagick;

namespace PrideBot.Plushies
{
    public class PlushieImageService
    {
        private readonly IConfigurationRoot config;

        public PlushieImageService(IConfigurationRoot config)
        {
            this.config = config;
        }

        public async Task<MemoryFile> WritePlushieImageAsync(UserPlushie userPlushie)
        {
            using var image = await GetPlushieImage(userPlushie.CharacterId, userPlushie.Rotation, userPlushie.Flip);
            return await image.WriteToMemoryFileAsync("plushie");
        }

        public async Task<MemoryFile> WritePlushieImageAsync(UserPlushieChoice userPlushieChoice)
        {
            using var image = await GetPlushieImage(userPlushieChoice.CharacterId, userPlushieChoice.Rotation, userPlushieChoice.Flip);
            return await image.WriteToMemoryFileAsync("plushie");
        }

        public Task<MemoryFile> WritePlushieCollectionImageAsync(IEnumerable<UserPlushie> userPlushies)
            => WritePlushieCollectionImageInternalAsync(userPlushies.Select(a => (a.CharacterId, a.Rotation, a.Flip)));

        public Task<MemoryFile> WritePlushieCollectionImageAsync(IEnumerable<UserPlushieChoice> userPlushieChoices)
            => WritePlushieCollectionImageInternalAsync(userPlushieChoices.Select(a => (a.CharacterId, a.Rotation, a.Flip)));

        // input collection is (character id, rotation)
        async Task<MemoryFile> WritePlushieCollectionImageInternalAsync(IEnumerable<(string, decimal, bool)> plushieData)
        {
            using var image = await GetPlushieCollectionImage(plushieData);
            return await image.WriteToMemoryFileAsync("plushies");
        }

        async Task<MagickImage> GetPlushieCollectionImage(IEnumerable<(string, decimal, bool)> plushieData)
        {
            // change rotations
            var flipRotation = plushieData.FirstOrDefault().Item2 > 0;
            var dataArray = plushieData.ToArray();

            var imageTasks = new List<Task<MagickImage>>();
            foreach (var data in plushieData)
            {
                var rotation = (decimal)Math.Abs(data.Item2) * (flipRotation ? -1m : 1m);
                imageTasks.Add(GetPlushieImage(data.Item1, rotation, data.Item3));
                flipRotation = !flipRotation;
            }
            await Task.WhenAll(imageTasks);
            var images = imageTasks
                .Select(a => a.Result)
                .ToArray();

            // Create a blank image and composite every plushie image over it
            var squeezeFactor = 96;
            var resultImage = new MagickImage(MagickColors.Transparent, images.Sum(a => a.Width) - squeezeFactor * (images.Count() - 1), images.Max(a => a.Height));
            for (int i = 0; i < images.Length; i++)
            {
                var image = images[i];
                resultImage.Composite(image, Gravity.Northwest, images.Take(i).Sum(a => a.Width) - (squeezeFactor * i), resultImage.Height - image.Height, CompositeOperator.Over);
            }

            foreach (var image in images)
            {
                image.Dispose();
            }
            return resultImage;
        }

        async Task<MagickImage> GetPlushieImage(string characterId, decimal rotation, bool flip)
        {
            var file = $"Assets/CharacterSprites/{characterId}.png";
            if (!File.Exists(file))
                file = $"Assets/CharacterSprites/DEFAULT.png";
            var charImage = new MagickImage(await File.ReadAllBytesAsync(file));
            charImage.BackgroundColor = MagickColors.Transparent;
            charImage.Modulate(new Percentage(115), new Percentage(90), new Percentage(100));
            if (flip)
                charImage.Flop();
            charImage.Rotate((double)rotation);
            charImage.Extent(48, 35, Gravity.Center, MagickColors.Transparent);
            charImage.InterpolativeResize(charImage.Width * 4, charImage.Height * 4, PixelInterpolateMethod.Nearest);
            return charImage;
        }
    }
}
