using System;
using System.Drawing;

static partial class Program
{
    static Frame queueFrame;
    static Frame mainFrame;
    static Frame logFrame;

    static void InitFrames()
    {
        queueFrame = new Frame {Title = "Queue length"};
        mainFrame = new Frame {Title = "Main"};
        logFrame = new Frame {Title = "Log"};

        FrameMaster.Instance.OnResize += (_, ea) =>
        {
            var (h, w) = ea;
            mainFrame.UpdatePosition(new Rectangle(0, 0, w / 2, h - 10));
            queueFrame.UpdatePosition(new Rectangle(w / 2, 0, w / 2, h - 10));
            logFrame.UpdatePosition(new Rectangle(0, h - 10, w, 10));
        };

        Console.SetOut(mainFrame);
        FrameMaster.Run();
    }
}