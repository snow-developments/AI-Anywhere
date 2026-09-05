using System;
using System.IO;
using System.Threading.Tasks;
using Anywhere.Models;
using Anywhere.Persistence;
using Xunit;

public class ProfileRepositoryTests : IDisposable {
  private readonly string _dbPath;
  private readonly AnywhereDbContext _db;

  public ProfileRepositoryTests() {
    _dbPath = Path.Combine(Path.GetTempPath(), $"acp_test_{Guid.NewGuid():N}.db");
    _db = new AnywhereDbContext(_dbPath);
    _db.Database.EnsureCreated(); // test fixtures use EnsureCreated for a fast disposable schema;
                                  // production startup uses Database.Migrate() instead (see Task 2 Step 6).
  }

  [Fact]
  public async Task InsertAsync_then_GetAsync_returns_the_same_profile() {
    ProfileRepository repo = new ProfileRepository(_db);
    AgentProfile profile = new AgentProfile {
      Name = "Claude Code",
      Command = "claude-code-acp",
      Args = new[] { "--stdio" },
      Env = new System.Collections.Generic.Dictionary<string, string>(),
      WorkingDir = @"C:\work",
    };

    int id = await repo.InsertAsync(profile);
    AgentProfile? fetched = await repo.GetAsync(id);

    Assert.NotNull(fetched);
    Assert.Equal("Claude Code", fetched!.Name);
    Assert.Equal("claude-code-acp", fetched.Command);
    Assert.Equal(new[] { "--stdio" }, fetched.Args);
  }

  public void Dispose() {
    _db.Dispose();
    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); // releases the pooled native handle so the temp file can be deleted on Windows
    File.Delete(_dbPath);
  }
}
