using Azure.Core;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PostToPoint.Windows
{
    public class OneDriveUploader
    {
        private readonly string _clientId;
        private readonly string _driveId;
        private readonly string _folderId;

        private readonly List<string> _scopes = new List<string> { "Files.ReadWrite.All", "offline_access" };

        private HttpClient? _httpClient;

        public OneDriveUploader(string clientId, string driveId, string folderId)
        {
            ArgumentNullException.ThrowIfNull(clientId);
            ArgumentNullException.ThrowIfNull(driveId);
            ArgumentNullException.ThrowIfNull(folderId);

            _clientId = clientId;
            _driveId = driveId;
            _folderId = folderId;
        }

        private async Task InitializeClientWithAccessToken()
        {
            if (_httpClient == null)
            {
                _httpClient = new HttpClient();

                string? accessToken = null;

                try
                {
                    // Initialize MSAL
                    var app = PublicClientApplicationBuilder
                        .Create(_clientId)
                        .WithRedirectUri("http://localhost")
                        .WithAuthority($"https://login.microsoftonline.com/common")
                        .Build();

                    // Enable token cache serialization
                    await OneDriveAccessTokenCacheHelper.EnableSerialization(app);

                    // Try to get token silently first (from cache)
                    var accounts = await app.GetAccountsAsync();
                    var account = accounts.FirstOrDefault();

                    if (account == null)
                    {
                        // No cached account found, acquire token interactively
                        var result = await app.AcquireTokenInteractive(_scopes)
                            .ExecuteAsync();
                        accessToken = result.AccessToken;
                    }
                    else
                    {
                        try
                        {
                            var result = await app.AcquireTokenSilent(_scopes, account)
                                .ExecuteAsync();
                            accessToken = result.AccessToken;
                        }
                        catch (MsalUiRequiredException)
                        {
                            // Token expired and refresh token is invalid, acquire new token interactively
                            var result = await app.AcquireTokenInteractive(_scopes)
                                .ExecuteAsync();
                            accessToken = result.AccessToken;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Handle any other exceptions
                    Console.WriteLine($"Error acquiring token: {ex.Message}");
                    throw;
                }

                if (accessToken == null)
                {
                    throw new InvalidOperationException("Failed to acquire access token");
                }

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
        }

        public async Task<(string shareLink, string uploadedFileId)> UploadLargeFileAndGetShareLink(string filePath)
        {
            ArgumentNullException.ThrowIfNull(filePath);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Input file not found", filePath);
            }

            await InitializeClientWithAccessToken();

            if (_httpClient == null)
            {
                throw new InvalidOperationException("HTTP client not initialized");
            }

            var fileName = Path.GetFileName(filePath);
            var fileContent = File.ReadAllBytes(filePath);

            // 1. Create upload session
            string createSessionUrl = $"https://graph.microsoft.com/v1.0/drives/{_driveId}/items/{_folderId}:/{fileName}:/createUploadSession";

            string? uploadUrl = null;

            var jsonContent = "{\"@microsoft.graph.conflictBehavior\": \"rename\",\"name\": \"" + fileName + "\"}";
            var sessionContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var sessionResponse = await _httpClient.PostAsync(createSessionUrl, sessionContent);

            if (!sessionResponse.IsSuccessStatusCode)
            {
                var errorContent = await sessionResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"Error creating upload session: {errorContent}");

                throw new HttpProtocolException((long)sessionResponse.StatusCode, "Error creating upload session", null);
            }
            else
            {
                var responseContent = await sessionResponse.Content.ReadAsStringAsync();
                var jsonResponse = JObject.Parse(responseContent);
                uploadUrl = jsonResponse["uploadUrl"]?.ToString();
                if (string.IsNullOrEmpty(uploadUrl))
                {
                    throw new InvalidOperationException("Failed to get upload URL from response");
                }
            }

            const int chunkSize = 320 * 1024; // 320 KB chunks
            long fileSize = new FileInfo(filePath).Length;

            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (var httpClient = new HttpClient())
            {
                for (long i = 0; i < fileSize; i += chunkSize)
                {
                    int currentChunkSize = (int)Math.Min(chunkSize, fileSize - i);
                    byte[] buffer = new byte[currentChunkSize];
                    await fileStream.ReadAsync(buffer, 0, currentChunkSize);

                    using (var chunkContent = new ByteArrayContent(buffer))
                    {
                        chunkContent.Headers.Add("Content-Range", $"bytes {i}-{i + currentChunkSize - 1}/{fileSize}");
                        chunkContent.Headers.Add("Content-Length", currentChunkSize.ToString());

                        var response = await httpClient.PutAsync(uploadUrl, chunkContent);
                        if (!response.IsSuccessStatusCode)
                        {
                            // Handle error
                            throw new HttpProtocolException((long)response.StatusCode, "Error in upload session on a file chunk", null);
                        }

                        if (i + currentChunkSize >= fileSize)
                        {
                            // Final chunk, parse the response to get the file details
                            var responseContent = await response.Content.ReadAsStringAsync();
                            var jsonResponse = JObject.Parse(responseContent);
                            var webUrl = jsonResponse["webUrl"]?.ToString();
                            string? fileId = jsonResponse["id"]?.ToString();

                            if (string.IsNullOrEmpty(fileId))
                            {
                                throw new InvalidOperationException("Failed to get file ID from response");
                            }

                            // Create sharing link with direct download
                            string createLinkUrl = $"https://graph.microsoft.com/v1.0/drives/{_driveId}/items/{fileId}/createLink";
                            var linkRequestBody = new
                            {
                                type = "view",
                                scope = "anonymous"
                            };

                            var linkRequest = new HttpRequestMessage(HttpMethod.Post, createLinkUrl)
                            {
                                Content = new StringContent(
                                    JsonConvert.SerializeObject(linkRequestBody),
                                    Encoding.UTF8,
                                    "application/json"
                                )
                            };

                            var linkResponse = await _httpClient.SendAsync(linkRequest);
                            var linkResponseContent = await linkResponse.Content.ReadAsStringAsync();
                            var linkJsonResponse = JObject.Parse(linkResponseContent);

                            // Get the sharing URL and transform it to direct download
                            var shareLink = linkJsonResponse["link"]?["webUrl"]?.ToString();
                            if (string.IsNullOrEmpty(shareLink))
                            {
                                throw new InvalidOperationException("Failed to get share link from response");
                            }

                            shareLink = shareLink.Replace("1drv.ms/v/s!", "1drv.ms/u/s!"); // Change /v/ to /u/

                            Console.WriteLine($"File uploaded successfully. File ID: {fileId}");

                            return (shareLink, fileId);
                        }
                    }
                }
            }

            throw new Exception("Failed to complete file upload");
        }
    }
}
