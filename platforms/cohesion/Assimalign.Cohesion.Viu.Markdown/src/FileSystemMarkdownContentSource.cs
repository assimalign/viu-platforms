using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Assimalign.Cohesion.Viu.Markdown;

/// <summary>Reads Markdown beneath a configured physical content directory for server or local hosts.</summary>
public sealed class FileSystemMarkdownContentSource : IMarkdownContentSource
{
    private readonly string _contentRootDirectory;
    private readonly string _assetPathPrefix;

    /// <summary>Sets a physical directory and optional asset prefix stripped before resolving physical paths.</summary>
    public FileSystemMarkdownContentSource(string contentRootDirectory, string assetPathPrefix = "")
    {
        _contentRootDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(contentRootDirectory));
        _assetPathPrefix = MarkdownContentCatalog.NormalizeAssetPath(assetPathPrefix).TrimEnd('/');
    }

    /// <summary>Reads an asset with cancellation, rejecting root traversal and symbolic-link path segments.</summary>
    public Task<string> ReadAsync(string assetPath, CancellationToken cancellationToken = default)
        => File.ReadAllTextAsync(ResolvePath(assetPath), cancellationToken);

    /// <summary>Returns all Markdown files beneath the root in ordinal order, excluding symbolic links.</summary>
    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<string> assets = [];
        EnumerationOptions enumerationOptions = new()
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = false,
        };
        foreach (string path in Directory.EnumerateFiles(_contentRootDirectory, "*", enumerationOptions))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            string relative = Path.GetRelativePath(_contentRootDirectory, path).Replace('\\', '/');
            assets.Add(_assetPathPrefix.Length == 0 ? relative : _assetPathPrefix + "/" + relative);
        }
        assets.Sort(StringComparer.Ordinal);
        return Task.FromResult<IReadOnlyList<string>>(assets.AsReadOnly());
    }

    private string ResolvePath(string assetPath)
    {
        string relative = MarkdownContentCatalog.NormalizeAssetPath(assetPath);
        if (_assetPathPrefix.Length > 0)
        {
            if (!relative.StartsWith(_assetPathPrefix + "/", StringComparison.Ordinal))
            {
                throw new ArgumentException("The asset path is outside the configured asset prefix.", nameof(assetPath));
            }
            relative = relative[(_assetPathPrefix.Length + 1)..];
        }
        string resolved = Path.GetFullPath(Path.Combine(_contentRootDirectory, relative));
        string fromRoot = Path.GetRelativePath(_contentRootDirectory, resolved);
        if (fromRoot == ".." || fromRoot.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            Path.IsPathRooted(fromRoot))
        {
            throw new ArgumentException("The asset path is outside the content root.", nameof(assetPath));
        }
        string segmentPath = _contentRootDirectory;
        foreach (string segment in fromRoot.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            segmentPath = Path.Combine(segmentPath, segment);
            if ((File.GetAttributes(segmentPath) & FileAttributes.ReparsePoint) != 0)
            {
                throw new ArgumentException("Symbolic links are not supported below the content root.", nameof(assetPath));
            }
        }
        return resolved;
    }
}
