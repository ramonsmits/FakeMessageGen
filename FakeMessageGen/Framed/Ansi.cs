using System;

static class Ansi
{
    public const string Bold = "\e[1m";
    public const string Underline = "\e[4m";
    public const string Reversed = "\e[7m";
    public const string Reset = "\e[0m";

    public static string GetAnsiColor(ConsoleColor color) => color switch
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

    // Move cursor to specific position (1-based coordinates)
    //string SetCursorPosition(int row, int col) => $"\e[{row};{col}H";

// Or alternatively you can use these to move relative to current position:
    const string CursorUp = "\e[1A";    // Up one line
    const string CursorDown = "\e[1B";   // Down one line
    const string CursorRight = "\e[1C";  // Right one column
    const string CursorLeft = "\e[1D";   // Left one column

// Hide/show cursor
    const string HideCursor = "\e[?25l";
    const string ShowCursor = "\e[?25h";

// Save and restore position
    const string SavePosition = "\e[s";
    const string RestorePosition = "\e[u";
}