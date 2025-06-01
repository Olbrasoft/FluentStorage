using FluentStorage;
using FluentStorage.Blobs;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Olbrasoft.FluentStorage.Github;

public class GitHubBlobStorage : IBlobStorage
{
    private readonly string _owner;
    private readonly string _repo;
    private readonly string _branch;
    private readonly string _token;
    private readonly HttpClient _httpClient;
    private bool _disposed;

    public GitHubBlobStorage(string owner, string repo, string branch, string token)
    {
        _owner = owner;
        _repo = repo;
        _branch = branch;
        _token = token;
        _httpClient = new HttpClient
        {
            DefaultRequestHeaders =
            {
                Authorization = new AuthenticationHeaderValue("token", _token),
                UserAgent = { ProductInfoHeaderValue.Parse("GitHubBlobStorage") }
            }
        };
    }

    public async Task DeleteAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(fullPath)) throw new ArgumentException("Full path cannot be null or empty", nameof(fullPath));

        var url = GetGitHubFileUrl(fullPath);

        // Get file info to get SHA
        var getResponse = await _httpClient.GetAsync(url, cancellationToken);
        if (!getResponse.IsSuccessStatusCode)
        {
            // If file doesn't exist, we simply skip it
            return;
        }

        var getFileContent = await getResponse.Content.ReadAsStringAsync();
        var fileInfo = JsonSerializer.Deserialize<GitHubFileResponse>(getFileContent);

        // Prepare delete request
        var deleteRequestBody = new
        {
            message = $"Delete {fullPath}",
            sha = fileInfo?.Sha,
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

    public async Task DeleteAsync(IEnumerable<string> fullPaths, CancellationToken cancellationToken = default)
    {
        if (fullPaths == null) throw new ArgumentNullException(nameof(fullPaths));

        foreach (string path in fullPaths)
        {
            await DeleteAsync(path, cancellationToken);
        }
    }

    public async Task<IReadOnlyCollection<bool>> ExistsAsync(IEnumerable<string> fullPaths, CancellationToken cancellationToken = default)
    {
        if (fullPaths == null) throw new ArgumentNullException(nameof(fullPaths));

        var results = new List<bool>();

        foreach (string path in fullPaths)
        {
            var url = GetGitHubFileUrl(path);
            var response = await _httpClient.GetAsync(url, cancellationToken);
            results.Add(response.IsSuccessStatusCode);
        }

        return results.AsReadOnly();
    }

    public async Task<IReadOnlyCollection<Blob>> GetBlobsAsync(IEnumerable<string> fullPaths, CancellationToken cancellationToken = default)
    {
        if (fullPaths == null || !fullPaths.Any())
            throw new ArgumentNullException(nameof(fullPaths));

        var blobs = new List<Blob>();

        foreach (var fullPath in fullPaths)
        {
            var url = GetGitHubFileUrl(fullPath);

            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var fileResponse = JsonSerializer.Deserialize<GitHubFileResponse>(content);

                blobs.Add(new Blob(fullPath)
                {
                    Size = fileResponse?.Size,
                    MD5 = fileResponse?.MD5
                });
            }
            else
            {
                blobs.Add(new Blob(fullPath, BlobItemKind.File));
            }
        }

        return blobs.AsReadOnly();
    }

    public async Task<IReadOnlyCollection<Blob>> ListAsync(ListOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new ListOptions();

        string path = options.FolderPath ?? string.Empty;
        var blobs = new List<Blob>();

        await ListInternalAsync(path, options, blobs, cancellationToken);
        
        return blobs.AsReadOnly();
    }

    private async Task ListInternalAsync(string currentPath, ListOptions? options, List<Blob> blobs, CancellationToken cancellationToken)
    {
        var url = GetGitHubFileUrl(currentPath);
        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Error listing files from GitHub: {response.StatusCode}, {error}");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var fileResponses = JsonSerializer.Deserialize<List<GitHubFileResponse>>(content);

        if (fileResponses == null || fileResponses.Count == 0)
        {
            return;
        }

        foreach (var file in fileResponses)
        {
            if (file.Type == "file")
            {
                string fullPath = file.Path;
                if (options != null && !options.IsMatch(fullPath)) continue;

                blobs.Add(new Blob(fullPath, BlobItemKind.File)
                {
                    Size = file.Size,
                    MD5 = file.MD5
                });
            }
            else if (file.Type == "dir" && options != null && options.Recurse)
            {
                await ListInternalAsync(file.Path, options, blobs, cancellationToken);
            }
        }
    }

    public Task<ITransaction> OpenTransactionAsync()
    {
        // GitHub API doesn't support transactions directly
        throw new NotSupportedException("Transactions are not supported with GitHub Blob Storage");
    }

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
                return contentBytes.Length == 0 ? new MemoryStream() : (Stream)new MemoryStream(contentBytes);
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

    public Task SetBlobsAsync(IEnumerable<Blob> blobs, CancellationToken cancellationToken = default)
    {
        // GitHub API doesn't directly support setting just blob metadata
        // This method is typically used to set metadata without changing blob content
        throw new NotSupportedException("Setting blob metadata only is not supported with GitHub Blob Storage");
    }

    public async Task WriteAsync(string fullPath, Stream dataStream, bool append = false, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(fullPath))
            throw new ArgumentNullException(nameof(fullPath));

        ArgumentNullException.ThrowIfNull(dataStream);

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
            var existingFileJson = await existingFileResponse.Content.ReadAsStringAsync();
            var existingFile = JsonSerializer.Deserialize<GitHubFileResponse>(existingFileJson);

            var deleteRequestBody = new
            {
                message = "Delete existing file to replace with a new one",
                sha = existingFile?.Sha,
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

    private Uri GetGitHubFileUrl(string fullPath)
    {
        // Ensure the path starts without a slash for GitHub API
        fullPath = fullPath.TrimStart('/');

        // Create GitHub API URL for accessing repository contents
        string url = $"https://api.github.com/repos/{_owner}/{_repo}/contents/{fullPath}?ref={_branch}";

        return new Uri(url);
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
            _httpClient?.Dispose();
        }

        _disposed = true;
    }

    private class GitHubFileResponse
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("sha")]
        public string Sha { get; set; } = string.Empty;

        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public long? Size { get; set; }

        [JsonPropertyName("md5")]
        public string MD5 { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
    }
}