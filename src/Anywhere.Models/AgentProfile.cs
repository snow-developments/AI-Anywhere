namespace Anywhere.Models;

public sealed class AgentProfile {
  public int Id { get; set; }
  public required string Name { get; set; }
  public required string Command { get; set; }
  public string[] Args { get; set; } = Array.Empty<string>();
  public Dictionary<string, string> Env { get; set; } = new();
  public required string WorkingDir { get; set; }
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
