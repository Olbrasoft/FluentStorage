
using System.Net;
using Moq;
using Moq.Protected;



namespace Olbrasoft.FluentStorage.Github.Tests
{
    public class GitHubApiClientTests
    {

        [Fact]
        public async Task GetAsync_ReturnsResponse()
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            var client = new HttpClient(handlerMock.Object);
            var apiClient = new GitHubApiClientStub("token", client);
            var response = await apiClient.GetAsync(new Uri("https://example.com"), CancellationToken.None);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task PutAsync_SendsCorrectRequest()
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Put),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Created));

            var client = new HttpClient(handlerMock.Object);
            var apiClient = new GitHubApiClientStub("token", client);
            var response = await apiClient.PutAsync(new Uri("https://example.com"), new { foo = "bar" }, CancellationToken.None);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task DeleteAsync_SendsCorrectRequest()
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Delete),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NoContent));

            var client = new HttpClient(handlerMock.Object);
            var apiClient = new GitHubApiClientStub("token", client);
            var response = await apiClient.DeleteAsync(new Uri("https://example.com"), new { foo = "bar" }, CancellationToken.None);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenTokenIsNull()
        {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            var client = new HttpClient();
            Assert.Throws<ArgumentNullException>(() => new GitHubApiClient(null, client));
#pragma warning restore CS8625
        }

        [Fact]
        public void Constructor_ThrowsArgumentNullException_WhenClientIsNull()
        {                                                                                          // Helper stub to inject HttpClient
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            Assert.Throws<ArgumentNullException>(() => new GitHubApiClient("token", null));
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        }

        private class GitHubApiClientStub : GitHubApiClient
        {
            public GitHubApiClientStub(string token, HttpClient client) : base(token, client) { }
        }

        [Fact]
        public void Dispose_CanBeCalledMultipleTimesWithoutException()
        {
            var client = new HttpClient();
            var apiClient = new GitHubApiClient("token", client);
            apiClient.Dispose();
            apiClient.Dispose(); // Should not throw
        }
    }

}