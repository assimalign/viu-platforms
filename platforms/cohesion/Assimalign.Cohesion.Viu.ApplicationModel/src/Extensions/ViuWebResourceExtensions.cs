using System;

using Assimalign.Cohesion.ApplicationModel;

namespace Assimalign.Cohesion.ApplicationModel;

/// <summary>Composes Viu sites using the standard Web resource planner.</summary>
public static class ViuWebResourceExtensions
{
    extension(IApplicationBuilder builder)
    {
        /// <summary>Validates a Viu manifest, adds its Web resource, and wraps the descriptor.</summary>
        /// <param name="manifest">The build-produced Web manifest.</param>
        /// <param name="options">Optional validation and replica overrides.</param>
        /// <returns>The typed site descriptor.</returns>
        /// <exception cref="ArgumentException">The kind or required Viu properties are invalid.</exception>
        public IViuWebResourceDescriptor AddViuWeb(ResourceManifest manifest, ViuWebResourceOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            Validate(manifest, options?.ValidateClient ?? true);
            WebResourceOptions webOptions = new() { Replicas = options?.Replicas };
            webOptions.Storage.Size = options?.Storage.Size;
            return new ViuWebResourceDescriptor(builder.AddWeb(manifest, webOptions), manifest);
        }

        /// <summary>Binds a remote Viu Web resource with the standard Web binding behavior.</summary>
        /// <param name="declaration">The external declaration carrying a Web manifest.</param>
        /// <param name="configure">Configures the remote binding.</param>
        /// <returns>The typed remote site descriptor.</returns>
        /// <exception cref="ArgumentException">The manifest or required Viu properties are missing or invalid.</exception>
        public IViuWebResourceDescriptor RemoteReferenceViuWeb(
            ExternalResourceDeclaration declaration, Action<RemoteReferenceOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(declaration);
            ArgumentNullException.ThrowIfNull(configure);
            ResourceManifest manifest = declaration.Manifest
                ?? throw new ArgumentException("A remote Viu Web reference requires a Web manifest produced by a Web executable with a ViuClientReference.", nameof(declaration));
            Validate(manifest, true);
            return new ViuWebResourceDescriptor(builder.RemoteReferenceWeb(declaration, configure), manifest);
        }
    }

    private static void Validate(ResourceManifest manifest, bool validateClient)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (!string.Equals(manifest.Kind, "Web", StringComparison.Ordinal))
        {
            throw new ArgumentException("A Viu site requires Kind 'Web'. Use Assimalign.Cohesion.Sdk.Web with a ViuClientReference.", nameof(manifest));
        }
        if (validateClient) { ViuWebManifestProperties.Validate(manifest); }
    }
}
