using FluentStorage;
using FluentStorage.Blobs;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Olbrasoft.FluentStorage.Github;

/// <summary>
/// Implementation of <see cref="IBlobStorage"/> that uses GitHub repository as a blob storage.
/// </summary>
public class GitHubBlobStorage : IBlobStorage, IDisposable
{
    private readonly IGitHubApiClient _apiClient;
    private readonly IGitHubUrlBuilder _urlBuilder;
    private readonly string _branch;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubBlobStorage"/> class.
    /// </summary>
    /// <param name="apiClient">The GitHub API client for making HTTP requests.</param>
    /// <param name="urlBuilder">The URL builder for constructing GitHub API URLs.</param>
    /// <exception cref="ArgumentNullException">Thrown when apiClient or urlBuilder is null.</exception>
    public GitHubBlobStorage(IGitHubApiClient apiClient, IGitHubUrlBuilder urlBuilder)
    {
        ArgumentNullException.ThrowIfNull(apiClient);
        ArgumentNullException.ThrowIfNull(urlBuilder);

        _apiClient = apiClient;
        _urlBuilder = urlBuilder;
        _branch = _urlBuilder.Branch;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubBlobStorage"/> class with repository parameters.
    /// </summary>
    /// <param name="owner">The GitHub repository owner.</param>
    /// <param name="repo">The GitHub repository name.</param>
    /// <param name="branch">The branch name to work with.</param>
    /// <param name="token">The GitHub personal access token.</param>
    /// <exception cref="ArgumentNullException">Thrown when branch is null.</exception>
    /// <exception cref="ArgumentException">Thrown when owner, repo, or token parameters are invalid.</exception>
    public GitHubBlobStorage(string owner, string repo, string branch, string token)
    {
        ArgumentNullException.ThrowIfNull(branch);

        _branch = branch;
        _urlBuilder = new GitHubUrlBuilder(owner, repo, branch);
        _apiClient = new GitHubApiClient(token);
    }

    /// <summary>
    /// Deletes a blob by its full path.
    /// </summary>
    /// <param name="fullPath">The full path to the blob.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when fullPath is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when deletion fails.</exception>
    public async Task DeleteAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(fullPath)) throw new ArgumentException("Full path cannot be null or empty", nameof(fullPath));

        var url = _urlBuilder.BuildFileUrl(fullPath);

        // Retry logic for SHA conflicts (max 3 attempts)
        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            // Get current file info to get the latest SHA
            var getResponse = await _apiClient.GetAsync(url, cancellationToken);
            if (!getResponse.IsSuccessStatusCode)
            {
                // If the file doesn't exist, we simply skip it
                return;
            }

            var getFileContent = await getResponse.Content.ReadAsStringAsync(cancellationToken);
            var fileInfo = JsonSerializer.Deserialize<GitHubFileResponse>(getFileContent) ?? throw new InvalidOperationException("Failed to deserialize GitHub file info");

            // Prepare delete request with current SHA
            var deleteRequestBody = new
            {
                message = $"Delete {fullPath}",
                sha = fileInfo.Sha,
                branch = _branch
            };

            var deleteResponse = await _apiClient.DeleteAsync(url, deleteRequestBody, cancellationToken);

            if (deleteResponse.IsSuccessStatusCode)
            {
                // Success - file deleted
                return;
            }

            if (deleteResponse.StatusCode == HttpStatusCode.Conflict && attempt < maxRetries)
            {
                // SHA conflict - retry with a small delay
                await Task.Delay(100 * attempt, cancellationToken); // Progressive delay: 100ms, 200ms, 300ms
                continue;
            }

            if (deleteResponse.StatusCode == HttpStatusCode.NotFound)
            {
                // File doesn't exist - this is not an error for delete operation
                return;
            }

