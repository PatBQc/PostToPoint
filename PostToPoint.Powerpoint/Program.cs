﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Reddit;
using Reddit.Controllers;
using System.Linq;
using PuppeteerSharp;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using P = DocumentFormat.OpenXml.Presentation;
using A = DocumentFormat.OpenXml.Drawing;
using System.Drawing;
using System.Net;
using System.Text;

namespace PostToPoint.Powerpoint
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Replace with your Reddit app credentials and user credentials
            string appId = "";
            string appSecret = "";
            string username = "";
            string password = "";


            string accessToken = string.Empty;
            string userAgent = "PostToPoint";
            string redirectUri = "http://localhost:8080";

            string batchTag = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");

            DateTime oldestPostAccepted = DateTime.Now.AddDays(-7);
            string timeQuery = "week"; // one of hour, day, week, month, year, all
            string sortQuery = "new"; // one of hot, new, top, rising, controversial

            bool downloadImages = true;

            var authenticator = new RedditAuthenticator(appId, appSecret, username, password, userAgent, redirectUri);

            try
            {
                accessToken = await authenticator.GetAccessTokenAsync();
                Console.WriteLine($"Access Token: {accessToken}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            // Initialize Reddit client
            var reddit = new RedditClient(appId: appId, appSecret: appSecret, accessToken: accessToken);

            // Get saved posts (change to GetUpvoted for liked posts)
            var savedPosts = reddit.Account.Me.GetPostHistory(where: "saved", context: 3, t: timeQuery, limit: 100, sort: sortQuery);
            var upvotedPosts = reddit.Account.Me.GetPostHistory(where: "upvoted", context: 3, t: timeQuery, limit: 100, sort: sortQuery);

            List<Post> posts = new List<Post>(savedPosts);
            posts.AddRange(upvotedPosts);

            var redditImportantPosts = new List<Post>(
                posts.Where(x => x.Created > oldestPostAccepted)
                     .DistinctBy(x => x.Permalink)
                     .DistinctBy(x => GetPostImportantPart(x))
                     );

            var postDataList = new List<PostData>();
            foreach (var post in redditImportantPosts)
            {
                var postData = new PostData()
                {
                    Subreddit = post.Subreddit,
                    Created = post.Created,
                    Post = post,
                    Title = post.Title
                };

                if (post is LinkPost linkPost)
                {
                    postData.Url = linkPost.URL;
                }

                if (post is SelfPost selfPost)
                {
                    postData.SelfText = selfPost.SelfText;
                    postData.SelfTextHTML = selfPost.SelfTextHTML;
                }

                postDataList.Add(postData);
            }

            // Directory to save presentation
            string outputDir = "Outputs";
            Directory.CreateDirectory(outputDir);

            if (downloadImages)
            {
                outputDir = await DownloadAndSaveImages(batchTag, postDataList, outputDir);
            }

            // Create PowerPoint presentation
            CreatePresentation(postDataList, Path.Combine("Outputs", batchTag  + "-PostToPoint.pptx"));

            Console.WriteLine("Presentation created successfully.");
        }

        private static async Task<string> DownloadAndSaveImages(string batchTag, List<PostData> postDataList, string outputDir)
        {
            // Directory to save screenshots
            outputDir = Path.Combine(outputDir, batchTag);
            Directory.CreateDirectory(outputDir);

            // Set up PuppeteerSharp
            await new BrowserFetcher().DownloadAsync();

            // For each post, capture screenshot if it's a URL
            foreach (var postData in postDataList)
            {
                if (!string.IsNullOrEmpty(postData.Url) && Uri.IsWellFormedUriString(postData.Url, UriKind.Absolute))
                {
                    if (postData.Url.Contains("i.redd.it"))
                    {
                        await DownloadFileAsync(postData.Url, outputDir, postData);
                    }
                    else
                    {
                        string screenshotPath = Path.Combine(outputDir, $"{Guid.NewGuid()}.png");
                        await CaptureScreenshotAsync(postData.Url, screenshotPath);
                        postData.ScreenshotPath = screenshotPath;
                    }
                }
                else
                {
                    if (postData.SelfTextHTML != null)
                    {
                        string screenshotPath = Path.Combine(outputDir, $"{Guid.NewGuid()}.png");
                        await CaptureScreenshotHtmlAsync(postData.SelfTextHTML, screenshotPath);
                        postData.ScreenshotPath = screenshotPath;
                    }
                    else
                    {
                        postData.ScreenshotPath = null;
                    }
                }
            }

            return outputDir;
        }

        static string GetPostImportantPart(Post post)
        {
            if (post is LinkPost linkPost)
            {
                return linkPost.URL;
            }

            if (post is SelfPost selfPost)
            {
                return selfPost.SelfText;
            }

            return post.Title;
        }

        static async Task CaptureScreenshotAsync(string url, string filePath)
        {
            // Download Chromium if necessary
            await new BrowserFetcher().DownloadAsync();

            try
            {
                using (var browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Headless = true,
                }))
                using (var page = await browser.NewPageAsync())
                {
                    await page.SetUserAgentAsync("PostToPoint");

                    await page.SetViewportAsync(new ViewPortOptions
                    {
                        Width = 1280,
                        //Height = 1280
                    });

                    await page.GoToAsync(url);

                    await page.ScreenshotAsync(filePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error on url: " + url);
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.ToString());
                Console.WriteLine();
            }
        }

        static async Task DownloadFileAsync(string url, string downloadFolder, PostData postData)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("PostToPoint");

                using (var response = await client.GetAsync(url))
                {
                    response.EnsureSuccessStatusCode();

                    // Try to get filename from content-disposition header
                    //var fileName = GetFileNameFromContentDisposition(response) ??
                    //              // If not found, try to get from URL
                    //              GetFileNameFromUrl(url) ??
                    //              // If still not found, generate a name with extension from content-type
                    //              GetFileNameFromContentType(response);

                    var fileName = // If still not found, generate a name with extension from content-type
                                    GetFileNameFromContentType(response) ??
                                    // If not found, try to get from URL
                                    GetFileNameFromUrl(url);

                    string destinationPath = Path.Combine(downloadFolder, $"{Guid.NewGuid()}-{fileName}");

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = File.Create(destinationPath))
                    {
                        await stream.CopyToAsync(fileStream);
                    }

                    postData.ScreenshotPath = destinationPath;
                }
            }
        }

        static string GetFileNameFromContentDisposition(HttpResponseMessage response)
        {
            if (response.Content.Headers.ContentDisposition != null)
            {
                return response.Content.Headers.ContentDisposition.FileName?.Trim('"');
            }
            return null;
        }

        static string GetFileNameFromUrl(string url)
        {
            try
            {
                return Path.GetFileName(new Uri(url).LocalPath);
            }
            catch
            {
                return null;
            }
        }

        static string GetFileNameFromContentType(HttpResponseMessage response)
        {
            // Get content type (e.g., "image/jpeg")
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (string.IsNullOrEmpty(contentType))
                return "downloaded_file";

            // Convert content type to extension (e.g., "image/jpeg" -> ".jpg")
            var extension = contentType.Split('/').LastOrDefault();
            if (extension != null)
            {
                switch (extension.ToLower())
                {
                    case "jpeg":
                    case "jpg":
                        extension = ".jpg";
                        break;
                    case "png":
                        extension = ".png";
                        break;
                    case "pdf":
                        extension = ".pdf";
                        break;
                    // Add more cases as needed
                    default:
                        extension = "." + extension;
                        break;
                }
            }

            return $"downloaded_file{extension}";
        }



        static async Task CaptureScreenshotHtmlAsync(string html, string filePath)
        {
            // Download Chromium if necessary
            await new BrowserFetcher().DownloadAsync();

            using (var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
            }))
            using (var page = await browser.NewPageAsync())
            {
                await page.SetUserAgentAsync("PostToPoint");

                await page.SetViewportAsync(new ViewPortOptions
                {
                    Width = 1280,
                    Height = 720
                });

                await page.SetContentAsync(html);

                await page.ScreenshotAsync(filePath);
            }
        }

        static void CreatePresentation(List<PostData> postDataList, string filename)
        {
            // TODO implement a Disposable generator for the presentation
            PresentationCreator.CreatePresentation(filename);

            foreach (var postData in postDataList)
            {
                if (postData.ScreenshotPath != null)
                {
                    PresentationCreator.AddSlide(filename, postData.Title, postData.ScreenshotPath, GetPostDescriptions(postData.Post));
                    // creator.AddTitleImageSlide(postData.Title, postData.ScreenshotPath, postData.GetMetadata());
                }
                else
                {
                    PresentationCreator.AddSlide(filename, postData.Title, null, GetPostDescriptions(postData.Post));
                    // creator.AddTitleHtmlSlide(postData.Title, postData.SelfTextHTML, postData.GetMetadata());
                }
            }
        }

        static string GetPostDescriptions(Post post)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Title: {post.Title}");
            sb.AppendLine($"Subreddit: {post.Subreddit}");
            sb.AppendLine($"Date: {post.Created}");
            if (post is LinkPost linkPost)
            {
                sb.AppendLine($"URL: {linkPost.URL}");
            }
            sb.AppendLine($"Permalink: {post.Permalink}");
            sb.AppendLine($"Votes: Upvotes {post.UpVotes} ⬆️  Downvotes {post.DownVotes} ⬇️  Upvote Ratio {post.UpvoteRatio*100} %");
            sb.AppendLine($"NSFW: {post.NSFW}");
            //sb.AppendLine($"Comments: {post.Comments.Top[0].NumReplies}");

            return sb.ToString();
        }
    }
}
