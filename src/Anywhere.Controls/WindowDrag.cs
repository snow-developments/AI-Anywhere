using System.Runtime.InteropServices;

namespace Anywhere.Controls;

/// <summary>
/// Win32 interop for dragging borderless windows via a custom title bar.
/// </summary>
public static class WindowDrag {
  private const int wmNclbuttondown = 0x00A1;
  private const int htcaption = 2;

  [DllImport("user32.dll")]
  private static extern bool ReleaseCapture();

  [DllImport("user32.dll", CharSet = CharSet.Unicode)]
  private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

  /// <summary>
  /// Begins a native window-drag operation for the given window handle, as if
  /// the user had clicked its title bar. Call from a control's MouseDown
  /// handler to make a borderless form draggable via a custom title bar.
  /// </summary>
  public static void Begin(IntPtr handle) {
    ReleaseCapture();
    _ = SendMessage(handle, wmNclbuttondown, htcaption, 0);
  }
}
