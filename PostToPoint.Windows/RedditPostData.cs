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

        public string GetCompleteContent()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(GetMetadata());

            if(!string.IsNullOrEmpty(SelfText))
            {
                sb.AppendLine(SelfText);
            }

            sb.AppendLine(GetFormattedComments(Post, 2, 20));

            return sb.ToString();
        }

        public string GetUri()
        {
            if (Uri.IsWellFormedUriString(Url, UriKind.Absolute))
            {
                return Url;
            }

            return $"https://www.reddit.com{Post.Permalink}";
        }

        public static string GetFormattedComments(Post post, int maxDepth = -1, int maxComments = -1)
        {
            var sb = new StringBuilder();
            foreach (var comment in post.Comments.GetComments())
            {
                AppendComment(sb, comment, 0, maxDepth, 0, maxComments);
            }
            return sb.ToString();
        }

        private static void AppendComment(StringBuilder sb, Comment comment, int depth, int maxDepth, int commentCount, int maxComments)
        {
            if (maxDepth != -1 && depth > maxDepth) return;
            if (maxComments != -1 && commentCount > maxComments) return;

            // Add indentation based on depth
            string indent = new string(' ', depth * 4);

            // Add the comment
            sb.AppendLine($"{indent}{comment.Body}");

            // Recursively process replies
            if (comment.Replies != null)
            {
                foreach (var reply in comment.Replies)
                {
                    AppendComment(sb, reply, depth + 1, maxDepth, commentCount+1, maxComments);
                }
            }
        }


    }
}
