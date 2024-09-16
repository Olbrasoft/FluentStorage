namespace Olbrasoft.FluentStorage.Github.Tests;
public class GitHubConnectionTests
{

    //test GithubConnection is public class
    [Fact]
    public void GitHubConnection_IsPublicClass()
    {
        //Arrange
        var type = typeof(GitHubConnection);

        //Act
        var isPublic = type.IsPublic;

        //Assert
        Assert.True(isPublic);
    }


    //Assembly name is Olbrasoft.FluentStorage.Github
    [Fact]
    public void Assembly_Name_Is_Olbrasoft_FluentStorage_Github()
    {
        //Arrange
        var type = typeof(GitHubConnection);

        //Act
        var assembly = type.Assembly;

        //Assert
        Assert.Equal("Olbrasoft.FluentStorage.Github", assembly.GetName().Name);
    }


    //namespace is Olbrasoft.FluentStorage.Github
    [Fact]
    public void Namespace_Is_Olbrasoft_FluentStorage_Github()
    {
        //Arrange
        var type = typeof(GitHubConnection);

        //Act
        var @namespace = type.Namespace;

        //Assert
        Assert.Equal("Olbrasoft.FluentStorage.Github", @namespace);
    }

    //properties string owner, string repo, string branch, string token
    [Fact]
    public void Properties_Owner_Repository_Branch_Token()
    {
        //Arrange
        var type = typeof(GitHubConnection);

        //Act
        var properties = type.GetProperties();

        //Assert
        Assert.Collection(properties, p => Assert.Equal("Owner", p.Name),
            p => Assert.Equal("Repository", p.Name),
            p => Assert.Equal("Branch", p.Name),
            p => Assert.Equal("Token", p.Name));
    }

    //constructor with parameters string owner, string repo, string branch, string token init properties
    [Fact]
    public void Constructor_With_Parameters_Owner_Repository_Branch_Token_Init_Properties()
    {
        //Arrange
        var owner = "owner";
        var repository = "repository";
        var branch = "branch";
        var token = "token";

        //Act
        var gitHubConnection = new GitHubConnection(owner, repository, branch, token);

        //Assert
        Assert.Equal(owner, gitHubConnection.Owner);
        Assert.Equal(repository, gitHubConnection.Repository);
        Assert.Equal(branch, gitHubConnection.Branch);
        Assert.Equal(token, gitHubConnection.Token);
    }


}
