using System.Text;

namespace AFH.Location.Application.Advisers;

public static class SkillKeyNormaliser
{
    private static readonly char[] Separators =
    [
        ' ',
        '\t',
        '\r',
        '\n'
    ];

    public static IReadOnlyList<string> ToSkillKeys(
        IEnumerable<string>? skills)
    {
        if (skills is null)
            return [];

        return skills
            .Select(ToSkillKey)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToArray();
    }

    public static string ToSkillKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        value = string.Join(
            " ",
            value.Trim()
                .Split(
                    Separators,
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries));

        value = value
            .ToLowerInvariant()
            .Replace("&", "and")
            .Replace("/", " ")
            .Replace("-", " ");

        var builder = new StringBuilder();
        var previousDash = false;

        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
                previousDash = false;
                continue;
            }

            if (!previousDash)
            {
                builder.Append('-');
                previousDash = true;
            }
        }

        return builder
            .ToString()
            .Trim('-');
    }
}
