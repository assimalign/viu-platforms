using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;

using Assimalign.Cohesion.Viu.Markdown.Generator;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

using Shouldly;

using Xunit;

namespace Assimalign.Cohesion.Viu.Markdown.Tests;

public sealed class GeneratorTests
{
    [Fact]
    public void GeneratedDeclarativeTableMatchesRuntimeExtractionAndCompiles()
    {
        ContentText[] files = [
            new("index.md", "# Home\n\nWelcome.\n\n[Guide](guide/index.md)"),
            new("guide/index.md", "# Guide\n\nGuide summary.\n\n[B](b.md) [A](a.md)"),
            new("guide/a.md", "# A\n\nFirst summary.\n\n> **Status:** Partial."),
            new("guide/b.md", "# B\n\nSecond summary.")];
        GeneratorDriverRunResult result = Run(files, out Compilation output);
        result.Diagnostics.ShouldBeEmpty();
        output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        string generated = result.Results.Single().GeneratedSources.Single().SourceText.ToString();
        generated.ShouldContain("namespace TestContent;");
        generated.ShouldNotContain("MarkdownText");
        generated.ShouldNotContain("catalog-input");
        MarkdownContentCatalogBuilder builder = new();
        foreach (ContentText file in files)
        {
            builder.Add(file.RelativePath, file.Text);
        }
        MarkdownContentCatalog expected = builder.Build();
        ReadPageEntries(generated).ShouldBe(expected.Pages.Select(page =>
            new MarkdownPageEntry(page.RoutePath, page.AssetPath, page.Title, page.Description, page.Status,
                page.SourceRelativePath, page.Order)));
    }

    [Fact]
    public void GeneratedTextIsDeterministicAcrossInputOrderAndChangesIncrementally()
    {
        ContentText first = new("index.md", "# Home\n\nDescription.");
        ContentText second = new("second.md", "# Second");
        string forward = Generated(Run([first, second], out _));
        string reversed = Generated(Run([second, first], out _));
        reversed.ShouldBe(forward);

        GeneratorDriver driver = CreateDriver([first, second]);
        driver = driver.RunGenerators(CreateCompilation());
        driver = driver.ReplaceAdditionalText(second, new ContentText("second.md", "# Changed"));
        driver = driver.RunGenerators(CreateCompilation());
        Generated(driver.GetRunResult()).ShouldContain("\"Changed\"");
        Generated(driver.GetRunResult()).ShouldNotContain("\"Second\"");
    }

    [Fact]
    public void GeneratorReportsDuplicateRoutesMissingHeadingsAndBrokenIndexLinks()
    {
        GeneratorDriverRunResult result = Run([
            new("guide.md", "Missing heading\n\nDescription."),
            new("guide/index.md", "# Guide\n\nDescription.\n\n[Broken](../missing.md)")], out _);
        result.Diagnostics.Select(diagnostic => diagnostic.Id).OrderBy(identifier => identifier)
            .ShouldBe(["VIUMD001", "VIUMD002", "VIUMD003"]);
        result.Diagnostics.Single(diagnostic => diagnostic.Id == "VIUMD001").Severity.ShouldBe(DiagnosticSeverity.Error);
        result.Diagnostics.Single(diagnostic => diagnostic.Id == "VIUMD002").Severity.ShouldBe(DiagnosticSeverity.Warning);
        result.Results.Single().GeneratedSources.ShouldBeEmpty();
    }

    [Fact]
    public void MissingHeadingFallsBackAndUnselectedAdditionalFilesAreIgnored()
    {
        GeneratorDriverRunResult result = Run([
            new("plain-page.md", "Plain\n\nDescription."),
            new("ignored.md", "", selected: false)], out _);
        result.Diagnostics.Single().Id.ShouldBe("VIUMD002");
        ReadPageEntries(Generated(result)).Single().Title.ShouldBe("Plain Page");
    }

