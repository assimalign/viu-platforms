namespace Assimalign.Cohesion.Viu.Markdown;

/// <summary>Reports deterministic catalog extraction errors and warnings.</summary>
/// <param name="Identifier">The stable diagnostic identifier shared with the source generator.</param>
/// <param name="SourceRelativePath">The file producing the diagnostic.</param>
/// <param name="Message">The explanation without machine-dependent absolute paths.</param>
/// <param name="IsError">Whether this diagnostic prevents catalog creation.</param>
public sealed record MarkdownContentDiagnostic(string Identifier, string SourceRelativePath, string Message, bool IsError);
