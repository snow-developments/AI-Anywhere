using Anywhere.Models;
using Microsoft.EntityFrameworkCore;

namespace Anywhere.Persistence;

public sealed class SessionRepository {
  private readonly AnywhereDbContext _db;

  public SessionRepository(AnywhereDbContext db) => _db = db;

  public async Task<int> InsertAsync(int profileId, string workingDir) {
    var session = new Session { ProfileId = profileId, WorkingDir = workingDir };
    _db.Sessions.Add(session);
    await _db.SaveChangesAsync();
    return session.Id;
  }

  public Task<List<Session>> ListAllAsync()
      => _db.Sessions.AsNoTracking().OrderByDescending(s => s.Id).ToListAsync();
}
