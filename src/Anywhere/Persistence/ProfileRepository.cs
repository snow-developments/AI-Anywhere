using Anywhere.Models;
using Microsoft.EntityFrameworkCore;

namespace Anywhere.Persistence;

public sealed class ProfileRepository {
  private readonly AnywhereDbContext _db;

  public ProfileRepository(AnywhereDbContext db) => _db = db;

  public async Task<int> InsertAsync(AgentProfile profile) {
    _db.Profiles.Add(profile);
    await _db.SaveChangesAsync();
    return profile.Id;
  }

  public Task<AgentProfile?> GetAsync(int id)
      => _db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);

  public Task<List<AgentProfile>> ListAllAsync()
      => _db.Profiles.AsNoTracking().OrderBy(p => p.Id).ToListAsync();
}
