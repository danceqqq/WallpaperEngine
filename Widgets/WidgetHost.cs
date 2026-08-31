using Microsoft.Xna.Framework.Graphics;
using WallpaperEngine.Content;
using WallpaperEngine.Core;
using WallpaperEngine.Audio;

namespace WallpaperEngine.Widgets
{
	internal static class WidgetHost
	{
		internal static bool Busy => WePlayerUI.Busy;

		internal static void Update()
		{
			if (!WeModMenu.OnTitle)
				return;
			WePlayerUI.Update();
			QuoteWidget.Refresh();
		}

		internal static void HandleInput()
		{
			if (!WeModMenu.OnTitle)
				return;
			WePlayerUI.HandleInput();
		}

		internal static void Draw(SpriteBatch spriteBatch, float fade)
		{
			WePlayerUI.Draw(spriteBatch, fade);
			ClockWidget.Draw(spriteBatch, fade);
			QuoteWidget.Draw(spriteBatch, fade);
			MoonWidget.Draw(spriteBatch, fade);
		}
	}
}
