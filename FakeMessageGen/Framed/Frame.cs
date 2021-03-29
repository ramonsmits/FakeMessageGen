using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

class Frame : TextWriter
{
    readonly List<object> data = new();
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
    
    public override void Write(char value)
    {
        if (value == '\n')
        {
            data.Add(current.ToString());
            current.GetStringBuilder().Clear();
        }
        else if(char.IsControl(value))
        {
            return;
        }
        else
        {
            current.Write(value);
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
        Console.ForegroundColor = ConsoleColor.DarkGray;
        var beam = Title.PadRight(Position.Width, '═');
        Console.SetCursorPosition(Position.Left, Position.Top);
        
        FrameMaster.Out.Write(beam);
        //Console.Write(beam);
        Console.ForegroundColor = ConsoleColor.Gray;
    }

    static string spinChars = "\\|/-";
    int spinStep;
 
    [Conditional("DEBUG")]
    void Spin()
    {
        var c = spinChars[++spinStep % spinChars.Length];
        Console.SetCursorPosition(Position.Left+Position.Width-1,Position.Top);
        FrameMaster.Out.Write(c);
    }
    
    public void Render()
    {
        // If no new data has been added, the frame content does not need to be rendered again if all other frame behave
        if (!update) return;
        update = false;

        lock (FrameMaster.Instance)
        {
            Spin();

            var set = data.TakeLast(Position.Height - 1).ToArray();

            for (var i = 0; i < Position.Height - 1; i++)
            {
                var line = set.Length > i ? set[i].ToString() : filler;

                var row = Position.Top + 1 + i;

                Console.SetCursorPosition(Position.Left, row);

                if (line.Length > Position.Width)
                {
                    line = line.Substring(0, Position.Width - 1) + ">";
                }
                else
                {
                    line = line.PadRight(Position.Width, ' ');
                }

                FrameMaster.Out.Write(line);
            }
        }
    }
}