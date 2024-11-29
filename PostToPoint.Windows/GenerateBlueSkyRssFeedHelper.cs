using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

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
            string redditToBlueskyFilename
            )
        {
            // Get upvoted and saved posts from Reddit using RedditHelper class in this project
            var redditPosts = await RedditHelper.GetMyImportantRedditPosts(appId, redirectUri, appSecret, username, password, downloadImages);

            var redditToBlueskyPrompt = File.ReadAllText(redditToBlueskyFilename);

            StringBuilder sbBlogs = new StringBuilder();
            sbBlogs.AppendLine("IMPORTANT: you must perform the following, given a Reddit Post (section # REDDIT POST) while using the style and angle provided in the reference blog posts (section # REFERENCE BLOG POSTS):");
            sbBlogs.AppendLine();
            sbBlogs.AppendLine("---------------------------------------------------------------");
            sbBlogs.AppendLine("# YOUR IMPORTANT PROMPT TO PERFORM: ");
            sbBlogs.AppendLine(redditToBlueskyPrompt);
            sbBlogs.AppendLine("---------------------------------------------------------------");
            sbBlogs.AppendLine();

            sbBlogs.AppendLine("---------------------------------------------------------------");
            sbBlogs.AppendLine("# REDDIT POST");
            sbBlogs.AppendLine("---------------------------------------------------------------");
            sbBlogs.AppendLine("{reddit_post_to_convert}");
            sbBlogs.AppendLine();

            sbBlogs.AppendLine("---------------------------------------------------------------");
            sbBlogs.AppendLine("# REFERENCE BLOG POSTS");
            // Let's ground our context with existing blog posts
            foreach (var blogPost in System.IO.Directory.GetFiles(blogPostDirectory, "*.md"))
            {
                sbBlogs.AppendLine("---------------------------------------------------------------");
                sbBlogs.AppendLine("Blog post " + blogPost);
                sbBlogs.AppendLine("---------------------------------------------------------------");
                sbBlogs.AppendLine(System.IO.File.ReadAllText(blogPost));
                sbBlogs.AppendLine("---------------------------------------------------------------");
                sbBlogs.AppendLine("End of blog post" + blogPost);
                sbBlogs.AppendLine("---------------------------------------------------------------");
            }

            // transform reddit posts to rss items using System.ServiceModel.Syndication
            var rssItems = new List<SyndicationItem>();
            foreach (var redditPost in redditPosts)
            {
                var llmQuery = sbBlogs.ToString().Replace("{reddit_post_to_convert}", redditPost.GetCompleteContent());

                // We will prompt the LLM to convert the reddit posts to Blue Sky posts
                var description = await AnthropicHelper.CallClaude(llmQuery, llmChoice);

                var uriLength = redditPost.GetUri().Length + 1;

                if (description.Length > 300 - uriLength)
                {
                    description = description.Substring(0, 297 - uriLength) + "..." + redditPost.GetUri();
                }
                else
                {
                    description = description + " " + redditPost.GetUri();
                }

                var item = new SyndicationItem(redditPost.Title, description, new Uri(redditPost.GetUri()));
                item.PublishDate = redditPost.Created;
                rssItems.Add(item);
            }

            // Create a new SyndicationFeed
            var feed = new SyndicationFeed(rssTitle, rssDescription, new Uri(rssUri), Guid.NewGuid().ToString(), DateTimeOffset.Now, rssItems);

            // Save that in the rssDirectory with filename "bluesky-auto-post.rss"
            using (var writer = XmlWriter.Create(rssFilename))
            {
                var formatter = new Rss20FeedFormatter(feed);
                formatter.WriteTo(writer);
            }

        }
    }
}
