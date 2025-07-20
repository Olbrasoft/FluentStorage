namespace Olbrasoft.FluentStorage.Github
{
    public class GitHubUrlBuilder
    {
        private readonly string _owner;
        private readonly string _repo;
        private readonly string _branch;

        public GitHubUrlBuilder(string owner, string repo, string branch)
        {
            if (string.IsNullOrWhiteSpace(owner))
                throw new ArgumentException("Owner cannot be null or whitespace.", nameof(owner));
            if (string.IsNullOrWhiteSpace(repo))
                throw new ArgumentException("Repo cannot be null or whitespace.", nameof(repo));
            if (string.IsNullOrWhiteSpace(branch))
                throw new ArgumentException("Branch cannot be null or whitespace.", nameof(branch));
            _owner = owner;
            _repo = repo;
            _branch = branch;
        }

        public Uri BuildFileUrl(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                throw new ArgumentException("FullPath cannot be null or whitespace.", nameof(fullPath));

            fullPath = fullPath.TrimStart('/');
            string url = $"https://api.github.com/repos/{_owner}/{_repo}/contents/{fullPath}?ref={_branch}";
            return new Uri(url);
        }

    }
}
