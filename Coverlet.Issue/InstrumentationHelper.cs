using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace Coverlet.Issue;

internal static class InstrumentationHelper
{
    private static readonly RegexOptions s_regexOptions = RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.IgnoreCase;

    public static bool IsValidFilterExpression(string filter)
    {
        if (filter == null)
            return false;

        if (!filter.StartsWith("["))
            return false;

        if (!filter.Contains("]"))
            return false;

        if (filter.Count(f => f == '[') > 1)
            return false;

        if (filter.Count(f => f == ']') > 1)
            return false;

        if (filter.IndexOf(']') < filter.IndexOf('['))
            return false;

        if (filter.IndexOf(']') - filter.IndexOf('[') == 1)
            return false;

        if (filter.EndsWith("]"))
            return false;

        if (new Regex(@"[^\w*]", s_regexOptions, TimeSpan.FromSeconds(10)).IsMatch(filter.Replace(".", "").Replace("?", "").Replace("[", "").Replace("]", "")))
            return false;

        return true;
    }

    public static bool IsModuleExcluded(string module, string[] excludeFilters)
    {
        if (excludeFilters == null || excludeFilters.Length == 0)
            return false;

        module = Path.GetFileNameWithoutExtension(module);
        if (module == null)
            return false;

        foreach (var filter in excludeFilters)
        {
            #pragma warning disable IDE0057 // Use range operator
            var typePattern = filter.Substring(filter.IndexOf(']') + 1);

            if (typePattern != "*")
                continue;

            var modulePattern = filter.Substring(1, filter.IndexOf(']') - 1);
            #pragma warning restore IDE0057 // Use range operator
            modulePattern = WildcardToRegex(modulePattern);

            var regex = new Regex(modulePattern, s_regexOptions, TimeSpan.FromSeconds(10));

            if (regex.IsMatch(module))
                return true;
        }

        return false;
    }

    public static bool IsModuleIncluded(string module, string[] includeFilters)
    {
        if (includeFilters == null || includeFilters.Length == 0)
            return true;

        module = Path.GetFileNameWithoutExtension(module);
        if (module == null)
            return false;

        foreach (var filter in includeFilters)
        {
            #pragma warning disable IDE0057 // Use range operator
            var modulePattern = filter.Substring(1, filter.IndexOf(']') - 1);
            #pragma warning restore IDE0057 // Use range operator

            if (modulePattern == "*")
                return true;
            modulePattern = WildcardToRegex(modulePattern);

            var regex = new Regex(modulePattern, s_regexOptions, TimeSpan.FromSeconds(10));

            if (regex.IsMatch(module))
                return true;
        }

        return false;
    }

    public static IEnumerable<string> SelectModules(IEnumerable<string> modules, string[] includeFilters, string[] excludeFilters)
    {
        var escapeSymbol = '!';
        var modulesLookup = modules
            .Where(x => x != null)
            .ToLookup(x => $"{escapeSymbol}{Path.GetFileNameWithoutExtension(x)}{escapeSymbol}");

        var regexInput = string.Join(Environment.NewLine, modulesLookup.Select(x => x.Key));

        var validIncludeFilters = (includeFilters ?? Array.Empty<string>())
            .Where(IsValidFilterExpression)
            .Where(x => x.EndsWith("*"))
            .ToArray();

        if (validIncludeFilters.Any())
        {
            var regexPatterns = validIncludeFilters.Select(x =>
                $"{escapeSymbol}{WildcardToRegex(x.Substring(1, x.IndexOf(']') - 1)).Trim('^', '$')}{escapeSymbol}");
            var pattern = string.Join("|", regexPatterns);
            var matches = Regex.Matches(regexInput, pattern, RegexOptions.IgnoreCase).Cast<Match>();

            // Select only the modules that match the include filters
            regexInput = string.Join(
                Environment.NewLine,
                matches.Where(x => x.Success).Select(x => x.Groups[0].Value));
        }

        var validExcludeFilters = (excludeFilters ?? Array.Empty<string>())
            .Where(IsValidFilterExpression)
            .Where(x => x.EndsWith("*"))
            .ToArray();
        var excludedModules = ImmutableHashSet<string>.Empty;
        if (validExcludeFilters.Any())
        {
            var regexPatterns = validExcludeFilters.Select(x =>
                $"{escapeSymbol}{WildcardToRegex(x.Substring(1, x.IndexOf(']') - 1)).Trim('^', '$')}{escapeSymbol}");
            var pattern = string.Join("|", regexPatterns);
            var matches = Regex.Matches(regexInput, pattern, RegexOptions.IgnoreCase).Cast<Match>();
            excludedModules = matches.Where(x => x.Success).Select(x => x.Groups[0].Value).ToImmutableHashSet();
        }

        return regexInput
            .Split(new[] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries)
            .Where(x => !excludedModules.Contains(x))
            .SelectMany(x => modulesLookup[x]);
    }

    private static string WildcardToRegex(string pattern)
    {
        return "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", "?") + "$";
    }
}
