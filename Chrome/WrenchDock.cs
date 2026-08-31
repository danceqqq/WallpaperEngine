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
		private const float Slot = 52f;
		private const float BarH = 50f;
		private const float Pad = 14f;
		private const float Tile = 40f;
		private const float Lift = 26f;

		private static float _focus;
		private static float _lift;
		private static int _hover = -1;
		private static bool _frameInput;
		private static bool _mouseHeld;

		internal static bool Busy => HoverBar() || _hover >= 0;

		internal static Rectangle HitRect() => BarBounds();

		internal static void Reset()
		{
			_hover = -1;
			_focus = 0f;
			_lift = 0f;
		}

		internal static void Update()
		{
			if (!WeModMenu.OnTitle) {
				_hover = -1;
				_lift = 0f;
				return;
			}

			float target = _hover >= 0 ? 1f : 0f;
			_lift = MathHelper.Lerp(_lift, target, 0.28f);
			if (Math.Abs(_lift - target) < 0.012f)
				_lift = target;
			if (_hover >= 0)
				_focus = MathHelper.Lerp(_focus, _hover, 0.32f);
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

			if (WePanels.IsOpen) {
				_hover = -1;
				Main.blockMouse = true;
				return;
			}

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

			WeDraw.WithLinear(spriteBatch, () => DrawBar(spriteBatch, BarBounds(), _focus, _lift, 1f, interactive: true));
		}

		internal static void DrawPreview(SpriteBatch spriteBatch, Rectangle dest, float fade, bool on)
		{
			var inner = new Rectangle(dest.X + 16, dest.Y + 16, dest.Width - 32, dest.Height - 32);
			float focus = on ? 4f : 2f;
			float lift = on ? 0.55f + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f) * 0.12f : 0f;
			DrawBar(spriteBatch, inner, focus, lift, fade, interactive: false);
		}

		private static void DrawBar(SpriteBatch spriteBatch, Rectangle bounds, float focus, float lift, float fade, bool interactive)
		{
			int n = WrenchHub.Actions.Length;
			float slot = bounds.Width / (float)n;
			float barH = Math.Max(22f, bounds.Height * (BarH / (BarH + Lift + 12f)));
			float barTop = bounds.Bottom - barH - 4f;
			var bar = new Rectangle(bounds.X, (int)barTop, bounds.Width, (int)barH);
			Color fill = new Color(24, 26, 32);
			FillRound(spriteBatch, bar, bar.Height * 0.5f, fill * fade);

			for (int i = 0; i < n; i++) {
				float pop = lift * Smooth(1f - Math.Abs(i - focus));
				Vector2 rest = new(bar.X + slot * (i + 0.5f), bar.Center.Y);
				Vector2 center = rest - new Vector2(0f, pop * Lift * (bar.Height / BarH));
				float size = MathHelper.Lerp(bar.Height * 0.46f, Tile * (bar.Height / BarH), pop);
				bool hot = interactive && i == _hover;

				if (pop > 0.08f)
					FillRound(
						spriteBatch,
						TileRect(center, size),
						size * 0.28f,
						Color.Lerp(fill, WeAccent.Deep, 0.65f) * (fade * pop));

				Texture2D icon = WeIcons.Get(WrenchHub.IconName(WrenchHub.Actions[i]));
				if (icon != null) {
					float iconScale = (size * 0.7f) / Math.Max(1, Math.Max(icon.Width, icon.Height));
					spriteBatch.Draw(
						icon, center, null, WeAccent.Icon(hot, pop > 0.45f) * fade,
						0f, icon.Size() * 0.5f, iconScale, SpriteEffects.None, 0f);
				}

				if (interactive && pop > 0.72f && hot) {
					string label = WeText.UI(WrenchHub.TipKey(WrenchHub.Actions[i]));
					DrawLabel(spriteBatch, new Vector2(center.X, center.Y - size * 0.5f - 12f), label, fade * pop);
				}
			}
		}

		private static void DrawLabel(SpriteBatch spriteBatch, Vector2 center, string text, float fade)
		{
			if (string.IsNullOrEmpty(text) || fade < 0.25f)
				return;

			var font = FontAssets.MouseText.Value;
			Vector2 size = font.MeasureString(text) * 0.62f;
			var rect = new Rectangle(
				(int)(center.X - size.X * 0.5f - 9f),
				(int)(center.Y - size.Y * 0.5f - 4f),
				(int)size.X + 18,
				(int)size.Y + 8);
			FillRound(spriteBatch, rect, rect.Height * 0.5f, new Color(18, 20, 26) * fade);
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, font, text,
				new Vector2(rect.X + 9, rect.Y + 4),
				Color.White * fade, 0f, Vector2.Zero, new Vector2(0.62f));
		}

		private static void FillRound(SpriteBatch spriteBatch, Rectangle rect, float radius, Color color)
		{
			if (rect.Width < 2 || rect.Height < 2 || color.A < 8)
				return;

			int r = Math.Clamp((int)MathF.Round(radius), 1, Math.Min(rect.Width, rect.Height) / 2);
			WeDraw.Fill(spriteBatch, new Rectangle(rect.X + r, rect.Y, Math.Max(1, rect.Width - r * 2), r), color);
			WeDraw.Fill(spriteBatch, new Rectangle(rect.X + r, rect.Bottom - r, Math.Max(1, rect.Width - r * 2), r), color);
			WeDraw.Fill(spriteBatch, new Rectangle(rect.X, rect.Y + r, rect.Width, Math.Max(1, rect.Height - r * 2)), color);
			Texture2D circle = WeDraw.Circle();
			float scale = r * 2f / circle.Width;
			Vector2 origin = circle.Size() * 0.5f;
			spriteBatch.Draw(circle, new Vector2(rect.X + r, rect.Y + r), null, color, 0f, origin, scale, SpriteEffects.None, 0f);
			spriteBatch.Draw(circle, new Vector2(rect.Right - r, rect.Y + r), null, color, 0f, origin, scale, SpriteEffects.None, 0f);
			spriteBatch.Draw(circle, new Vector2(rect.X + r, rect.Bottom - r), null, color, 0f, origin, scale, SpriteEffects.None, 0f);
			spriteBatch.Draw(circle, new Vector2(rect.Right - r, rect.Bottom - r), null, color, 0f, origin, scale, SpriteEffects.None, 0f);
		}

		private static Rectangle TileRect(Vector2 center, float size)
		{
			int s = Math.Max(14, (int)size);
			return new Rectangle((int)(center.X - s * 0.5f), (int)(center.Y - s * 0.5f), s, s);
		}

		private static float Smooth(float t)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			return t * t * (3f - 2f * t);
		}

		private static Rectangle BarBounds()
		{
			float scale = WrenchToolbar.Scale;
			int n = WrenchHub.Actions.Length;
			float width = (n * Slot + Pad * 2f) * scale;
			float height = (BarH + Lift + 18f) * scale;
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
