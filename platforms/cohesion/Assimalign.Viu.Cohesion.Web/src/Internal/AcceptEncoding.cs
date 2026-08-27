using System;
using System.Collections.Generic;
using System.Globalization;

namespace Assimalign.Viu.Cohesion.Web.Internal;

internal static class AcceptEncoding
{
    public static string Select(string? header, IReadOnlyList<string> serverCodings)
    {
        List<AcceptedEncoding> accepted = Parse(header);
        double bestQuality = 0;
        string? bestCoding = null;

        foreach (string serverCoding in serverCodings)
        {
            double quality = GetQuality(accepted, serverCoding);
            if (quality > bestQuality)
            {
                bestQuality = quality;
                bestCoding = serverCoding;
            }
        }

        bool identityExplicit = TryGetIdentityQuality(accepted, out double identityQuality);
        if (bestCoding is not null)
        {
            return identityExplicit && identityQuality > bestQuality
                ? "identity"
                : bestCoding;
        }

        // RFC 9110 permits either 406 or ignoring a field that refuses every representation.
        // Static asset serving deliberately degrades to identity, matching Cohesion StaticFiles.
        return "identity";
    }

    private static List<AcceptedEncoding> Parse(string? header)
    {
        List<AcceptedEncoding> accepted = [];
        if (string.IsNullOrWhiteSpace(header))
        {
            return accepted;
        }

        ReadOnlySpan<char> value = header.AsSpan();
        foreach (Range range in value.Split(','))
        {
            ReadOnlySpan<char> item = value[range].Trim();
            int semicolon = item.IndexOf(';');
            ReadOnlySpan<char> coding = (semicolon < 0 ? item : item[..semicolon]).Trim();
            if (coding.IsEmpty)
            {
                continue;
            }

            double quality = 1;
            ReadOnlySpan<char> parameters = semicolon < 0
                ? ReadOnlySpan<char>.Empty
                : item[(semicolon + 1)..];
            foreach (Range parameterRange in parameters.Split(';'))
            {
                ReadOnlySpan<char> parameter = parameters[parameterRange].Trim();
                int equals = parameter.IndexOf('=');
                if (equals < 0
                    || !parameter[..equals].Trim().Equals("q", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                quality = double.TryParse(
                    parameter[(equals + 1)..].Trim(),
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out double parsed)
                    ? Math.Clamp(parsed, 0, 1)
                    : 0;
                break;
            }

            accepted.Add(new AcceptedEncoding(coding.ToString(), quality));
        }

        return accepted;
    }

    private static double GetQuality(List<AcceptedEncoding> accepted, string coding)
    {
        double wildcard = -1;
        foreach (AcceptedEncoding entry in accepted)
        {
            if (entry.Coding.Equals(coding, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Quality;
            }
            if (entry.Coding == "*" && wildcard < 0)
            {
                wildcard = entry.Quality;
            }
        }

        return wildcard < 0 ? 0 : wildcard;
    }

    private static bool TryGetIdentityQuality(
        List<AcceptedEncoding> accepted,
        out double quality)
    {
        quality = 0;
        double wildcard = -1;
        foreach (AcceptedEncoding entry in accepted)
        {
            if (entry.Coding.Equals("identity", StringComparison.OrdinalIgnoreCase))
            {
                quality = entry.Quality;
                return true;
            }
            if (entry.Coding == "*" && wildcard < 0)
            {
                wildcard = entry.Quality;
            }
        }

        if (wildcard < 0)
        {
            return false;
        }

        quality = wildcard;
        return true;
    }

    private sealed record AcceptedEncoding(string Coding, double Quality);
}
