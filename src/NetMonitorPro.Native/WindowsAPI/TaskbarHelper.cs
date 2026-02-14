using System.Runtime.InteropServices;

namespace NetMonitorPro.Native.WindowsAPI;

/// <summary>
/// Detects taskbar position and screen work area for overlay snapping.
/// </summary>
public static class TaskbarHelper
{
    public enum TaskbarPosition
    {
        Bottom, Top, Left, Right, Unknown
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public int lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("shell32.dll")]
    private static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

    private const uint ABM_GETTASKBARPOS = 5;
    private const uint ABE_LEFT = 0;
    private const uint ABE_TOP = 1;
    private const uint ABE_RIGHT = 2;
    private const uint ABE_BOTTOM = 3;

    /// <summary>
    /// Gets the current taskbar position using the Shell API.
    /// </summary>
    public static TaskbarPosition GetTaskbarPosition()
    {
        var data = new APPBARDATA { cbSize = Marshal.SizeOf<APPBARDATA>() };
        SHAppBarMessage(ABM_GETTASKBARPOS, ref data);

        return data.uEdge switch
        {
            ABE_LEFT => TaskbarPosition.Left,
            ABE_TOP => TaskbarPosition.Top,
            ABE_RIGHT => TaskbarPosition.Right,
            ABE_BOTTOM => TaskbarPosition.Bottom,
            _ => TaskbarPosition.Bottom
        };
    }

    /// <summary>
    /// Calculates the overlay snap position near the taskbar.
    /// </summary>
    /// <param name="overlayWidth">Overlay window width.</param>
    /// <param name="overlayHeight">Overlay window height.</param>
    /// <param name="workArea">System work area (from SystemParameters).</param>
    /// <param name="screenWidth">Primary screen width.</param>
    /// <param name="screenHeight">Primary screen height.</param>
    public static (double Left, double Top) GetOverlaySnapPosition(
        double overlayWidth, double overlayHeight,
        (double Left, double Top, double Width, double Height) workArea,
        double screenWidth, double screenHeight)
    {
        const double offset = 5;
        var position = GetTaskbarPosition();

        return position switch
        {
            TaskbarPosition.Bottom => (
                workArea.Left + workArea.Width - overlayWidth - offset,
                workArea.Top + workArea.Height - overlayHeight - offset),

            TaskbarPosition.Top => (
                workArea.Left + workArea.Width - overlayWidth - offset,
                workArea.Top + offset),

            TaskbarPosition.Right => (
                workArea.Left + workArea.Width - overlayWidth - offset,
                workArea.Top + workArea.Height - overlayHeight - offset),

            TaskbarPosition.Left => (
                workArea.Left + offset,
                workArea.Top + workArea.Height - overlayHeight - offset),

            _ => (screenWidth - overlayWidth - offset - 50,
                  screenHeight - overlayHeight - offset - 50)
        };
    }
}
