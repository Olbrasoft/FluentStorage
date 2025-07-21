
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
        public void BuildFileUrl_WithValidFullPath_ReturnsValidUri()
        {
            // Arrange
            var builder = new GitHubUrlBuilder("owner", "repo", "main");

            // Act & Assert - should work with valid paths
            var result1 = builder.BuildFileUrl("valid-file.txt");
            var result2 = builder.BuildFileUrl("folder/file.md");

            Assert.NotNull(result1);
            Assert.NotNull(result2);
            Assert.Contains("valid-file.txt", result1.ToString());
            Assert.Contains("folder/file.md", result2.ToString());
        }

        [Fact]
        public void Constructor_ValidArguments_CreatesInstance()
        {
            var builder = new GitHubUrlBuilder("owner", "repo", "main");

            Assert.NotNull(builder);
            Assert.Equal("main", builder.Branch);
        }

        [Theory]
        [InlineData("user", "repo", "main")]
        [InlineData("organization", "my-repo", "develop")]
        [InlineData("owner123", "repo-name", "feature/branch")]
        [InlineData("a", "b", "c")] // Single character parameters
        public void Constructor_WithVariousValidParameters_CreatesInstanceSuccessfully(string owner, string repo, string branch)
        {
            // Act
            var builder = new GitHubUrlBuilder(owner, repo, branch);

            // Assert
            Assert.NotNull(builder);
            Assert.Equal(branch, builder.Branch);

            // Verify the constructor parameters are used correctly by building a URL
            var url = builder.BuildFileUrl("test.txt");
            Assert.Contains(owner, url.ToString());
            Assert.Contains(repo, url.ToString());
            Assert.Contains(branch, url.ToString());
        }

        [Fact]
        public void Constructor_WithLongValidParameters_WorksCorrectly()
        {
            // Arrange - test with longer valid strings
            var longOwner = "very-long-organization-name-with-many-characters";
            var longRepo = "extremely-long-repository-name-with-descriptive-title";
            var longBranch = "feature/very-long-branch-name-describing-complex-feature";

            // Act
            var builder = new GitHubUrlBuilder(longOwner, longRepo, longBranch);

            // Assert
            Assert.NotNull(builder);
            Assert.Equal(longBranch, builder.Branch);

            // Verify all parameters are correctly used in URL construction
            var url = builder.BuildFileUrl("path/to/file.txt");
            Assert.Contains(longOwner, url.ToString());
            Assert.Contains(longRepo, url.ToString());
            Assert.Contains(longBranch, url.ToString());
        }

        [Theory]
        [InlineData("simple.txt")]
        [InlineData("/with-leading-slash.md")]
        [InlineData("folder/subfolder/file.json")]
        [InlineData("deep/nested/path/to/document.xml")]
        [InlineData("file-with-special_chars123.txt")]
        [InlineData("a")] // Single character file name
        public void BuildFileUrl_WithVariousValidPaths_ReturnsValidUri(string fullPath)
        {
            // Arrange
            var builder = new GitHubUrlBuilder("owner", "repo", "main");

            // Act
            var result = builder.BuildFileUrl(fullPath);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsAbsoluteUri);
            Assert.Contains("api.github.com", result.ToString());
            Assert.Contains("owner", result.ToString());
            Assert.Contains("repo", result.ToString());
            Assert.Contains("main", result.ToString());

            // Verify that leading slash is properly trimmed
            var expectedPath = fullPath.TrimStart('/');
            Assert.Contains(expectedPath, result.ToString());
        }

        [Fact]
        public void BuildFileUrl_WithLeadingSlash_TrimsSlashCorrectly()
        {
            // Arrange
            var builder = new GitHubUrlBuilder("test-owner", "test-repo", "test-branch");

            // Act
            var resultWithSlash = builder.BuildFileUrl("/folder/file.txt");
            var resultWithoutSlash = builder.BuildFileUrl("folder/file.txt");

            // Assert - both should produce the same URL
            Assert.Equal(resultWithoutSlash.ToString(), resultWithSlash.ToString());
            Assert.Contains("folder/file.txt", resultWithSlash.ToString());
            Assert.DoesNotContain("/folder/file.txt", resultWithSlash.ToString().Replace("contents/folder/file.txt", "REPLACED"));
        }

        [Fact]
        public void BuildFileUrl_WithComplexPath_ConstructsCorrectUrl()
        {
            // Arrange
            var builder = new GitHubUrlBuilder("my-org", "my-repo", "feature/new-api");
            var complexPath = "src/components/ui/Button/Button.component.tsx";

            // Act
            var result = builder.BuildFileUrl(complexPath);

            // Assert
            var expectedUrl = "https://api.github.com/repos/my-org/my-repo/contents/src/components/ui/Button/Button.component.tsx?ref=feature/new-api";
            Assert.Equal(expectedUrl, result.ToString());
        }
    }
}
