namespace Olbrasoft.FluentStorage.Github;

/// <summary>
/// Interface for building GitHub API URLs.
/// </summary>
public interface IGitHubUrlBuilder
{
    /// <summary>
    /// Gets the branch name.
    /// </summary>
    string Branch { get; }

    /// <summary>
    /// Builds a URL for accessing a file in the GitHub repository.
    /// </summary>
    /// <param name="fullPath">The full path to the file in the repository.</param>
    /// <returns>The URI for accessing the file through GitHub API.</returns>
    /// <exception cref="ArgumentException">Thrown when fullPath is null or whitespace.</exception>
    Uri BuildFileUrl(string fullPath);
}
