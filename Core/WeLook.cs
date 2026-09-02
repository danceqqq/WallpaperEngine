using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using WallpaperEngine.UI;

namespace WallpaperEngine.Core
{
	internal static class WeLook
	{
		internal static bool LogoPulse => !WeSave.Data.DisableLogoPulse;

		internal static Color MenuIdle => WeSave.Data.MenuTextCustom
			? new Color(WeSave.Data.MenuTextR, WeSave.Data.MenuTextG, WeSave.Data.MenuTextB)
			: Color.White;

		internal static float FontScaleX => Math.Clamp(WeSave.Data.FontScaleX <= 0.01f ? 1f : WeSave.Data.FontScaleX, 0.5f, 1.8f);
		internal static float FontScaleY => Math.Clamp(WeSave.Data.FontScaleY <= 0.01f ? 1f : WeSave.Data.FontScaleY, 0.5f, 1.8f);

		internal static bool CustomMenuDraw
		{
			get
			{
				WeType.Ensure();
				return WeType.Active || WeSave.Data.ButtonStyle != 0 ||
				       Math.Abs(FontScaleX - 1f) > 0.01f || Math.Abs(FontScaleY - 1f) > 0.01f;
			}
		}

		internal static string ButtonKey() => WeSave.Data.ButtonStyle switch {
			1 => "BtnOutline",
			2 => "BtnAccent",
			3 => "BtnPlate",
			_ => "BtnVanilla"
		};

		internal static void StabilizeLogo(ref float rotation, ref float bounce)
		{
			if (LogoPulse)
				return;
			rotation = 0f;
			bounce = 1f;
		}

		internal static Color Paint(Color fallback, float fade)
		{
			Color src = WeSave.Data.MenuTextCustom ? MenuIdle : fallback;
			return src * fade;
		}

		internal static Color IdleOr(Color color)
		{
			if (!WeSave.Data.MenuTextCustom || color.A < 16)
				return color;
			return new Color(MenuIdle.R, MenuIdle.G, MenuIdle.B, color.A);
		}

		internal static Vector2 MenuScale(float baseScale) =>
			new(baseScale * FontScaleX, baseScale * FontScaleY);

		internal static Vector2 Measure(DynamicSpriteFont vanilla, string text, float baseScale)
		{
			if (string.IsNullOrEmpty(text) || vanilla == null)
				return Vector2.Zero;
			Vector2 scale = MenuScale(baseScale);
			if (WeType.Active) {
				float match = vanilla.MeasureString("Ag").Y / Math.Max(1f, WeType.Line);
				Vector2 m = WeType.Measure(text);
				return new Vector2(m.X * scale.X * match, m.Y * scale.Y * match);
			}

			Vector2 size = vanilla.MeasureString(text);
			return new Vector2(size.X * scale.X, size.Y * scale.Y);
		}

		internal static Vector2 DrawStyled(
			SpriteBatch spriteBatch,
			DynamicSpriteFont vanilla,
			string text,
			Vector2 position,
			Color color,
			float baseScale,
			Vector2 origin,
			float rotation,
			SpriteEffects effects,
			float layerDepth,
			bool borderPass,
			int? styleOverride = null)
		{
			if (string.IsNullOrEmpty(text) || color.A < 8 || vanilla == null)
				return Vector2.Zero;

			Vector2 scale = MenuScale(baseScale);
			bool atlas = WeType.Active;
			if (atlas) {
				float match = vanilla.MeasureString("Ag").Y / Math.Max(1f, WeType.Line);
				scale *= match;
				Vector2 oldSize = vanilla.MeasureString(text);
				Vector2 newSize = WeType.Measure(text);
				if (oldSize.X > 0.5f && oldSize.Y > 0.5f && newSize.X > 0.5f)
					origin = new Vector2(origin.X / oldSize.X * newSize.X, origin.Y / oldSize.Y * newSize.Y);
			}

			int style = styleOverride ?? WeSave.Data.ButtonStyle;
			if (style == 3)
				DrawPlate(spriteBatch, vanilla, text, position, origin, scale, color, atlas);

			bool thick = style == 1;
			bool accentRim = style == 2;
			bool vanillaRim = style == 0 && (borderPass || atlas);
			if (thick || accentRim || vanillaRim) {
				float spread = thick ? 3.4f : 2.2f;
				Color rim = accentRim
					? WeAccent.Mid * (color.A / 255f)
					: Color.Black * (color.A / 255f);
				DrawOutline(spriteBatch, vanilla, text, position, origin, scale, rotation, effects, layerDepth, rim, spread, atlas);
				if (thick)
					DrawOutline(spriteBatch, vanilla, text, position, origin, scale, rotation, effects, layerDepth, rim, 1.6f, atlas);
			}

			DrawRaw(spriteBatch, vanilla, text, position, color, rotation, origin, scale, effects, layerDepth, atlas);
			Vector2 size = atlas ? WeType.Measure(text) : vanilla.MeasureString(text);
			return new Vector2(size.X * scale.X, size.Y * scale.Y);
		}

