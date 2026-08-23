using System;
using System.Diagnostics;
using System.IO;
using static SDL2.SDL;

/// <summary>
/// Faza 1 AOT: SDL + sztuczny framebuffer 512x240.
/// FPS: Console + plik /aot_phase1_log.txt (bez file_io_redirect mono).
/// </summary>
public static class Program
{
    const int WinW = 960;
    const int WinH = 540;
    const int SrcW = 512;
    const int SrcH = 240;
    const string LogPath = "/aot_phase1_log.txt";

    static void Log(string msg)
    {
        try { Console.WriteLine(msg); } catch { /* ignore */ }
        try { File.AppendAllText(LogPath, msg + "\n"); } catch { /* ignore */ }
    }

    public static void Main(string[] args)
    {
        Log("[AOT-Phase1] start");

        if (SDL_Init(SDL_INIT_VIDEO | SDL_INIT_JOYSTICK) != 0)
        {
            Log("SDL_Init failed: " + SDL_GetError());
            return;
        }

        IntPtr window = SDL_CreateWindow(
            "AOT Phase1",
            SDL_WINDOWPOS_UNDEFINED,
            SDL_WINDOWPOS_UNDEFINED,
            WinW,
            WinH,
            0);

        if (window == IntPtr.Zero)
        {
            Log("SDL_CreateWindow failed: " + SDL_GetError());
            SDL_Quit();
            return;
        }

        SDL_SetHint(SDL_HINT_RENDER_SCALE_QUALITY, "nearest");

        IntPtr renderer = SDL_CreateRenderer(
            window,
            -1,
            SDL_RendererFlags.SDL_RENDERER_ACCELERATED);

        if (renderer == IntPtr.Zero)
        {
            renderer = SDL_CreateRenderer(
                window,
                -1,
                SDL_RendererFlags.SDL_RENDERER_SOFTWARE);
        }

        if (renderer == IntPtr.Zero)
        {
            Log("SDL_CreateRenderer failed: " + SDL_GetError());
            SDL_DestroyWindow(window);
            SDL_Quit();
            return;
        }

        IntPtr texture = SDL_CreateTexture(
            renderer,
            SDL_PIXELFORMAT_ABGR8888,
            (int)SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
            SrcW,
            SrcH);

        if (texture == IntPtr.Zero)
        {
            Log("SDL_CreateTexture failed: " + SDL_GetError());
            SDL_DestroyRenderer(renderer);
            SDL_DestroyWindow(window);
            SDL_Quit();
            return;
        }

        Log("[AOT-Phase1] SDL OK, entering loop");

        var pixels = new uint[SrcW * SrcH];
        var sw = Stopwatch.StartNew();
        int frames = 0;
        int t = 0;

        bool running = true;
        while (running)
        {
            while (SDL_PollEvent(out SDL_Event evt) != 0)
            {
                if (evt.type == SDL_EventType.SDL_QUIT)
                    running = false;

                if (evt.type == SDL_EventType.SDL_KEYDOWN &&
                    evt.key.keysym.sym == SDL_Keycode.SDLK_ESCAPE)
                    running = false;

                // Switch: często pad jako joystick — zostaw pętlę bez wymuszania wyjścia
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
                {
                    SDL_UpdateTexture(texture, IntPtr.Zero, (IntPtr)p, SrcW * 4);
                }
            }

            SDL_RenderClear(renderer);
            SDL_RenderCopy(renderer, texture, IntPtr.Zero, IntPtr.Zero);
            SDL_RenderPresent(renderer);
            frames++;

            if (sw.ElapsedMilliseconds >= 1000)
            {
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
