using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

class Frame : TextWriter
{
    const int MaxLength = 100;
    readonly Queue<object> data = new(MaxLength);
    readonly StringWriter current = new();
    string filler;

    public Rectangle Position { get; private set; }
    public string Title = string.Empty;
    bool update;

    public override Encoding Encoding { get; } = Encoding.UTF8;

    public void UpdatePosition(Rectangle value)
    {
        Position = value;
        update = true;
    }

    public override void Write(string value)
    {
        lock (current) // TODO: Not sure if this is actually needed...
        {
            base.Write(value);
        }
    }

    public override void Write(char value)
    {
        lock (current)
        {
            if (value == '\n')
            {
                lock (data)
                {
                    var line = current.ToString();

                    if (!string.IsNullOrEmpty(line))
                    {
                        data.Enqueue(line);
                    }

                    if (data.Count > MaxLength) data.Dequeue();
                }

                current.GetStringBuilder().Clear();
            }
            else if (value == '\e')
            {
                // Do nothing ANSI escape
                current.Write(value);
            }
            else if (char.IsControl(value))
            {
                return;
            }
            else
            {
                current.Write(value);
            }
        }

        update = true;
    }

    public Frame()
    {
        FrameMaster.Instance.frames.Add(this);
        FrameMaster.Instance.OnResizeAfter += (s, ea) =>
        {
            filler = new string(' ', Position.Width);
            RenderBeam();
        };
    }

    void RenderBeam()
    {
        lock (FrameMaster.Out)
        {
            var beam = Ansi.GetAnsiColor(ConsoleColor.White) + Title.PadRight(Position.Width - 2, '═') + Ansi.Reset;
            SetCursorPosition(Position.Top + 1, Position.Left + 1);
            FrameMaster.Out.Write(beam);
        }
    }

    static string spinChars = "⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏"; // Copied from podman

    int spinStep;

    void Spin()
    {
        var c = spinChars[++spinStep % spinChars.Length];
        SetCursorPosition(Position.Top + 1, Position.Left + Position.Width - 1);
        FrameMaster.Out.Write(c);
    }

    readonly List<object> set = new(MaxLength);

    public void Render()
    {
        // If no new data has been added, the frame content does not need to be rendered again if all other frame behave
        if (!update) return;
        update = false;

        lock (FrameMaster.Instance)
        {
            Spin();

            lock (data)
            {
                set.Clear();
                set.AddRange(data.TakeLast(Position.Height - 1));
            }

            for (var i = 0; i < Position.Height - 1; i++)
            {
                var line = set.Count > i ? set[i].ToString() : filler;

                var row = Position.Top + 1 + i;

                SetCursorPosition(row + 1, Position.Left + 1);

                if (line.Length > Position.Width)
                {
                    line = line.Substring(0, Position.Width) + ">";
                }
                else
                {
                    line = line.PadRight(Position.Width, ' ');
                }

                line += Ansi.Reset;

                FrameMaster.Out.Write(line);
            }
        }
    }

    private void SetCursorPosition(int row, int column)
    {
        FrameMaster.Out.Write($"\e[{row};{column}H"); // row, col
    }
}