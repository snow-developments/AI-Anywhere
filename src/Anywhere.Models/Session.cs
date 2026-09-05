namespace Anywhere.Models;

public sealed class Session {
  public int Id { get; set; }
  public int ProfileId { get; set; }
  public required string WorkingDir { get; set; }
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
