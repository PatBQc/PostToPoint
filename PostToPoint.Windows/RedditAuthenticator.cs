﻿using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace PostToPoint.Windows
{
    public class RedditAuthenticator
    {
        private const string TokenUrl = "https://www.reddit.com/api/v1/access_token";
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _username;
        private readonly string _password;
        private readonly string _userAgent;
        private readonly string _redirectUri;

        public RedditAuthenticator(string clientId, string clientSecret, string username, string password, string userAgent, string redirectUri)
        {
            ArgumentNullException.ThrowIfNull(clientId);
            ArgumentNullException.ThrowIfNull(clientSecret);
            ArgumentNullException.ThrowIfNull(username);
            ArgumentNullException.ThrowIfNull(password);
            ArgumentNullException.ThrowIfNull(userAgent);
            ArgumentNullException.ThrowIfNull(redirectUri);

            _clientId = clientId;
            _clientSecret = clientSecret;
            _username = username;
            _password = password;
            _userAgent = userAgent;
            _redirectUri = redirectUri;
        }

        public async Task<string> GetAccessTokenAsync()
        {
            using (var client = new HttpClient())
            {
                var authHeader = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_clientId}:{_clientSecret}"));
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authHeader);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(_userAgent);

                var content = new FormUrlEncodedContent(new[]
                {
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("username", _username),
                new KeyValuePair<string, string>("password", _password),
                new KeyValuePair<string, string>("redirect_uri", _redirectUri)
            });

                var response = await client.PostAsync(TokenUrl, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = JsonSerializer.Deserialize<RedditTokenResponse>(responseContent) 
                        ?? throw new InvalidOperationException("Failed to deserialize token response");
                    return tokenResponse.access_token ?? throw new InvalidOperationException("Access token is null");
                }
                else
                {
                    throw new Exception($"Failed to obtain refresh token. Status code: {response.StatusCode}, Response: {responseContent}");
                }
            }
        }
    }
}
