﻿using System;
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

        [Option("llm-choice", Required = false, HelpText = "LLM Choice to work with (includes only 'claude-3-5-sonnet-latest' is supported)")]
        public string LlmChoice { get; set; }

        [Option("blog-post-template", Required = false, HelpText = "The template file for your blog post in .md format")]
        public string BlogPostTemplateFilename { get; set; }


    }
}
