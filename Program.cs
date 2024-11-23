using System;
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

namespace PostToPoint
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
            // var savedPosts = reddit.Account.Me.GetSaved(limit: 100);
            var redditImportantPosts = reddit.Account.Me.GetPostHistory(where: "saved", context: 3, t: "all", limit: 100, sort: "new");
            redditImportantPosts.AddRange(reddit.Account.Me.GetPostHistory(where: "upvoted", context: 3, t: "all", limit: 100, sort: "new"));

            // TODO : use URL instead of PermaLink (availabvle in "LinkPost" cast)
            redditImportantPosts = new List<Post>(redditImportantPosts.DistinctBy(x => x.Permalink));

            var postDataList = new List<PostData>();
            foreach (var post in redditImportantPosts)
            {
                var postData = new PostData()
                {
                    // SelfText = post.SelfText,
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

            // Set up PuppeteerSharp
            await new BrowserFetcher().DownloadAsync();

            // Directory to save screenshots
            string screenshotsDir = "Screenshots";
            Directory.CreateDirectory(screenshotsDir);

            // For each post, capture screenshot if it's a URL
            foreach (var postData in postDataList)
            {
                if (!string.IsNullOrEmpty(postData.Url) && Uri.IsWellFormedUriString(postData.Url, UriKind.Absolute))
                {
                    if (postData.Url.Contains("i.redd.it"))
                    {
                        await DownloadFileAsync(postData.Url, screenshotsDir, postData);
                    }
                    else
                    {
                        string screenshotPath = Path.Combine(screenshotsDir, $"{Guid.NewGuid()}.png");
                        await CaptureScreenshotAsync(postData.Url, screenshotPath);
                        postData.ScreenshotPath = screenshotPath;
                    }
                }
                else
                {
                    if (postData.SelfTextHTML != null)
                    {
                        string screenshotPath = Path.Combine(screenshotsDir, $"{Guid.NewGuid()}.png");
                        await CaptureScreenshotHtmlAsync(postData.SelfTextHTML, screenshotPath);
                        postData.ScreenshotPath = screenshotPath;
                    }
                    else
                    {
                        postData.ScreenshotPath = null;
                    }
                }
            }

            // Create PowerPoint presentation
            CreatePresentation(postDataList);

            Console.WriteLine("Presentation created successfully.");
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

        static void CreatePresentation(List<PostData> postDataList)
        {
            string filename = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + "-" + "PostToPoint.pptx";

            //using var creator = new PresentationCreator();
            //creator.CreatePresentation(filename);

            PresentationCreator.CreatePresentation(filename);
            //PresentationCreator.AddSlide(filename, 
            //    "Pat was here - My Slide Title", 
            //    @"C:\Users\pbelanger\source\repos\PostToPoint\bin\Debug\net8.0\Screenshots\7a20db1a-db93-46e5-ba1a-db5a45e55ec8.png", 
            //    "This is a comment on the slide by Pat that was here");

            foreach (var postData in postDataList)
            {
                if (postData.ScreenshotPath != null)
                {
                    PresentationCreator.AddSlide(filename, postData.Title, postData.ScreenshotPath, postData.GetMetadata());
                    // creator.AddTitleImageSlide(postData.Title, postData.ScreenshotPath, postData.GetMetadata());
                }
                else
                {
                    PresentationCreator.AddSlide(filename, postData.Title, null, postData.GetMetadata());
                    // creator.AddTitleHtmlSlide(postData.Title, postData.SelfTextHTML, postData.GetMetadata());
                }
            }
        }



    }
}
