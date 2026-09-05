using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Anywhere.Models;
using Anywhere.Persistence;
using Xunit;

public class MessageRepositoryTests : IDisposable {
  private readonly string dbPath;
  private readonly AnywhereDbContext db;

  public MessageRepositoryTests() {
    dbPath = Path.Combine(Path.GetTempPath(), $"acp_test_{Guid.NewGuid():N}.db");
    db = new AnywhereDbContext(dbPath);
    db.Database.EnsureCreated();
  }

  [Fact]
  public async Task Messages_persist_and_list_in_insertion_order() {
    ProfileRepository profiles = new ProfileRepository(db);
    int profileId = await profiles.InsertAsync(new AgentProfile {
      Name = "Test Agent",
      Command = "echo",
      Args = Array.Empty<string>(),
      Env = new System.Collections.Generic.Dictionary<string, string>(),
    });

    SessionRepository sessions = new SessionRepository(db);
    int sessionId = await sessions.InsertAsync(profileId, @"C:\work");

    MessageRepository messages = new MessageRepository(db);
    await messages.InsertAsync(sessionId, "user", "hello", null);
    await messages.InsertAsync(sessionId, "agent", "hi there", null);

    List<Message> history = await messages.ListForSessionAsync(sessionId);

    Assert.Equal(2, history.Count);
    Assert.Equal("user", history[0].Role);
    Assert.Equal("hello", history[0].Content);
    Assert.Equal("agent", history[1].Role);
  }

  public void Dispose() {
    db.Dispose();
    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    File.Delete(dbPath);
  }
}
