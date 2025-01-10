using Microsoft.AspNetCore.StaticFiles;
using Reddit.Things;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Security.Policy;
using System.ServiceModel.Syndication;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace PostToPoint.Windows
{
    public class GenerateBlueSkyRssFeedHelper
    {
        public static async Task GenerateBlueSkyRssFeed(
            string appId,
            string redirectUri,
            string appSecret,
            string username,
            string password,
            bool downloadImages,
            string rssTitle,
            string rssDescription,
            string rssUri,
            string rssFilename,
            string blogPostDirectory,
            string llmChoice,
            string redditToBlueskyFilename,
            string postContentDirectory,
            string redirectDirectory
            )
        {
            ArgumentNullException.ThrowIfNull(appId);
            ArgumentNullException.ThrowIfNull(redirectUri);
            ArgumentNullException.ThrowIfNull(appSecret);
            ArgumentNullException.ThrowIfNull(username);
            ArgumentNullException.ThrowIfNull(password);
            ArgumentNullException.ThrowIfNull(rssTitle);
            ArgumentNullException.ThrowIfNull(rssDescription);
            ArgumentNullException.ThrowIfNull(rssUri);
            ArgumentNullException.ThrowIfNull(rssFilename);
            ArgumentNullException.ThrowIfNull(blogPostDirectory);
            ArgumentNullException.ThrowIfNull(llmChoice);
            ArgumentNullException.ThrowIfNull(redditToBlueskyFilename);
            ArgumentNullException.ThrowIfNull(postContentDirectory);
            ArgumentNullException.ThrowIfNull(redirectDirectory);

            if (!Directory.Exists(blogPostDirectory))
            {
                throw new DirectoryNotFoundException($"Blog post directory not found: {blogPostDirectory}");
            }

            if (!Directory.Exists(postContentDirectory))
            {
                throw new DirectoryNotFoundException($"Post content directory not found: {postContentDirectory}");
            }

            if (!Directory.Exists(redirectDirectory))
            {
                throw new DirectoryNotFoundException($"Redirect directory not found: {redirectDirectory}");
            }

            if (!File.Exists(redditToBlueskyFilename))
            {
                throw new FileNotFoundException($"Reddit to Bluesky prompt file not found", redditToBlueskyFilename);
            }

            // Get upvoted and saved posts from Reddit using RedditHelper class in this project
            var redditPosts = await RedditHelper.GetMyImportantRedditPosts(appId, redirectUri, appSecret, username, password, downloadImages);

            // Order them from older to newer
            redditPosts = redditPosts.OrderBy(x => x.Created).ToList();

            // We will ground our request with context from the mission, the blog posts and the Reddit posts
            var previousMessages = new List<LlmUserAgentMessagePair>();

            // Start the Conversation with the LLM by specifying our mission
            AppendMissionMessages(previousMessages);

            // Append the blog posts to the conversation
            AppendBlogContextMessages(previousMessages, blogPostDirectory);

            // transform reddit posts to rss items using System.ServiceModel.Syndication
            var redditToBlueskyPrompt = File.ReadAllText(redditToBlueskyFilename);

            var rssItems = new List<SyndicationItem>();
            Debug.WriteLine("");
            Debug.WriteLine("Reddit posts count: " + redditPosts.Count);
            int redditPostIndex = 0;
            foreach (var redditPost in redditPosts)
            {
                Debug.WriteLine("Reddit post index: " + ++redditPostIndex + " of " + redditPosts.Count);

                if (SqliteHelper.DoesPostExistInBluesky(redditPost))
                {
                    Debug.WriteLine("Already posted about this Reddit post, skipping");
                    continue;
                }

                // Append the Reddit posts to the conversation
                AppendRedditPostMessages(previousMessages, redditPost);

                // We will prompt the LLM to convert the reddit posts to Blue Sky posts
                var retry = 10;
                string? description = null;
                while (retry-- > 0 && description == null)
                {
                    try
                    {
                        description = await AnthropicHelper.CallClaude(previousMessages, redditToBlueskyPrompt, llmChoice);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("Error calling Anthropic: " + e.Message);
                        await Task.Delay(30000);
                    }
                }

                if (description == null)
                {
                    throw new InvalidOperationException("Failed to get a response from LLM after 10 retries");
                }

                description = CleanupText(description);

                previousMessages.RemoveAt(previousMessages.Count - 1);

                string shortUri = ShortenUri(redditPost.GetUri(), redirectDirectory, redditPost.Title, description, redditPost.Title + " " + description + " " + redditPost.GetUri());

                var uriLength = shortUri.Length + 1;

                if (description.Length > 300 - uriLength - 1)
                {
                    description = description.Substring(0, 300 - uriLength - 1);
                }

                var item = new SyndicationItem(redditPost.Title, description, new Uri(shortUri), "reddit-" + redditPost.Post.Id, new DateTimeOffset(redditPost.Created.ToUniversalTime(), TimeSpan.Zero));
                item.PublishDate = redditPost.Created;

                var itemUri = redditPost.GetUri();

                string imageUri = string.Empty;

                if (itemUri.Contains("/i.redd.it/"))
                {
                    imageUri = itemUri;
                    item.Links.Add(new SyndicationLink(new Uri(itemUri), "related", "image", GetMimeType(itemUri), await GetLength(itemUri)));
                }

                string videoUri = string.Empty;

                if (itemUri.Contains("/v.redd.it/"))
                {
                    // Download video using yt-dlp (similar to once known youtube-dl)
                    var videoStream = await RedditHelper.GetRedditVideoStream(redditPost);
                    var videoFilename = Path.Combine(postContentDirectory, Path.GetFileName(itemUri) + "-" + Path.GetFileName(videoStream));
                    YtdlpHelper.DownloadVideo(redditPost.GetUri(), videoFilename);

                    // Trim to max 59 secondes
                    string shortVideoFilename = Path.Combine(Path.GetDirectoryName(videoFilename) ?? postContentDirectory, Path.GetFileNameWithoutExtension(videoFilename) + "-59s" + Path.GetExtension(videoFilename));
                    FfmpegHelper.ShortenVideo(videoFilename, shortVideoFilename);

                    var shareLink = GoogleDriveUploader.UploadAndShareFile(shortVideoFilename,
                        "1VIuGmlZ_yd_e0FofoBLvuyf_vtT6PBdl",
                        @".\configs\pointtopost-566f8e782ab7.json.secret");

                    videoUri = shareLink;

                    // Add link to Bluesky post in the RSS feed
                    item.Links.Add(new SyndicationLink(new Uri(shareLink), "related", "video", GetMimeType(shortVideoFilename), new FileInfo(shortVideoFilename).Length));

                    File.Delete(videoFilename);
                    File.Delete(shortVideoFilename);
                }

                rssItems.Add(item);

                using var webhook = new ZapierWebhook(App.Options.ZapierBlueSkyWebHookUri);
                bool success = await webhook.SendToWebhook(
                            description,
                            shortUri,
                            imageUri,
                            videoUri
                        );

                SqliteHelper.AppendRedditPostToBluesky(redditPost, description, shortUri, imageUri, videoUri);

                if (success)
                {
                    Console.WriteLine("Successfully sent to Zapier webhook");
                }
                else
                {
                    Console.WriteLine("Failed to send to webhook");
                }
            }

            // Create a new SyndicationFeed
            var feed = new SyndicationFeed(rssTitle, rssDescription, new Uri(rssUri), "https://www.patb.ca/rss/bluesky-auto-post.rss", DateTimeOffset.Now, rssItems);
            XNamespace atom = "http://www.w3.org/2005/Atom";
            feed.ElementExtensions.Add(
                new XElement(atom + "link",
                    new XAttribute("href", "https://www.patb.ca/rss/bluesky-auto-post.rss"),
                    new XAttribute("rel", "self"),
                    new XAttribute("type", "application/rss+xml"))
            );

            // Save that in the rssDirectory with filename "bluesky-auto-post.rss"
            using (var writer = XmlWriter.Create(rssFilename))
            {
                var formatter = new Rss20FeedFormatter(feed, true);
                formatter.WriteTo(writer);
            }
        }

        private static string ShortenUri(string uri, string redirectDirectory, string title, string subheadline, string teaser)
        {
            ArgumentNullException.ThrowIfNull(uri);
            ArgumentNullException.ThrowIfNull(redirectDirectory);
            ArgumentNullException.ThrowIfNull(title);
            ArgumentNullException.ThrowIfNull(subheadline);
            ArgumentNullException.ThrowIfNull(teaser);

            var uriHash = CalculateUriHash(uri);

            var directory = Path.Combine(redirectDirectory, uriHash);
            Directory.CreateDirectory(directory);

            var uriFilenameInUri = ToBase36(Directory.GetFiles(directory).Length);

            var slug = $"{uriHash}{uriFilenameInUri}";
            var shortUri = $"https://patb.ca/r/{slug}";

            var templateFilename = Path.Combine(redirectDirectory, "_template.md");
            if (!File.Exists(templateFilename))
            {
                throw new FileNotFoundException("Template file not found", templateFilename);
            }

            var template = File.ReadAllText(templateFilename);
            template = template.Replace("{title}", CleanForMarkdownMeta(title));
            template = template.Replace("{subheadline}", CleanForMarkdownMeta(subheadline));
            template = template.Replace("{teaser}", CleanForMarkdownMeta(teaser));
            template = template.Replace("{slug}", $"{slug}");
            template = template.Replace("{redirect}", uri);

            var filename = Path.Combine(directory, $"{DateTime.Now:yyyy-MM-dd}-{uriFilenameInUri}.md");
            File.WriteAllText(filename, template);

            return shortUri;
        }

        public static string CleanForMarkdownMeta(string? unsafeString)
        {
            if (string.IsNullOrEmpty(unsafeString))
            {
                return string.Empty;
            }

            string unsafeChars = "\"\'\r\n<>";
            var safeString = unsafeString;
            foreach (char c in unsafeChars)
            {
                safeString = safeString.Replace(c, ' ');
            }

            return safeString;
        }

        private static string CalculateUriHash(string uri)
        {
            ArgumentNullException.ThrowIfNull(uri);

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(uri);
                byte[] hash = sha256.ComputeHash(bytes);

                int hash36 = Math.Abs(BitConverter.ToInt32(hash, 0) % (36 * 36 + 1));

                return ToBase36(hash36);
            }
        }

        private static string ToBase36(long number)
        {
            string digits = "0123456789abcdefghijklmnopqrstuvwxyz";

            if (number == 0) return "0";

            bool isNegative = number < 0;
            number = Math.Abs(number);

            var result = new StringBuilder();

            while (number > 0)
            {
                var remainder = (int)(number % 36);
                result.Insert(0, digits[remainder]);
                number /= 36;
            }

            if (isNegative)
                result.Insert(0, '-');

            return result.ToString();
        }

        private static void AppendRedditPostMessages(List<LlmUserAgentMessagePair> previousMessages, RedditPostData redditPost)
        {
            ArgumentNullException.ThrowIfNull(previousMessages);
            ArgumentNullException.ThrowIfNull(redditPost);

            previousMessages.Add(new LlmUserAgentMessagePair()
            {
                UserMessage = $"""
                    Here is the Reddit post I would like to convert to a Bluesky post:
                    ---------------------------------------------------------------
                    {redditPost.GetCompleteContent()}
                    ---------------------------------------------------------------
                    """,
                AgentMessage = "Excellent, I will create a Bluesky post from this Reddit post using the style and angle provided in the reference blog posts.  I also understand that my answer for the post will only contain the post, nothing more."
            });
        }

        private static void AppendMissionMessages(List<LlmUserAgentMessagePair> previousMessages)
        {
            ArgumentNullException.ThrowIfNull(previousMessages);

            previousMessages.Add(new LlmUserAgentMessagePair()
            {
                UserMessage = """
                You will have to create a Bluesky post.  Bluesky is a microblogging platform like Twitter.
                I will provide example blog posts I written to get a sense of the style and angle provided in the reference blog posts for your writting.
                I will then give you the Reddit Post to work from.
                You will receive my instruction after that, where you will answer only with your writing, nothing more as you know is important.
                """,
                AgentMessage = "Excellent, I will create a Bluesky post from a Reddit post using the style and angle provided in the reference blog posts.  I also understand that my answer for the post will only contain the post, nothing more."
            });
        }

        private static void AppendBlogContextMessages(List<LlmUserAgentMessagePair> previousMessages, string blogPostDirectory)
        {
            ArgumentNullException.ThrowIfNull(previousMessages);
            ArgumentNullException.ThrowIfNull(blogPostDirectory);

            if (!Directory.Exists(blogPostDirectory))
            {
                throw new DirectoryNotFoundException($"Blog post directory not found: {blogPostDirectory}");
            }

            var blogPosts = Directory.GetFiles(blogPostDirectory, "*.md").OrderBy(x => x).Take(1);

            var sbBlogs = new StringBuilder();
            foreach (var blogPost in blogPosts)
            {
                sbBlogs.AppendLine("---------------------------------------------------------------");
                sbBlogs.AppendLine("Blog post " + blogPost);
                sbBlogs.AppendLine("---------------------------------------------------------------");
                sbBlogs.AppendLine(File.ReadAllText(blogPost));
                sbBlogs.AppendLine("---------------------------------------------------------------");
                sbBlogs.AppendLine("End of blog post" + blogPost);
                sbBlogs.AppendLine("---------------------------------------------------------------");
                sbBlogs.AppendLine();
            }

            previousMessages.Add(new LlmUserAgentMessagePair()
            {
                UserMessage = $"""
                Here are the reference blog posts that you should use to create the Bluesky post.
                Understand the point of view, the style, and the angle of the blog posts.
                ---------------------------------------------------------------
                {sbBlogs}
                ---------------------------------------------------------------
                """,
                AgentMessage = "Excellent, I understant your style, your point of view and how you thrive to walk the middle ground while beeing engaging and to bring within everyone's reach.  I also understand that my answer for the post will only contain the post, nothing more."
            });
        }

        private static string GetMimeType(string itemUri)
        {
            ArgumentNullException.ThrowIfNull(itemUri);

            var provider = new FileExtensionContentTypeProvider();
            if (provider.TryGetContentType(itemUri, out string? contentType))
            {
                return contentType;
            }

            return "application/octet-stream";
        }

        private static async Task<long> GetLength(string uri)
        {
            ArgumentNullException.ThrowIfNull(uri);

            using var client = new HttpClient();
            var imageData = await client.GetByteArrayAsync(uri);
            return imageData.Length;
        }

        public static string CleanupText(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            // Remove spaces before punctuation
            text = Regex.Replace(text, @"\s+([,.!?:;])", "$1");

            // Replace multiple spaces with single space
            text = Regex.Replace(text, @"\s+", " ");

            // Replace CRLF with space
            text = text.Replace("\r\n", " ").Replace("\n", " ");

            return text.Trim();
        }
    }
}
