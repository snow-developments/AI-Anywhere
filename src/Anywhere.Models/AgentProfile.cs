namespace Anywhere.Models;

public sealed class AgentProfile {
  public int Id { get; set; }
  public required string Name { get; set; }
  public required string Command { get; set; }
  public string[] Args { get; set; } = Array.Empty<string>();
  public Dictionary<string, string> Env { get; set; } = new();
  /// <summary>
  /// Optional per-profile default directory that pre-fills the picker when
  /// starting a conversation. Never used directly as a session <c>cwd</c> —
  /// the working directory is a per-conversation runtime detail on
  /// <see cref="Session.WorkingDir"/>.
  /// </summary>
  public string? WorkingDir { get; set; }
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
