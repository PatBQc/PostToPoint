using Microsoft.AspNetCore.StaticFiles;
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
            // Get upvoted and saved posts from Reddit using RedditHelper class in this project
            var redditPosts = await RedditHelper.GetMyImportantRedditPosts(appId, redirectUri, appSecret, username, password, downloadImages);

            // We will ground our request with context from the mission, the blog posts and the Reddit posts
            List<LlmUserAgentMessagePair> previousMessages = new List<LlmUserAgentMessagePair>();

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


                // Append the Reddit posts to the conversation
                AppendRedditPostMessages(previousMessages, redditPost);


                // We will prompt the LLM to convert the reddit posts to Blue Sky posts
                var description = await AnthropicHelper.CallClaude(previousMessages, redditToBlueskyPrompt, llmChoice);
                description = CleanupText(description);

                previousMessages.RemoveAt(previousMessages.Count - 1);

                // var description = "This is a test description for the RSS feed item";

                string shortUri = ShortenUri(redditPost.GetUri(), redirectDirectory);

                var uriLength = shortUri.Length + 1;

                if (description.Length > 300 - uriLength - 1)
                {
                    description = description.Substring(0, 300 - uriLength - 1);
                }

                var item = new SyndicationItem(redditPost.Title, description, new Uri(shortUri), redditPost.Post.Id, new DateTimeOffset(redditPost.Created.ToUniversalTime(), TimeSpan.Zero));
                item.PublishDate = redditPost.Created;

                var itemUri = redditPost.GetUri();

                if (itemUri.Contains("/i.redd.it/"))
                {
                    item.Links.Add(new SyndicationLink(new Uri(itemUri), "related", "image", GetMimeType(itemUri), await GetLength(itemUri)));
                }

                if (itemUri.Contains("   /v.redd.it/"))
                {
                    // Querry Reddit Json to find the video stream
                    var videoStream = await RedditHelper.GetRedditVideoStream(redditPost);

                    // Download video and save it in the post content directory
                    using var httpClient = new HttpClient();
                    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PostToPoint/1.0");

                    var videoFilename = Path.Combine(postContentDirectory, Path.GetFileName(itemUri));

                    Directory.CreateDirectory(postContentDirectory);

                    var video = await httpClient.GetByteArrayAsync(videoStream);
                    File.WriteAllBytes(videoFilename, video);

                    // TODO you are here ;)

                    // Download the audio part of the video

                    // Combine video + audio with FFMPEG

                    // Trim to max 59 secondes
                    // TODO FFMPEG

                    // Upload to Bluesky

                    // Add link to Bluesky post in the RSS feed
                }

                // Just for this once ;)
                await Task.Delay(20000);


                rssItems.Add(item);
            }

            // Create a new SyndicationFeed
            // TODO change the id URI to something from the configs
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

        private static string ShortenUri(string uri, string redirectDirectory)
        {
            var uriHash = CalculateUriHash(uri);

            var directory = Path.Combine(redirectDirectory, uriHash);
            Directory.CreateDirectory(directory);

            var uriFilenameInUri = ToBase36(Directory.GetFiles(directory).Length);

            var slug = $"{uriHash}{uriFilenameInUri}";
            var shortUri = $"https://patb.ca/r/{slug}";

            var templateFilename = Path.Combine(redirectDirectory, "_template.md");

            // The template file should match this format
            // ---
            // layout: redirect
            // title               : "{title}"
            // subheadline: "{subheadline}"
            // teaser: "{teaser}"
            // lang: fr
            // header:
            //     image_fullwidth: "header_projets.webp"
            // permalink: "/goto/{slug}"
            // ref                 : "/goto/{slug}"
            // sitemap: false
            // redirect_to: { redirect}
            // ---

            var template = File.ReadAllText(templateFilename);
            template = template.Replace("{title}", "Redirecting...");
            template = template.Replace("{subheadline}", "Redirecting...");
            template = template.Replace("{teaser}", "Redirecting...");
            template = template.Replace("{slug}", $"{slug}");
            template = template.Replace("{redirect}", uri);

            var filename = Path.Combine(directory, $"{DateTime.Now.ToString("yyyy-MM-dd")}-{uriFilenameInUri}.md");
            File.WriteAllText(filename, template);

            return shortUri;
        }

        private static string CalculateUriHash(string uri)
        {
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
            previousMessages.Add(new LlmUserAgentMessagePair()
            {
                UserMessage = """
                You will have to create a Bluesky post.  Bluesky is a microblogging platform like Twitter.
                I will provide example blog posts I written to get a sense of the style and angle provided in the reference blog posts for your writting.
                I will then give you give you the Reddit Post to work from.
                You will receive my instruction after that, where you will answer only with your writing, nothing more as you know is important.
                """,
                AgentMessage = "Excellent, I will create a Bluesky post from a Reddit post using the style and angle provided in the reference blog posts.  I also understand that my answer for the post will only contain the post, nothing more."
            });
        }

        private static void AppendBlogContextMessages(List<LlmUserAgentMessagePair> previousMessages, string blogPostDirectory)
        {
            StringBuilder sbBlogs = new StringBuilder();
            foreach (var blogPost in System.IO.Directory.GetFiles(blogPostDirectory, "*.md").OrderBy(x => x))
            {
                sbBlogs.AppendLine("---------------------------------------------------------------");
                sbBlogs.AppendLine("Blog post " + blogPost);
                sbBlogs.AppendLine("---------------------------------------------------------------");
                sbBlogs.AppendLine(System.IO.File.ReadAllText(blogPost));
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
                {sbBlogs.ToString()}
                ---------------------------------------------------------------
                """,
                AgentMessage = "Excellent, I understant your style, your point of view and how you thrive to walk the middle ground while beeing engaging and to bring within everyone’s reach.  I also understand that my answer for the post will only contain the post, nothing more."
            });
        }

        private static string GetMimeType(string itemUri)
        {
            var provider = new FileExtensionContentTypeProvider();

            string contentType; ;
            provider.TryGetContentType(itemUri, out contentType);

            return contentType ?? "application/octet-stream";
        }

        private static async Task<long> GetLength(string uri)
        {
            using (HttpClient client = new HttpClient())
            {
                // Download image data
                byte[] imageData = await client.GetByteArrayAsync(uri);

                return imageData.Length;
            }
        }

        public static string CleanupText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

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
