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

        [Option("buffer-access-token", Required = false, HelpText = "Your Buffer access token")]
        public string BufferAccessToken { get; set; }

        [Option("buffer-profile-id-bluesky", Required = false, HelpText = "Your Bluesky Buffer profile ID")]
        public string BufferProfileId { get; set; }

        [Option("post-to-bluesky-prompt", Required = false, HelpText = "Filename for LLM Prompt to post to Bluesky through Buffer profile")]
        public string PostToBlueskyPrompt { get; set; }

        [Option("llm-choice", Required = false, HelpText = "LLM Choice to work with (includes only 'claude-3-5-sonnet-latest' is supported)")]
        public string LlmChoice { get; set; }

        [Option("blog-post-template", Required = false, HelpText = "The template file for your blog post in .md format")]
        public string BlogPostTemplateFilename { get; set; }


    }
}
