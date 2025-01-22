using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;



namespace PostToPoint.Windows
{

    public class WebhookData
    {
        public string PostBluesky { get; set; } = string.Empty;
        public string PostTwitter { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string ImageLink { get; set; } = string.Empty;
        public string VideoLink { get; set; } = string.Empty;
    }


    public class ZapierWebhook : IDisposable
    {
        private readonly string webhookUrl;
        private readonly HttpClient client;
        private bool disposed = false;

        public ZapierWebhook(string webhookUrl)
        {
            this.webhookUrl = webhookUrl ?? throw new ArgumentNullException(nameof(webhookUrl));
            this.client = new HttpClient();
        }

        public async Task<bool> SendToWebhook(string postBluesky, string postTwitter, string link, string imageLink, string videoLink)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(ZapierWebhook));
            }

            try
            {
                var webhookData = new WebhookData
                {
                    PostBluesky = postBluesky ?? string.Empty,
                    PostTwitter = postTwitter ?? string.Empty,
                    Link = link ?? string.Empty,
                    ImageLink = imageLink ?? string.Empty,
                    VideoLink = videoLink ?? string.Empty
                };

                var json = JsonSerializer.Serialize(webhookData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(webhookUrl, content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                // Add proper logging here
                Console.WriteLine($"Error sending to webhook: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    client.Dispose();
                }

                disposed = true;
            }
        }
    }
}
