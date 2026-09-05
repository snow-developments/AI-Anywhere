using System;
using System.IO;
using System.Threading.Tasks;
using Anywhere.Models;
using Anywhere.Persistence;
using Xunit;

public class ProfileRepositoryTests : IDisposable {
  private readonly string dbPath;
  private readonly AnywhereDbContext db;

  public ProfileRepositoryTests() {
    dbPath = Path.Combine(Path.GetTempPath(), $"acp_test_{Guid.NewGuid():N}.db");
    db = new AnywhereDbContext(dbPath);
    db.Database.EnsureCreated(); // test fixtures use EnsureCreated for a fast disposable schema;
                                 // production startup uses Database.Migrate() instead (see Task 2 Step 6).
  }

  [Fact]
  public async Task InsertAsync_then_GetAsync_returns_the_same_profile() {
    ProfileRepository repo = new ProfileRepository(db);
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
    Assert.Equal(@"C:\work", fetched.WorkingDir);
  }

  [Fact]
  public async Task InsertAsync_allows_a_null_default_working_dir() {
    ProfileRepository repo = new ProfileRepository(db);
    int id = await repo.InsertAsync(new AgentProfile {
      Name = "No default dir",
      Command = "agent",
      Args = Array.Empty<string>(),
      Env = new System.Collections.Generic.Dictionary<string, string>(),
    });

    Assert.Null((await repo.GetAsync(id))!.WorkingDir);
  }

  [Fact]
  public async Task UpdateAsync_then_GetAsync_returns_the_updated_fields() {
    ProfileRepository repo = new ProfileRepository(db);
    int id = await repo.InsertAsync(new AgentProfile {
      Name = "Original",
      Command = "cmd1",
      Args = Array.Empty<string>(),
      Env = new System.Collections.Generic.Dictionary<string, string>(),
    });

    AgentProfile? toUpdate = await repo.GetAsync(id);
    toUpdate!.Name = "Renamed";
    toUpdate.Command = "cmd2";
    await repo.UpdateAsync(toUpdate);

    AgentProfile? fetched = await repo.GetAsync(id);
    Assert.Equal("Renamed", fetched!.Name);
    Assert.Equal("cmd2", fetched.Command);
  }

  [Fact]
  public async Task DeleteAsync_removes_the_profile() {
    ProfileRepository repo = new ProfileRepository(db);
    int id = await repo.InsertAsync(new AgentProfile {
      Name = "Temp",
      Command = "cmd",
      Args = Array.Empty<string>(),
      Env = new System.Collections.Generic.Dictionary<string, string>(),
    });

    await repo.DeleteAsync(id);

    Assert.Null(await repo.GetAsync(id));
  }

  public void Dispose() {
    db.Dispose();
    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); // releases the pooled native handle so the temp file can be deleted on Windows
    File.Delete(dbPath);
  }
}
