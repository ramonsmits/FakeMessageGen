using System;
using System.Drawing;
using System.IO;

static partial class Program
{
    static Frame queueFrame;
    static Frame mainFrame;
    static Frame logFrame;

    static TextWriter queue = Console.Out;
    static TextWriter main = Console.Out;
    static TextWriter log = Console.Out;

    static FrameMaster InitFrames()
    {
        var instance = FrameMaster.Instance;

        queue = queueFrame = new Frame { Title = "Queue length" };
        main = mainFrame = new Frame { Title = "Main" };
        log = logFrame = new Frame { Title = "Log" };

        instance.OnResize += (_, ea) =>
        {
            var (h, w) = ea;
            const int heightTop = 16;
            mainFrame.UpdatePosition(new Rectangle(0, 0, w / 2 - 1, heightTop));
            queueFrame.UpdatePosition(new Rectangle(w / 2, 0, w / 2, heightTop));
            logFrame.UpdatePosition(new Rectangle(0, heightTop, w, h - heightTop));
        };

        Console.WriteLine();
        Console.SetOut(main);

        instance.Run();

        return instance;
    }
}