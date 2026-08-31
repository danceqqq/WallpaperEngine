using Microsoft.Xna.Framework.Graphics;
using WallpaperEngine.Content;
using WallpaperEngine.Core;
using WallpaperEngine.Audio;

namespace WallpaperEngine.Widgets
{
	internal static class WidgetHost
	{
		internal static bool Busy => WePlayerUI.Busy || DiscordWidget.Busy;

		internal static void Update()
		{
			if (!WeModMenu.OnTitle)
				return;
			WePlayerUI.Update();
			QuoteWidget.Refresh();
			DiscordFeed.Tick();
			DiscordWidget.TickInput();
		}

		internal static void HandleInput()
		{
			if (!WeModMenu.OnTitle)
				return;
			WePlayerUI.HandleInput();
			DiscordWidget.HandleInput();
		}

		internal static void Draw(SpriteBatch spriteBatch, float fade)
		{
			WePlayerUI.Draw(spriteBatch, fade);
			ClockWidget.Draw(spriteBatch, fade);
			QuoteWidget.Draw(spriteBatch, fade);
			MoonWidget.Draw(spriteBatch, fade);
			DiscordWidget.Draw(spriteBatch, fade);
		}
	}
}
