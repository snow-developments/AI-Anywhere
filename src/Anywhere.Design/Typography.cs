using System.Drawing;
using System.Runtime.Versioning;

namespace Anywhere.Design;

[SupportedOSPlatform("windows")]
public static class Typography {
  public static Font Body() => new("Segoe UI", 9f);
  public static Font Monospace() => new("Cascadia Mono", 9f);
}
