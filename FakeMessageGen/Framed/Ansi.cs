using System;

static class Ansi
{
    public static string Bold { get; private set; } = "\e[1m";
    public static string Underline { get; private set; } = "\e[4m";
    public static string Reversed { get; private set; } = "\e[7m";
    public static string Reset { get; private set; } = "\e[0m";

    static bool enabled = true;

    /// <summary>
    /// Turns every escape sequence into an empty string so plain (redirected) output contains no control characters.
    /// </summary>
    public static void Disable()
    {
        enabled = false;
        Bold = Underline = Reversed = Reset = string.Empty;
    }

    public static string GetAnsiColor(ConsoleColor color) => !enabled ? string.Empty : color switch
    {
        ConsoleColor.Black => "\e[30m",
        ConsoleColor.DarkBlue => "\e[34m",
        ConsoleColor.DarkGreen => "\e[32m",
        ConsoleColor.DarkCyan => "\e[36m",
        ConsoleColor.DarkRed => "\e[31m",
        ConsoleColor.DarkMagenta => "\e[35m",
        ConsoleColor.DarkYellow => "\e[33m",
        ConsoleColor.Gray => "\e[37m",
        ConsoleColor.DarkGray => "\e[90m",
        ConsoleColor.Blue => "\e[94m",
        ConsoleColor.Green => "\e[92m",
        ConsoleColor.Cyan => "\e[96m",
        ConsoleColor.Red => "\e[91m",
        ConsoleColor.Magenta => "\e[95m",
        ConsoleColor.Yellow => "\e[93m",
        ConsoleColor.White => "\e[97m",
        _ => "\e[0m"
    };
}
