using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.UI.Chat;
using WallpaperEngine.Chrome;
using WallpaperEngine.Content;
using WallpaperEngine.Core;

namespace WallpaperEngine.UI
{
	internal static class WeSplash
	{
		private static bool _visible;
		private static bool _dontShow;
		private static float _fade;
		private static bool _mouseHeld;
		private static bool _holdLock;

		internal static bool Visible => _visible || _fade > 0.02f;

		internal static void OnThemeSelected()
		{
			if (WeSave.Data.SplashDismissed)
				return;
			_visible = true;
			_dontShow = false;
		}

		internal static void Show()
		{
			_visible = true;
			_dontShow = false;
		}

		internal static void Hide()
		{
			_visible = false;
			_fade = 0f;
		}

		internal static void Update()
		{
			if (!WeModMenu.OnTitle) {
				_visible = false;
				_fade = 0f;
				return;
			}

			_fade = MathHelper.Lerp(_fade, _visible ? 1f : 0f, 0.2f);
			if (!_visible && _fade < 0.02f)
				_fade = 0f;
		}

		internal static void Dismiss(bool savePreference)
		{
			_visible = false;
			if (savePreference || _dontShow) {
				WeSave.Data.SplashDismissed = true;
				WeSave.Save();
			}

			SoundEngine.PlaySound(SoundID.MenuClose);
			WrenchToolbar.OnThemeSelected();
		}

		internal static void HandleInput()
		{
			if (!_visible)
				return;

			Main.blockMouse = true;
			bool pressed = WeInput.Edge(ref _mouseHeld, ref _holdLock);
			if (!pressed)
				return;

			Rectangle card = Card();
			if (CheckHit(card).Contains(Main.mouseX, Main.mouseY)) {
				_dontShow = !_dontShow;
				SoundEngine.PlaySound(SoundID.MenuTick);
				WeInput.LockHold(ref _holdLock);
				return;
			}

			if (OkHit(card).Contains(Main.mouseX, Main.mouseY)) {
				Dismiss(_dontShow);
				WeInput.LockHold(ref _holdLock);
			}
		}

		internal static void Draw(SpriteBatch spriteBatch)
		{
			if (_fade <= 0.01f)
				return;

			WeDraw.WithLinear(spriteBatch, () => {
				WeDraw.Fill(spriteBatch, WeDraw.CoverRect, Color.Black * (0.62f * _fade));
				Rectangle card = Card();
				WeDraw.Fill(spriteBatch, card, new Color(22, 24, 30) * (0.96f * _fade));
				WeDraw.Border(spriteBatch, card, WeAccent.Mid * _fade);

				var font = FontAssets.MouseText.Value;
				DrawText(spriteBatch, font, WeText.UI("SplashTitle"), new Vector2(card.X + 28, card.Y + 22), 1.05f, WeAccent.Light * _fade);
				DrawWrapped(spriteBatch, font, WeText.UI("Splash1"), card.X + 28, card.Y + 64, card.Width - 56, _fade);
				DrawWrapped(spriteBatch, font, WeText.UI("Splash2"), card.X + 28, card.Y + 124, card.Width - 56, _fade);
				DrawWrapped(spriteBatch, font, WeText.UI("Splash3"), card.X + 28, card.Y + 184, card.Width - 56, _fade);

				DrawCheck(spriteBatch, CheckHit(card), _dontShow, WeText.UI("DontShow"), _fade);
				DrawButton(spriteBatch, OkHit(card), WeText.UI("GotIt"), _fade);
			});
		}

		private static Rectangle Card()
		{
			int w = Math.Min(620, Main.screenWidth - 80);
			int h = 380;
			return new Rectangle((Main.screenWidth - w) / 2, (Main.screenHeight - h) / 2, w, h);
		}

		private static Rectangle OkHit(Rectangle card) => new(card.Right - 168, card.Bottom - 58, 140, 34);

		private static Rectangle CheckHit(Rectangle card) => new(card.X + 28, card.Bottom - 54, 22, 22);

		private static void DrawButton(SpriteBatch spriteBatch, Rectangle hit, string text, float fade)
		{
			bool hover = hit.Contains(Main.mouseX, Main.mouseY);
			WeDraw.Fill(spriteBatch, hit, (hover ? WeAccent.Mid : new Color(32, 36, 44)) * fade);
			WeDraw.Border(spriteBatch, hit, WeAccent.Light * fade);
			var font = FontAssets.MouseText.Value;
			Vector2 size = font.MeasureString(text) * 0.85f;
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, font, text,
				new Vector2(hit.X + (hit.Width - size.X) * 0.5f, hit.Y + (hit.Height - size.Y) * 0.5f),
				Color.White * fade, 0f, Vector2.Zero, new Vector2(0.85f));
		}

		private static void DrawCheck(SpriteBatch spriteBatch, Rectangle hit, bool on, string label, float fade)
		{
			WeDraw.Fill(spriteBatch, hit, new Color(18, 20, 26) * fade);
			WeDraw.Border(spriteBatch, hit, WeAccent.Mid * fade);
			if (on)
				WeDraw.Fill(spriteBatch, new Rectangle(hit.X + 4, hit.Y + 4, hit.Width - 8, hit.Height - 8), WeAccent.Light * fade);
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, label,
				new Vector2(hit.Right + 10, hit.Y + 2),
				Color.White * fade, 0f, Vector2.Zero, new Vector2(0.8f));
		}

		private static void DrawText(SpriteBatch spriteBatch, ReLogic.Graphics.DynamicSpriteFont font, string text, Vector2 pos, float scale, Color color)
		{
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, text, pos, color, 0f, Vector2.Zero, new Vector2(scale));
		}

		private static void DrawWrapped(SpriteBatch spriteBatch, ReLogic.Graphics.DynamicSpriteFont font, string text, int x, int y, int width, float fade)
		{
			string wrapped = Wrap(font, text, width / 0.82f);
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, font, wrapped,
				new Vector2(x, y), Color.White * (0.92f * fade), 0f, Vector2.Zero, new Vector2(0.82f));
		}

		private static string Wrap(ReLogic.Graphics.DynamicSpriteFont font, string text, float width)
		{
			if (string.IsNullOrEmpty(text) || font.MeasureString(text).X <= width)
				return text;

			string[] words = text.Split(' ');
			var line = "";
			var result = "";
			foreach (string word in words) {
				string next = string.IsNullOrEmpty(line) ? word : line + " " + word;
				if (font.MeasureString(next).X > width && line.Length > 0) {
					result += line + "\n";
					line = word;
				}
				else
					line = next;
			}

			return result + line;
		}
	}
}
