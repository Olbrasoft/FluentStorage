using FluentStorage;
using FluentStorage.Blobs;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Olbrasoft.FluentStorage.Github;

/// <summary>
/// Implementation of <see cref="IBlobStorage"/> that uses GitHub repository as a blob storage.
/// </summary>
public class GitHubBlobStorage : IBlobStorage
{
    private readonly string _owner;
    private readonly string _repo;
    private readonly string _branch;
    private readonly string _token;
    private readonly HttpClient _httpClient;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubBlobStorage"/> class.
    /// </summary>
    /// <param name="owner">The GitHub repository owner (username or organization).</param>
    /// <param name="repo">The GitHub repository name.</param>
    /// <param name="branch">The branch to use (e.g., "main" or "master").</param>
    /// <param name="token">The GitHub personal access token with repository access.</param>
    public GitHubBlobStorage(string owner, string repo, string branch, string token)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _branch = branch ?? throw new ArgumentNullException(nameof(branch));
        _token = token ?? throw new ArgumentNullException(nameof(token));
        _httpClient = new HttpClient
        {
            DefaultRequestHeaders =
            {
                Authorization = new AuthenticationHeaderValue("token", _token),
                UserAgent = { ProductInfoHeaderValue.Parse("GitHubBlobStorage") }
            }
        };
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

        var url = GetGitHubFileUrl(fullPath);

        // Get file info to get SHA
        var getResponse = await _httpClient.GetAsync(url, cancellationToken);
        if (!getResponse.IsSuccessStatusCode)
        {
            // If the file doesn't exist, we simply skip it
            return;
        }

        var getFileContent = await getResponse.Content.ReadAsStringAsync(cancellationToken);
        var fileInfo = JsonSerializer.Deserialize<GitHubFileResponse>(getFileContent);
        
        if (fileInfo == null)
        {
            throw new InvalidOperationException("Failed to deserialize GitHub file info");
        }

        // Prepare delete request
        var deleteRequestBody = new
        {
            message = $"Delete {fullPath}",
            sha = fileInfo.Sha,
            branch = _branch
        };

        var deleteJsonRequestBody = JsonSerializer.Serialize(deleteRequestBody);
        var deleteContent = new StringContent(deleteJsonRequestBody, Encoding.UTF8, "application/json");

        var requestMessage = new HttpRequestMessage(HttpMethod.Delete, url)
        {
            Content = deleteContent
        };

        var deleteResponse = await _httpClient.SendAsync(requestMessage, cancellationToken);

        if (!deleteResponse.IsSuccessStatusCode)
        {
            var error = await deleteResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Error deleting file from GitHub: {deleteResponse.StatusCode}, {error}");
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

        foreach (var url in pathsList.Select(GetGitHubFileUrl))
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
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
            var url = GetGitHubFileUrl(fullPath);

            using var response = await _httpClient.GetAsync(url, cancellationToken);
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

        string path = options.FolderPath ?? string.Empty;
        var blobs = new List<Blob>();

        await ListInternalAsync(path, options, blobs, cancellationToken);
        
        return blobs.AsReadOnly();
    }

    private async Task ListInternalAsync(string currentPath, ListOptions options, List<Blob> blobs, CancellationToken cancellationToken)
    {
        // Removed redundant null checks that are guaranteed by the caller
        var url = GetGitHubFileUrl(currentPath);
        var response = await _httpClient.GetAsync(url, cancellationToken);

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

        var url = GetGitHubFileUrl(fullPath);

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);

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
    /// <exception cref="NotSupportedException">Always thrown as setting blob metadata is not supported by GitHub Blob Storage.</exception>
    public Task SetBlobsAsync(IEnumerable<Blob> blobs, CancellationToken cancellationToken = default)
    {
        // Prevent compiler warning about possible multiple enumeration
        // Even though this code will never execute due to the exception
        if (blobs == null)
            throw new ArgumentNullException(nameof(blobs));

        // GitHub API doesn't directly support setting just blob metadata
        throw new NotSupportedException("Setting blob metadata only is not supported with GitHub Blob Storage");
    }

    /// <summary>
    /// Writes data to a blob.
    /// </summary>
    /// <param name="fullPath">The full path to the blob.</param>
    /// <param name="dataStream">The stream containing data to write.</param>
    /// <param name="append">Whether to append to existing data (not fully supported in GitHub).</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fullPath or dataStream is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when writing to GitHub fails.</exception>
    public async Task WriteAsync(string fullPath, Stream dataStream, bool append = false, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(fullPath))
            throw new ArgumentNullException(nameof(fullPath));

        ArgumentNullException.ThrowIfNull(dataStream);

        // Initialize the variable directly at declaration to prevent warning
        byte[] fileBytes;
        using (var memoryStream = new MemoryStream())
        {
            await dataStream.CopyToAsync(memoryStream, token);
            fileBytes = memoryStream.ToArray();
        }

        var content = Convert.ToBase64String(fileBytes);
        var url = GetGitHubFileUrl(fullPath);

        var existingFileResponse = await _httpClient.GetAsync(url, token);

        if (existingFileResponse.IsSuccessStatusCode)
        {
            var existingFileJson = await existingFileResponse.Content.ReadAsStringAsync(token);
            var existingFile = JsonSerializer.Deserialize<GitHubFileResponse>(existingFileJson);

            if (existingFile == null)
            {
                throw new InvalidOperationException("Failed to deserialize existing GitHub file info");
            }

            var deleteRequestBody = new
            {
                message = "Delete existing file to replace with a new one",
                sha = existingFile.Sha,
                branch = _branch
            };

            var deleteJsonRequestBody = JsonSerializer.Serialize(deleteRequestBody);
            var deleteContent = new StringContent(deleteJsonRequestBody, Encoding.UTF8, "application/json");
            var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, url)
            {
                Content = deleteContent
            };

            var deleteResponse = await _httpClient.SendAsync(deleteRequest, token);

            if (!deleteResponse.IsSuccessStatusCode)
            {
                var error = await deleteResponse.Content.ReadAsStringAsync(token);
                throw new InvalidOperationException($"Error deleting file from GitHub: {deleteResponse.StatusCode}, {error}");
            }
        }

        var requestBody = new
        {
            message = append ? "Append to existing file" : "Create or overwrite file",
            content,
            branch = _branch
        };

        var jsonRequestBody = JsonSerializer.Serialize(requestBody);
        var contentString = new StringContent(jsonRequestBody, Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync(url, contentString, token);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(token);
            throw new InvalidOperationException($"Error uploading file to GitHub: {response.StatusCode}, {error}");
        }
    }

    /// <summary>
    /// Constructs a GitHub API URL for accessing a file or directory.
    /// </summary>
    /// <param name="fullPath">The full path to the file or directory.</param>
    /// <returns>The GitHub API URL.</returns>
    private Uri GetGitHubFileUrl(string fullPath)
    {
        // Ensure the path starts without a slash for GitHub API
        fullPath = fullPath.TrimStart('/');

        // Create GitHub API URL for accessing repository contents
        string url = $"https://api.github.com/repos/{_owner}/{_repo}/contents/{fullPath}?ref={_branch}";

        return new Uri(url);
    }

    /// <summary>
    /// Disposes the resources used by this instance.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the resources used by this instance.
    /// </summary>
    /// <param name="disposing">Whether to dispose managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Remove unnecessary null check since _httpClient is initialized in constructor
            _httpClient.Dispose();
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
