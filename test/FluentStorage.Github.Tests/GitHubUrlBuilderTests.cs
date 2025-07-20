
using System;
using Olbrasoft.FluentStorage.Github;

namespace Olbrasoft.FluentStorage.Github.Tests
{
    public class GitHubUrlBuilderTests
    {
        [Theory]
        [InlineData("olbrasoft", "repo", "main", "file.txt", "https://api.github.com/repos/olbrasoft/repo/contents/file.txt?ref=main")]
        [InlineData("user", "repo2", "dev", "/folder/file.md", "https://api.github.com/repos/user/repo2/contents/folder/file.md?ref=dev")]
        public void BuildFileUrl_ConstructsCorrectUrl(string owner, string repo, string branch, string fullPath, string expectedUrl)
        {
            var builder = new GitHubUrlBuilder(owner, repo, branch);
            var uri = builder.BuildFileUrl(fullPath);
            Assert.Equal(expectedUrl, uri.ToString());
        }

        [Fact]
        public void Constructor_ThrowsOnNullArguments()
        {
            Assert.Throws<ArgumentException>(() => new GitHubUrlBuilder(string.Empty, "repo", "main"));
            Assert.Throws<ArgumentException>(() => new GitHubUrlBuilder("owner", string.Empty, "main"));
            Assert.Throws<ArgumentException>(() => new GitHubUrlBuilder("owner", "repo", string.Empty));
        }
    }
}
