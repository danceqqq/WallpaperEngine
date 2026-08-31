using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI.Chat;
using WallpaperEngine.Core;

namespace WallpaperEngine.UI
{
	internal static class RoundButton
	{
		internal static readonly Color Panel = new Color(29, 27, 32) * 0.86f;

		internal static Rectangle Hit(Vector2 center, float radius) =>
			new((int)(center.X - radius), (int)(center.Y - radius), (int)(radius * 2f), (int)(radius * 2f));

		internal static bool Hover(Vector2 center, float radius) =>
			Hit(center, radius).Contains(Main.mouseX, Main.mouseY);

		internal static void Draw(SpriteBatch spriteBatch, Vector2 center, float radius, float alpha, bool active = false)
		{
			Texture2D circle = WeDraw.Circle();
			bool hover = Hover(center, radius);
			Color accent = WeAccent.Glyph(hover, active);
			Color fill = hover || active ? accent * (0.35f * alpha) : Color.White * (0.08f * alpha);
			spriteBatch.Draw(circle, center, null, fill, 0f, circle.Size() * 0.5f, radius * 2f / circle.Width, SpriteEffects.None, 0f);
			spriteBatch.Draw(circle, center, null, accent * ((hover || active ? 0.95f : 0.45f) * alpha), 0f, circle.Size() * 0.5f, (radius * 2f + 3f) / circle.Width, SpriteEffects.None, 0f);
			spriteBatch.Draw(circle, center, null, Panel * (0.2f * alpha), 0f, circle.Size() * 0.5f, (radius * 2f - 3f) / circle.Width, SpriteEffects.None, 0f);
		}

		internal static void DrawLetter(SpriteBatch spriteBatch, Vector2 center, float radius, string letter, float alpha, bool active = false)
		{
			Draw(spriteBatch, center, radius, alpha, active);
			var font = FontAssets.DeathText.Value;
			float scale = radius / 28f;
			Vector2 size = font.MeasureString(letter) * scale;
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch,
				font,
				letter,
				center - size * 0.5f + new Vector2(0f, 2f * scale),
				WeAccent.Icon(Hover(center, radius), active) * alpha,
				0f,
				Vector2.Zero,
				new Vector2(scale));
		}

		internal static void DrawIcon(SpriteBatch spriteBatch, Vector2 center, float radius, Texture2D icon, float rotation, float alpha, bool active = false)
		{
			Draw(spriteBatch, center, radius, alpha, active);
			if (icon == null || icon.IsDisposed)
				return;

			bool hover = Hover(center, radius);
			float pop = hover ? 1.14f : active ? 1.06f : 1f;
			float size = radius * 1.22f * pop;
			Vector2 origin = icon.Size() * 0.5f;
			float scale = size / Math.Max(1, Math.Max(icon.Width, icon.Height));
			if (hover) {
				spriteBatch.Draw(
					icon, center, null, WeAccent.Light * (0.32f * alpha),
					rotation, origin, scale * 1.2f, SpriteEffects.None, 0f);
			}

			spriteBatch.Draw(
				icon, center, null, WeAccent.Icon(hover, active) * alpha,
				rotation, origin, scale, SpriteEffects.None, 0f);
		}

		internal static void DrawWrench(SpriteBatch spriteBatch, Vector2 center, float radius, float rotation, float alpha, bool active)
		{
			Draw(spriteBatch, center, radius, alpha, active);
			Texture2D pixel = WeDraw.Pixel;
			Color color = WeAccent.Glyph(Hover(center, radius) || active, active) * alpha;
			Vector2 dir = rotation.ToRotationVector2();
			Vector2 perp = new(-dir.Y, dir.X);
			DrawThick(spriteBatch, pixel, center - dir * radius * 0.42f, center + dir * radius * 0.42f, 5.5f, color);
			Vector2 head = center + dir * radius * 0.28f;
			DrawThick(spriteBatch, pixel, head - perp * radius * 0.28f, head + perp * radius * 0.28f, 4.2f, color);
			DrawThick(spriteBatch, pixel, head + perp * radius * 0.22f, head + perp * radius * 0.22f + dir * radius * 0.18f, 3.4f, color);
			DrawThick(spriteBatch, pixel, head - perp * radius * 0.22f, head - perp * radius * 0.22f + dir * radius * 0.18f, 3.4f, color);
		}

		internal static void DrawThick(SpriteBatch spriteBatch, Texture2D pixel, Vector2 from, Vector2 to, float thickness, Color color)
		{
			Vector2 delta = to - from;
			float length = delta.Length();
			if (length < 0.5f)
				return;

			spriteBatch.Draw(
				pixel,
				from,
				null,
				color,
				delta.ToRotation(),
				new Vector2(0f, 0.5f),
				new Vector2(length / pixel.Width, thickness / pixel.Height),
				SpriteEffects.None,
				0f);
		}

		internal static void Tooltip(SpriteBatch spriteBatch, Vector2 center, float radius, string text, float alpha) =>
			DrawTooltip(spriteBatch, center, radius, text, alpha, Vector2.Zero);

		internal static void TooltipRadial(SpriteBatch spriteBatch, Vector2 center, Vector2 anchor, float radius, string text, float alpha)
		{
			Vector2 dir = center - anchor;
			if (dir.LengthSquared() < 1f)
				dir = new Vector2(0f, -1f);
			else
				dir.Normalize();
			DrawTooltip(spriteBatch, center, radius, text, alpha, dir);
		}

		private static void DrawTooltip(SpriteBatch spriteBatch, Vector2 center, float radius, string text, float alpha, Vector2 dir)
		{
			if (!Hover(center, radius) || string.IsNullOrEmpty(text) || alpha < 0.4f)
				return;

			var font = FontAssets.MouseText.Value;
			Vector2 size = font.MeasureString(text) * 0.82f;
			Vector2 tip = dir.LengthSquared() > 0.01f
				? center + dir * (radius + 12f + size.Length() * 0.12f)
				: new Vector2(center.X, center.Y - radius - 18f - size.Y * 0.5f);
			var rect = new Rectangle(
				(int)(tip.X - size.X * 0.5f - 10f),
				(int)(tip.Y - size.Y * 0.5f - 5f),
				(int)size.X + 20,
				(int)size.Y + 10);
			rect.X = (int)MathHelper.Clamp(rect.X, 8, Main.screenWidth - rect.Width - 8);
			rect.Y = (int)MathHelper.Clamp(rect.Y, 8, Main.screenHeight - rect.Height - 8);
			WeDraw.Fill(spriteBatch, rect, new Color(22, 24, 30) * (0.92f * alpha));
			WeDraw.Border(spriteBatch, rect, WeAccent.Mid * alpha);
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch,
				font,
				text,
				new Vector2(rect.X + 10, rect.Y + 5),
				Color.White * alpha,
				0f,
				Vector2.Zero,
				new Vector2(0.82f));
		}
	}
}