		internal static void DrawPreview(
			SpriteBatch spriteBatch,
			string text,
			Vector2 center,
			Color color,
			float fade,
			float baseScale,
			int? style = null)
		{
			var font = FontAssets.DeathText.Value;
			if (font == null || string.IsNullOrEmpty(text))
				return;
			Vector2 origin = font.MeasureString(text) * 0.5f;
			DrawStyled(
				spriteBatch, font, text, center, color * fade, baseScale, origin,
				0f, SpriteEffects.None, 0f, true, style);
		}

		private static void DrawRaw(
			SpriteBatch spriteBatch,
			DynamicSpriteFont vanilla,
			string text,
			Vector2 position,
			Color color,
			float rotation,
			Vector2 origin,
			Vector2 scale,
			SpriteEffects effects,
			float layerDepth,
			bool atlas)
		{
			if (atlas) {
				WeType.Draw(spriteBatch, text, position, color, rotation, origin, scale, effects, layerDepth);
				return;
			}

			spriteBatch.DrawString(vanilla, text, position, color, rotation, origin, scale, effects, layerDepth);
		}

		private static void DrawOutline(
			SpriteBatch spriteBatch,
			DynamicSpriteFont vanilla,
			string text,
			Vector2 position,
			Vector2 origin,
			Vector2 scale,
			float rotation,
			SpriteEffects effects,
			float layerDepth,
			Color rim,
			float spread,
			bool atlas)
		{
			for (int x = -1; x <= 1; x++) {
				for (int y = -1; y <= 1; y++) {
					if (x == 0 && y == 0)
						continue;
					DrawRaw(
						spriteBatch, vanilla, text, position + new Vector2(x, y) * spread, rim,
						rotation, origin, scale, effects, layerDepth, atlas);
				}
			}
		}

		private static void DrawPlate(
			SpriteBatch spriteBatch,
			DynamicSpriteFont vanilla,
			string text,
			Vector2 position,
			Vector2 origin,
			Vector2 scale,
			Color color,
			bool atlas)
		{
			Vector2 size = atlas ? WeType.Measure(text) : vanilla.MeasureString(text);
			Vector2 scaled = new(size.X * scale.X, size.Y * scale.Y);
			Vector2 topLeft = position - new Vector2(origin.X * scale.X, origin.Y * scale.Y);
			var plate = new Rectangle(
				(int)MathF.Floor(topLeft.X - 16f),
				(int)MathF.Floor(topLeft.Y - 5f),
				Math.Max(8, (int)MathF.Ceiling(scaled.X + 32f)),
				Math.Max(8, (int)MathF.Ceiling(scaled.Y + 10f)));
			float a = color.A / 255f;
			WeDraw.Fill(spriteBatch, plate, new Color(12, 14, 20) * (0.72f * a));
			WeDraw.Border(spriteBatch, plate, WeAccent.Mid * a);
		}
	}
}
