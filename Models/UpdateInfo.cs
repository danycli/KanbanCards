using System;
using System.Collections.Generic;

namespace KanbanCards.Models;

/// <summary>
/// Status of an in-app update process.
/// </summary>
public enum UpdateStep
{
    Idle,
    Checking,
    Downloading,
    Installing,
    Failed,
    Cancelled
}

/// <summary>
/// Metadata for a downloadable asset from a GitHub Release.
/// </summary>
public class ReleaseAsset
{
    public string Name { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public long Size { get; set; }
    public string? ContentType { get; set; }
}

/// <summary>
/// Information about an available application update discovered from GitHub Releases.
/// </summary>
public class UpdateInfo
{
    public string Version { get; set; } = string.Empty;
    public string TagName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string HtmlUrl { get; set; } = string.Empty;
    public string? RawBody { get; set; }
    public IReadOnlyList<string> Summary { get; set; } = Array.Empty<string>();
    public DateTime? PublishedAt { get; set; }
    public ReleaseAsset? InstallerAsset { get; set; }
    public ReleaseAsset? ChecksumAsset { get; set; }
    public bool IsInstalled { get; set; }
}
