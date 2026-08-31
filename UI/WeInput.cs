using Microsoft.Xna.Framework.Input;
using Terraria;

namespace WallpaperEngine.UI
{
	internal static class WeInput
	{
		internal static bool LeftDown => Mouse.GetState().LeftButton == ButtonState.Pressed;

		internal static bool Edge(ref bool held, ref bool holdLock)
		{
			bool down = LeftDown;
			if (!down)
				holdLock = false;
			bool pressed = down && !held && !holdLock;
			held = down;
			return pressed;
		}

		internal static void LockHold(ref bool holdLock)
		{
			holdLock = true;
			Main.mouseLeftRelease = false;
			Main.blockMouse = true;
		}
	}
}
