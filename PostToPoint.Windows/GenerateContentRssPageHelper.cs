using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PostToPoint.Windows
{
    public class GenerateContentRssPageHelper
    {
        public static async Task GenerateContentRssPage(string appId,
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

            if (!Directory.Exists(redirectDirectory))
            {
                throw new DirectoryNotFoundException($"Redirect directory not found: {redirectDirectory}");
            }

            if (!Directory.Exists(blogPostDirectory))
            {
                throw new DirectoryNotFoundException($"Blog post directory not found: {blogPostDirectory}");
            }

            // Get all the published content files
            var contentFiles = Directory.GetFiles(redirectDirectory, "*.md", SearchOption.AllDirectories);

            // Remove the _template.md file or any other file that starts with _ considered as template or system file
            contentFiles = contentFiles.Where(x => 
            {
                var fileName = Path.GetFileName(x);
                return fileName != null && !fileName.StartsWith("_");
            }).ToArray();

            var redditPosts = await RedditHelper.GetMyImportantRedditPosts(appId, redirectUri, appSecret, username, password, downloadImages, "year", "new", 120);

            var redditPostsDic = redditPosts.ToDictionary(x => GenerateBlueSkyRssFeedHelper.CleanForMarkdownMeta(x.Title));

            int current = 0;
            foreach (var file in contentFiles)
            {
                Debug.WriteLine($"Processing {file} {++current}/{contentFiles.Length}");
                await UpgradeFileWithContent(file, redditPostsDic, blogPostDirectory, llmChoice);
            }
        }

        private static int notfound = 0;

        private static async Task UpgradeFileWithContent(string file, Dictionary<string, RedditPostData> redditPostsDic, string blogPostDirectory, string llmChoice)
        {
            ArgumentNullException.ThrowIfNull(file);
            ArgumentNullException.ThrowIfNull(redditPostsDic);
            ArgumentNullException.ThrowIfNull(blogPostDirectory);
            ArgumentNullException.ThrowIfNull(llmChoice);

            var md = MarkdownTemplate.ParseFromFile(file);

            var dirName = Path.GetDirectoryName(file);
            if (string.IsNullOrEmpty(dirName))
            {
                throw new InvalidOperationException($"Could not get directory name from file path: {file}");
            }

            var fileName = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrEmpty(fileName))
            {
                throw new InvalidOperationException($"Could not get file name from file path: {file}");
            }

            //TODO: don't know why, but sometime the permalink from reddit gets in the slug in the final .md file.
            // a fix is written in FIX_SLUG_FROM_PERMALINK
            // This is the fix... if the slug is too long, assume it's not the correct one and make the correction.
            if (md.Slug.Length > 3)
            {
                md.Slug = Path.GetFileName(dirName) + fileName.Substring(11);
                md.SaveToFile(file);
            }

            if (!string.IsNullOrWhiteSpace(md.Content))
            {
                Debug.WriteLine($"File already has content, skipping: {file}");
                return;
            }

            // Find the original reddit post that was used to create the post
            if (!redditPostsDic.ContainsKey(md.Title))
            {
                ++notfound;
                Debug.WriteLine($"Reddit post not found for {md.Title}");
                Debug.WriteLine($"not found: {notfound}");
                return;
            }

            var redditPostData = redditPostsDic[md.Title];

            // We will ground our request with context from the mission, the blog posts and the Reddit posts
            var previousMessages = new List<LlmUserAgentMessagePair>();

            // Start the Conversation with the LLM by specifying our mission
            AppendMissionMessages(previousMessages);

            // Append the blog posts to the conversation
            AppendBlogContextMessages(previousMessages, blogPostDirectory);

            // Append the Reddit posts to the conversation
            AppendRedditPostMessages(previousMessages, redditPostData);

            var redditToBlogPostPrompt = """
                Create my blog post content with 4 sections, written in markdown format.  You can use markdown to format the text.
                
                The sections are: "Récapitulatif factuel", "Point de vue neutre", "Point de vue optimiste", "Point de vue pessimiste".

                Each section should be written in a way that is engaging and brings the subject within everyone's reach.

                The Factual Recap "Récapitulatif factuel" should be a objective and factual recap of the post.  If there are technical term to understand, you should explain them in a way that is easy to understand.  This should be the more informative section of them all.

                The Neutral View "Point de vue neutre" should be a neutral interpretation of the post, like the zen middle view without the spiritism.  It should represent what is probable, not necessarily what is possible.  This should be the more engaging and thought provoking section of them all.

                The Optimistic "Point de vue optimiste" View should be a optimistic view of the post, more like what we might ear in the tech bro Silicon Valley community. It's the more positive and enthusiastic section of them all, betting on what's possible even if a little improbable.

                The Pessimistic "Point de vue pessimiste" View should be a pessimistic view of the post, akin to but not as harsh as the AI doomers can be.  It's the more negative and cautious section of them all, betting on what's probable even if a little improbable.

                Each section will be H1 headers in the markdown file, followed by the content of the section.  It's all written in french for Quebec audience, so be sure to use the right words and expressions.
                """;

            // We will prompt the LLM to convert the reddit posts to Blue Sky posts
            var retry = 10;
            string? description = null;
            while (retry-- > 0 && description == null)
            {
                try
                {
                    description = await AnthropicHelper.CallClaude(previousMessages, redditToBlogPostPrompt, llmChoice);
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Error calling Anthropic: " + e.Message);

                    // Just for this once ;)
                    await Task.Delay(30000);
                }
            }

            if (description == null)
            {
                throw new InvalidOperationException("Failed to get a response from LLM after 10 retries");
            }

            md.Content = $"""
                Article Reddit: {redditPostData.Title} [https://www.reddit.com{redditPostData.Post.Permalink}](https://www.reddit.com{redditPostData.Post.Permalink})

                {(redditPostData.UrlIsImage ? redditPostData.MarkdownImageLink : string.Empty)}

                {description}
                """;

            md.SaveToFile(file);
        }

        private static void AppendMissionMessages(List<LlmUserAgentMessagePair> previousMessages)
        {
            ArgumentNullException.ThrowIfNull(previousMessages);

            previousMessages.Add(new LlmUserAgentMessagePair()
            {
                UserMessage = """
                You will have to create a small blog post.  It will be hosted on my own website www.patb.ca
                On my website, I explore innovation and particularly artificial intelligence.  
                I am looking for a blog post that is engaging and that brings the subject within everyone's reach.
                I want to help readers that don't know to much about technology or AI to understand and make up their own mind.  That's the important and distinctive part of my blog.
                To do so, I want a objective factual recap of the post, a neutral interpretation (like the zen middle view without the spiritism), an optimistic view (more like what we might ear in the tech bro Silicon Valley community) and a pessimistic view (akin to but not as harsh as the AI doomers can be).
                Our goal is to break down complex ideas into digestible content, so please make it as accessible as possible using plain language. Feel free to make it engaging like an editorial – use examples, tell stories to illustrate points, and connect abstract concepts to real-world situations that people can relate to.
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
                Here are the reference blog posts that you should use to create our own blog post.
                Understand the point of view, the style, and the angle of the blog posts.
                Don't copy the content, but use it as a reference to create our own blog post.
                Don't use the Frontmatter content, only the blog content part, not the metadata.
                ---------------------------------------------------------------
                {sbBlogs}
                ---------------------------------------------------------------
                """,
                AgentMessage = """
                Excellent, I understant your style, your point of view and how you thrive to walk the middle ground while beeing engaging and to bring within everyone's reach.
                I also understand that in my answers I will add the factual recap, the neutral interpretation, the optimistic view and the pessimistic view.
                I also understand that my answer for the post will only contain the post, nothing more, no metadata.
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
                    Here is the Reddit post I would like to convert to a blog post following my guidelines:
                    ---------------------------------------------------------------
                    {redditPost.GetCompleteContent()}
                    ---------------------------------------------------------------
                    """,
                AgentMessage = "Excellent, I will create a blog post from this Reddit post using the style and angle provided in the reference blog posts.  I also understand that my answer for the post will only contain the post, nothing more."
            });
        }
    }
}
