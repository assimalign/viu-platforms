namespace Assimalign.Cohesion.Viu.Markdown;

/// <summary>Describes a resolved link destination and whether the Viu router owns navigation.</summary>
/// <param name="Destination">The route or ordinary anchor destination, including any fragment.</param>
/// <param name="IsInternal">Whether the destination is a Viu route rendered through RouterLink.</param>
public readonly record struct MarkdownLinkResolution(string Destination, bool IsInternal);
