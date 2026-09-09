using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Questlog.Tracking;

public static class ForegroundWindowTracker
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    public record WindowInfo(string ProcessName, string WindowTitle);

    public static WindowInfo? GetActiveWindow()
    {
        try
        {
            var hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return null;

            var titleBuilder = new StringBuilder(512);
            GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
            var title = titleBuilder.ToString().Trim();

            GetWindowThreadProcessId(hWnd, out var processId);
            if (processId == 0) return null;

            string processName = "Unknown";
            try
            {
                using var proc = Process.GetProcessById((int)processId);
                processName = proc.ProcessName;
            }
            catch
            {
                // Process might have terminated
            }

            return new WindowInfo(processName, string.IsNullOrEmpty(title) ? processName : title);
        }
        catch
        {
            return null;
        }
    }
}
