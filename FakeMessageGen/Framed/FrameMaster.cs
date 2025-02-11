using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

class FrameMaster : IDisposable
{
    public static readonly FrameMaster Instance = new();
    public readonly List<Frame> frames = new();
    public static readonly TextWriter Out = Console.Out;

    int Height;
    int Width;

    public event EventHandler<(int h, int w)> OnResize;
    public event EventHandler<(int h, int w)> OnResizeAfter;

    Timer UpdateUITimer;

    public void Run(int updateInterval = 1000 / 60) // FPS
    {
        Console.CursorVisible = false;
        UpdateUITimer = new Timer(Refresh, null, updateInterval, updateInterval);
    }

    void Refresh(object state)
    {
        lock (UpdateUITimer)
        {
            Update();
        }
    }

    void Update()
    {
        var height = Console.WindowHeight;
        var width = Console.WindowWidth;

        if (Width != width || Height != height)
        {
            Height = height;
            Width = width;
            Console.Clear();
            OnResize?.Invoke(this, (height, width));
            OnResizeAfter?.Invoke(this, (height, width));
        }

        foreach (var f in frames) f.Render();
    }

    public void Dispose()
    {
        UpdateUITimer.Dispose();
        Console.SetOut(FrameMaster.Out); // Reset console out
    }
}