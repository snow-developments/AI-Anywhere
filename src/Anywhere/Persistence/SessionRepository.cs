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

  public Task<List<Session>> ListAllAsync()
      => db.Sessions.AsNoTracking().OrderByDescending(s => s.Id).ToListAsync();
}
