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
    Application.Run(new MainForm());
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
