using Microsoft.AspNetCore.StaticFiles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.ServiceModel.Syndication;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PostToPoint.Windows
{
    class GenerateEverythingHelper
    {
        private static bool _isTestMode = true;

        private const string RedditToBlogPostPrompt = """
                Create my blog post content with 4 sections, written in markdown format.  You can use markdown to format the text.
                
                The sections are: "Récapitulatif factuel", "Point de vue neutre", "Point de vue optimiste", "Point de vue pessimiste".

                Each section should be written in a way that is engaging and brings the subject within everyone's reach.

                The Factual Recap "Récapitulatif factuel" should be a objective and factual recap of the post. 
                If there are technical term to understand, you should explain them in a way that is easy to understand.  
                This should be the more informative section of them all.

                The Neutral View "Point de vue neutre" should be a neutral interpretation of the post, like the zen middle view without the spiritism.  
                It should represent what is probable, not necessarily what is possible.  
                This should be the more engaging and thought provoking section of them all.

                The Optimistic "Point de vue optimiste" View should be a optimistic view of the post, more like what we might ear in the tech bro Silicon Valley community. 
                It's the more positive and enthusiastic section of them all, betting on what's possible even if a little improbable.

                The Pessimistic "Point de vue pessimiste" View should be a pessimistic view of the post, akin to but not as harsh as the AI doomers can be.  
                It's the more negative and cautious section of them all, betting on what's probable even if a little improbable.

                Each section will be H1 headers in the markdown file, followed by the content of the section.  
                It's all written in french for Quebec audience, so be sure to use the right words and expressions.
                """;

        private const string RedditToTwitterPrompt = """
                Provide only the direct answer to the following question, without any explanation or additional context:

                You will be transforming a Reddit post into a concise and engaging Twitter post in French (Canadian french or Québec french). 
                
                Follow these steps carefully:

                1. Here is the Reddit post content in the section "# REDDIT POST"

                2. Summarize the key points of the Reddit post, focusing on the most interesting or important information.

                3. Create an engaging c post in French based on your summary. The post should be attention-grabbing and informative while maintaining the essence of the original content.

                4. Include 3 to 5 relevant hashtags in French at the end of your post. These hashtags should be related to the main topics or themes of the content.

                5. Ensure that your entire Twitter post, including the hashtags, is less than 250 characters in total.

                6. IMPORTANT: Write your Twitter post without any additional comments or explanations. The output should contain only the content of the Twitter post itself.

                IMPORTANT: Provide your Twitter post as the final output, adhering to all the guidelines mentioned above. 
                IMPORTANT: ANSWER WITH ONLY THE CONTENT OF THE TWITTER POST.  NOTHING ELSE, NOTHING MORE.  NO ADDITIONNAL COMMENTS.  NOTHING MORE THEN THE POST CONTENT.
                IMPORTANT: YOU OUTPUT ONLY THE POST.
                IMPORTANT: YOU DO NOT SAY THING LIKE "here is your post"
                """;

        public static async Task GenerateEverything(string appId,
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

            Debug.WriteLine("");
            Debug.WriteLine("Reddit posts count: " + redditPosts.Count);
            int redditPostIndex = 0;

            var baseMessages = previousMessages.ToList();

            foreach (var redditPost in redditPosts)
            {
                previousMessages = baseMessages.ToList();

                Debug.WriteLine("Reddit post index: " + ++redditPostIndex + " of " + redditPosts.Count);

                if (SqliteHelper.DoesPostExistInBluesky(redditPost))
                {
                    Debug.WriteLine("Already posted about this Reddit post, skipping");
                    continue;
                }

                // Append the Reddit posts to the conversation
                AppendRedditPostMessages(previousMessages, redditPost);

                // We will prompt the LLM to convert the reddit posts to our content
                // Here we will start to ask for blog analysis, then twitter, then bluesky, ...
                string answerBlogPost = await QueryLlm(llmChoice, previousMessages, RedditToBlogPostPrompt);
                previousMessages.Add(new LlmUserAgentMessagePair() { AgentMessage = RedditToBlogPostPrompt, UserMessage = answerBlogPost });

                // Now Twitter
                string answerTwitter = await QueryLlm(llmChoice, previousMessages, RedditToTwitterPrompt);
                previousMessages.Add(new LlmUserAgentMessagePair() { AgentMessage = RedditToTwitterPrompt, UserMessage = answerTwitter });

                // Now Bluesky
                string answerBluesky = await QueryLlm(llmChoice, previousMessages, redditToBlueskyPrompt);
                previousMessages.Add(new LlmUserAgentMessagePair() { AgentMessage = redditToBlueskyPrompt, UserMessage = answerBluesky });

                answerBluesky = MicroblogingCleanupText(answerBluesky);

                previousMessages.RemoveAt(previousMessages.Count - 1);

                string shortUri = ShortenUri(redditPost.GetUri(), redirectDirectory, redditPost.Title, answerBluesky, redditPost.Title + " " + answerBluesky + " " + redditPost.GetUri());

                var uriLength = shortUri.Length + 1;

                if (answerBluesky.Length > 300 - uriLength - 1)
                {
                    answerBluesky = answerBluesky.Substring(0, 300 - uriLength - 1);
                }

                var itemUri = redditPost.GetUri();

                string imageUri = string.Empty;

                if (itemUri.Contains("/i.redd.it/"))
                {
                    imageUri = itemUri;
                }

                string videoUri = string.Empty;

                if (itemUri.Contains("/v.redd.it/"))
                {
                    // Download video using yt-dlp (similar to once known youtube-dl)
                    var videoStream = await RedditHelper.GetRedditVideoStream(redditPost);
                    var videoFilename = Path.Combine(postContentDirectory, Path.GetFileName(itemUri) + "-" + Path.GetFileName(videoStream));
                    YtdlpHelper.DownloadVideo(redditPost.GetUri(), videoFilename);

                    // Trim to max 59 secondes
                    var videoDir = Path.GetDirectoryName(videoFilename);
                    if (string.IsNullOrEmpty(videoDir))
                    {
                        videoDir = postContentDirectory;
                    }

                    string shortVideoFilename = Path.Combine(videoDir, Path.GetFileNameWithoutExtension(videoFilename) + "-59s" + Path.GetExtension(videoFilename));
                    FfmpegHelper.ShortenVideo(videoFilename, shortVideoFilename);

                    var shareLink = GoogleDriveUploader.UploadAndShareFile(shortVideoFilename,
                        "1VIuGmlZ_yd_e0FofoBLvuyf_vtT6PBdl",
                        @".\configs\pointtopost-566f8e782ab7.json.secret");

                    videoUri = shareLink;

                    File.Delete(videoFilename);
                    File.Delete(shortVideoFilename);
                }

                var webhookUrl = App.Options.ZapierBlueSkyWebHookUri ?? throw new InvalidOperationException("Zapier webhook URL not configured");
                using var webhook = new ZapierWebhook(webhookUrl);
                bool success = await webhook.SendToWebhook(
                            answerBluesky,
                            shortUri,
                            imageUri,
                            videoUri
                        );

                SqliteHelper.AppendRedditPostToBluesky(redditPost, answerBluesky, shortUri, imageUri, videoUri);

                if (success)
                {
                    Console.WriteLine("Successfully sent to Zapier webhook");
                }
                else
                {
                    Console.WriteLine("Failed to send to webhook");
                }
            }

            // Get all the published content files
            var contentFiles = Directory.GetFiles(redirectDirectory, "*.md", SearchOption.AllDirectories);

            // Remove the _template.md file or any other file that starts with _ considered as template or system file
            contentFiles = contentFiles.Where(x => 
            {
                var fileName = Path.GetFileName(x);
                return fileName != null && !fileName.StartsWith("_");
            }).ToArray();

            var redditPostsDic = redditPosts.ToDictionary(x => GenerateBlueSkyRssFeedHelper.CleanForMarkdownMeta(x.Title));

            int current = 0;
            foreach (var file in contentFiles)
            {
                Debug.WriteLine($"Processing {file} {++current}/{contentFiles.Length}");
            }
        }

        private static void AppendMissionMessages(List<LlmUserAgentMessagePair> previousMessages)
        {
            ArgumentNullException.ThrowIfNull(previousMessages);

            previousMessages.Add(new LlmUserAgentMessagePair()
            {
                UserMessage = """
                You will have to analyse content from a Reddit post and create corresponding contents for my blog, Twitter, Bluesky, LinkedIn, ...  
                I will provide example blog posts I written to get a sense of my style and angle provided in the reference blog posts for your writting.
                I will then give you the Reddit Post, content (self text, linked article, ...) and commentaries to work from.
                You will receive my instruction after that, where you will answer only with your writing, nothing more as you know is important.
                """,
                AgentMessage = """
                Excellent, I will create content from a Reddit post (and it's content, comments, ...) using the style and angle provided in the reference blog posts.  
                I also understand that my answer for the post will only contain the created content, nothing more.
                """
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

            var sbBlogs = new StringBuilder();
            var blogFiles = Directory.GetFiles(blogPostDirectory, "*.md").OrderBy(x => x).ToArray();

            if (_isTestMode)
            {
                blogFiles = blogFiles.Take(1).ToArray();
            }

            foreach (var blogPost in blogFiles)
            {
                sbBlogs.AppendLine("---------------------------------------------------------------");
                sbBlogs.AppendLine("Blog post " + blogPost);
                sbBlogs.AppendLine("---------------------------------------------------------------");
                sbBlogs.AppendLine(File.ReadAllText(blogPost));
                sbBlogs.AppendLine("---------------------------------------------------------------");
                sbBlogs.AppendLine("End of blog post" + blogPost);
                sbBlogs.AppendLine("---------------------------------------------------------------");
                sbBlogs.AppendLine();
                sbBlogs.AppendLine();
            }

            previousMessages.Add(new LlmUserAgentMessagePair()
            {
                UserMessage = $"""
                Here are the reference blog posts that you should use to analyse and create the content.
                Understand the point of view, the style, and the angle of the blog posts.
                ---------------------------------------------------------------
                {sbBlogs}
                ---------------------------------------------------------------
                """,
                AgentMessage = """
                Excellent, I understant your style and your point of view.
                You thrive to walk the middle ground version while beeing engaging and to bring within everyone's reach.  
                I also understand that my answer for the post will only contain the content, nothing more.
                """
            });
        }

        private static void AppendRedditPostMessages(List<LlmUserAgentMessagePair> previousMessages, RedditPostData redditPost)
        {
            ArgumentNullException.ThrowIfNull(previousMessages);
            ArgumentNullException.ThrowIfNull(redditPost);

            previousMessages.Add(new LlmUserAgentMessagePair()
            {
                UserMessage = $"""
                    Here is the Reddit post I would like to analyse and convert to my content:
                    ---------------------------------------------------------------
                    {redditPost.GetCompleteContent()}
                    ---------------------------------------------------------------
                    """,
                AgentMessage = """
                    Excellent, I will analyse and create content from this Reddit post using the style and angle provided in the reference blog posts.  
                    I also understand that my answer for the post will only contain the post, nothing more.
                    """
            });
        }

        private static async Task<string> QueryLlm(string llmChoice, List<LlmUserAgentMessagePair> previousMessages, string prompt)
        {
            ArgumentNullException.ThrowIfNull(llmChoice);
            ArgumentNullException.ThrowIfNull(previousMessages);
            ArgumentNullException.ThrowIfNull(prompt);

            var retry = 10;
            string? description = null;

            while (retry-- > 0 && description == null)
            {
                try
                {
                    description = await AnthropicHelper.CallClaude(previousMessages, prompt, llmChoice);
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

            return description;
        }

        public static string MicroblogingCleanupText(string? text)
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
    }
}
