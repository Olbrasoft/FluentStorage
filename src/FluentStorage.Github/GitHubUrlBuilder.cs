namespace Olbrasoft.FluentStorage.Github;

/// <summary>
/// Builder class for constructing GitHub API URLs for file operations.
/// </summary>
public class GitHubUrlBuilder : IGitHubUrlBuilder
{
    private readonly string _owner;
    private readonly string _repo;
    private readonly string _branch;

    /// <summary>
    /// Gets the branch name.
    /// </summary>
    public string Branch => _branch;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubUrlBuilder"/> class.
    /// </summary>
    /// <param name="owner">The GitHub repository owner.</param>
    /// <param name="repo">The GitHub repository name.</param>
    /// <param name="branch">The branch name.</param>
    /// <exception cref="ArgumentException">Thrown when owner, repo, or branch is null or whitespace.</exception>
    public GitHubUrlBuilder(string owner, string repo, string branch)
    {
        // Use ArgumentException.ThrowIfNullOrWhiteSpace for modern .NET 8.0+ style
        ArgumentException.ThrowIfNullOrWhiteSpace(owner, nameof(owner));
        ArgumentException.ThrowIfNullOrWhiteSpace(repo, nameof(repo));
        ArgumentException.ThrowIfNullOrWhiteSpace(branch, nameof(branch));

        _owner = owner;
        _repo = repo;
        _branch = branch;
    }

    /// <summary>
    /// Builds a URL for accessing a file in the GitHub repository through the GitHub API.
    /// </summary>
    /// <param name="fullPath">The full path to the file in the repository.</param>
    /// <returns>The URI for accessing the file through GitHub API.</returns>
    /// <exception cref="ArgumentException">Thrown when fullPath is null or whitespace.</exception>
    public Uri BuildFileUrl(string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath, nameof(fullPath));

        fullPath = fullPath.TrimStart('/');
        string url = $"https://api.github.com/repos/{_owner}/{_repo}/contents/{fullPath}?ref={_branch}";
        return new Uri(url);
    }

}
