using System;
using System.IO;
using System.Threading.Tasks;
using Anywhere.Models;
using Anywhere.Persistence;
using Xunit;

public class SessionRepositoryTests : IDisposable {
  private readonly string dbPath;
  private readonly AnywhereDbContext db;

  public SessionRepositoryTests() {
    dbPath = Path.Combine(Path.GetTempPath(), $"acp_test_{Guid.NewGuid():N}.db");
    db = new AnywhereDbContext(dbPath);
    db.Database.EnsureCreated();
  }

  [Fact]
  public async Task UpdateWorkingDirAsync_overwrites_in_place() {
    var profiles = new ProfileRepository(db);
    int profileId = await profiles.InsertAsync(new AgentProfile {
      Name = "Agent",
      Command = "agent",
      Args = Array.Empty<string>(),
      Env = new System.Collections.Generic.Dictionary<string, string>(),
    });

    var sessions = new SessionRepository(db);
    int sessionId = await sessions.InsertAsync(profileId, @"C:\one");

    await sessions.UpdateWorkingDirAsync(sessionId, @"C:\two");

    var fetched = await sessions.ListAllAsync();
    Assert.Equal(@"C:\two", Assert.Single(fetched).WorkingDir);
  }

  public void Dispose() {
    db.Dispose();
    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    File.Delete(dbPath);
  }
}
