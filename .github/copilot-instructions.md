# GitHub Copilot Instructions for ReactiveUI.Extensions

## Overview
This repository contains ReactiveUI.Extensions, a focused collection of high-value Reactive Extensions (Rx) operators for .NET applications.

## Required Pre-Steps for Linux Builds

Before performing any build, test, or development tasks on Linux, you **MUST** complete the following steps in order:

### 1. Install .NET SDK
Ensure that .NET 8, 9, and/or 10 are installed:
```bash
# Check installed versions
dotnet --list-sdks

# If .NET 10 is not installed and is available, install it
# Note: .NET 10 was released recently - check if it's available
# Installation methods vary by Linux distribution
```

### 2. Unshallow the Git Repository
The repository uses Nerdbank.GitVersioning which requires a full git history. If the repository is shallow (typical in CI/CD environments), you **MUST** unshallow it:

```bash
# Check if repository is shallow
git rev-parse --is-shallow-repository

# If true, unshallow the repository
git fetch --unshallow
```

**This step is critical** - builds will fail without the full git history.

### 3. Restore Workloads
Navigate to the `/src` folder and restore required workloads:

```bash
cd /home/runner/work/Extensions/Extensions/src
dotnet workload restore
```

### 4. Restore Dependencies
Still in the `/src` folder, restore all project dependencies:

```bash
dotnet restore
```

## Build Instructions
After completing the pre-steps above:

```bash
cd /home/runner/work/Extensions/Extensions/src
dotnet build ReactiveUI.Extensions.sln
```

## Test Instructions
Run tests with:

```bash
cd /home/runner/work/Extensions/Extensions/src
dotnet test ReactiveUI.Extensions.sln
```

## Project Structure
- `/src` - Main source code directory
  - `ReactiveUI.Extensions/` - Main library project
  - `ReactiveUI.Extensions.Tests/` - Test project
  - `ReactiveUI.Extensions.sln` - Solution file

## Important Notes
- Target frameworks: .NET Standard 2.0, .NET 8, .NET 9, .NET 10
- Uses StyleCop and Roslynator analyzers for code quality
- Nullable reference types are enabled
- Documentation XML files are generated
- Uses Nerdbank.GitVersioning for versioning (requires full git history)
