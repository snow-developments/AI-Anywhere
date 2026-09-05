namespace Anywhere.Controls;

public record PermissionRequest(
  string RequestId,
  string ToolName,
  string Description,
  string? OldContent,
  string? NewContent);
