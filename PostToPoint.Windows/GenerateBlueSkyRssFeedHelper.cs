using System;
using System.Collections.Generic;
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
            string rssFilename)
        {
            // Get upvoted and saved posts from Reddit using RedditHelper class in this project
            var redditPosts = await RedditHelper.GetMyImportantRedditPosts(appId, redirectUri, appSecret, username, password, downloadImages);

            // transform reddit posts to rss items using System.ServiceModel.Syndication
            var rssItems = new List<SyndicationItem>();
            foreach (var redditPost in redditPosts)
            {
                var item = new SyndicationItem(redditPost.Title, redditPost.SelfTextHTML, new Uri(redditPost.GetUri()));
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
