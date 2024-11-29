using Reddit.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PostToPoint.Windows
{
    class RedditPostData
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string SelfText { get; set; }
        public string SelfTextHTML { get; set; }
        public string Subreddit { get; set; }
        public DateTime Created { get; set; }
        public string ScreenshotPath { get; set; }

        public Post Post { get; set; }

        public string GetMetadata()
        {
            return $"Title: {Title}\nSubreddit: {Subreddit}\nDate: {Created}\nURL: {Url}";
        }
    }
}
