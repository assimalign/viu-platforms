using System;
using System.Collections.Generic;
using System.Linq;

namespace Assimalign.Cohesion.Viu.Markdown.Internal;

internal sealed class ExtractedPage
{
    internal string RoutePath = "";
    internal string AssetPath = "";
    internal string SourceRelativePath = "";
    internal string Title = "";
    internal string Description = "";
    internal string? Status;
    internal int Order;
    internal readonly List<string> NavigationLinks = new List<string>();
}

internal sealed class ExtractedSection
{
    internal string Path = "";
    internal string Title = "";
    internal int Order;
    internal readonly List<ExtractedPage> Pages = new List<ExtractedPage>();
    internal readonly List<ExtractedSection> Sections = new List<ExtractedSection>();
}

internal sealed class ExtractionDiagnostic
{
    internal ExtractionDiagnostic(string identifier, string path, string message, bool error = false)
    {
        Identifier = identifier;
        Path = path;
        Message = message;
        IsError = error;
    }

    internal string Identifier { get; }
    internal string Path { get; }
    internal string Message { get; }
    internal bool IsError { get; }
}

internal static class ContentExtraction
{
    internal static ExtractedPage Extract(string path, string markdown, string prefix, string routeBase,
        Action<ExtractionDiagnostic> report, string? assetName = null)
    {
        string relativePath = NormalizePath(path);
        string assetRelativePath = string.IsNullOrWhiteSpace(assetName) ? relativePath : NormalizePath(assetName!);
        if (relativePath.Split('/').Any(segment => segment == "..") || relativePath.Contains(':'))
        {
            report(new ExtractionDiagnostic("VIUMD006", relativePath,
                "The source path must remain beneath the configured content root.", true));
        }
        string[] lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        string firstLine = lines[0].TrimStart('\uFEFF');
        bool hasHeading = firstLine.StartsWith("# ", StringComparison.Ordinal);
        string title = hasHeading ? HeadingTitle(firstLine.Substring(2)) : "";
        if (!hasHeading || title.Length == 0)
        {
            title = Humanize(FileName(relativePath));
            report(new ExtractionDiagnostic("VIUMD002", relativePath,
                "The first line has no nonempty H1 heading; the title falls back to the file name."));
        }

        string route = relativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? relativePath.Substring(0, relativePath.Length - 3) : relativePath;
        if (route == "index")
        {
            route = "";
        }
        else if (route.EndsWith("/index", StringComparison.Ordinal))
        {
            route = route.Substring(0, route.Length - 6);
        }

        string assetPrefix = NormalizePath(prefix).TrimEnd('/');
        ExtractedPage page = new ExtractedPage
        {
            RoutePath = NormalizeRoute(routeBase.TrimEnd('/') + "/" + route),
            AssetPath = assetPrefix.Length > 0 ? assetPrefix + "/" + assetRelativePath : assetRelativePath,
            SourceRelativePath = relativePath,
            Title = title,
        };

        int cursor = 2;
        List<string> description = new List<string>();
        while (cursor < lines.Length && lines[cursor].Trim().Length > 0 &&
            !lines[cursor].StartsWith("#", StringComparison.Ordinal) &&
            !lines[cursor].StartsWith(">", StringComparison.Ordinal))
        {
            description.Add(lines[cursor++].Trim());
        }
        page.Description = string.Join(" ", description);
        while (cursor < lines.Length && lines[cursor].Trim().Length == 0)
        {
            cursor++;
        }
        const string statusMarker = "> **Status:**";
        if (cursor < lines.Length && lines[cursor].StartsWith(statusMarker, StringComparison.Ordinal))
        {
            List<string> status = new List<string> { lines[cursor++].Substring(statusMarker.Length).Trim() };
            while (cursor < lines.Length && lines[cursor].StartsWith(">", StringComparison.Ordinal))
            {
                status.Add(lines[cursor++].Substring(1).Trim());
            }
            page.Status = string.Join(" ", status);
        }

        if (FileName(relativePath) == "index")
        {
            ReadNavigationLinks(lines, page.NavigationLinks);
        }
        return page;
    }

