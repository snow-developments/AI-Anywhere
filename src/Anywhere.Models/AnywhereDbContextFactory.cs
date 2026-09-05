using Microsoft.EntityFrameworkCore.Design;

namespace Anywhere.Models;

public sealed class AnywhereDbContextFactory : IDesignTimeDbContextFactory<AnywhereDbContext> {
  public AnywhereDbContext CreateDbContext(string[] args)
      => new(AnywhereDbContext.DefaultDbPath());
}
