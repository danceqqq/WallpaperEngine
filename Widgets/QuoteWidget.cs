using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI.Chat;
using WallpaperEngine.Core;
using WallpaperEngine.Layout;
using WallpaperEngine.UI;

namespace WallpaperEngine.Widgets
{
	internal static class QuoteWidget
	{
		private static string _line = "";
		private static DateTime _stamp;

		internal static bool Enabled => WeSave.Data.QuoteWidget && SceneGraph.Visible(SceneGraph.Quote);

		internal static Vector2 Anchor => SceneGraph.Pixel(SceneGraph.Quote);

		internal static float Scale => SceneGraph.ScaleOf(SceneGraph.Quote);

		internal static Rectangle HitRect()
		{
			Vector2 size = Measure();
			Vector2 pos = Anchor;
			return new Rectangle(
				(int)(pos.X - size.X * 0.5f - 12f),
				(int)(pos.Y - size.Y * 0.5f - 8f),
				(int)size.X + 24,
				(int)size.Y + 16);
		}

		internal static void EnsureFile()
		{
			try {
				WeSave.EnsureFolders();
				if (File.Exists(WeSave.QuotePath))
					return;
				File.WriteAllText(WeSave.QuotePath,
					"# One quote per line. Lines starting with # are ignored.\n" +
					"# Одна цитата на строку. Строки с # игнорируются.\n");
			}
			catch {
			}
		}

		internal static void Refresh()
		{
			if (!WeSave.Data.QuoteWidget)
				return;

			try {
				if (!File.Exists(WeSave.QuotePath)) {
					_line = "";
					return;
				}

				DateTime stamp = File.GetLastWriteTimeUtc(WeSave.QuotePath);
				if (stamp == _stamp && _line != null)
					return;

				_stamp = stamp;
				_line = "";
				foreach (string raw in File.ReadAllLines(WeSave.QuotePath)) {
					string text = raw.Trim();
					if (text.Length == 0 || text.StartsWith("#"))
						continue;
					_line = text;
					break;
				}
			}
			catch {
				_line = "";
			}
		}

		internal static void Draw(SpriteBatch spriteBatch, float fade)
		{
			if (!Enabled || fade <= 0f)
				return;

			string greeting = Greeting();
			string quote = string.IsNullOrEmpty(_line) ? "" : "\"" + _line + "\"";
			var titleFont = FontAssets.DeathText.Value;
			var bodyFont = FontAssets.MouseText.Value;
			float titleScale = 0.38f * Scale;
			float bodyScale = 0.78f * Scale;
			Vector2 titleSize = titleFont.MeasureString(greeting) * titleScale;
			Vector2 quoteSize = string.IsNullOrEmpty(quote) ? Vector2.Zero : bodyFont.MeasureString(quote) * bodyScale;
			float width = Math.Max(titleSize.X, quoteSize.X);
			float height = titleSize.Y + (quoteSize.Y > 0f ? quoteSize.Y + 4f : 0f);
			Vector2 origin = Anchor - new Vector2(width, height) * 0.5f;

			WeDraw.WithLinear(spriteBatch, () => {
				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch, titleFont, greeting, origin,
					WeAccent.Light * fade, 0f, Vector2.Zero, new Vector2(titleScale));
				if (string.IsNullOrEmpty(quote))
					return;
				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch, bodyFont, quote,
					new Vector2(Anchor.X - quoteSize.X * 0.5f, origin.Y + titleSize.Y + 2f),
					Color.White * (0.88f * fade), 0f, Vector2.Zero, new Vector2(bodyScale));
			});
		}

		private static Vector2 Measure()
		{
			string greeting = Greeting();
			var titleFont = FontAssets.DeathText.Value;
			var bodyFont = FontAssets.MouseText.Value;
			float titleScale = 0.38f * Scale;
			float bodyScale = 0.78f * Scale;
			Vector2 title = titleFont.MeasureString(greeting) * titleScale;
			if (string.IsNullOrEmpty(_line))
				return title;
			Vector2 quote = bodyFont.MeasureString("\"" + _line + "\"") * bodyScale;
			return new Vector2(Math.Max(title.X, quote.X), title.Y + quote.Y + 6f);
		}

		private static string Greeting()
		{
			int hour = DateTime.Now.Hour;
			string key = hour < 5 ? "GreetingNight" : hour < 12 ? "GreetingMorning" : hour < 18 ? "GreetingAfternoon" : hour < 22 ? "GreetingEvening" : "GreetingNight";
			return WeText.UI(key);
		}
	}
}
