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
    }
}
