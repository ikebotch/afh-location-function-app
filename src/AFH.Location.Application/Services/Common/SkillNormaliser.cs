namespace AFH.Location.Application.Services.Common;

public static class SkillNormaliser
{
    public static IReadOnlyList<string> NormaliseSkills(IEnumerable<string>? skills)
    {
        if (skills is null)
            return [];

        return skills
            .Select(NormaliseSkill)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToArray();
    }

    public static string NormaliseSkill(string? skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            return string.Empty;

        return skill
            .Trim()
            .Trim('"')
            .Replace("\\u0022", string.Empty)
            .Replace("\u0022", string.Empty)
            .Replace("\\u0026", "&")
            .Replace("\u0026", "&")
            .Trim();
    }

    public static string ToCsv(IEnumerable<string>? skills)
    {
        return string.Join('|', NormaliseSkills(skills));
    }

    public static IReadOnlyList<string> FromCsv(string? skillsCsv)
    {
        if (string.IsNullOrWhiteSpace(skillsCsv))
            return [];

        return NormaliseSkills(
            skillsCsv.Split(
                '|',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries));
    }
}