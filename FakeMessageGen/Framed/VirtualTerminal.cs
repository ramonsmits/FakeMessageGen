using System;
using System.Runtime.InteropServices;

/// <summary>
/// Detects whether the attached console interprets ANSI/VT escape sequences, which the framed UI and colors depend on.
/// </summary>
static partial class VirtualTerminal
{
    /// <summary>
    /// Returns true when escape sequences will be rendered rather than printed as garbage.
    /// On Windows there is no query for this; the console (conhost) only processes escape sequences once the
    /// application switches on ENABLE_VIRTUAL_TERMINAL_PROCESSING, and cmd.exe does not do that for us
    /// (Windows Terminal does). So the check is: try to enable it, and treat failure as "not supported"
    /// (legacy console, Server 2012 and older, "Use legacy console" enabled, or output not a console at all).
    /// </summary>
    public static bool TryEnable()
    {
        if (Console.IsOutputRedirected)
        {
            return false;
        }

        if (OperatingSystem.IsWindows())
        {
            return TryEnableWindows();
        }

        // Unix terminals interpret escape sequences natively unless the terminal declares itself dumb
        var term = Environment.GetEnvironmentVariable("TERM");
        return !string.IsNullOrEmpty(term) && !term.Equals("dumb", StringComparison.OrdinalIgnoreCase);
    }

    const int STD_OUTPUT_HANDLE = -11;
    const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
    const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;

    static bool TryEnableWindows()
    {
        var handle = GetStdHandle(STD_OUTPUT_HANDLE);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
        {
            return false;
        }

        if (!GetConsoleMode(handle, out var mode))
        {
            return false;
        }

        if ((mode & ENABLE_VIRTUAL_TERMINAL_PROCESSING) != 0)
        {
            return true; // Already on, e.g. Windows Terminal or a parent that enabled it
        }

        // DISABLE_NEWLINE_AUTO_RETURN is optional, retry without it as older builds reject unknown flags
        return SetConsoleMode(handle, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN)
               || SetConsoleMode(handle, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr GetStdHandle(int nStdHandle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
}
