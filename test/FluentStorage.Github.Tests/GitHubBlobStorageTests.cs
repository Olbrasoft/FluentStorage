using FluentStorage.Blobs;
using System.Text;

namespace Olbrasoft.FluentStorage.Github.Tests;

public class GitHubBlobStorageTests
{
    //GitHubBlobStorage is public class
    [Fact]
    public void GithubBlobStorage_Is_Public_Class()
    {
        //Arrange
        var type = typeof(GitHubBlobStorage);

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
        var type = typeof(GitHubBlobStorage);

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
        var type = typeof(GitHubBlobStorage);

        //Act
        var @namespace = type.Namespace;

        //Assert
        Assert.Equal("Olbrasoft.FluentStorage.Github", @namespace);

    }

    //Implement interface IBlobStorage
    [Fact]
    public void Implement_Interface_IBlobStorage()
    {
        //Arrange
        var type = typeof(GitHubBlobStorage);

        //Act
        var isImplement = type.GetInterfaces().Contains(typeof(IBlobStorage));

        //Assert
        Assert.True(isImplement);

    }




    [Fact]
    public async Task ClearDirectory()
    {
        var owner = "Olbrasoft";
        var repo = "FluentStorageTesting";
        var branch = "main";
        var token = "";
        var directory = "Tests";

        var storage = new GitHubBlobStorage(owner, repo, branch, token);

        var files = await storage.ListAsync(directory);

        if (files.Count != 0)
        {
            foreach (var f in files)
            {
                await storage.DeleteAsync(directory + "/" + f.Name);
            }
        }



    }


    //writeasnc test
    [Fact]
    public async Task WriteAsync()
    {
        //Arrange
        var owner = "Olbrasoft";
        var repo = "FluentStorageTesting";
        var branch = "main";
        var token = "";
        var directory = "Tests";
        var helloWorld = "Hello World!";


        var storage = new GitHubBlobStorage(owner, repo, branch, token);


        var files = await storage.ListAsync(directory);

        if (files.Count != 0)
        {
            foreach (var f in files)
            {
                await storage.DeleteAsync(directory + "/" + f.Name);
            }
        }


        var fullPath = $"{directory}/test.txt";



        var exists = await storage.ExistsAsync(fullPath);

        Assert.False(exists);


        var content = $"I am test.txt {helloWorld}";

        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        await storage.WriteAsync(fullPath, stream);

        exists = await storage.ExistsAsync(fullPath);
        Assert.True(exists);


        // Přečtěte text ze souboru
        var text = await storage.ReadTextAsync(fullPath);


        // Ověřte, že dekódovaný text odpovídá tomu, co bylo zapsáno
        Assert.Equal(content, text);


        fullPath = $"{directory}/test1.txt";

        content = $"I am test1.txt {helloWorld}";

        exists = await storage.ExistsAsync(fullPath);

        Assert.False(exists);

        await storage.WriteAsync(fullPath, stream);

        exists = await storage.ExistsAsync(fullPath);

        Assert.True(exists);
        await storage.WriteAsync(fullPath, stream);

        exists = await storage.ExistsAsync(fullPath);

        Assert.True(exists);

        files = await storage.ListAsync(directory);

        Assert.Equal(2, files.Count);

        if (files.Count != 0)
        {
            foreach (var f in files)
            {
                await storage.DeleteAsync(directory + "/" + f.Name);
            }
        }

        files = await storage.ListAsync(directory);

        Assert.Empty(files);



        //Act



        //Assert




    }
}