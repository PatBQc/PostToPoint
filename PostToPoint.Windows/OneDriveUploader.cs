using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PostToPoint.Windows
{

    // Example usage:
    // public class Program
    // {
    //     public static async Task Main(string[] args)
    //     {
    //         try
    //         {
    //             var uploader = new OneDriveUploader();
    //             await uploader.Initialize();
    // 
    //             string filePath = @"C:\Path\To\Your\Video.mp4";
    //             string oneDriveFolderPath = "/FolderName"; // Specify your OneDrive folder path
    // 
    //             string downloadUrl = await uploader.UploadVideoAndGetShareableLink(filePath, oneDriveFolderPath);
    //             Console.WriteLine($"Direct download link: {downloadUrl}");
    //         }
    //         catch (Exception ex)
    //         {
    //             Console.WriteLine($"Error: {ex.Message}");
    //         }
    //     }
    // }

    public class OneDriveUploader
    {
        // Replace these values with your own Azure AD application details
        private readonly string ClientId;
        private readonly string TenantId;
        private readonly string ClientSecret;

        private HttpClient _httpClient = null;


        public OneDriveUploader(string clientId, string tenantId, string clientSecret)
        {
            ClientId = clientId;
            TenantId = tenantId;
            ClientSecret = clientSecret;
        }


        private async Task InitializeClientWithAccessToken()
        {
            if (_httpClient == null)
            {
                _httpClient = new HttpClient();

                var tokenEndpoint = $"https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/token";
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    {"client_id", ClientId},
                    {"scope", "https://graph.microsoft.com/.default"},
                    {"client_secret", ClientSecret},
                    {"grant_type", "client_credentials"}
                });

                var response = await _httpClient.PostAsync(tokenEndpoint, content);
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var tokenData = JsonSerializer.Deserialize<JsonDocument>(jsonResponse);
                var accessToken = tokenData.RootElement.GetProperty("access_token").GetString();

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
        }

        public async Task<(string shareLink, string uploadedFileId)> UploadFileAndGetShareLink(string filePath)
        {
            await InitializeClientWithAccessToken();

            // 1. Upload the file
            var fileName = Path.GetFileName(filePath);
            var fileContent = File.ReadAllBytes(filePath);

            // Small file upload (less than 4MB)
            var uploadUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{fileName}:/content";
            var fileContentByteArrayContent = new ByteArrayContent(fileContent);
            fileContentByteArrayContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var uploadResponse = await _httpClient.PutAsync(uploadUrl, fileContentByteArrayContent);
            var uploadJsonResponse = await uploadResponse.Content.ReadAsStringAsync();
            var uploadData = JsonSerializer.Deserialize<JsonDocument>(uploadJsonResponse);
            var fileId = uploadData.RootElement.GetProperty("id").GetString();

            // 2. Create sharing link
            var sharingUrl = $"https://graph.microsoft.com/v1.0/me/drive/items/{fileId}/createLink";
            var sharingContent = new StringContent(
                JsonSerializer.Serialize(new
                {
                    type = "view",
                    scope = "anonymous"
                }),
                Encoding.UTF8, new MediaTypeHeaderValue("application/json")
            );

            var sharingResponse = await _httpClient.PostAsync(sharingUrl, sharingContent);
            var sharingJsonResponse = await sharingResponse.Content.ReadAsStringAsync();
            var sharingData = JsonSerializer.Deserialize<JsonDocument>(sharingJsonResponse);
            var shareLink = sharingData.RootElement.GetProperty("link").GetProperty("webUrl").GetString();

            // Convert the sharing link to a direct download link
            shareLink = shareLink.Replace("?web=1", "?download=1");

            return (shareLink, fileId);
        }

        // For large files (>4MB), use upload session
        public async Task<(string shareLink, string uploadedFileId)> UploadLargeFileAndGetShareLink_old(string filePath)
        {

            var fileName = Path.GetFileName(filePath);
            var fileContent = File.ReadAllBytes(filePath);

            // 1. Create upload session
            var createSessionUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{fileName}:/createUploadSession";
            var sessionResponse = await _httpClient.PostAsync(createSessionUrl, null);
            var sessionJsonResponse = await sessionResponse.Content.ReadAsStringAsync();
            var sessionData = JsonSerializer.Deserialize<JsonDocument>(sessionJsonResponse);
            var uploadUrl = sessionData.RootElement.GetProperty("uploadUrl").GetString();

            // 2. Upload the file in chunks
            const int chunkSize = 320 * 1024; // 320 KB chunks
            var totalLength = fileContent.Length;

            for (var i = 0; i < totalLength; i += chunkSize)
            {
                var chunk = new byte[Math.Min(chunkSize, totalLength - i)];
                Array.Copy(fileContent, i, chunk, 0, chunk.Length);

                using var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
                request.Content = new ByteArrayContent(chunk);
                request.Content.Headers.ContentRange = new ContentRangeHeaderValue(i, i + chunk.Length - 1, totalLength);
                request.Content.Headers.ContentLength = chunk.Length;

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to upload chunk: {await response.Content.ReadAsStringAsync()}");
                }

                if (i + chunk.Length >= totalLength)
                {
                    var finalResponse = await response.Content.ReadAsStringAsync();
                    var finalData = JsonSerializer.Deserialize<JsonDocument>(finalResponse);

                    var shareLink = finalData.RootElement.GetProperty("link").GetProperty("webUrl").GetString();

                    // Convert the sharing link to a direct download link
                    shareLink = shareLink.Replace("?web=1", "?download=1");

                    var fileId = finalData.RootElement.GetProperty("id").GetString();

                    return (shareLink, fileId);
                }
            }

            throw new Exception("Failed to complete file upload");
        }

        public async Task<(string shareLink, string uploadedFileId)> UploadLargeFileAndGetShareLink(string filePath)
        {
            await InitializeClientWithAccessToken();

            var fileName = Path.GetFileName(filePath);
            var fileContent = File.ReadAllBytes(filePath);

            // Before: find the Share folder
            var folderPath = "Partage/rss"; // or any folder name
            var folderUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{folderPath}";
            var folderResponse = await _httpClient.GetAsync(folderUrl);
            var jsonResponse = await folderResponse.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonDocument>(jsonResponse);
            var folderId = data.RootElement.GetProperty("id").GetString();

            // 1. Create upload session
            //var createSessionUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{fileName}:/createUploadSession";
            var createSessionUrl = $"https://graph.microsoft.com/v1.0/me/drive/items/folder_id:/children/{fileName}:/createUploadSession";

            // Add the required JSON body
            var sessionRequestBody = new StringContent(
                JsonSerializer.Serialize(new
                {
                    item = new
                    {
                        conflictBehavior = "rename",
                        name = fileName
                    }
                }),
                Encoding.UTF8,
                "application/json"
            );

            var sessionResponse = await _httpClient.PostAsync(createSessionUrl, sessionRequestBody);

            if (!sessionResponse.IsSuccessStatusCode)
            {
                var errorContent = await sessionResponse.Content.ReadAsStringAsync();
                throw new Exception($"Failed to create upload session. Status: {sessionResponse.StatusCode}, Error: {errorContent}");
            }

            var sessionJsonResponse = await sessionResponse.Content.ReadAsStringAsync();
            var sessionData = JsonSerializer.Deserialize<JsonDocument>(sessionJsonResponse);
            var uploadUrl = sessionData.RootElement.GetProperty("uploadUrl").GetString();

            // Rest of the code remains the same...
            // 2. Upload the file in chunks
            const int chunkSize = 320 * 1024; // 320 KB chunks
            var totalLength = fileContent.Length;

            for (var i = 0; i < totalLength; i += chunkSize)
            {
                var chunk = new byte[Math.Min(chunkSize, totalLength - i)];
                Array.Copy(fileContent, i, chunk, 0, chunk.Length);

                using var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
                request.Content = new ByteArrayContent(chunk);
                request.Content.Headers.ContentRange = new ContentRangeHeaderValue(i, i + chunk.Length - 1, totalLength);
                request.Content.Headers.ContentLength = chunk.Length;

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to upload chunk: {await response.Content.ReadAsStringAsync()}");
                }

                if (i + chunk.Length >= totalLength)
                {
                    var finalResponse = await response.Content.ReadAsStringAsync();
                    var finalData = JsonSerializer.Deserialize<JsonDocument>(finalResponse);

                    var shareLink = finalData.RootElement.GetProperty("link").GetProperty("webUrl").GetString();

                    // Convert the sharing link to a direct download link
                    shareLink = shareLink.Replace("?web=1", "?download=1");

                    var fileId = finalData.RootElement.GetProperty("id").GetString();

                    return (shareLink, fileId);
                }
            }

            throw new Exception("Failed to complete file upload");
        }
    }

}
