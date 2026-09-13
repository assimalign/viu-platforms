using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using Assimalign.Cohesion.Content.Markdown;

namespace Assimalign.Cohesion.Viu.Markdown.Internal;

internal sealed class MarkdownHeadingIdentifiers
{
    private readonly Dictionary<string, int> _duplicateCounts = new(StringComparer.Ordinal);

    internal string Create(IList<MarkdownInline> inlines)
    {
        string text = Flatten(inlines);
        var identifier = new StringBuilder(text.Length);
        bool separatorPending = false;
        foreach (char character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                if (separatorPending && identifier.Length > 0)
                {
                    identifier.Append('-');
                }

                identifier.Append(char.ToLowerInvariant(character));
                separatorPending = false;
            }
            else if (char.IsWhiteSpace(character) || character == '-')
            {
                separatorPending = identifier.Length > 0;
            }
        }

        string basis = identifier.Length == 0 ? "section" : identifier.ToString();
        if (!_duplicateCounts.TryGetValue(basis, out int count))
        {
            _duplicateCounts.Add(basis, 0);
            return basis;
        }

        _duplicateCounts[basis] = ++count;
        return basis + "-" + count.ToString(CultureInfo.InvariantCulture);
    }

    internal static string Flatten(IList<MarkdownInline> inlines)
    {
        var builder = new StringBuilder();
        var pending = new Stack<MarkdownInline>();
        Push(pending, inlines);
        while (pending.TryPop(out MarkdownInline? inline))
        {
            switch (inline)
            {
                case MarkdownLiteral literal:
                    builder.Append(literal.Text);
                    break;
                case MarkdownCodeSpan code:
                    builder.Append(code.Literal);
                    break;
                case MarkdownLineBreak:
                    builder.Append(' ');
                    break;
                case MarkdownLink link:
                    Push(pending, link.Inlines);
                    break;
                case MarkdownImage image:
                    Push(pending, image.Inlines);
                    break;
                case MarkdownEmphasis emphasis:
                    Push(pending, emphasis.Inlines);
                    break;
                case MarkdownStrong strong:
                    Push(pending, strong.Inlines);
                    break;
            }
        }

        return builder.ToString();
    }

    private static void Push(Stack<MarkdownInline> pending, IList<MarkdownInline> inlines)
    {
        for (int index = inlines.Count - 1; index >= 0; index--)
        {
            pending.Push(inlines[index]);
        }
    }
}
