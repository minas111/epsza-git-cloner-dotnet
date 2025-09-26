# Git Cloner - .NET 8

A cross-platform .NET 8 console application that clones and updates Git repositories from a list of URLs, with automatic shell script permissions handling.

## Features

- **Cross-platform**: Runs on Windows, macOS, and Linux
- **Batch cloning**: Clone multiple repositories from a text file
- **Smart updates**: Pulls latest changes for existing repositories
- **Shell script permissions**: Automatically sets execute permissions on `.sh` files (macOS/Linux only)
- **Beautiful output**: Colored table display with status information
- **Progress tracking**: Real-time progress bar during operations

## Quick Start

```bash
dotnet run --project GitCloner.csproj
```

## Usage

### Basic Usage
```bash
# Uses default files: repos.txt and cloned-repos/ folder
dotnet run

# Specify custom repo list file
dotnet run path/to/your-repos.txt

# Specify both repo list and clone directory
dotnet run path/to/your-repos.txt path/to/clone-directory
```

### Repository List Format
Create a `repos.txt` file with one repository URL per line:

```
https://github.com/user/repo1
https://github.com/user/repo2
git@github.com:user/repo3.git
# This is a comment - will be ignored
https://github.com/user/repo4
```

## Shell Script Permissions

The application automatically handles shell script permissions:

- **Windows**: No action needed (not applicable)
- **macOS/Linux**: Automatically runs `chmod +x` on all `.sh` files found in cloned repositories

This ensures that shell scripts in your repositories are immediately executable after cloning, which is particularly useful for:
- Build scripts
- Setup scripts
- CI/CD scripts
- Development tools

## Output

The application displays a formatted table with:
- **UserName**: Repository owner
- **RepoName**: Repository name
- **Status**: New, Updated, Existing, or Error
- **LastUpdatedTime**: When the operation was performed

**Color coding:**
- 🟢 **Green rows**: New repositories or repositories with updates
- ⚪ **White rows**: Existing repositories with no changes
- 🔴 **Red status**: Repositories with errors

## Requirements

- .NET 8.0 or later
- Git (for repository operations)
- Internet connection (for cloning/fetching)

## Dependencies

- **LibGit2Sharp**: Git operations
- **Spectre.Console**: Beautiful console output and progress bars