    [Fact]
    public void CustomPrefixAndRouteBaseFlowToGeneratedMetadata()
    {
        GeneratorDriver driver = CreateDriver([new ContentText("guide/index.md", "# Guide")],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["build_property.ViuMarkdownGeneratedNamespace"] = "Custom.Content",
                ["build_property.ViuMarkdownContentPrefix"] = "assets/content/",
                ["build_property.ViuMarkdownRouteBase"] = "/manual/",
            });
        driver = driver.RunGenerators(CreateCompilation());
        MarkdownPageEntry entry = ReadPageEntries(Generated(driver.GetRunResult())).Single();
        entry.RoutePath.ShouldBe("/manual/guide");
        entry.AssetPath.ShouldBe("assets/content/guide/index.md");
    }

    [Fact]
    public void HashedAssetNamesFromBuildMetadataReplaceTheSourcePathInAssetPathsOnly()
    {
        // Arrange: the build supplies the packaged asset name; the source path stays the identity.
        ContentText[] files = [
            new("index.md", "# Home\n\nWelcome.\n\n[Guide](guide/index.md)", assetName: "a1b2c3d4e5f6.md"),
            new("guide/index.md", "# Guide\n\nSummary.\n\n[Home](../index.md)", assetName: "0f1e2d3c4b5a.md")];
        GeneratorDriver driver = CreateDriver(files, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["build_property.ViuMarkdownGeneratedNamespace"] = "TestContent",
            ["build_property.ViuMarkdownContentPrefix"] = "_content/Docs/docs/",
            ["build_property.ViuMarkdownRouteBase"] = "/docs",
        });

        // Act
        driver = driver.RunGenerators(CreateCompilation());
        MarkdownPageEntry[] entries = ReadPageEntries(Generated(driver.GetRunResult())).ToArray();

        // Assert
        entries.Select(entry => entry.AssetPath).ShouldBe(["_content/Docs/docs/a1b2c3d4e5f6.md", "_content/Docs/docs/0f1e2d3c4b5a.md"]);
        entries.Select(entry => entry.SourceRelativePath).ShouldBe(["index.md", "guide/index.md"]);
        entries.Select(entry => entry.RoutePath).ShouldBe(["/docs", "/docs/guide"]);
    }

    [Fact]
    public void InvalidNamespaceAndDuplicateSourcePathsAreBuildErrors()
    {
        GeneratorDriver driver = CreateDriver([new ContentText("index.md", "# Home")],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["build_property.ViuMarkdownGeneratedNamespace"] = "Invalid-Name",
            });
        driver = driver.RunGenerators(CreateCompilation());
        driver.GetRunResult().Diagnostics.Single().Id.ShouldBe("VIUMD005");
        Run([new("index.md", "# Home"), new("index.md", "# Duplicate")], out _)
            .Diagnostics.Select(diagnostic => diagnostic.Id).ShouldContain("VIUMD004");
        Run([new("../outside.md", "# Outside")], out _)
            .Diagnostics.Select(diagnostic => diagnostic.Id).ShouldContain("VIUMD006");
    }

    [Fact]
    public void ViuDocsRouteFixtureDerivesAllFortyFiveIdenticalRoutes()
    {
        string[] expected = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", "viu-docs-routes.tsv"))
            .Where(line => line.Length > 0 && !line.StartsWith('#')).OrderBy(line => line, StringComparer.Ordinal).ToArray();
        expected.Length.ShouldBe(45);
        ContentText[] files = expected.Select(line => new ContentText(line.Split('\t')[1][5..], "# Fixture\n\nDescription.")).ToArray();
        GeneratorDriverRunResult result = Run(files, out Compilation output);
        result.Diagnostics.ShouldBeEmpty();
        output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        ReadPageEntries(Generated(result)).Select(entry => entry.RoutePath + "\t" + entry.AssetPath)
            .OrderBy(line => line, StringComparer.Ordinal).ShouldBe(expected);
    }

    private static GeneratorDriverRunResult Run(ContentText[] files, out Compilation output)
    {
        GeneratorDriver driver = CreateDriver(files).RunGeneratorsAndUpdateCompilation(CreateCompilation(), out output, out _);
        return driver.GetRunResult();
    }

    private static GeneratorDriver CreateDriver(ContentText[] files, Dictionary<string, string>? options = null)
        => CSharpGeneratorDriver.Create(
            generators: [new MarkdownContentGenerator().AsSourceGenerator()],
            additionalTexts: files,
            parseOptions: new CSharpParseOptions(LanguageVersion.Preview),
            optionsProvider: new ContentOptionsProvider(options ?? new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["build_property.ViuMarkdownGeneratedNamespace"] = "TestContent",
            }));

    private static CSharpCompilation CreateCompilation()
    {
        string[] runtimeAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        IEnumerable<MetadataReference> references = runtimeAssemblies
            .Append(typeof(MarkdownContentCatalog).Assembly.Location)
            .Distinct(StringComparer.Ordinal).Select(path => MetadataReference.CreateFromFile(path));
        return CSharpCompilation.Create("GeneratedCatalogTests", references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static string Generated(GeneratorDriverRunResult result)
        => result.Results.Single().GeneratedSources.Single().SourceText.ToString();

    private static IEnumerable<MarkdownPageEntry> ReadPageEntries(string source)
    {
        foreach (ImplicitObjectCreationExpressionSyntax creation in CSharpSyntaxTree.ParseText(source).GetRoot()
            .DescendantNodes().OfType<ImplicitObjectCreationExpressionSyntax>()
            .Where(creation => creation.ArgumentList.Arguments.Count == 7))
        {
            SeparatedSyntaxList<ArgumentSyntax> arguments = creation.ArgumentList.Arguments;
            string Value(int index) => ((LiteralExpressionSyntax)arguments[index].Expression).Token.ValueText;
            yield return new MarkdownPageEntry(Value(0), Value(1), Value(2), Value(3),
                arguments[4].Expression.IsKind(SyntaxKind.NullLiteralExpression) ? null : Value(4),
                Value(5), int.Parse(Value(6), System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    private sealed class ContentText(string relativePath, string text, bool selected = true, string? assetName = null) : AdditionalText
    {
        internal string RelativePath { get; } = relativePath;
        internal string Text { get; } = text;
        internal bool Selected { get; } = selected;
        internal string? AssetName { get; } = assetName;
        public override string Path => "/catalog-input/" + RelativePath;
        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(Text);
    }

    private sealed class ContentOptionsProvider(Dictionary<string, string> properties) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new ContentOptions(properties);
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new ContentOptions([]);
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            ContentText content = (ContentText)textFile;
            Dictionary<string, string> values = new(StringComparer.Ordinal)
            {
                ["build_metadata.AdditionalFiles.ViuMarkdownContent"] = content.Selected ? "true" : "false",
                ["build_metadata.AdditionalFiles.ViuMarkdownSourceRelativePath"] = content.RelativePath,
            };
            if (content.AssetName is not null)
            {
                values["build_metadata.AdditionalFiles.ViuMarkdownAssetName"] = content.AssetName;
            }
            return new ContentOptions(values);
        }
    }

    private sealed class ContentOptions(Dictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value) => values.TryGetValue(key, out value!);
    }
}
