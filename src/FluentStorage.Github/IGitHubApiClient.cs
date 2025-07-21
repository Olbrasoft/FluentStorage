namespace Olbrasoft.FluentStorage.Github;

/// <summary>
/// Interface for GitHub API client operations.
/// </summary>
public interface IGitHubApiClient : IDisposable
{
    /// <summary>
    /// Performs a GET request to the specified URL.
    /// </summary>
    /// <param name="url">The URL to send the GET request to.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The HTTP response message.</returns>
    Task<HttpResponseMessage> GetAsync(Uri url, CancellationToken cancellationToken);

    /// <summary>
    /// Performs a PUT request to the specified URL with the given body.
    /// </summary>
    /// <param name="url">The URL to send the PUT request to.</param>
    /// <param name="body">The request body object to serialize as JSON.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The HTTP response message.</returns>
    Task<HttpResponseMessage> PutAsync(Uri url, object body, CancellationToken cancellationToken);

    /// <summary>
    /// Performs a DELETE request to the specified URL with the given body.
    /// </summary>
    /// <param name="url">The URL to send the DELETE request to.</param>
    /// <param name="body">The request body object to serialize as JSON.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The HTTP response message.</returns>
    Task<HttpResponseMessage> DeleteAsync(Uri url, object body, CancellationToken cancellationToken);
}
