using FluentStorage;
using FluentStorage.Blobs;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Olbrasoft.FluentStorage.Github;

public class GitHubBlobStorage : IBlobStorage
{
    private readonly string _owner;
    private readonly string _repo;
    private readonly string _branch;
    private readonly string _token;
    private readonly HttpClient _httpClient;

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

    public async Task DeleteAsync(IEnumerable<string> fullPaths, CancellationToken cancellationToken = default)
    {
        if (fullPaths == null || !fullPaths.Any())
            throw new ArgumentNullException(nameof(fullPaths));

        foreach (var fullPath in fullPaths)
        {
            if (string.IsNullOrEmpty(fullPath))
                throw new ArgumentException("Full path cannot be null or empty", nameof(fullPath));

            var url = GetGitHubFileUrl(fullPath);

            // Get file info to get SHA
            var getResponse = await _httpClient.GetAsync(url, cancellationToken);
            if (!getResponse.IsSuccessStatusCode)
            {
                // If file doesn't exist, we simply skip it
                continue;
            }

            var getFileContent = await getResponse.Content.ReadAsStringAsync();
            var fileInfo = JsonConvert.DeserializeObject<GitHubFileResponse>(getFileContent);

            // Prepare delete request
            var deleteRequestBody = new
            {
                message = $"Delete {fullPath}",
                sha = fileInfo?.Sha,
                branch = _branch
            };

            var deleteJsonRequestBody = JsonConvert.SerializeObject(deleteRequestBody);
            var deleteContent = new StringContent(deleteJsonRequestBody, Encoding.UTF8, "application/json");

            var requestMessage = new HttpRequestMessage(HttpMethod.Delete, url)
            {
                Content = deleteContent
            };

            var deleteResponse = await _httpClient.SendAsync(requestMessage, cancellationToken);

            if (!deleteResponse.IsSuccessStatusCode)
            {
                var error = await deleteResponse.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Error deleting file from GitHub: {deleteResponse.StatusCode}, {error}");
            }
        }
    }


    public async Task<IReadOnlyCollection<bool>> ExistsAsync(IEnumerable<string> fullPaths, CancellationToken cancellationToken = default)
    {
        if (fullPaths == null || !fullPaths.Any())
            if (fullPaths == null || !fullPaths.Any())
                throw new ArgumentNullException(nameof(fullPaths));

        var results = new List<bool>(fullPaths.Count());

        foreach (var fullPath in fullPaths)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                results.Add(false);
                continue;
            }