            // Other error or max retries reached
            var error = await deleteResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Error deleting file from GitHub after {attempt} attempts: {deleteResponse.StatusCode}, {error}");
        }
    }

    /// <summary>
    /// Deletes multiple blobs by their full paths.
    /// </summary>
    /// <param name="fullPaths">The collection of full paths to the blobs.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fullPaths is null.</exception>
    public async Task DeleteAsync(IEnumerable<string> fullPaths, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fullPaths);

        // Convert to list to avoid multiple enumeration
        var pathsList = fullPaths.ToList();
        foreach (var path in pathsList)
        {
            await DeleteAsync(path, cancellationToken);
        }
    }

    /// <summary>
    /// Checks if blobs exist at the specified paths.
    /// </summary>
    /// <param name="fullPaths">The collection of full paths to check.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of boolean values indicating the existence of each blob.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fullPaths is null.</exception>
    public async Task<IReadOnlyCollection<bool>> ExistsAsync(IEnumerable<string> fullPaths, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fullPaths);

        var results = new List<bool>();
        // Convert to list to avoid multiple enumeration
        var pathsList = fullPaths.ToList();

        foreach (var fullPath in pathsList)
        {
            var url = _urlBuilder.BuildFileUrl(fullPath);
            var response = await _apiClient.GetAsync(url, cancellationToken);
            results.Add(response.IsSuccessStatusCode);
        }

        return results.AsReadOnly();
    }

    /// <summary>
    /// Gets blob information for the specified paths.
    /// </summary>
    /// <param name="fullPaths">The collection of full paths to the blobs.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of blob information.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fullPaths is null.</exception>
    /// <exception cref="ArgumentException">Thrown when fullPaths is empty.</exception>
    public async Task<IReadOnlyCollection<Blob>> GetBlobsAsync(IEnumerable<string> fullPaths, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fullPaths);

        // Convert to list immediately to avoid multiple enumeration
        var pathsList = fullPaths.ToList();

        if (pathsList.Count == 0)
            throw new ArgumentException("Collection cannot be empty", nameof(fullPaths));

        var blobs = new List<Blob>();

        foreach (var fullPath in pathsList)
        {
            var url = _urlBuilder.BuildFileUrl(fullPath);
            using var response = await _apiClient.GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var fileResponse = JsonSerializer.Deserialize<GitHubFileResponse>(content);

                var blob = new Blob(fullPath);
                if (fileResponse != null)
                {
                    blob.Size = fileResponse.Size;
                    blob.MD5 = fileResponse.Md5 ?? string.Empty;
                }
                blobs.Add(blob);
            }
            else
            {
                blobs.Add(new Blob(fullPath));
            }
        }

        return blobs.AsReadOnly();
    }

    /// <summary>
    /// Lists blobs in the storage based on the provided options.
    /// </summary>
    /// <param name="options">The listing options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of blobs matching the criteria.</returns>
    /// <exception cref="InvalidOperationException">Thrown when listing operation fails.</exception>
    public async Task<IReadOnlyCollection<Blob>> ListAsync(ListOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new ListOptions();

        string path;
        if (options.FolderPath != null)
        {
            path = options.FolderPath;
        }
        else
        {
            path = string.Empty;
        }

        var blobs = new List<Blob>();

        await ListInternalAsync(path, options, blobs, cancellationToken).ConfigureAwait(false);

        return blobs.AsReadOnly();
    }

    /// <summary>
    /// Recursively lists files and directories from GitHub repository.
    /// </summary>
    /// <param name="currentPath">The current path being processed.</param>
    /// <param name="options">The listing options including recursion and filtering.</param>
    /// <param name="blobs">The collection to add found blobs to.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when GitHub API returns an error.</exception>
    private async Task ListInternalAsync(string currentPath, ListOptions options, List<Blob> blobs, CancellationToken cancellationToken)
    {
        // Removed redundant null checks that are guaranteed by the caller
        var url = _urlBuilder.BuildFileUrl(currentPath);
        var response = await _apiClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"An error listing files from GitHub: {response.StatusCode}, {error}");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var fileResponses = JsonSerializer.Deserialize<List<GitHubFileResponse>>(content);

        if (fileResponses == null || fileResponses.Count == 0)
        {
            return;
        }

        foreach (var file in fileResponses)
        {
            switch (file.Type)
            {
                case "file":
                    {
                        var fullPath = file.Path;
                        if (!options.IsMatch(fullPath)) continue;

                        blobs.Add(new Blob(fullPath)
                        {
                            Size = file.Size,
                            MD5 = file.Md5 ?? string.Empty
                        });
                        break;
                    }
                case "dir" when options.Recurse:
                    await ListInternalAsync(file.Path, options, blobs, cancellationToken);
                    break;
            }
        }
    }

    /// <summary>
    /// Opens a transaction.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="NotSupportedException">Always thrown as transactions are not supported by GitHub Blob Storage.</exception>
    public Task<ITransaction> OpenTransactionAsync()
    {
        // GitHub API doesn't support transactions directly
        throw new NotSupportedException("Transactions are not supported with GitHub Blob Storage");
    }

    /// <summary>
    /// Opens a blob for reading.
    /// </summary>
    /// <param name="fullPath">The full path to the blob.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A stream for reading the blob content, or null if the blob does not exist.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fullPath is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when an error occurs while accessing the GitHub file.</exception>
    public async Task<Stream?> OpenReadAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(fullPath))
            throw new ArgumentNullException(nameof(fullPath));

        var url = _urlBuilder.BuildFileUrl(fullPath);
        try
        {
            var response = await _apiClient.GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrEmpty(jsonResponse))
                {
                    return null;
                }
                var githubFile = JsonSerializer.Deserialize<GitHubFileResponse>(jsonResponse);
                if (githubFile?.Content == null)
                {
                    return null;
                }
                // Decode content from base64
                var contentBytes = Convert.FromBase64String(githubFile.Content);
                return new MemoryStream(contentBytes);
            }
            // If status code is 404, return null, same as Azure Blob Storage for non-existent blob
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("An error occurred while accessing the GitHub file.", ex);
        }
        return null;
    }

    /// <summary>
    /// Sets metadata on existing blobs.
    /// </summary>
    /// <param name="blobs">The collection of blobs with metadata to set.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="NotSupportedException">Always thrown as GitHub Blob Storage does not support setting blob metadata.</exception>
    public Task SetBlobsAsync(IEnumerable<Blob> blobs, CancellationToken cancellationToken = default)
    {
        // Prevent compiler warning about possible multiple enumeration
        // Even though this code will never execute due to the exception
        ArgumentNullException.ThrowIfNull(blobs);

        // GitHub API doesn't directly support setting just blob metadata
        throw new NotSupportedException("Setting blob metadata only is not supported with GitHub Blob Storage");
    }

    /// <summary>
    /// Writes data to a blob.
    /// </summary>
    /// <param name="fullPath">The full path to the blob.</param>
    /// <param name="dataStream">The stream containing data to write.</param>
    /// <param name="append">If true, appends data to the existing blob; otherwise, overwrites the blob.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fullPath or dataStream is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when writing to GitHub fails.</exception>
    public async Task WriteAsync(string fullPath, Stream dataStream, bool append = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(fullPath))
            throw new ArgumentNullException(nameof(fullPath));

        ArgumentNullException.ThrowIfNull(dataStream);

        byte[] newFileBytes;
        using (var memoryStream = new MemoryStream())
        {
            await dataStream.CopyToAsync(memoryStream, cancellationToken);
            newFileBytes = memoryStream.ToArray();
        }

        var url = _urlBuilder.BuildFileUrl(fullPath);

        // Retry logic for SHA conflicts (max 3 attempts)
        const int maxRetries = 3;
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            var finalContentBytes = newFileBytes;
            string? existingSha = null;

            var existingFileResponse = await _apiClient.GetAsync(url, cancellationToken);
            if (existingFileResponse.IsSuccessStatusCode)
            {
                var existingFileJson = await existingFileResponse.Content.ReadAsStringAsync(cancellationToken);
                var existingFile = JsonSerializer.Deserialize<GitHubFileResponse>(existingFileJson);
                existingSha = existingFile?.Sha;

                if (append && existingFile?.Content != null)
                {
                    // Append new content to the existing file content
                    var existingContentBytes = Convert.FromBase64String(existingFile.Content.Replace("\n", string.Empty, StringComparison.Ordinal));
                    using var combinedStream = new MemoryStream();
                    combinedStream.Write(existingContentBytes, 0, existingContentBytes.Length);
                    combinedStream.Write(newFileBytes, 0, newFileBytes.Length);
                    finalContentBytes = combinedStream.ToArray();
                }
                else
                {
                    // For overwrite, use new content
                    finalContentBytes = newFileBytes;
                }
            }
            else if (append)
            {
                // If append and file does not exist, behave as create
                finalContentBytes = newFileBytes;
                existingSha = null;
            }
            else
            {
                // For create/overwrite when file doesn't exist
                finalContentBytes = newFileBytes;
                existingSha = null;
            }

            var content = Convert.ToBase64String(finalContentBytes);
            var requestBody = new
            {
                message = append ? "Append to existing file" : "Create or overwrite file",
                content,
                branch = _branch,
                sha = existingSha // null if the file does not exist
            };

            var response = await _apiClient.PutAsync(url, requestBody, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                // Success - file written
                return;
            }

            if (response.StatusCode == HttpStatusCode.Conflict && attempt < maxRetries)
            {
                // SHA conflict - retry with a small delay
                await Task.Delay(100 * attempt, cancellationToken); // Progressive delay: 100ms, 200ms, 300ms
                continue;
            }

            // Other error or max retries reached
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Error uploading file to GitHub after {attempt} attempts: {response.StatusCode}, {error}");
        }
    }

    // ...existing code...

    /// <summary>
    /// Disposes the resources used by this instance.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="GitHubBlobStorage"/> and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _apiClient.Dispose();
        }
        _disposed = true;
    }

    /// <summary>
    /// Model class for GitHub file API response.
    /// </summary>
    private class GitHubFileResponse
    {
        /// <summary>
        /// Gets or sets the base64-encoded content of the file.
        /// </summary>
        [JsonPropertyName("content")]
        public string? Content { get; init; }

        /// <summary>
        /// Gets or sets the SHA hash of the file.
        /// </summary>
        [JsonPropertyName("sha")]
        public string Sha { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the path of the file.
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the size of the file in bytes.
        /// </summary>
        [JsonPropertyName("size")]
        public long? Size { get; init; }

        /// <summary>
        /// Gets or sets the MD5 hash of the file.
        /// </summary>
        [JsonPropertyName("md5")]
        public string? Md5 { get; init; }

        /// <summary>
        /// Gets or sets the type of the entry (file or dir).
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;
    }
}
