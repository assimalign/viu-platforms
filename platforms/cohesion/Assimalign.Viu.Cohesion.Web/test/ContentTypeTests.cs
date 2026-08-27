using Assimalign.Viu.Cohesion.Web.Internal;
using Shouldly;
using Xunit;

namespace Assimalign.Viu.Cohesion.Web.Tests;

public sealed class ContentTypeTests
{
    [Theory(DisplayName = "Viu Cohesion Web - content types: maps WebAssembly build artifacts")]
    [InlineData("app.wasm", "application/wasm")]
    [InlineData("app.webcil", "application/octet-stream")]
    [InlineData("app.dll", "application/octet-stream")]
    [InlineData("app.pdb", "application/octet-stream")]
    [InlineData("icudt.dat", "application/octet-stream")]
    [InlineData("timezone.blat", "application/octet-stream")]
    [InlineData("module.mjs", "text/javascript")]
    [InlineData("module.cjs", "text/javascript")]
    [InlineData("boot.json", "application/json")]
    [InlineData("app.wasm.br", "application/wasm")]
    public void Get_KnownWebAssemblyArtifact_ShouldReturnExpectedContentType(string path, string expected)
    {
        // Act
        string actual = ViuContentTypes.Get(path);

        // Assert
        actual.ShouldBe(expected);
    }
}
