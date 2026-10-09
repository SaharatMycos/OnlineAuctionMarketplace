using System.Text.RegularExpressions;

namespace Marketplace.SharedKernel.Configuration;

/// <summary>
/// Loads the repository-root <c>.env</c> (next to <c>Marketplace.slnx</c>) into environment variables, so
/// <c>dotnet run</c> and <c>dotnet ef</c> use the same secrets as docker compose. Variables that are
/// already set win, and a missing file is a no-op (containers get their settings from compose instead).
/// Supports <c>KEY=VALUE</c>, <c>#</c> comments, optional quotes and <c>${VAR}</c> references.
/// </summary>
public static partial class DotEnv
{
    public const string FileName = ".env";

    /// <summary>Returns the loaded file's path, or null when there is none.</summary>
    public static string? Load()
    {
        var path = FindFile();
        if (path is null) return null;

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (line.StartsWith("export ", StringComparison.Ordinal)) line = line[7..].TrimStart();

            var eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var key = line[..eq].Trim();
            var value = Unquote(line[(eq + 1)..].Trim());
            value = Reference().Replace(value, m =>
                Environment.GetEnvironmentVariable(m.Groups[1].Value)
                ?? values.GetValueOrDefault(m.Groups[1].Value)
                ?? "");
            values[key] = value;

            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, value);
        }
        return path;
    }

    private static string? FindFile()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                if (!File.Exists(Path.Combine(dir.FullName, "Marketplace.slnx"))) continue;
                var path = Path.Combine(dir.FullName, FileName);
                return File.Exists(path) ? path : null;
            }
        }
        return null;
    }

    private static string Unquote(string value) =>
        value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0] ? value[1..^1] : value;

    [GeneratedRegex(@"\$\{([A-Za-z_][A-Za-z0-9_]*)\}")]
    private static partial Regex Reference();
}
