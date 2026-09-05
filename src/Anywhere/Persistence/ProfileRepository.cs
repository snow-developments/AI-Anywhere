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

  public async Task UpdateAsync(AgentProfile profile) {
    // GetAsync/ListAllAsync return detached (AsNoTracking) entities, but an
    // InsertAsync earlier in the same context leaves its instance tracked —
    // Update() on a second instance with the same key then throws an identity
    // conflict. Evict any stale tracked copy first.
    var tracked = db.ChangeTracker.Entries<AgentProfile>()
      .FirstOrDefault(e => e.Entity.Id == profile.Id);
    if (tracked is not null) tracked.State = EntityState.Detached;

    db.Profiles.Update(profile);
    await db.SaveChangesAsync();
  }

  public async Task DeleteAsync(int id) {
    var profile = await db.Profiles.FindAsync(id);
    if (profile is null) return;
    db.Profiles.Remove(profile);
    await db.SaveChangesAsync();
  }
}
