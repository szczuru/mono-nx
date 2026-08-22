using System;
using System.Diagnostics;
using static SDL2.SDL;

/// <summary>
/// Faza 1: AOT stress — sam SDL + sztuczny „framebuffer” 512x240.
/// Bez RecompOne / game.recomp. Cel: pomiar FPS native ARM64.
/// </summary>
public static class Program
{
    const int WinW = 960;
    const int WinH = 540;
    const int SrcW = 512;
    const int SrcH = 240;

    public static void Main(string[] args)
    {
        Console.WriteLine("[AOT-Phase1] start");
        if (SDL_Init(SDL_INIT_VIDEO | SDL_INIT_JOYSTICK) != 0)
        {
            Console.WriteLine("SDL_Init: " + SDL_GetError());
            return;
        }

        var window = SDL_CreateWindow("AOT Phase1", SDL_WINDOWPOS_UNDEFINED, SDL_WINDOWPOS_UNDEFINED, WinW, WinH, 0);
        if (window == IntPtr.Zero) { Console.WriteLine(SDL_GetError()); return; }

        SDL_SetHint(SDL_HINT_RENDER_SCALE_QUALITY, "nearest");
        var renderer = SDL_CreateRenderer(window, -1, SDL_RendererFlags.SDL_RENDERER_ACCELERATED);
        if (renderer == IntPtr.Zero)
            renderer = SDL_CreateRenderer(window, -1, SDL_RendererFlags.SDL_RENDERER_SOFTWARE);
        if (renderer == IntPtr.Zero) { Console.WriteLine(SDL_GetError()); return; }

        var texture = SDL_CreateTexture(renderer, SDL_PIXELFORMAT_ABGR8888,
            (int)SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING, SrcW, SrcH);
        if (texture == IntPtr.Zero) { Console.WriteLine(SDL_GetError()); return; }

        var pixels = new uint[SrcW * SrcH];
        var sw = Stopwatch.StartNew();
        int frames = 0;
        int t = 0;

        while (true)
        {
            while (SDL_PollEvent(out var evt) != 0)
            {
                if (evt.type == SDL_EventType.SDL_QUIT) goto end;
                if (evt.type == SDL_EventType.SDL_KEYDOWN && evt.key.keysym.sym == SDL_Keycode.SDLK_ESCAPE)
                    goto end;
            }

            // tani wzór zamiast VRAM — tylko obciążenie upload+scale
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

            SDL_RenderClear(renderer);
            SDL_RenderCopy(renderer, texture, IntPtr.Zero, IntPtr.Zero);
            SDL_RenderPresent(renderer);
            frames++;

            if (sw.ElapsedMilliseconds >= 1000)
            {
                Console.WriteLine($"[AOT-Phase1] FPS={frames}");
                frames = 0;
                sw.Restart();
            }
        }
    end:
        SDL_DestroyTexture(texture);
        SDL_DestroyRenderer(renderer);
        SDL_DestroyWindow(window);
        SDL_Quit();
        Console.WriteLine("[AOT-Phase1] end");
    }
}
