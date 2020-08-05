# dotnetCampus.FileDownloader

| Build | NuGet |
|--|--|
|![](https://github.com/dotnet-campus/dotnetCampus.FileDownloader/workflows/.NET%20Core/badge.svg)|[![](https://img.shields.io/nuget/v/dotnetCampus.FileDownloader.svg)](https://www.nuget.org/packages/dotnetCampus.FileDownloader)|

The repo includes the file download library and the file download tool.

# File download tool

A dotnet tool to download files.

## Install

```
dotnet tool install -g dotnetCampus.FileDownloader.Tool
```

## Usage

```
DownloadFile -u [the download url] -o [the download file]
```

# File download library

## Install

```
dotnet add package dotnetCampus.FileDownloader
```

## Usage

```csharp
var segmentFileDownloader = new SegmentFileDownloader(url, file);

await segmentFileDownloader.DownloadFileAsync();
```