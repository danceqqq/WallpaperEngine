using System;
using System.Globalization;
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
	internal static class ClockWidget
	{
		internal static bool Enabled => WeSave.Data.ClockWidget && SceneGraph.Visible(SceneGraph.Clock);

		internal static Vector2 Anchor => SceneGraph.Pixel(SceneGraph.Clock);

		internal static float Scale => SceneGraph.ScaleOf(SceneGraph.Clock);

		internal static Rectangle HitRect()
		{
			Vector2 pos = Anchor;
			float scale = Scale;
			if (WeSave.Data.ClockAnalog)
				return RoundButton.Hit(pos, 46f * scale);
			return new Rectangle((int)(pos.X - 110 * scale), (int)(pos.Y - 28 * scale), (int)(220 * scale), (int)(WeSave.Data.ClockDate ? 64 * scale : 40 * scale));
		}

		internal static void Draw(SpriteBatch spriteBatch, float fade)
		{
			if (!Enabled || fade <= 0f)
				return;

			DateTime now = DateTime.Now;
			WeDraw.WithLinear(spriteBatch, () => {
				if (WeSave.Data.ClockAnalog)
					DrawAnalog(spriteBatch, now, fade);
				else
					DrawDigital(spriteBatch, now, fade);
			});
		}

		private static void DrawDigital(SpriteBatch spriteBatch, DateTime now, float fade)
		{
			string time = now.ToString(WeSave.Data.Clock24h ? "HH:mm:ss" : "hh:mm:ss tt", CultureInfo.CurrentCulture);
			var font = FontAssets.DeathText.Value;
			float scale = 0.55f * Scale;
			Vector2 size = font.MeasureString(time) * scale;
			Vector2 pos = Anchor - size * 0.5f;
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, time, pos, Color.White * fade, 0f, Vector2.Zero, new Vector2(scale));
			if (!WeSave.Data.ClockDate)
				return;

			string date = now.ToString("D", CultureInfo.CurrentCulture);
			var small = FontAssets.MouseText.Value;
			Vector2 dateSize = small.MeasureString(date) * 0.78f * Scale;
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, small, date,
				new Vector2(Anchor.X - dateSize.X * 0.5f, pos.Y + size.Y - 4f),
				WeAccent.Light * fade, 0f, Vector2.Zero, new Vector2(0.78f * Scale));
		}

		private static void DrawAnalog(SpriteBatch spriteBatch, DateTime now, float fade)
		{
			Vector2 center = Anchor;
			float radius = 42f * Scale;
			RoundButton.Draw(spriteBatch, center, radius, fade);
			float hour = (now.Hour % 12 + now.Minute / 60f) / 12f * MathHelper.TwoPi - MathHelper.PiOver2;
			float minute = (now.Minute + now.Second / 60f) / 60f * MathHelper.TwoPi - MathHelper.PiOver2;
			float second = now.Second / 60f * MathHelper.TwoPi - MathHelper.PiOver2;
			RoundButton.DrawThick(spriteBatch, WeDraw.Pixel, center, center + hour.ToRotationVector2() * radius * 0.52f, 3.4f, WeAccent.Light * fade);
			RoundButton.DrawThick(spriteBatch, WeDraw.Pixel, center, center + minute.ToRotationVector2() * radius * 0.78f, 2.4f, Color.White * fade);
			RoundButton.DrawThick(spriteBatch, WeDraw.Pixel, center, center + second.ToRotationVector2() * radius * 0.86f, 1.4f, WeAccent.Mid * fade);
			if (!WeSave.Data.ClockDate)
				return;

			string date = now.ToString("d", CultureInfo.CurrentCulture);
			Vector2 size = FontAssets.MouseText.Value.MeasureString(date) * 0.7f * Scale;
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, date,
				new Vector2(center.X - size.X * 0.5f, center.Y + radius + 6f),
				Color.White * fade, 0f, Vector2.Zero, new Vector2(0.7f * Scale));
		}

		internal static void DrawPreview(SpriteBatch spriteBatch, Rectangle box, float fade)
		{
			if (fade <= 0.02f || box.Width < 8 || box.Height < 8)
				return;

			DateTime now = DateTime.Now;
			Vector2 center = box.Center.ToVector2();
			if (WeSave.Data.ClockAnalog) {
				float radius = Math.Min(box.Width, box.Height) * 0.36f;
				RoundButton.Draw(spriteBatch, center, radius, fade);
				float hour = (now.Hour % 12 + now.Minute / 60f) / 12f * MathHelper.TwoPi - MathHelper.PiOver2;
				float minute = (now.Minute + now.Second / 60f) / 60f * MathHelper.TwoPi - MathHelper.PiOver2;
				float second = now.Second / 60f * MathHelper.TwoPi - MathHelper.PiOver2;
				RoundButton.DrawThick(spriteBatch, WeDraw.Pixel, center, center + hour.ToRotationVector2() * radius * 0.52f, 2.6f, WeAccent.Light * fade);
				RoundButton.DrawThick(spriteBatch, WeDraw.Pixel, center, center + minute.ToRotationVector2() * radius * 0.78f, 1.8f, Color.White * fade);
				RoundButton.DrawThick(spriteBatch, WeDraw.Pixel, center, center + second.ToRotationVector2() * radius * 0.86f, 1.2f, WeAccent.Mid * fade);
				return;
			}

			string time = now.ToString(WeSave.Data.Clock24h ? "HH:mm" : "h:mm tt", CultureInfo.CurrentCulture);
			var font = FontAssets.DeathText.Value;
			Vector2 size = font.MeasureString(time);
			float scale = Math.Min((box.Width - 10f) / Math.Max(1f, size.X), (box.Height - 8f) / Math.Max(1f, size.Y));
			scale = Math.Min(scale, 0.48f);
			Vector2 pos = center - size * scale * 0.5f;
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, time, pos, Color.White * fade, 0f, Vector2.Zero, new Vector2(scale));
		}
	}
}
