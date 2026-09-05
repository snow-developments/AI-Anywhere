using System.Reflection;

namespace Anywhere;

/// <summary>
/// System tray icon shown for the lifetime of the application. Owns the
/// "Show / Exit" context menu and re-opens the <see cref="SplashForm"/> when
/// the user double-clicks the icon or picks "Show".
/// </summary>
internal sealed class TrayIcon : IDisposable {
  private readonly NotifyIcon notifyIcon;
  private readonly SplashForm splash;

  internal TrayIcon(SplashForm splash) {
    this.splash = splash;
    notifyIcon = new NotifyIcon {
      Icon = LoadAppIcon(),
      Text = "Anywhere",
      Visible = true
    };

    var menu = BuildContextMenu();
    notifyIcon.ContextMenuStrip = menu;

    notifyIcon.DoubleClick += (_, _) => ShowSplash();
  }

  // Modern-style tray popup: delegates rendering to the OS via
  // ToolStripSystemRenderer, so the popup picks up the user's current
  // light/dark theme and visual styles automatically (matches the app's
  // Application.SetColorMode(System)). Avoids a custom ProfessionalColorTable,
  // which leaves item foreground/background mismatched and renders empty.
  private ContextMenuStrip BuildContextMenu() {
    var menu = new ContextMenuStrip {
      RenderMode = ToolStripRenderMode.System,
      ShowCheckMargin = false,
      ShowImageMargin = true,
    };
    menu.Items.Add("Show", null, (_, _) => ShowSplash());
    menu.Items.Add(new ToolStripSeparator());
    menu.Items.Add("Exit", null, (_, _) => Application.Exit());
    return menu;
  }

  private static Icon LoadAppIcon() {
    // Anywhere.Anywhere.ico is embedded in the entry assembly (see Anywhere.csproj).
    var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Anywhere.Anywhere.ico")
      ?? throw new InvalidOperationException("Embedded icon 'Anywhere.Anywhere.ico' not found.");
    return new Icon(stream);
  }

  private void ShowSplash() {
    if (splash.IsDisposed) return;
    if (splash.Visible) {
      splash.BringToFront();
      if (splash.WindowState == FormWindowState.Minimized) {
        splash.WindowState = FormWindowState.Normal;
      }
      return;
    }
    splash.Show();
  }

  public void Dispose() {
    notifyIcon.Visible = false;
    notifyIcon.Dispose();
  }
}
