namespace Anywhere.Models;

public sealed class Message {
  public int Id { get; set; }
  public int SessionId { get; set; }
  public required string Role { get; set; } // "user" | "agent" | "system"
  public required string Content { get; set; }
  public string? ToolCallJson { get; set; }
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
