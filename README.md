# FluentStorage.Github

A GitHub repository blob storage extension for [FluentStorage](https://github.com/robinrodricks/FluentStorage).

## Overview

FluentStorage.Github is an extension library that enables you to use GitHub repositories as blob storage through the FluentStorage framework. This allows you to store, retrieve, and manage files directly in GitHub repositories using a unified storage interface.

## Features

- **Full IBlobStorage Implementation**: Complete implementation of the FluentStorage IBlobStorage interface
- **GitHub API Integration**: Uses GitHub's Contents API for file operations
- **File Operations**: Create, read, update, and delete files in GitHub repositories
- **Directory Listing**: List files and directories with filtering and recursive options
- **Branch Support**: Work with specific branches in your GitHub repository
- **SHA Conflict Resolution**: Automatic retry logic for handling concurrent modifications
- **Append Operations**: Support for appending content to existing files
- **Metadata Support**: Access file size, MD5 hashes, and other metadata

## Supported Operations

- **WriteAsync**: Create or overwrite files, with support for appending
- **OpenReadAsync**: Read file content as a stream
- **DeleteAsync**: Delete single files or multiple files
- **ListAsync**: List files and directories with filtering options
- **ExistsAsync**: Check if files exist
- **GetBlobsAsync**: Get blob information including metadata

## Installation

```bash
dotnet add package Olbrasoft.FluentStorage.Github
```

## Usage

### Basic Setup

```csharp
using Olbrasoft.FluentStorage.Github;

// Create GitHub blob storage instance
var storage = new GitHubBlobStorage(
    owner: "your-username",
    repo: "your-repository", 
    branch: "main",
    token: "your-github-token"
);

// Or use with dependency injection
var apiClient = new GitHubApiClient("your-github-token");
var urlBuilder = new GitHubUrlBuilder("your-username", "your-repository", "main");
var storage = new GitHubBlobStorage(apiClient, urlBuilder);
```

### Writing Files

```csharp
// Write a text file
using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Hello, GitHub!"));
await storage.WriteAsync("path/to/file.txt", stream);

// Append to existing file
await storage.WriteAsync("path/to/file.txt", stream, append: true);
```

### Reading Files

```csharp
// Read file content
using var stream = await storage.OpenReadAsync("path/to/file.txt");
if (stream != null)
{
    using var reader = new StreamReader(stream);
    var content = await reader.ReadToEndAsync();
    Console.WriteLine(content);
}
```

### Listing Files

```csharp
// List all files
var files = await storage.ListAsync();

// List files with options
var options = new ListOptions
{
    FolderPath = "docs/",
    Recurse = true,
    FilePrefix = "readme"
};
var filteredFiles = await storage.ListAsync(options);
```

### Checking File Existence

```csharp
var paths = new[] { "file1.txt", "file2.txt" };
var exists = await storage.ExistsAsync(paths);
```

### Deleting Files

```csharp
// Delete a single file
await storage.DeleteAsync("path/to/file.txt");

// Delete multiple files
var filesToDelete = new[] { "file1.txt", "file2.txt" };
await storage.DeleteAsync(filesToDelete);
```

## Authentication

You need a GitHub Personal Access Token with appropriate repository permissions:

1. Go to GitHub Settings → Developer settings → Personal access tokens
2. Generate a new token with `Contents` repository permissions
3. Use the token in your GitHubBlobStorage configuration

## Configuration

### GitHubConnection

Use the `GitHubConnection` class to encapsulate connection parameters:

```csharp
var connection = new GitHubConnection(
    owner: "your-username",
    repository: "your-repository", 
    branch: "main",
    token: "your-github-token"
);

var storage = new GitHubBlobStorage(
    connection.Owner, 
    connection.Repository, 
    connection.Branch, 
    connection.Token
);
```

## Error Handling

The library includes robust error handling with automatic retry logic for SHA conflicts and comprehensive exception handling for various GitHub API scenarios.

## Threading and Concurrency

The library is designed to handle concurrent operations safely with automatic SHA conflict resolution and retry mechanisms.

## Limitations

- **No Transaction Support**: GitHub API doesn't support atomic transactions
- **File Size Limits**: Subject to GitHub's file size limitations
- **Rate Limiting**: Subject to GitHub API rate limits
- **No Metadata Updates**: Setting blob metadata only is not supported

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

## License

This project is licensed under the MIT License.

## Related Projects

- [FluentStorage](https://github.com/robinrodricks/FluentStorage) - The main FluentStorage library
