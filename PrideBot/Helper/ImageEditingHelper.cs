using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImageMagick;
using System.IO;
using Microsoft.Extensions.Configuration;
using System.Drawing;
using System.Drawing.Imaging;

namespace PrideBot
{
    static class ImageEditingHelper
    {
        // Some fun stuff about how discord displays gifs
        public const int MinAnimationDelay = 2;
        public const int DefaultAnimationDelay = 10;

        public static async Task<MemoryStream> SaveFirstFrameOfGifAsync(MemoryStream inputStream, float mult)
        {
            using var collection = new MagickImageCollection(inputStream);
            var stream = new MemoryStream();
            await collection[0].WriteAsync(stream, MagickFormat.Png);
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        public static async Task<MemoryStream> ResizeAnimatedImageAsync(MemoryStream inputStream, float mult)
        {
            using var collection = new MagickImageCollection(inputStream);
            foreach (var frame in collection)
            {
                frame.Resize((int)((float)frame.Width * mult), (int)((float)frame.Height * mult));
                frame.RePage();
            }
            var stream = new MemoryStream();
            await collection.WriteAsync(stream, MagickFormat.Gif);
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        public static async Task<byte[]> RasterizeSvgImageAsync(byte[] svgData)
        {
            using (var s = new MemoryStream(svgData))
            {
                using var image = new MagickImage(MagickColors.Transparent, 500, 500);
                image.Settings.BackgroundColor = MagickColors.Transparent;
                var a = image.Density;
                await image.ReadAsync(s);
                image.Interpolate = PixelInterpolateMethod.Nearest;
                image.BackgroundColor = MagickColors.Transparent;

                using var stream = new MemoryStream();
                await image.WriteAsync(stream, MagickFormat.Gif);
                return stream.ToArray();
            }
        }

        public static async Task WriteToFileAsync(this IMagickImage<byte> image, string path)
        {
            var fs = File.Create(path);
            await image.WriteAsync(fs, MagickFormat.Png);
            fs.Close();
        }

        public static async Task<MemoryFile> WriteToMemoryFileAsync(this IMagickImage<byte> image, string name)
        {
            var stream = new MemoryStream();
            await image.WriteAsync(stream, MagickFormat.Png);
            stream.Seek(0, SeekOrigin.Begin);
            if (stream.Length > WebHelper.MaxFileMB * 1000000)
                throw new CommandException("The output for your image was too large for me to upload.");
            return new MemoryFile(stream, $"{name}.png");
        }

        public static async Task<MemoryFile> WriteToMemoryFileAsync(this IMagickImageCollection<byte> collection, string name)
        {
            if (collection.Count == 1)
                return await WriteToMemoryFileAsync(collection.FirstOrDefault(), name);
            var stream = new MemoryStream();
            await collection.WriteAsync(stream, MagickFormat.Gif);
            stream.Seek(0, SeekOrigin.Begin);
            if (stream.Length > WebHelper.MaxFileMB * 1000000)
                throw new CommandException("The output for your image was too large for me to upload.");
            return new MemoryFile(stream, $"{name}.gif");
        }

        public static bool IsTransparent(this IMagickImageCollection<byte> collection)
            => collection
                .SkipLast(1)
                .All(a => a.GifDisposeMethod == GifDisposeMethod.Background);
    }
}