    internal static ExtractedSection Arrange(IEnumerable<ExtractedPage> input, Action<ExtractionDiagnostic> report)
    {
        List<ExtractedPage> pages = input.Select(ClonePage).OrderBy(page => page.SourceRelativePath, StringComparer.Ordinal).ToList();
        Dictionary<string, ExtractedPage> routes = new Dictionary<string, ExtractedPage>(StringComparer.Ordinal);
        Dictionary<string, ExtractedPage> paths = new Dictionary<string, ExtractedPage>(StringComparer.Ordinal);
        foreach (ExtractedPage page in pages)
        {
            if (routes.ContainsKey(page.RoutePath))
            {
                report(new ExtractionDiagnostic("VIUMD001", page.SourceRelativePath,
                    "Duplicate route '" + page.RoutePath + "'.", true));
            }
            else
            {
                routes.Add(page.RoutePath, page);
            }
            if (paths.ContainsKey(page.SourceRelativePath))
            {
                report(new ExtractionDiagnostic("VIUMD004", page.SourceRelativePath,
                    "Duplicate source path '" + page.SourceRelativePath + "'.", true));
            }
            else
            {
                paths.Add(page.SourceRelativePath, page);
            }
        }

        foreach (ExtractedPage index in pages.Where(page => FileName(page.SourceRelativePath) == "index"))
        {
            foreach (string link in index.NavigationLinks)
            {
                string target = ResolvePath(index.SourceRelativePath, link);
                if (!paths.ContainsKey(target))
                {
                    report(new ExtractionDiagnostic("VIUMD003", index.SourceRelativePath,
                        "Relative Markdown link '" + link + "' does not resolve to a content page."));
                }
            }
        }

        return CreateSection("", pages, paths);
    }

    internal static string NormalizePath(string path) => path.Replace('\\', '/').TrimStart('/');

    private static ExtractedPage ClonePage(ExtractedPage source)
    {
        ExtractedPage page = new ExtractedPage
        {
            RoutePath = source.RoutePath,
            AssetPath = source.AssetPath,
            SourceRelativePath = source.SourceRelativePath,
            Title = source.Title,
            Description = source.Description,
            Status = source.Status,
            Order = source.Order,
        };
        page.NavigationLinks.AddRange(source.NavigationLinks);
        return page;
    }

    internal static string NormalizeRoute(string path)
    {
        string normalized = path.Replace('\\', '/').Trim();
        return normalized.Length == 0 || normalized == "/" ? "/" : "/" + normalized.Trim('/');
    }

    internal static string ResolvePath(string currentPath, string destination)
    {
        List<string> segments = new List<string>();
        string path = destination;
        int suffix = path.IndexOfAny(new[] { '#', '?' });
        if (suffix >= 0)
        {
            path = path.Substring(0, suffix);
        }
        if (!path.StartsWith("/", StringComparison.Ordinal))
        {
            AddSegments(segments, Folder(currentPath));
        }
        AddSegments(segments, path);
        return string.Join("/", segments);
    }

