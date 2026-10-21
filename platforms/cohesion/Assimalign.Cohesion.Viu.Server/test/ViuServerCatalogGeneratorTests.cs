using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Assimalign.Cohesion.Viu.Server.Generator;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using Shouldly;
using Xunit;

namespace Assimalign.Cohesion.Viu.Server.Tests;

public sealed class ViuServerCatalogGeneratorTests
{
    [Theory]
    [InlineData(OutputKind.ConsoleApplication, null, true)]
    [InlineData(OutputKind.ConsoleApplication, "", true)]
    [InlineData(OutputKind.ConsoleApplication, "false", false)]
    [InlineData(OutputKind.DynamicallyLinkedLibrary, null, false)]
    [InlineData(OutputKind.DynamicallyLinkedLibrary, "true", true)]
    public void CohesionHostPublishesReferencedCatalogsThroughDirectCompilableDelegates(
        OutputKind kind, string? enabled, bool expected)
    {
        CSharpParseOptions parsing = new(LanguageVersion.Preview);
        MetadataReference[] references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator).Append(Path.Combine(AppContext.BaseDirectory, "Assimalign.Viu.Components.dll"))
            .Append(Path.Combine(AppContext.BaseDirectory, "Assimalign.Viu.ServerRenderer.dll"))
            .Distinct(StringComparer.OrdinalIgnoreCase).Select(path => MetadataReference.CreateFromFile(path)).ToArray();
        string librarySource = """
            [assembly: Assimalign.Viu.Components.ComponentCatalog(typeof(Library.Components))]
            [assembly: Assimalign.Viu.ServerRenderer.ServerRenderCatalog(typeof(Library.Renders))]
            namespace Library;
            public static class Components
            {
                public static void Register(Assimalign.Viu.Components.ComponentFactory components) { }
            }
            public static class Renders
            {
                public static void Register(Assimalign.Viu.ServerRenderer.ServerRenderRegistry renders) { }
            }
            """;
        CSharpCompilation library = CSharpCompilation.Create("ViuLibrary",
            [CSharpSyntaxTree.ParseText(librarySource, parsing)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using MemoryStream image = new();
        library.Emit(image).Success.ShouldBeTrue();
        CSharpCompilation host = CSharpCompilation.Create("CohesionWeb",
            [CSharpSyntaxTree.ParseText("public static class Program { public static void Main() { } }", parsing)],
            references.Append(MetadataReference.CreateFromImage(image.ToArray())), new CSharpCompilationOptions(kind));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new ViuServerCatalogGenerator().AsSourceGenerator()], parseOptions: parsing,
            optionsProvider: new CatalogOptionsProvider(enabled));

        driver = driver.RunGeneratorsAndUpdateCompilation(host, out Compilation generated, out ImmutableArray<Diagnostic> diagnostics);

        diagnostics.ShouldBeEmpty();
        generated.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        ImmutableArray<GeneratedSourceResult> sources = driver.GetRunResult().Results.Single().GeneratedSources;
        if (!expected) { sources.ShouldBeEmpty(); return; }
        string source = sources.ShouldHaveSingleItem().SourceText.ToString();
        source.ShouldContain("global::Library.Components.Register");
        source.ShouldContain("global::Library.Renders.Register");
        source.ShouldContain("ViuLibrary, Version=0.0.0.0");
        source.ShouldContain("ModuleInitializer");
        source.ShouldNotContain("GetCustomAttributes");
    }

    private sealed class CatalogOptionsProvider(string? enabled) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new CatalogOptions(enabled);
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;
    }

    private sealed class CatalogOptions(string? enabled) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            value = enabled ?? "";
            return key == "build_property.ViuAutoRegisterGeneratedCatalogs" && enabled is not null;
        }
    }
}
