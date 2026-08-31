using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI.Chat;
using WallpaperEngine.Content;
using WallpaperEngine.Core;
using WallpaperEngine.Layout;
using WallpaperEngine.UI;

namespace WallpaperEngine.Chrome
{
	internal static class WrenchDock
	{
		private const float Slot = 54f;
		private const float BarH = 52f;
		private const float Pad = 16f;
		private const float DipR = 30f;
		private const float Tile = 44f;
		private const float Lift = 28f;

		private static float _focus;
		private static float _open = 1f;
		private static int _hover = -1;
		private static bool _frameInput;
		private static bool _mouseHeld;

		internal static bool Busy => HoverBar() || _hover >= 0;

		internal static Rectangle HitRect() => BarBounds();

		internal static void Reset()
		{
			_hover = -1;
			_focus = 0f;
			_open = 1f;
		}

		internal static void Update()
		{
			if (!WeModMenu.OnTitle) {
				_hover = -1;
				return;
			}

			int target = _hover >= 0 ? _hover : WrenchHub.ActiveIndex();
			if (target < 0)
				target = (int)MathF.Round(_focus);
			_focus = MathHelper.Lerp(_focus, target, 0.22f);
			if (Math.Abs(_focus - target) < 0.01f)
				_focus = target;
			_open = MathHelper.Lerp(_open, 1f, 0.18f);
		}

		internal static void HandleInput()
		{
			if (_frameInput || LayoutEditor.Editing || WeSplash.Visible)
				return;

			_frameInput = true;
			bool pressed = Main.mouseLeft && !_mouseHeld;
			_mouseHeld = Main.mouseLeft;
			if (!WeModMenu.OnTitle)
				return;

			_hover = HitIndex();
			if (_hover >= 0 || HoverBar())
				Main.blockMouse = true;

			if (!pressed || _hover < 0)
				return;

			WrenchHub.Activate(WrenchHub.Actions[_hover]);
			Main.mouseLeftRelease = false;
			Main.blockMouse = true;
			if (!WeSave.Data.WrenchOpened) {
				WeSave.Data.WrenchOpened = true;
				WeSave.Save();
			}
		}

		internal static void EndFrame() => _frameInput = false;

		internal static void Draw(SpriteBatch spriteBatch)
		{
			if (!WeModMenu.OnTitle || !SceneGraph.Visible(SceneGraph.Wrench))
				return;

			WeDraw.WithLinear(spriteBatch, () => DrawBar(spriteBatch, BarBounds(), _focus, 1f, interactive: true));
		}

		internal static void DrawPreview(SpriteBatch spriteBatch, Rectangle dest, float fade, bool on)
		{
			var inner = new Rectangle(dest.X + 18, dest.Y + 18, dest.Width - 36, dest.Height - 36);
			DrawBar(spriteBatch, inner, on ? 4.2f + MathF.Sin(Main.GlobalTimeWrappedHourly * 1.6f) * 0.35f : 2f, fade, interactive: false);
		}

		private static void DrawBar(SpriteBatch spriteBatch, Rectangle bounds, float focus, float fade, bool interactive)
		{
			int n = WrenchHub.Actions.Length;
			float slot = bounds.Width / (float)n;
			float barTop = bounds.Y + Lift * (bounds.Height / (BarH + Lift + 10f));
			float barH = Math.Max(22f, bounds.Height - (barTop - bounds.Y) - 6f);
			var bar = new Rectangle(bounds.X, (int)barTop, bounds.Width, (int)barH);
			float dipX = bar.X + slot * (focus + 0.5f);
			float dipR = Math.Min(DipR * (bar.Height / BarH), slot * 0.48f);
			Color fill = new Color(22, 24, 30) * (0.94f * fade);
			DrawBarWithDip(spriteBatch, bar, dipX, dipR, fill);
			WeDraw.Border(spriteBatch, bar, WeAccent.Mid * (0.55f * fade));

			for (int i = 0; i < n; i++) {
				float dist = Math.Abs(i - focus);
				float pop = MathHelper.Clamp(1f - dist, 0f, 1f);
				pop = pop * pop * (3f - 2f * pop);
				Vector2 center = new(bar.X + slot * (i + 0.5f), bar.Center.Y - pop * Lift * (bar.Height / BarH));
				float size = MathHelper.Lerp(bar.Height * 0.42f, Tile * (bar.Height / BarH), pop);
				bool hot = interactive && i == _hover;
				bool active = WrenchHub.IsOn(WrenchHub.Actions[i]);
				if (pop > 0.12f)
					DrawTile(spriteBatch, center, size, fade * MathHelper.Lerp(0.45f, 1f, pop), hot || active);

				Texture2D icon = WeIcons.Get(WrenchHub.IconName(WrenchHub.Actions[i]));
				if (icon != null) {
					float iconScale = (size * 0.72f) / Math.Max(1, Math.Max(icon.Width, icon.Height));
					spriteBatch.Draw(
						icon, center, null, WeAccent.Icon(hot, active) * fade,
						0f, icon.Size() * 0.5f, iconScale, SpriteEffects.None, 0f);
				}

				if (pop > 0.55f && interactive) {
					string label = WeText.UI(WrenchHub.TipKey(WrenchHub.Actions[i]));
					DrawLabel(spriteBatch, new Vector2(center.X, bar.Bottom - 7f), label, fade * pop);
				}
			}
		}

