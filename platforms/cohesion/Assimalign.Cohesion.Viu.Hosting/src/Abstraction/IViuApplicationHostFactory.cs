using Assimalign.Viu;

namespace Assimalign.Cohesion.Viu.Hosting;

/// <summary>Creates a host-specific Viu runtime from a snapshotted composition.</summary>
/// <remarks>
/// Implementations must use explicit construction and registration. Reflection-based activation,
/// assembly scanning, and module initialization are not part of this seam.
/// </remarks>
public interface IViuApplicationHostFactory
{
    /// <summary>Creates the runtime application for one Cohesion host.</summary>
    /// <param name="applicationOptions">
    /// A private options snapshot whose references represent the builder composition at build time.
    /// </param>
    /// <returns>The single-use Viu runtime application owned by the Cohesion host.</returns>
    IApplication CreateApplication(ApplicationOptions applicationOptions);
}
