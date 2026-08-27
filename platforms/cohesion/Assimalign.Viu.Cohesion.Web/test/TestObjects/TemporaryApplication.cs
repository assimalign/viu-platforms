using System;
using System.IO;

namespace Assimalign.Viu.Cohesion.Web.Tests;

internal sealed class TemporaryApplication : IDisposable
{
    public TemporaryApplication()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "viu-cohesion-tests", Guid.NewGuid().ToString("N"));
        ContentRootPath = Path.Combine(RootPath, "wwwroot");
        Directory.CreateDirectory(ContentRootPath);
    }

    public string RootPath { get; }

    public string ContentRootPath { get; }

    public string WriteContent(string relativePath, string content)
    {
        string path = Path.Combine(ContentRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public string WriteRootFile(string name, string content)
    {
        string path = Path.Combine(RootPath, name);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}
