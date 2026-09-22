using System.Collections.Generic;
using System.Text.Json.Serialization.Metadata;

using Assimalign.Cohesion.ApplicationModel;

namespace Assimalign.Cohesion.ApplicationModel;

internal sealed class ViuWebResourceDescriptor : IViuWebResourceDescriptor
{
    internal ViuWebResourceDescriptor(IWebResourceDescriptor web, ResourceManifest manifest)
    {
        Web = web;
        _ = ViuWebManifestProperties.TryRead(manifest, out string client, out bool fallback, out string path);
        ClientAssembly = client;
        SpaFallback = fallback;
        BasePath = path;
    }

    public IWebResourceDescriptor Web { get; }
    public string ClientAssembly { get; }
    public bool SpaFallback { get; }
    public string BasePath { get; }
    public IApplicationResource Resource => Web.Resource;
    public ResourcePlan? Plan => Web.Plan;
    public IReadOnlyList<IApplicationResourceDescriptor> Dependencies => Web.Dependencies;
    public IReadOnlyList<IResourceCommand> Commands => Web.Commands;

    public IResourceCommand AddCommand<TPayload>(string kind, string key, TPayload payload,
        JsonTypeInfo<TPayload> typeInfo, bool optional = false) => Web.AddCommand(kind, key, payload, typeInfo, optional);

    public IViuWebResourceDescriptor DependsOn(IApplicationResourceDescriptor resource)
    {
        Web.DependsOn(resource);
        return this;
    }

    public IViuWebResourceDescriptor DependsOn(params IApplicationResourceDescriptor[] resources)
    {
        Web.DependsOn(resources);
        return this;
    }

    IApplicationResourceDescriptor IApplicationResourceDescriptor.DependsOn(IApplicationResourceDescriptor resource) => DependsOn(resource);
    IApplicationResourceDescriptor IApplicationResourceDescriptor.DependsOn(params IApplicationResourceDescriptor[] resources) => DependsOn(resources);
}
