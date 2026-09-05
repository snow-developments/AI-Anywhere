using Anywhere.Models;
using Microsoft.EntityFrameworkCore;

namespace Anywhere.Persistence;

public sealed class ProfileRepository {
  private readonly AnywhereDbContext db;

  public ProfileRepository(AnywhereDbContext db) => this.db = db;

  public async Task<int> InsertAsync(AgentProfile profile) {
    db.Profiles.Add(profile);
    await db.SaveChangesAsync();
    return profile.Id;
  }

  public Task<AgentProfile?> GetAsync(int id)
      => db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);

  public Task<List<AgentProfile>> ListAllAsync()
      => db.Profiles.AsNoTracking().OrderBy(p => p.Id).ToListAsync();
}
