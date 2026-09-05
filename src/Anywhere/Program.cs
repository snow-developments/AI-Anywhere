namespace Anywhere;

internal static class Program {
  /// <summary>
  ///  The main entry point for the application.
  /// </summary>
  [STAThread]
  internal static void Main() {
    AppDomain.CurrentDomain.UnhandledException += (_, e) => HandleException(e.ExceptionObject as Exception);
    Application.ThreadException += (_, e) => HandleException(e.Exception);
    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

    // To customize application configuration such as set high DPI settings or default font,
    // see https://aka.ms/applicationconfiguration.
    ApplicationConfiguration.Initialize();
    // Follow the OS light/dark preference using WinForms' built-in color mode.
#pragma warning disable WFO5001 // Application.SetColorMode is experimental (dark mode).
    Application.SetColorMode(SystemColorMode.System);
#pragma warning restore WFO5001

    var splash = new SplashForm();
    using var tray = new TrayIcon(splash);
    Application.Run(splash);
  }

  private static void HandleException(Exception? e) {
    if (e is null) return;

    var result = MessageBox.Show(
      $"An unhandled exception occurred:\n\n{(e.InnerException != null ? $"{e.Message}\n\n{e.InnerException.Message}" : e.Message)}",
      "AI Anywhere Error",
      MessageBoxButtons.AbortRetryIgnore,
      MessageBoxIcon.Error);

    if (result == DialogResult.Abort) {
      Environment.Exit(1);
    } else if (result == DialogResult.Retry) {
      Application.Restart();
    }
  }
}