    private static void AddSegments(List<string> segments, string path)
    {
        foreach (string segment in path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }
            if (segment == "..")
            {
                if (segments.Count > 0)
                {
                    segments.RemoveAt(segments.Count - 1);
                }
                continue;
            }
            segments.Add(segment);
        }
    }

    private static ExtractedSection CreateSection(string folder, List<ExtractedPage> pages,
        Dictionary<string, ExtractedPage> paths)
    {
        paths.TryGetValue(folder.Length == 0 ? "index.md" : folder + "/index.md", out ExtractedPage? index);
        ExtractedSection section = new ExtractedSection
        {
            Path = folder,
            Title = index?.Title ?? (folder.Length == 0 ? "Content" : Humanize(folder.Substring(folder.LastIndexOf('/') + 1))),
        };
        List<string> links = index?.NavigationLinks.Select(link => ResolvePath(index.SourceRelativePath, link)).ToList()
            ?? new List<string>();
        string prefix = folder.Length == 0 ? "" : folder + "/";
        List<string> children = pages.Where(page => page.SourceRelativePath.StartsWith(prefix, StringComparison.Ordinal))
            .Select(page => page.SourceRelativePath.Substring(prefix.Length))
            .Where(relative => relative.Contains('/'))
            .Select(relative => prefix + relative.Substring(0, relative.IndexOf('/')))
            .Distinct(StringComparer.Ordinal).ToList();
        List<NavigationItem> navigation = pages.Where(page => Folder(page.SourceRelativePath) == folder)
            .Select(page => new NavigationItem(page.SourceRelativePath, page,
                ReferenceEquals(page, index) ? -1 : NavigationRank(links, page.SourceRelativePath, false)))
            .Concat(children.Select(child => new NavigationItem(child, null, NavigationRank(links, child, true))))
            .OrderBy(item => item.Rank)
            .ThenBy(item => item.Page?.Order ?? 0)
            .ThenBy(item => item.Path, StringComparer.Ordinal).ToList();
        for (int order = 0; order < navigation.Count; order++)
        {
            NavigationItem item = navigation[order];
            if (item.Page is ExtractedPage page)
            {
                page.Order = order;
                section.Pages.Add(page);
            }
            else
            {
                ExtractedSection child = CreateSection(item.Path, pages, paths);
                child.Order = order;
                section.Sections.Add(child);
            }
        }
        return section;
    }

    private sealed class NavigationItem(string path, ExtractedPage? page, int rank)
    {
        internal string Path { get; } = path;
        internal ExtractedPage? Page { get; } = page;
        internal int Rank { get; } = rank;
    }

    private static int NavigationRank(List<string> links, string path, bool folder)
    {
        for (int index = 0; index < links.Count; index++)
        {
            if (folder ? links[index].StartsWith(path + "/", StringComparison.Ordinal) : links[index] == path)
            {
                return index;
            }
        }
        return int.MaxValue;
    }

    private static string Folder(string path)
    {
        int separator = path.LastIndexOf('/');
        return separator < 0 ? "" : path.Substring(0, separator);
    }

    private static string FileName(string path)
    {
        string name = path.Substring(path.LastIndexOf('/') + 1);
        return name.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - 3) : name;
    }

    private static string Humanize(string value)
    {
        string[] words = value.Replace('-', ' ').Replace('_', ' ').Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words.Select(word => char.ToUpperInvariant(word[0]) + word.Substring(1)));
    }

    private static string HeadingTitle(string value)
    {
        string title = value.Trim();
        int closingStart = title.Length;
        while (closingStart > 0 && title[closingStart - 1] == '#')
        {
            closingStart--;
        }
        return closingStart < title.Length && (closingStart == 0 || char.IsWhiteSpace(title[closingStart - 1]))
            ? title.Substring(0, closingStart).TrimEnd() : title;
    }

    private static void ReadNavigationLinks(string[] lines, List<string> destinations)
    {
        char fence = '\0';
        int fenceLength = 0;
        foreach (string line in lines)
        {
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                int length = trimmed.TakeWhile(character => character == trimmed[0]).Count();
                if (fence == '\0')
                {
                    fence = trimmed[0];
                    fenceLength = length;
                }
                else if (trimmed[0] == fence && length >= fenceLength)
                {
                    fence = '\0';
                }
                continue;
            }
            if (fence != '\0' || line.StartsWith("    ", StringComparison.Ordinal) || line.StartsWith("\t", StringComparison.Ordinal))
            {
                continue;
            }
            List<int> openBrackets = new List<int>();
            for (int index = 0; index < line.Length - 1; index++)
            {
                if (IsEscaped(line, index))
                {
                    continue;
                }
                if (line[index] == '`')
                {
                    int codeEnd = FindCodeSpanEnd(line, index);
                    if (codeEnd >= 0)
                    {
                        index = codeEnd;
                    }
                    continue;
                }
                if (line[index] == '[')
                {
                    openBrackets.Add(index);
                    continue;
                }
                if (line[index] != ']' || openBrackets.Count == 0)
                {
                    continue;
                }
                int openingBracket = openBrackets[openBrackets.Count - 1];
                openBrackets.RemoveAt(openBrackets.Count - 1);
                bool image = openingBracket > 0 && line[openingBracket - 1] == '!' && !IsEscaped(line, openingBracket - 1);
                if (image || line[index + 1] != '(')
                {
                    continue;
                }
                int start = index + 2;
                int end = line.IndexOf(')', start);
                if (end < 0)
                {
                    continue;
                }
                string destination = line.Substring(start, end - start).Trim();
                if (destination.StartsWith("<", StringComparison.Ordinal))
                {
                    int angleEnd = destination.IndexOf('>');
                    destination = angleEnd > 0 ? destination.Substring(1, angleEnd - 1) : destination;
                }
                else
                {
                    int space = destination.IndexOfAny(new[] { ' ', '\t' });
                    if (space >= 0)
                    {
                        destination = destination.Substring(0, space);
                    }
                }
                int suffix = destination.IndexOfAny(new[] { '#', '?' });
                string path = suffix >= 0 ? destination.Substring(0, suffix) : destination;
                if (!path.StartsWith("/", StringComparison.Ordinal) && !path.Contains(':') &&
                    path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    destinations.Add(destination);
                }
                index = end;
            }
        }
    }

    private static bool IsEscaped(string line, int position)
    {
        int backslashes = 0;
        while (position > 0 && line[--position] == '\\')
        {
            backslashes++;
        }
        return (backslashes & 1) != 0;
    }

    private static int FindCodeSpanEnd(string line, int start)
    {
        int openingLength = 1;
        while (start + openingLength < line.Length && line[start + openingLength] == '`')
        {
            openingLength++;
        }
        int cursor = start + openingLength;
        while (cursor < line.Length)
        {
            if (line[cursor] != '`')
            {
                cursor++;
                continue;
            }
            int closingLength = 1;
            while (cursor + closingLength < line.Length && line[cursor + closingLength] == '`')
            {
                closingLength++;
            }
            if (closingLength == openingLength)
            {
                return cursor + closingLength - 1;
            }
            cursor += closingLength;
        }
        return -1;
    }
}
