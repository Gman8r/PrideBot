using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Globalization;
using Discord;
using System.Text.RegularExpressions;

namespace PrideBot
{
    public static class WebHelper
    {
        public const int MaxFileMB = 8;

        public static async Task<byte[]> DownloadWebFileDataAsync(string url, bool throwException = true)
        {
            try
            {
                using (var client = new WebClient())
                {
                    return await client.DownloadDataTaskAsync(url);
                }
            }
            catch
            {
                if (throwException)
                    throw new CommandException($"I couldn't download that file (<{url}>).");
                else
                    return null;
            }
        }
        
        public static async Task<long> GetWebFileSizeAsync(string url)
        {
            try
            {
                using (var client = new WebClient())
                {
                    await client.OpenReadTaskAsync(url);
                    return Convert.ToInt64(client.ResponseHeaders["Content-Length"]);
                }
            }
            catch
            {
                return 0L;
            }
        }

        // TODO can we actually make this async, or?
        public static async Task<string> GetFileExtension(string url)
        {
            try
            {
                Uri uri = new Uri(url);
                var fileInfo = new FileInfo(uri.AbsolutePath);
                return fileInfo.Extension;
            }
            catch (Exception e)
            {
                if (e.GetType() == typeof(CommandException))
                    throw e;
                return null;
            }
        }

        public static async Task<bool> IsImageUrlAsync(string url)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);

                req.Method = "HEAD";
                using (var resp = await req.GetResponseAsync())
                {
                    return resp.ContentType.ToLower(CultureInfo.InvariantCulture)
                        .StartsWith("image/");
                }
            }
            catch (Exception e)
            {
                if (e.GetType() == typeof(CommandException))
                    throw e;
                return false;
            }
        }

        public static async Task<string> GetExternalGifvUrlAsync(IEmbed embed)
        {
            if (embed.Type != EmbedType.Gifv)
                return null;

            if (embed.Url.Contains("giphy.com", StringComparison.OrdinalIgnoreCase))
            {
                var text = await DownloadWebFileDataAsync(embed.Url);
                var content = Encoding.Default.GetString(text);
                var regex = new Regex("content=\"(https?:[^\"]*giphy.com[^\"]*\\.gif)\"");
                var match = regex.Match(content);
                if (match.Success)
                    return match.Groups?.Cast<Group>()?.ElementAtOrDefault(1)?.Value;
            }
            else if (embed.Url.Contains("tenor.com", StringComparison.OrdinalIgnoreCase))
            {
                var text = await DownloadWebFileDataAsync(embed.Url);
                var content = Encoding.Default.GetString(text);
                var regex = new Regex("content=\"(https?:[^\"]*tenor.com[^\"]*\\.gif[^\"]*)\"");
                var match = regex.Match(content);
                if (match.Success)
                    return match.Groups?.Cast<Group>()?.ElementAtOrDefault(1)?.Value;
            }
            else if (embed.Url.Contains("gfycat.com", StringComparison.OrdinalIgnoreCase))
            {
                var text = await DownloadWebFileDataAsync(embed.Url);
                var content = Encoding.Default.GetString(text);
                var regex = new Regex("content=\"(https?:[^\"]*gfycat.com[^\"]*size_restricted\\.gif[^\"]*)\"");
                var match = regex.Match(content);
                if (match.Success)
                    return match.Groups?.Cast<Group>()?.ElementAtOrDefault(1)?.Value;
            }

            return null;
        }
    }
}
