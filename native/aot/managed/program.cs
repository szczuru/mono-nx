using System;
using System.Diagnostics;
using System.IO;
using static SDL2.SDL;

public static class Program
{
    const int WinW = 960;
    const int WinH = 540;
    const int SrcW = 512;
    const int SrcH = 240;

    static readonly string[] LogPaths =
    {
        "sdmc:/switch/aot_phase1_log.txt",
        "sdmc:/aot_phase1_log.txt",
        "/switch/aot_phase1_log.txt",
        "/aot_phase1_log.txt",
    };

    static int _lastFps;
    static bool _logOk;

    static void Log(string msg)
    {
        try { Console.WriteLine(msg); } catch { }

        foreach (var path in LogPaths)
        {
            try
            {
                File.AppendAllText(path, msg + Environment.NewLine);
                _logOk = true;
                return;
            }
            catch { }
        }
    }

    public static void Main(string[] args)
    {
        Log("[AOT-Phase1] start");

        if (SDL_Init(SDL_INIT_VIDEO | SDL_INIT_JOYSTICK) != 0)
        {
            Log("SDL_Init: " + SDL_GetError());
            return;
        }

        var window = SDL_CreateWindow("AOT Phase1",
            SDL_WINDOWPOS_UNDEFINED, SDL_WINDOWPOS_UNDEFINED, WinW, WinH, 0);
        if (window == IntPtr.Zero)
        {
            Log("Window: " + SDL_GetError());
            SDL_Quit();
            return;
        }

        SDL_SetHint(SDL_HINT_RENDER_SCALE_QUALITY, "nearest");
        var renderer = SDL_CreateRenderer(window, -1, SDL_RendererFlags.SDL_RENDERER_ACCELERATED);
        if (renderer == IntPtr.Zero)
            renderer = SDL_CreateRenderer(window, -1, SDL_RendererFlags.SDL_RENDERER_SOFTWARE);
        if (renderer == IntPtr.Zero)
        {
            Log("Renderer: " + SDL_GetError());
            SDL_DestroyWindow(window);
            SDL_Quit();
            return;
        }

        var texture = SDL_CreateTexture(renderer, SDL_PIXELFORMAT_ABGR8888,
            (int)SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING, SrcW, SrcH);
        if (texture == IntPtr.Zero)
        {
            Log("Texture: " + SDL_GetError());
            SDL_DestroyRenderer(renderer);
            SDL_DestroyWindow(window);
            SDL_Quit();
            return;
        }

        Log("[AOT-Phase1] SDL OK");

        var pixels = new uint[SrcW * SrcH];
        var sw = Stopwatch.StartNew();
        int frames = 0;
        int t = 0;
        bool running = true;

        while (running)
        {
            while (SDL_PollEvent(out var evt) != 0)
            {
                if (evt.type == SDL_EventType.SDL_QUIT) running = false;
                if (evt.type == SDL_EventType.SDL_KEYDOWN &&
                    evt.key.keysym.sym == SDL_Keycode.SDLK_ESCAPE)
                    running = false;
            }

            t++;
            for (int i = 0; i < pixels.Length; i++)
            {
                byte c = (byte)((i + t) & 0xFF);
                pixels[i] = (uint)(c | (c << 8) | (0x40 << 16) | (0xFFu << 24));
            }

            unsafe
            {
                fixed (uint* p = pixels)
                    SDL_UpdateTexture(texture, IntPtr.Zero, (IntPtr)p, SrcW * 4);
            }

            // tło: zielone jeśli log się zapisał kiedykolwiek, czerwone jeśli nie
            if (_logOk)
                SDL_SetRenderDrawColor(renderer, 0, 40, 0, 255);
            else
                SDL_SetRenderDrawColor(renderer, 40, 0, 0, 255);
            SDL_RenderClear(renderer);

            SDL_RenderCopy(renderer, texture, IntPtr.Zero, IntPtr.Zero);

            // pasek FPS u dołu: szerokość ~ FPS (max 60 → pełna szerokość)
            int barW = Math.Clamp(_lastFps * WinW / 60, 0, WinW);
            byte g = (byte)Math.Clamp(_lastFps * 4, 0, 255);
            SDL_SetRenderDrawColor(renderer, (byte)(255 - g), g, 0, 255);
            var bar = new SDL_Rect { x = 0, y = WinH - 24, w = barW, h = 24 };
            SDL_RenderFillRect(renderer, ref bar);

            SDL_RenderPresent(renderer);
            frames++;

            if (sw.ElapsedMilliseconds >= 1000)
            {
                _lastFps = frames;
                Log($"[AOT-Phase1] FPS={frames}");
                frames = 0;
                sw.Restart();
            }
        }

        SDL_DestroyTexture(texture);
        SDL_DestroyRenderer(renderer);
        SDL_DestroyWindow(window);
        SDL_Quit();
        Log("[AOT-Phase1] end");
    }
}