            var requestUrl = GetGitHubFileUrl(fullPath);
            var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            results.Add(response.IsSuccessStatusCode);
        }

        return results.AsReadOnly();
        throw new ArgumentNullException(nameof(fullPaths));


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
                var fileResponse = JsonConvert.DeserializeObject<GitHubFileResponse>(content);

                blobs.Add(new Blob(fullPath)
                {
                    Size = fileResponse?.Size, // Assuming Size is part of response
                    MD5 = fileResponse?.MD5  // Assuming MD5 is part of response
                });
            }
            else
            {
                blobs.Add(new Blob(fullPath, BlobItemKind.File)); // Assume file if not found
            }
        }

        return blobs.AsReadOnly();
    }

    public async Task<IReadOnlyCollection<Blob>> ListAsync(ListOptions? options = null, CancellationToken cancellationToken = default)
    {
        // If no options are provided, use default ListOptions
        if (options == null)
        {
            options = new ListOptions();
        }

        // Initialize a list to hold the resulting blobs
        var blobs = new List<Blob>();

        // Start the recursive listing from the specified folder path
        await ListInternalAsync(options.FolderPath, options, blobs, cancellationToken);

        // Return the list of blobs as a read-only collection
        return blobs.AsReadOnly();
    }

    private async Task ListInternalAsync(string currentPath, ListOptions options, List<Blob> blobs, CancellationToken cancellationToken)
    {
        // Construct the URL for the GitHub API request
        var url = GetGitHubFileUrl(currentPath);

        // Send a GET request to the GitHub API
        var response = await _httpClient.GetAsync(url, cancellationToken);

        // If the response indicates the directory does not exist (404 Not Found), return early with an empty list
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return;
        }

        // If the response is not successful and it's not a 404, throw an exception
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Error listing files from GitHub: {response.StatusCode}, {error}");
        }

        // Deserialize the response content into a list of GitHubFileResponse objects
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var items = JsonConvert.DeserializeObject<List<GitHubFileResponse>>(content);

        // If the response is empty or null, return early
        if (items == null || !items.Any())
        {
            return;
        }

        foreach (var item in items)
        {
            // Determine if the item is a directory
            var isDirectory = string.Equals(item.Type, "dir", StringComparison.OrdinalIgnoreCase);
            var blobItemKind = isDirectory ? BlobItemKind.Folder : BlobItemKind.File;

            // Create a Blob object for the current item
            var blob = new Blob(item.Path, blobItemKind)
            {
                Size = item.Size ?? 0,
                MD5 = item.MD5 ?? string.Empty
            };

            // Apply the prefix filter defined in ListOptions
            if (!options.IsMatch(blob))
            {
                continue;
            }

            // Add the blob to the list of results
            blobs.Add(blob);

            // If the maximum number of results is reached, stop processing
            if (options.MaxResults.HasValue && blobs.Count >= options.MaxResults.Value)
            {
                return;
            }

            // If the item is a directory and recursion is enabled, recursively list its contents
            if (isDirectory && options.Recurse)
            {
                await ListInternalAsync(item.Path, options, blobs, cancellationToken);
            }
        }
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

                var githubFile = JsonConvert.DeserializeObject<GitHubFileResponse>(jsonResponse);

                if (githubFile?.Content == null)
                {
                    return null;
                }

                // Dekódujte obsah z base64
                var contentBytes = Convert.FromBase64String(githubFile.Content);

                // Pokud je dekódovaný obsah prázdný, vrátí se prázdný MemoryStream
                return contentBytes.Length == 0 ? new MemoryStream() : (Stream)new MemoryStream(contentBytes);
            }

            // Pokud je status kód 404, vrátí null, stejně jako Azure Blob Storage pro neexistující blob
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


    public Task<ITransaction> OpenTransactionAsync() => throw new NotImplementedException();


    public async Task WriteAsync(string fullPath, Stream dataStream, bool append = false, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(fullPath))
            throw new ArgumentNullException(nameof(fullPath));

        ArgumentNullException.ThrowIfNull(dataStream);

        // Read the stream into a byte array
        byte[] fileBytes;
        using (var memoryStream = new MemoryStream())
        {
            await dataStream.CopyToAsync(memoryStream, token);
            fileBytes = memoryStream.ToArray();
        }

        // Convert file content to Base64 string
        var content = Convert.ToBase64String(fileBytes);

        // Build the URL for the GitHub API request
        var url = GetGitHubFileUrl(fullPath);

        // Check if the file already exists
        var existingFileResponse = await _httpClient.GetAsync(url, token);

        if (existingFileResponse.IsSuccessStatusCode)
        {
            // If the file exists, retrieve its SHA for deletion
            var existingFileJson = await existingFileResponse.Content.ReadAsStringAsync();
            var existingFile = JsonConvert.DeserializeObject<GitHubFileResponse>(existingFileJson);

            // Delete the existing file
            var deleteRequestBody = new
            {
                message = "Delete existing file to replace with a new one",
                sha = existingFile?.Sha,
                branch = _branch
            };

            var deleteJsonRequestBody = JsonConvert.SerializeObject(deleteRequestBody);
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

        // Prepare the request body for creating or updating the file
        var requestBody = new
        {
            message = append ? "Append to existing file" : "Create or overwrite file",
            content,
            branch = _branch // Target branch to upload the file
        };

        // Serialize the request body to JSON format
        var jsonRequestBody = JsonConvert.SerializeObject(requestBody);

        // Create an HTTP PUT request with the file content
        var contentString = new StringContent(jsonRequestBody, Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync(url, contentString, token);

        // Handle the response
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(token);
            throw new InvalidOperationException($"Error uploading file to GitHub: {response.StatusCode}, {error}");
        }
    }


    public void Dispose()
    {
        _httpClient?.Dispose();
        GC.SuppressFinalize(this);
    }

    private string GetGitHubFileUrl(string fullPath)
    {
        return $"https://api.github.com/repos/{_owner}/{_repo}/contents/{fullPath}";
    }

    public Task SetBlobsAsync(IEnumerable<Blob> blobs, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    private class GitHubFileResponse
    {
        [JsonProperty("content")]
        public string? Content { get; set; }

        [JsonProperty("sha")]
        public string Sha { get; set; } = string.Empty;

        [JsonProperty("path")]
        public string Path { get; set; } = string.Empty;

        [JsonProperty("size")]
        public long? Size { get; set; }

        [JsonProperty("md5")]
        public string MD5 { get; set; } = string.Empty;

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;
    }
}