		private static void DrawBarWithDip(SpriteBatch spriteBatch, Rectangle bar, float dipX, float dipR, Color fill)
		{
			dipX = MathHelper.Clamp(dipX, bar.X + dipR + 8f, bar.Right - dipR - 8f);
			int r = Math.Max(10, (int)(bar.Height * 0.42f));
			FillRound(spriteBatch, new Rectangle(bar.X, bar.Y, Math.Max(8, (int)(dipX - dipR - bar.X)), bar.Height), r, fill);
			FillRound(spriteBatch, new Rectangle((int)(dipX + dipR), bar.Y, Math.Max(8, (int)(bar.Right - dipX - dipR)), bar.Height), r, fill);
			int under = Math.Max(8, bar.Height - (int)dipR + 2);
			WeDraw.Fill(spriteBatch, new Rectangle((int)(dipX - dipR), bar.Bottom - under, (int)(dipR * 2f), under), fill);
			Texture2D circle = WeDraw.Circle();
			float cScale = dipR * 2f / circle.Width;
			spriteBatch.Draw(circle, new Vector2(dipX - dipR, bar.Y + dipR), null, fill, 0f, circle.Size() * 0.5f, cScale, SpriteEffects.None, 0f);
			spriteBatch.Draw(circle, new Vector2(dipX + dipR, bar.Y + dipR), null, fill, 0f, circle.Size() * 0.5f, cScale, SpriteEffects.None, 0f);
		}

		private static void DrawTile(SpriteBatch spriteBatch, Vector2 center, float size, float fade, bool on)
		{
			int s = Math.Max(16, (int)size);
			var rect = new Rectangle((int)(center.X - s * 0.5f), (int)(center.Y - s * 0.5f), s, s);
			Color fill = Color.Lerp(WeAccent.Deep, WeAccent.Mid, on ? 0.55f : 0.2f) * fade;
			FillRound(spriteBatch, rect, s * 0.28f, fill);
			WeDraw.Border(spriteBatch, rect, (on ? WeAccent.Light : WeAccent.Mid) * fade);
		}

		private static void DrawLabel(SpriteBatch spriteBatch, Vector2 center, string text, float fade)
		{
			if (string.IsNullOrEmpty(text) || fade < 0.2f)
				return;

			var font = FontAssets.MouseText.Value;
			Vector2 size = font.MeasureString(text) * 0.62f;
			var rect = new Rectangle(
				(int)(center.X - size.X * 0.5f - 8f),
				(int)(center.Y - size.Y * 0.5f - 3f),
				(int)size.X + 16,
				(int)size.Y + 6);
			FillRound(spriteBatch, rect, 8f, new Color(18, 20, 26) * (0.92f * fade));
			WeDraw.Border(spriteBatch, rect, WeAccent.Light * fade);
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, font, text,
				new Vector2(rect.X + 8, rect.Y + 3),
				WeAccent.Light * fade, 0f, Vector2.Zero, new Vector2(0.62f));
		}

		private static void FillRound(SpriteBatch spriteBatch, Rectangle rect, float radius, Color color)
		{
			int r = Math.Clamp((int)radius, 4, Math.Min(rect.Width, rect.Height) / 2);
			WeDraw.Fill(spriteBatch, new Rectangle(rect.X + r, rect.Y, Math.Max(1, rect.Width - r * 2), rect.Height), color);
			WeDraw.Fill(spriteBatch, new Rectangle(rect.X, rect.Y + r, rect.Width, Math.Max(1, rect.Height - r * 2)), color);
			Texture2D circle = WeDraw.Circle();
			float scale = r * 2f / circle.Width;
			Vector2 origin = circle.Size() * 0.5f;
			spriteBatch.Draw(circle, new Vector2(rect.X + r, rect.Y + r), null, color, 0f, origin, scale, SpriteEffects.None, 0f);
			spriteBatch.Draw(circle, new Vector2(rect.Right - r, rect.Y + r), null, color, 0f, origin, scale, SpriteEffects.None, 0f);
			spriteBatch.Draw(circle, new Vector2(rect.X + r, rect.Bottom - r), null, color, 0f, origin, scale, SpriteEffects.None, 0f);
			spriteBatch.Draw(circle, new Vector2(rect.Right - r, rect.Bottom - r), null, color, 0f, origin, scale, SpriteEffects.None, 0f);
		}

		private static Rectangle BarBounds()
		{
			float scale = WrenchToolbar.Scale;
			int n = WrenchHub.Actions.Length;
			float width = (n * Slot + Pad * 2f) * scale;
			float height = (BarH + Lift + 12f) * scale;
			Vector2 origin = WrenchToolbar.Anchor;
			return new Rectangle(
				(int)(origin.X - width * 0.5f),
				(int)(origin.Y - height * 0.5f),
				(int)width,
				(int)height);
		}

		private static bool HoverBar() => BarBounds().Contains(Main.mouseX, Main.mouseY);

		private static int HitIndex()
		{
			if (!HoverBar())
				return -1;

			Rectangle bar = BarBounds();
			int n = WrenchHub.Actions.Length;
			float slot = bar.Width / (float)n;
			int index = (int)((Main.mouseX - bar.X) / slot);
			return Math.Clamp(index, 0, n - 1);
		}
	}
}
