using System.Threading.Tasks;

using Assimalign.Cohesion.Http;
using Assimalign.Cohesion.Web;
using Assimalign.Cohesion.Web.Hosting;

using Shouldly;

using Xunit;

namespace Assimalign.Cohesion.Viu.Server.Tests;

public sealed class WebApplicationViuExtensionsTests
{
    [Fact(DisplayName = "Viu Cohesion Web - application builder: registered Viu application serves through the built host")]
    public async Task UseViuApplication_RegisteredOnApplicationBuilder_ShouldServeConfiguredApplication()
    {
        // Arrange
        using TemporaryApplication content = new();
        content.WriteContent("index.html", "<html><body>builder front door</body></html>");
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.AddViuApplication(options => options.ContentRootPath = content.ContentRootPath);
        await using WebApplication application = builder.Build();
        TestHttpContext context = new("/", HttpMethod.Get);

        // Act
        application.UseViuApplication();
        IWebApplicationPipeline pipeline = ((IWebApplicationPipelineBuilder)application).Build();
        await pipeline.ExecuteAsync(context);

        // Assert
        context.Response.StatusCode.ShouldBe(HttpStatusCode.Ok);
        context.ReadResponseBody().ShouldContain("builder front door", Case.Sensitive);
    }
}
