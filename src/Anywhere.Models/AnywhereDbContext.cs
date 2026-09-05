using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Anywhere.Models;

public sealed class AnywhereDbContext : DbContext {
  private readonly string _dbPath;

  public AnywhereDbContext(string dbPath) => _dbPath = dbPath;

  public DbSet<AgentProfile> Profiles => Set<AgentProfile>();
  public DbSet<Session> Sessions => Set<Session>();
  public DbSet<Message> Messages => Set<Message>();

  public static string DefaultDbPath() {
    string dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Anywhere");
    Directory.CreateDirectory(dir);
    return Path.Combine(dir, "acp-client.db");
  }

  protected override void OnConfiguring(DbContextOptionsBuilder options)
      => options.UseSqlite($"Data Source={_dbPath}");

  protected override void OnModelCreating(ModelBuilder modelBuilder) {
    modelBuilder.Entity<AgentProfile>()
        .Property(p => p.Args)
        .HasConversion(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions?)null) ?? Array.Empty<string>());

    modelBuilder.Entity<AgentProfile>()
        .Property(p => p.Env)
        .HasConversion(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new());
  }
}
