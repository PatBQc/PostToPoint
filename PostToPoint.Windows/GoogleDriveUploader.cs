using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using System.IO;
using System.Security;

namespace PostToPoint.Windows
{
    internal class GoogleDriveUploader
    {
        public static string UploadAndShareFile(string filename, string folderId, string credentialFilename)
        {
            ArgumentNullException.ThrowIfNull(filename);
            ArgumentNullException.ThrowIfNull(folderId);
            ArgumentNullException.ThrowIfNull(credentialFilename);

            if (!File.Exists(filename))
            {
                throw new FileNotFoundException("Input file not found", filename);
            }

            if (!File.Exists(credentialFilename))
            {
                throw new FileNotFoundException("Credential file not found", credentialFilename);
            }

            // Load service account credentials
            var credential = GoogleCredential.FromFile(credentialFilename)
                .CreateScoped(DriveService.ScopeConstants.Drive);

            // Create Drive API service
            var service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Drive API Service Account Example",
            });

            // File metadata
            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = Path.GetFileName(filename),
                Parents = new List<string> { folderId }
            };

            // File upload
            using (var fsSource = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                var request = service.Files.Create(fileMetadata, fsSource, "video/mp4");
                request.Fields = "id";
                var result = request.Upload();

                if (result.Status == UploadStatus.Failed)
                {
                    var errorMessage = result.Exception?.Message ?? "Unknown error";
                    Console.WriteLine($"Error uploading file: {errorMessage}");
                    throw new Exception($"Google Drive file upload failed: {errorMessage}");
                }

                var fileId = request.ResponseBody?.Id;
                if (string.IsNullOrEmpty(fileId))
                {
                    throw new InvalidOperationException("File upload succeeded but no file ID was returned");
                }

                Console.WriteLine($"File ID: {fileId}");

                // Set file permissions to make it publicly accessible
                var permission = new Google.Apis.Drive.v3.Data.Permission
                {
                    Type = "anyone",
                    Role = "reader"
                };

                try
                {
                    service.Permissions.Create(permission, fileId).Execute();
                    Console.WriteLine("File shared publicly successfully");

                    string directDownloadLink = $"https://drive.google.com/uc?export=download&id={fileId}";

                    Console.WriteLine($"Direct Download Link: {directDownloadLink}");
                    return directDownloadLink;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error setting permissions: {ex.Message}");
                    throw new SecurityException($"Error setting permissions for file {fileId}", ex);
                }
            }
        }
    }
}
