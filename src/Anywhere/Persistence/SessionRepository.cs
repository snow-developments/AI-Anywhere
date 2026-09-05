using Anywhere.Models;
using Microsoft.EntityFrameworkCore;

namespace Anywhere.Persistence;

public sealed class SessionRepository {
  private readonly AnywhereDbContext db;

  public SessionRepository(AnywhereDbContext db) => this.db = db;

  public async Task<int> InsertAsync(int profileId, string workingDir) {
    var session = new Session { ProfileId = profileId, WorkingDir = workingDir };
    db.Sessions.Add(session);
    await db.SaveChangesAsync();
    return session.Id;
  }

  /// <summary>
  /// Overwrites a session's working directory in place. The directory is a
  /// runtime detail the user can change at any point in a conversation; ACP
  /// requires a fresh agent session to pick it up (see the per-conversation
  /// working-directory plan).
  /// </summary>
  public async Task UpdateWorkingDirAsync(int sessionId, string workingDir) {
    var session = await db.Sessions.FindAsync(sessionId)
      ?? throw new InvalidOperationException($"Session {sessionId} not found.");
    session.WorkingDir = workingDir;
    await db.SaveChangesAsync();
  }

  public Task<List<Session>> ListAllAsync()
      => db.Sessions.AsNoTracking().OrderByDescending(s => s.Id).ToListAsync();
}
