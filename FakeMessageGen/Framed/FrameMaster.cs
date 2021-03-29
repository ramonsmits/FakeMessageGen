using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

class FrameMaster
{
    public static readonly FrameMaster Instance = new();
    public readonly List<Frame> frames = new();
    public static readonly TextWriter Out = Console.Out;

    int Height;
    int Width;

    public event EventHandler<(int h, int w)> OnResize;
    public event EventHandler<(int h, int w)> OnResizeAfter;

    static Timer UpdateUITimer;
    
    public static void Run(int updateInterval = 100)
    {
        Console.CursorVisible = false;
        UpdateUITimer = new Timer(Refresh, null, updateInterval, updateInterval);
    }

    static void Refresh(object state)
    {
        lock (UpdateUITimer)
        {
            Instance.Update();
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

        Console.SetWindowPosition(0, 0);
    }
}