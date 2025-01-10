﻿﻿﻿﻿﻿using Microsoft.Graph.Drives.Item.Items.Item.GetActivitiesByIntervalWithStartDateTimeWithEndDateTimeWithInterval;
using PuppeteerSharp;
using Reddit;
using Reddit.Controllers;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PostToPoint.Windows
{
    class RedditHelper
    {
        private static string _accessToken = string.Empty;

        public static async Task<List<RedditPostData>> GetMyImportantRedditPosts(
            string appId, 
            string redirectUri, 
            string appSecret, 
            string username, 
            string password, 
            bool downloadImages, 
            string timeQuery = "week", 
            string sortQuery = "new",
            int oldestDaysInThePast = 7)
        {
            ArgumentNullException.ThrowIfNull(appId);
            ArgumentNullException.ThrowIfNull(redirectUri);
            ArgumentNullException.ThrowIfNull(appSecret);
            ArgumentNullException.ThrowIfNull(username);
            ArgumentNullException.ThrowIfNull(password);
            ArgumentNullException.ThrowIfNull(timeQuery);
            ArgumentNullException.ThrowIfNull(sortQuery);

            string batchTag = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");

            DateTime oldestPostAccepted = DateTime.Now.AddDays(-oldestDaysInThePast);
            //string timeQuery = "week"; // one of hour, day, week, month, year, all
            //string sortQuery = "new"; // one of hot, new, top, rising, controversial

            var authenticator = new RedditAuthenticator(appId, appSecret, username, password, "PostToPoint", redirectUri);

            try
            {
                _accessToken = await authenticator.GetAccessTokenAsync();
                Console.WriteLine($"Access Token: {_accessToken}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            // Initialize Reddit client
            var reddit = new RedditClient(appId: appId, appSecret: appSecret, accessToken: _accessToken);

            var savedPosts = GetRedditPosts("saved", timeQuery, sortQuery, reddit);
            var upvotedPosts = GetRedditPosts("upvoted", timeQuery, sortQuery, reddit);

            List<Post> posts = new List<Post>(savedPosts);
            posts.AddRange(upvotedPosts);

            var redditImportantPosts = new List<Post>(
                posts.Where(x => x.Created > oldestPostAccepted)
                     .DistinctBy(x => x.Permalink)
                     //.DistinctBy(x => GetPostImportantPart(x))
                     .DistinctBy(x => x.Title)
                     );

            var postDataList = new List<RedditPostData>();
            foreach (var post in redditImportantPosts)
            {
                string url = post is LinkPost linkPost ? linkPost.URL : $"https://www.reddit.com{post.Permalink}";

                var postData = new RedditPostData()
                {
                    Subreddit = post.Subreddit,
                    Created = post.Created,
                    Post = post,
                    Title = post.Title,
                    Url = url
                };

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

            return postDataList;
        }

        private static List<Post> GetRedditPosts(string whereQuery, string timeQuery, string sortQuery, RedditClient reddit)
        {
            var continuationPostList = new List<Post>();
            string? after = null;

            // Get saved posts (change to GetUpvoted for liked posts)
            do
            {
                // Retrieve a batch of posts
                var subPostList = reddit.Account.Me.GetPostHistory(where: whereQuery, context: 3, t: timeQuery, limit: 100, sort: sortQuery, after: after);

                // Add the retrieved posts to the collection
                continuationPostList.AddRange(subPostList);

                // Get the "after" value for the next batch
                after = subPostList.Count > 0 ? subPostList[subPostList.Count - 1].Fullname : null;

                // Continue until we've retrieved all posts or reached a desired limit
            } while (after != null && continuationPostList.Count < 10000);

            return continuationPostList;
        }

        private static async Task<string> DownloadAndSaveImages(string batchTag, List<RedditPostData> postDataList, string outputDir)
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

        static async Task DownloadFileAsync(string url, string downloadFolder, RedditPostData postData)
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

        static string? GetFileNameFromContentDisposition(HttpResponseMessage response)
        {
            if (response.Content.Headers.ContentDisposition != null)
            {
                return response.Content.Headers.ContentDisposition.FileName?.Trim('"');
            }
            return null;
        }

        static string? GetFileNameFromUrl(string url)
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
            sb.AppendLine($"Votes: Upvotes {post.UpVotes} ⬆️  Downvotes {post.DownVotes} ⬇️  Upvote Ratio {post.UpvoteRatio * 100} %");
            sb.AppendLine($"NSFW: {post.NSFW}");
            //sb.AppendLine($"Comments: {post.Comments.Top[0].NumReplies}");

            return sb.ToString();
        }

        internal static async Task<string> GetRedditVideoStream(RedditPostData postData)
        {
            string uri = "https://www.reddit.com" + postData.Post.Permalink + ".json";
            uri = uri.Replace("/.json", ".json");

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PostToPoint/1.0");

            var response = await httpClient.GetAsync(uri);
            var jsonString = await response.Content.ReadAsStringAsync();

            using JsonDocument document = JsonDocument.Parse(jsonString);
            JsonElement root = document.RootElement;

            // Find the starting position after "fallback_url\": \""
            int startIndex = jsonString.IndexOf("fallback_url");
            if (startIndex == -1)
            {
                throw new InvalidOperationException("Could not find fallback_url in response");
            }
            startIndex += "fallback_url\": \"".Length;

            // Find the closing quote
            int endIndex = jsonString.IndexOf("\"", startIndex);
            if (endIndex == -1)
            {
                throw new InvalidOperationException("Could not find end of fallback_url in response");
            }

            // Extract the URL
            string url = jsonString.Substring(startIndex, endIndex - startIndex);

            int queryIndex = url.IndexOf("?");
            if (queryIndex == -1)
            {
                throw new InvalidOperationException("Could not find query parameters in URL");
            }

            return url.Substring(0, queryIndex);
        }
    }
}
