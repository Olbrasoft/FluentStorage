using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Olbrasoft.FluentStorage.Github
{
    public class GitHubApiClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private bool _disposed;

        public GitHubApiClient(string token)
            : this(token, new HttpClient())
        {
        }

        public GitHubApiClient(string token, HttpClient client)
        {
            if (string.IsNullOrEmpty(token)) throw new ArgumentNullException(nameof(token));
            _httpClient = client ?? throw new ArgumentNullException(nameof(client));

            // Set headers only if not already set (for testability)
            if (_httpClient.DefaultRequestHeaders.Authorization == null)
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", token);

            if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
                _httpClient.DefaultRequestHeaders.UserAgent.Add(ProductInfoHeaderValue.Parse("GitHubBlobStorage"));
        }

        public async Task<HttpResponseMessage> GetAsync(Uri url, CancellationToken cancellationToken)
        {
            return await _httpClient.GetAsync(url, cancellationToken);
        }

        public async Task<HttpResponseMessage> PutAsync(Uri url, object body, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            return await _httpClient.PutAsync(url, content, cancellationToken);
        }

        public async Task<HttpResponseMessage> DeleteAsync(Uri url, object body, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Delete, url)
            {
                Content = content
            };
            return await _httpClient.SendAsync(request, cancellationToken);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                _httpClient.Dispose();
            }
            _disposed = true;
        }
    }
}
