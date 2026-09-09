using System;
using System.Runtime.InteropServices;

namespace Questlog.Tracking;

public static class IdleDetector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    public static TimeSpan GetIdleTime()
    {
        var lastInput = new LASTINPUTINFO();
        lastInput.cbSize = (uint)Marshal.SizeOf(lastInput);

        if (GetLastInputInfo(ref lastInput))
        {
            uint currentTick = (uint)Environment.TickCount;
            uint idleTicks = currentTick - lastInput.dwTime;
            return TimeSpan.FromMilliseconds(idleTicks);
        }

        return TimeSpan.Zero;
    }

    public static bool IsIdle(double idleThresholdSeconds = 120)
    {
        return GetIdleTime().TotalSeconds >= idleThresholdSeconds;
    }
}
