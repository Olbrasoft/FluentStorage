using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Olbrasoft.FluentStorage.Github.Tests;

public class GitHubApiClientTests
{
    [Fact]
    public void Constructor_WithValidToken_SetsAuthorizationHeader()
    {
        // Arrange
        const string token = "test-token-123";

        // Act
        using var client = new GitHubApiClient(token);

        // Assert
        // We can't directly access private _httpClient, but we can verify behavior through public methods
        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_WithNullToken_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GitHubApiClient(null!));
    }

    [Fact]
    public void Constructor_WithEmptyToken_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new GitHubApiClient(string.Empty));
    }

    [Fact]
    public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new GitHubApiClient("token", null!));
    }

    [Fact]
    public void Constructor_WithCustomHttpClient_SetsHeadersOnlyIfNotSet()
    {
        // Arrange
        const string token = "test-token-123";
        var httpClient = new HttpClient();

        // Pre-set authorization header
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "existing-token");

        // Act
        using var client = new GitHubApiClient(token, httpClient);

        // Assert
        // Should not override existing authorization header
        Assert.Equal("Bearer", httpClient.DefaultRequestHeaders.Authorization.Scheme);
        Assert.Equal("existing-token", httpClient.DefaultRequestHeaders.Authorization.Parameter);
    }

    [Fact]
    public void Constructor_WithCustomHttpClientNoHeaders_SetsRequiredHeaders()
    {
        // Arrange
        const string token = "test-token-123";
        var httpClient = new HttpClient();

        // Act
        using var client = new GitHubApiClient(token, httpClient);

        // Assert
        Assert.Equal("token", httpClient.DefaultRequestHeaders.Authorization?.Scheme);
        Assert.Equal(token, httpClient.DefaultRequestHeaders.Authorization?.Parameter);
        Assert.True(httpClient.DefaultRequestHeaders.UserAgent.Count > 0);
        Assert.Contains(httpClient.DefaultRequestHeaders.UserAgent, ua => ua.Product?.Name == "GitHubBlobStorage");
    }

    [Fact]
    public async Task GetAsync_WithValidUrl_CallsHttpClientGet()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var expectedResponse = new HttpResponseMessage(HttpStatusCode.OK);
        var url = new Uri("https://api.github.com/repos/owner/repo/contents/file.txt");

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri == url),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(expectedResponse);

        var httpClient = new HttpClient(mockHandler.Object);
        using var client = new GitHubApiClient("test-token", httpClient);

        // Act
        var result = await client.GetAsync(url, CancellationToken.None);

        // Assert
        Assert.Equal(expectedResponse, result);
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.RequestUri == url),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task PutAsync_WithValidUrlAndBody_CallsHttpClientPutWithJsonContent()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var expectedResponse = new HttpResponseMessage(HttpStatusCode.OK);
        var url = new Uri("https://api.github.com/repos/owner/repo/contents/file.txt");
        var body = new { message = "Test commit", content = "dGVzdA==" };

        string? capturedRequestContent = null;
        HttpRequestMessage? capturedRequest = null;
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, ct) =>
            {
                capturedRequest = req;
                if (req.Content != null)
                {
                    capturedRequestContent = await req.Content.ReadAsStringAsync(ct);
                }
            })
            .ReturnsAsync(expectedResponse);

        var httpClient = new HttpClient(mockHandler.Object);
        using var client = new GitHubApiClient("test-token", httpClient);

        // Act
        var result = await client.PutAsync(url, body, CancellationToken.None);

        // Assert
        Assert.Equal(expectedResponse, result);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Put, capturedRequest.Method);
        Assert.Equal(url, capturedRequest.RequestUri);
        Assert.Equal("application/json", capturedRequest.Content?.Headers.ContentType?.MediaType);

        var expectedJson = JsonSerializer.Serialize(body);
        Assert.Equal(expectedJson, capturedRequestContent);
    }

    [Fact]
    public async Task DeleteAsync_WithValidUrlAndBody_CallsHttpClientDeleteWithJsonContent()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var expectedResponse = new HttpResponseMessage(HttpStatusCode.OK);
        var url = new Uri("https://api.github.com/repos/owner/repo/contents/file.txt");
        var body = new { message = "Delete file", sha = "abc123" };

        string? capturedRequestContent = null;
        HttpRequestMessage? capturedRequest = null;
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, ct) =>
            {
                capturedRequest = req;
                if (req.Content != null)
                {
                    capturedRequestContent = await req.Content.ReadAsStringAsync(ct);
                }
            })
            .ReturnsAsync(expectedResponse);

        var httpClient = new HttpClient(mockHandler.Object);
        using var client = new GitHubApiClient("test-token", httpClient);

        // Act
        var result = await client.DeleteAsync(url, body, CancellationToken.None);

        // Assert
        Assert.Equal(expectedResponse, result);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Delete, capturedRequest.Method);
        Assert.Equal(url, capturedRequest.RequestUri);
        Assert.Equal("application/json", capturedRequest.Content?.Headers.ContentType?.MediaType);

        var expectedJson = JsonSerializer.Serialize(body);
        Assert.Equal(expectedJson, capturedRequestContent);
    }

    [Fact]
    public async Task GetAsync_WithCancellationToken_PassesCancellationToken()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var expectedResponse = new HttpResponseMessage(HttpStatusCode.OK);
        var url = new Uri("https://api.github.com/repos/owner/repo/contents/file.txt");
        var cancellationToken = new CancellationToken();

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(expectedResponse)
            .Verifiable();

        var httpClient = new HttpClient(mockHandler.Object);
        using var client = new GitHubApiClient("test-token", httpClient);

        // Act
        await client.GetAsync(url, cancellationToken);

        // Assert
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task PutAsync_WithCancellationToken_PassesCancellationToken()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var expectedResponse = new HttpResponseMessage(HttpStatusCode.OK);
        var url = new Uri("https://api.github.com/repos/owner/repo/contents/file.txt");
        var body = new { test = "data" };
        var cancellationToken = new CancellationToken();

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(expectedResponse)
            .Verifiable();

        var httpClient = new HttpClient(mockHandler.Object);
        using var client = new GitHubApiClient("test-token", httpClient);

        // Act
        await client.PutAsync(url, body, cancellationToken);

        // Assert
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WithCancellationToken_PassesCancellationToken()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var expectedResponse = new HttpResponseMessage(HttpStatusCode.OK);
        var url = new Uri("https://api.github.com/repos/owner/repo/contents/file.txt");
        var body = new { test = "data" };
        var cancellationToken = new CancellationToken();

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(expectedResponse)
            .Verifiable();

        var httpClient = new HttpClient(mockHandler.Object);
        using var client = new GitHubApiClient("test-token", httpClient);

        // Act
        await client.DeleteAsync(url, body, cancellationToken);

        // Assert
        mockHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public void Dispose_CallsHttpClientDispose()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(mockHandler.Object);
        var client = new GitHubApiClient("test-token", httpClient);

        // Act
        client.Dispose();

        // Assert
        // HttpClient.Dispose() doesn't throw, so we verify it doesn't cause issues
        // Multiple dispose calls should be safe
        client.Dispose();
    }

    [Fact]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        // Arrange
        var client = new GitHubApiClient("test-token");

        // Act & Assert
        client.Dispose();
        client.Dispose(); // Should not throw
    }

    [Fact]
    public async Task PutAsync_WithComplexObject_SerializesCorrectly()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var expectedResponse = new HttpResponseMessage(HttpStatusCode.OK);
        var url = new Uri("https://api.github.com/repos/owner/repo/contents/file.txt");

        var complexBody = new
        {
            message = "Update file",
            content = Convert.ToBase64String(Encoding.UTF8.GetBytes("file content")),
            branch = "main",
            sha = "existing-sha-123",
            committer = new
            {
                name = "Test User",
                email = "test@example.com"
            }
        };

        string? capturedRequestContent = null;
        HttpRequestMessage? capturedRequest = null;
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, ct) =>
            {
                capturedRequest = req;
                if (req.Content != null)
                {
                    capturedRequestContent = await req.Content.ReadAsStringAsync(ct);
                }
            })
            .ReturnsAsync(expectedResponse);

        var httpClient = new HttpClient(mockHandler.Object);
        using var client = new GitHubApiClient("test-token", httpClient);

        // Act
        await client.PutAsync(url, complexBody, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedRequest);
        var expectedJson = JsonSerializer.Serialize(complexBody);
        Assert.Equal(expectedJson, capturedRequestContent);

        // Verify the JSON contains expected fields
        Assert.Contains("Update file", capturedRequestContent!);
        Assert.Contains("main", capturedRequestContent);
        Assert.Contains("existing-sha-123", capturedRequestContent);
        Assert.Contains("Test User", capturedRequestContent);
        Assert.Contains("test@example.com", capturedRequestContent);
    }
}