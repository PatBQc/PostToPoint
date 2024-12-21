using System;
using System.IO;
using System.Text.RegularExpressions;

namespace PostToPoint.Windows
{
    public class MarkdownTemplate
    {
        public string Title { get; set; }
        public string Subheadline { get; set; }
        public string Teaser { get; set; }
        public string Slug { get; set; }
        public string Redirect { get; set; }
        public string Content { get; set; }

        public static MarkdownTemplate ParseFromFile(string filePath)
        {
            string fileContent = File.ReadAllText(filePath);
            var template = new MarkdownTemplate();

            // Extract values using regex
            template.Title = ExtractValue(fileContent, "title\\s*:\\s*\"([^\"]+)\"");
            template.Subheadline = ExtractValue(fileContent, "subheadline\\s*:\\s*\"([^\"]+)\"");
            template.Teaser = ExtractValue(fileContent, "teaser\\s*:\\s*\"([^\"]+)\"");
            template.Slug = ExtractValue(fileContent, "/r/([^\"\\s]+)");
            template.Redirect = ExtractValue(fileContent, "redirect_to\\s*:\\s*([^\\s]+)");

            // Extract content (everything after ---)
            var contentMatch = Regex.Match(fileContent.Substring(3), "---\\s*\\n(.*)$", RegexOptions.Singleline);
            if (contentMatch.Success)
            {
                template.Content = contentMatch.Groups[1].Value.Trim();
            }

            return template;
        }

        private static string ExtractValue(string content, string pattern)
        {
            var match = Regex.Match(content, pattern);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        public void SaveToFile(string filePath)
        {
            string template = """
                    ---
                    layout: redirect
                    title               : "{0}"
                    subheadline         : "{1}"
                    teaser              : "{2}"
                    lang: fr
                    header:
                       image_fullwidth  : "header_projets.webp"
                    permalink           : "/r/{3}"
                    ref                 : "/r/{3}"
                    sitemap: false
                    redirect_to:  {4}
                    ---
                    {5}
                    """;

            string fileContent = string.Format(
                template,
                Title,
                Subheadline,
                Teaser,
                Slug,
                Redirect,
                Content
            );

            File.WriteAllText(filePath, fileContent);
        }
    }
}
