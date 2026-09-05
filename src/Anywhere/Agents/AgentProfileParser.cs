namespace Anywhere.Agents;

/// <summary>
/// Parses the free-text, comma-separated Args field of the profile editor into
/// the <c>string[]</c> stored on <see cref="Anywhere.Models.AgentProfile"/>.
/// Whitespace around each entry is trimmed; spaces within an entry
/// (e.g. <c>--port 4000</c>) are preserved; empty entries are dropped.
/// </summary>
public static class AgentProfileParser {
  public static string[] ParseArgs(string raw)
    => raw.Split(',')
        .Select(part => part.Trim())
        .Where(part => part.Length > 0)
        .ToArray();
}
