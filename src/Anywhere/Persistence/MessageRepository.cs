using Anywhere.Models;
using Microsoft.EntityFrameworkCore;

namespace Anywhere.Persistence;

public sealed class MessageRepository {
  private readonly AnywhereDbContext db;

  public MessageRepository(AnywhereDbContext db) => this.db = db;

  public async Task InsertAsync(int sessionId, string role, string content, string? toolCallJson) {
    db.Messages.Add(new Anywhere.Models.Message {
      SessionId = sessionId,
      Role = role,
      Content = content,
      ToolCallJson = toolCallJson,
    });
    await db.SaveChangesAsync();
  }

  public Task<List<Anywhere.Models.Message>> ListForSessionAsync(int sessionId)
      => db.Messages.AsNoTracking()
          .Where(m => m.SessionId == sessionId)
          .OrderBy(m => m.Id)
          .ToListAsync();
}
