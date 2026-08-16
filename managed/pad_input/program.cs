using System.Diagnostics;
using System.Runtime.InteropServices;

// This is a workaround for an issue in the interpreter builds. It should not affect this specific program but it's left here as a reference. See the writeup for more info.
AppContext.SetSwitch("System.Resources.UseSystemResourceKeys", true);

#pragma warning disable CA1418 // Of course, this SDK doesn't know about our fork 
if (!OperatingSystem.IsOSPlatform("libnx"))
{ 
	Console.WriteLine("This example is only for mono-nx");
	return;
}

[DllImport("__Internal")] static extern void console_ensure_init();
[DllImport("__Internal")] static extern void console_update();

console_ensure_init();

LibnxPad pad = new();
while (LibnxApplet.appletMainLoop())
{
	pad.Update();

	if (pad.ButtonsDown.HasFlag(HidNpadButton.Plus))
		break;

	// Debugging example: open this folder in vscode with the mono debug extension and attach to it!
	if (pad.ButtonsDown.HasFlag(HidNpadButton.Minus))
		Debugger.Break();

	// The console class is not implemented so Console.Clear() will throw NotImplementedException
	// However you can manually use terminal escape codes to clear the screen
	Console.Write("\x1b[1;1H\x1b[2J");

	Console.WriteLine($"Buttons: {pad.Buttons}");
	Console.WriteLine($"Left stick: {pad.State.sticks_0}");
	Console.WriteLine($"Right stick: {pad.State.sticks_1}");

	Console.WriteLine("");
	Console.WriteLine("Press start to exit");

	// This locks us to the refresh rate
	console_update();
}