
namespace Olbrasoft.FluentStorage.Github;

/// <summary>
/// Represents connection parameters for accessing a GitHub repository.
/// </summary>
/// <param name="owner">The GitHub repository owner or organization name.</param>
/// <param name="repository">The GitHub repository name.</param>
/// <param name="branch">The branch name to work with.</param>
/// <param name="token">The GitHub personal access token for authentication.</param>
public class GitHubConnection(string owner, string repository, string branch, string token)
{
    /// <summary>
    /// Gets or sets the GitHub repository owner or organization name.
    /// </summary>
    public string Owner { get; set; } = owner;

    /// <summary>
    /// Gets or sets the GitHub repository name.
    /// </summary>
    public string Repository { get; set; } = repository;

    /// <summary>
    /// Gets or sets the branch name to work with.
    /// </summary>
    public string Branch { get; set; } = branch;

    /// <summary>
    /// Gets or sets the GitHub personal access token for authentication.
    /// </summary>
    public string Token { get; set; } = token;
}