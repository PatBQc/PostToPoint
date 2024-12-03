using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;

namespace PostToPoint.Windows
{
    public class CommandLineOptions
    {
        [Option("reddit-username", Required = false, HelpText = "Your Reddit username")]
        public string RedditUsername { get; set; }

        [Option("reddit-password", Required = false, HelpText = "Your Reddit password")]
        public string RedditPassword { get; set; }

        [Option("reddit-app-id", Required = false, HelpText = "Your Reddit client ID")]
        public string RedditAppId { get; set; }

        [Option("reddit-app-secret", Required = false, HelpText = "Your Reddit client secret")]
        public string RedditAppSecret { get; set; }

        [Option("reddit-redirect-uri", Required = false, HelpText = "Your Reddit redirect uri.  Must be the same as configured with your client ID")]
        public string RedditRedirectUri { get; set; }

        [Option("blog-to-bluesky-prompt", Required = false, HelpText = "Filename for LLM Prompt to convert a blog post to Bluesky")]
        public string BlogToBlueskyPrompt { get; set; }

        [Option("blog-to-linkedin-prompt", Required = false, HelpText = "Filename for LLM Prompt to convert a blog post to LinkedIn")]
        public string BlogToLinkedinPrompt { get; set; }

        [Option("post-to-blog-prompt", Required = false, HelpText = "Filename for LLM Prompt to convert a reddit post to blog")]
        public string PostToBlogPrompt { get; set; }

        [Option("post-to-bluesky-prompt", Required = false, HelpText = "Filename for LLM Prompt to convert a reddit post to Bluesky")]
        public string PostToBlueskyPrompt { get; set; }

        [Option("post-to-linkedin-prompt", Required = false, HelpText = "Filename for LLM Prompt to convert a reddit post to LinkedIn")]
        public string PostToLinkedinPrompt { get; set; }

        [Option("llm-choice", Required = false, HelpText = "LLM Choice to work with (includes only 'claude-3-5-sonnet-latest' is supported)", Default = "claude-3-5-sonnet-latest")]
        public string LlmChoice { get; set; }

        [Option("blog-post-template", Required = false, HelpText = "The template file for your blog post in .md format")]
        public string BlogPostTemplateFilename { get; set; }

        [Option("blog-directory", Required = false, HelpText = "Blog post .md files directory")]
        public string BlogDirectory { get; set; }

        [Option("rss-directory", Required = false, HelpText = "RSS files directory")]
        public string RssDirectory { get; set; }

        [Option("post-content-directory", Required = false, HelpText = "Post content files directory (aka where images, screenshots, videos, ... will be downloaded")]
        public string PostContentDirectory { get; set; }

        [Option("redirect-directory", Required = false, HelpText = "The directory where to put the redirects")]
        public string RedirectDirectory { get; set; }

        [Option("onedrive-application-client-id", Required = false, HelpText = "Your OneDrive application client ID")]
        public string OneDriveApplicationClientId { get; set; }

        [Option("onedrive-drive-id", Required = false, HelpText = "Your OneDrive drive id")]
        public string OneDriveDriveId { get; set; }

        [Option("onedrive-folder-id", Required = false, HelpText = "Your OneDrive folder id in the same drive as specified in drive id")]
        public string OneDriveFolderId { get; set; }

    }
}
