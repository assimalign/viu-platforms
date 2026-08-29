using System;
using System.IO;

namespace Assimalign.Cohesion.Viu.Server;

internal static class ViuContentTypes
{
    public static string Get(string path)
    {
        string extension = Path.GetExtension(RemoveCompressionExtension(path));
        return extension.ToLowerInvariant() switch
        {
            ".wasm" => "application/wasm",
            ".webcil" or ".dll" or ".pdb" or ".dat" or ".blat" => "application/octet-stream",
            ".mjs" or ".cjs" or ".js" => "text/javascript",
            ".json" => "application/json",
            ".html" or ".htm" => "text/html; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".ico" => "image/x-icon",
            ".txt" => "text/plain; charset=utf-8",
            _ => "application/octet-stream",
        };
    }

    public static bool IsHtml(string path)
    {
        string extension = Path.GetExtension(RemoveCompressionExtension(path));
        return extension.Equals(".html", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".htm", StringComparison.OrdinalIgnoreCase);
    }

    private static string RemoveCompressionExtension(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".br", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".gz", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(path)
            : path;
    }
}
