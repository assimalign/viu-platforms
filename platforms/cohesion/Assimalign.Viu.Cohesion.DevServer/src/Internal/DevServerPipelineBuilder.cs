using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Assimalign.Cohesion.Http;
using Assimalign.Cohesion.Web;

namespace Assimalign.Viu.Cohesion.DevServer;

internal sealed class DevServerPipelineBuilder : IWebApplicationPipelineBuilder
{
    private readonly DevServerApplicationContext _applicationContext = new();
    private readonly List<Func<WebApplicationMiddleware, WebApplicationMiddleware>> _registrations = [];

    public IWebApplicationPipelineBuilder Use(IWebApplicationMiddleware middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        _registrations.Add(next => context => middleware.InvokeAsync(context, next));
        return this;
    }

    public IWebApplicationPipelineBuilder Use(Func<WebApplicationMiddleware, WebApplicationMiddleware> middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        _registrations.Add(middleware);
        return this;
    }

    public IWebApplicationPipelineBuilder Use(
        Func<IWebApplicationContext, WebApplicationMiddleware, WebApplicationMiddleware> middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        _registrations.Add(next => middleware.Invoke(_applicationContext, next));
        return this;
    }

    public IWebApplicationPipeline Build()
    {
        WebApplicationMiddleware application = NotFoundAsync;
        for (int index = _registrations.Count - 1; index >= 0; index--)
        {
            application = _registrations[index].Invoke(application);
        }

        return new DevServerPipeline(application);
    }

    private static Task NotFoundAsync(IHttpContext context)
    {
        context.Response.StatusCode = HttpStatusCode.NotFound;
        return Task.CompletedTask;
    }
}
