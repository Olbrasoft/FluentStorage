namespace Olbrasoft.FluentStorage.Github;

public class GitHubConnection
{
    // properties string owner, string repo, string branch, string token
    public string Owner { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;

    public GitHubConnection(string owner, string repository, string branch, string token)
    {
        Owner = owner;
        Repository = repository;
        Branch = branch;
        Token = token;
    }

    public GitHubConnection()
    {
    }

}