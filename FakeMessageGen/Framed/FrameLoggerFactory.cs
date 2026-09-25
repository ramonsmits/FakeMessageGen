using System;
using System.Diagnostics;
using System.IO;
using System.Text;
//using Microsoft.Azure.Amqp.Framing;
using NServiceBus.Logging;

/// <summary>
/// Routes NServiceBus log output to a <see cref="TextWriter"/>: a <see cref="Frame"/> in interactive mode,
/// plain standard output when the output is redirected.
/// </summary>
class FrameLoggerFactory : ILoggerFactory
{
    readonly TextWriter Writer;
    readonly int MinLevel;

    /// <param name="writer">Destination of the log lines.</param>
    /// <param name="minLevel">0 = Debug, 1 = Info, 2 = Warn, 3 = Error, 4 = Fatal.</param>
    public FrameLoggerFactory(TextWriter writer, int minLevel = 0)
    {
        Writer = writer;
        MinLevel = minLevel;
    }

    public ILog GetLogger(Type type)
    {
        return new Logger(Writer, type.Name, MinLevel);
    }

    public ILog GetLogger(string name)
    {
        return new Logger(Writer, name, MinLevel);
    }

    class Logger : ILog
    {
        readonly int MinLevel;
        static readonly Stopwatch start = Stopwatch.StartNew();
        readonly TextWriter Writer;
        readonly string Name;

        public Logger(TextWriter writer, string name, int minLevel)
        {
            Writer = writer;
            Name = name;
            MinLevel = minLevel;
        }

        public void Debug(string message)
        {
            WL(0, null, message);
        }

        private static string GetAnsiColor(int level) => level switch
        {
            0 => Ansi.GetAnsiColor(ConsoleColor.DarkGreen),
            1 => Ansi.GetAnsiColor(ConsoleColor.Gray),
            2 => Ansi.GetAnsiColor(ConsoleColor.DarkYellow),
            3 => Ansi.GetAnsiColor(ConsoleColor.DarkRed),
            4 => Ansi.GetAnsiColor(ConsoleColor.Red),
            _ => Ansi.Reset
        };

        void WL(int level, Exception ex, string message, params object[] args)
        {
            if (level < MinLevel) return;

            var text = GetAnsiColor(level);
            text += start.Elapsed.TotalSeconds.ToString("N");
            text += " | ";
            text += Name;
            text += " | ";
            text += string.Format(message, args);
            if (ex != null) text += ex.Message;
            text += Ansi.Reset;

            Writer.WriteLine(text);
        }

        public void Debug(string message, Exception exception)
        {
            WL(0, exception, message);
        }

        public void DebugFormat(string format, params object[] args)
        {
            WL(0, null, format, args);
        }

        public void Info(string message)
        {
            WL(1, null, message);
        }

        public void Info(string message, Exception exception)
        {
            WL(1, exception, message);
        }

        public void InfoFormat(string format, params object[] args)
        {
            WL(1, null, format, args);
        }

        public void Warn(string message)
        {
            WL(2, null, message);
        }

        public void Warn(string message, Exception exception)
        {
            WL(2, exception, message);
        }

        public void WarnFormat(string format, params object[] args)
        {
            WL(2, null, format, args);
        }

        public void Error(string message)
        {
            WL(3, null, message);
        }

        public void Error(string message, Exception exception)
        {
            WL(3, exception, message);
        }

        public void ErrorFormat(string format, params object[] args)
        {
            WL(3, null, format, args);
        }

        public void Fatal(string message)
        {
            WL(4, null, message);
        }

        public void Fatal(string message, Exception exception)
        {
            WL(4, exception, message);
        }

        public void FatalFormat(string format, params object[] args)
        {
            WL(4, null, format, args);
        }

        public bool IsDebugEnabled => MinLevel <= 0;
        public bool IsInfoEnabled => MinLevel <= 1;
        public bool IsWarnEnabled => MinLevel <= 2;
        public bool IsErrorEnabled => MinLevel <= 3;
        public bool IsFatalEnabled => MinLevel <= 4;
    }
}