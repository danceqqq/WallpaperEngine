using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.UI.Chat;
using WallpaperEngine.Core;
using WallpaperEngine.Layout;
using WallpaperEngine.UI;

namespace WallpaperEngine.Audio
{
	internal static class WePlayerUI
	{
		private static readonly Color PanelColor = new Color(29, 27, 32) * 0.78f;
		private static readonly Color TextMain = new Color(255, 236, 236);
		private static readonly Color TextSub = new Color(210, 190, 190);
		private const float CollapsedRadius = 28f;
		private const float CardWidth = 420f;
		private const float CardHeight = 148f;
		private const float SeekY = 76f;
		private const float ControlsY = 118f;
		private const float SkipOffset = 40f;
		private const float ExtraOffset = 80f;
		private const float PlayRadius = 17f;
		private const float SkipRadius = 13f;
		private const float LoopRadius = 12f;

		private static float _expand;
		private static bool _hover;
		private static bool _draggingSeek;
		private static bool _frameInput;
		private static bool _mouseHeld;
		private static bool _holdLock;

		internal static bool Busy => _hover && WeSave.Data.PlayerWidget;

		internal static Vector2 Anchor => SceneGraph.Pixel(SceneGraph.Player);

		internal static Rectangle HitRect() => RoundButton.Hit(Anchor, CollapsedRadius + 14f);

		internal static void Reset()
		{
			_expand = 0f;
			_hover = false;
			_draggingSeek = false;
			_mouseHeld = false;
			_holdLock = false;
		}

		internal static void HandleInput()
		{
			if (_frameInput)
				return;
			_frameInput = true;
			UpdateInput();
		}

		internal static void EndFrame() => _frameInput = false;

		internal static void Update()
		{
			if (!WeInput.LeftDown) {
				_holdLock = false;
				_mouseHeld = false;
			}

			WePlaylist.Update();
			if (!Enabled)
				return;
			WeSpectrum.Update(null);
		}

		internal static void Draw(SpriteBatch spriteBatch, float fade)
		{
			if (fade <= 0f || !Enabled)
				return;

			Vector2 center = Anchor;
			float ease = Ease;
			float pulse = 1f + WeSpectrum.SmoothBeat * 0.1f;
			Rectangle card = GetCardRect(center, ease);
			WeDraw.WithLinear(spriteBatch, () => {
				WeSpectrum.Draw(spriteBatch, fade, ease, center, CollapsedRadius * pulse, card, pulse);
				DrawCollapsed(spriteBatch, center, fade, 1f - ease, pulse);
				if (ease > 0.02f)
					DrawCard(spriteBatch, card, fade * ease, ease);
			});
		}

		internal static void DrawPreview(SpriteBatch spriteBatch, Rectangle box, float fade)
		{
			if (fade <= 0.02f || box.Width < 8 || box.Height < 8)
				return;

			Vector2 center = box.Center.ToVector2();
			float radius = Math.Min(box.Width, box.Height) * 0.34f;
			RoundButton.Draw(spriteBatch, center, radius, fade, !WePlaylist.IsPaused);
			DrawPlayPause(spriteBatch, center, radius * 0.38f, WeAccent.Glyph(false) * fade, WePlaylist.IsPaused);
		}

		internal static void Unload() => Reset();

		private static bool Enabled => WeSave.Data.PlayerWidget && SceneGraph.Visible(SceneGraph.Player);

		private static float Ease
		{
			get
			{
				float t = MathHelper.Clamp(_expand, 0f, 1f);
				return t * t * (3f - 2f * t);
			}
		}

		private static void UpdateInput()
		{
			bool pressed = WeInput.Edge(ref _mouseHeld, ref _holdLock);
			if (!Enabled || WePanels.IsOpen || WeSplash.Visible || LayoutEditor.Editing) {
				_expand = MathHelper.Lerp(_expand, 0f, 0.22f);
				_draggingSeek = false;
				return;
			}
			Vector2 center = Anchor;
			float ease = Ease;
			Rectangle collapsed = RoundButton.Hit(center, CollapsedRadius + 12f);
			Rectangle card = GetCardRect(center, ease);
			Rectangle expanded = GetCardRect(center, 1f);
			Rectangle hit = _expand > 0.42f || _draggingSeek ? expanded : collapsed;
			_hover = hit.Contains(Main.mouseX, Main.mouseY);
			float target = _hover || _draggingSeek ? 1f : 0f;
			_expand = MathHelper.Lerp(_expand, target, 0.17f);
			if (Math.Abs(_expand - target) < 0.004f)
				_expand = target;
			if (_hover || _draggingSeek)
				Main.blockMouse = true;
			if (!_hover && !_draggingSeek)
				return;

			if (ease > 0.38f)
				HandleExpanded(card, pressed);
			else if (pressed && collapsed.Contains(Main.mouseX, Main.mouseY)) {
				SoundEngine.PlaySound(SoundID.MenuTick);
				WePlaylist.TogglePause();
				WeInput.LockHold(ref _holdLock);
			}
		}

		private static Rectangle GetCardRect(Vector2 center, float ease)
		{
			float width = MathHelper.Lerp(CollapsedRadius * 2f, CardWidth, ease);
			float height = MathHelper.Lerp(CollapsedRadius * 2f, CardHeight, ease);
			float baseline = center.Y + CollapsedRadius;
			return new Rectangle(
				(int)(center.X - width * 0.5f),
				(int)(baseline - height),
				(int)width,
				(int)height);
		}

		private static void HandleExpanded(Rectangle card, bool pressed)
		{
			LayoutCard(card, out Vector2 controls, out Rectangle seek, out Vector2 shuffle, out Vector2 loop, out float tx);
			if (_draggingSeek) {
				if (WeInput.LeftDown) {
					float t = MathHelper.Clamp((Main.mouseX - seek.X) / (float)Math.Max(1, seek.Width), 0f, 1f);
					WePlaylist.Seek01(t);
				}
				else
					_draggingSeek = false;
				return;
			}

			if (!pressed)
				return;

			if (seek.Contains(Main.mouseX, Main.mouseY)) {
				_draggingSeek = true;
				WePlaylist.Seek01(MathHelper.Clamp((Main.mouseX - seek.X) / (float)Math.Max(1, seek.Width), 0f, 1f));
				WeInput.LockHold(ref _holdLock);
				return;
			}

			if (HitCtrl(controls, PlayRadius * tx + 2f))
				WePlaylist.TogglePause();
			else if (HitCtrl(controls + new Vector2(-SkipOffset * tx, 0f), SkipRadius * tx + 3f))
				WePlaylist.Previous();
			else if (HitCtrl(controls + new Vector2(SkipOffset * tx, 0f), SkipRadius * tx + 3f))
				WePlaylist.Next();
			else if (HitCtrl(controls + new Vector2(-ExtraOffset * tx, 0f), SkipRadius * tx + 3f))
				WePanels.Open(WePanelId.Music);
			else if (HitCtrl(controls + new Vector2(ExtraOffset * tx, 0f), SkipRadius * tx + 3f)) {
				if (WeFiles.TryPickAudio(out string path))
					WeLibrary.Import(path);
			}
			else if (HitCtrl(loop, LoopRadius * tx + 4f))
				WePlaylist.ToggleLoop();
			else if (HitCtrl(shuffle, LoopRadius * tx + 4f))
				WePlaylist.ToggleShuffle();
			else
				return;

			SoundEngine.PlaySound(SoundID.MenuTick);
			WeInput.LockHold(ref _holdLock);
		}

		private static void LayoutCard(Rectangle card, out Vector2 controls, out Rectangle seek, out Vector2 shuffle, out Vector2 loop, out float tx)
		{
			tx = Math.Max(0.01f, card.Width / CardWidth);
			float ty = Math.Max(0.01f, card.Height / CardHeight);
			controls = new Vector2(card.X + card.Width * 0.5f, card.Y + ControlsY * ty);
			int pad = Math.Max(10, (int)(18f * tx));
			int seekY = card.Y + (int)(SeekY * ty);
			seek = new Rectangle(card.X + pad, seekY - 8, Math.Max(8, card.Width - pad * 2), 20);
			shuffle = new Vector2(card.X + 28f * tx, controls.Y);
			loop = new Vector2(card.Right - 28f * tx, controls.Y);
		}

		private static bool HitCtrl(Vector2 center, float radius) =>
			RoundButton.Hit(center, radius).Contains(Main.mouseX, Main.mouseY);

		private static void DrawCollapsed(SpriteBatch spriteBatch, Vector2 center, float fade, float visible, float pulse)
		{
			if (visible < 0.04f)
				return;
			RoundButton.Draw(spriteBatch, center, CollapsedRadius * pulse, fade * visible, !WePlaylist.IsPaused);
			DrawPlayPause(spriteBatch, center, 11f * pulse, WeAccent.Glyph(_hover) * (fade * visible), WePlaylist.IsPaused);
			string tip = WePlaylist.Current?.Title;
			if (!string.IsNullOrEmpty(tip) && tip != "—")
				RoundButton.Tooltip(spriteBatch, center, CollapsedRadius * pulse, tip, fade * visible);
		}

		private static void DrawCard(SpriteBatch spriteBatch, Rectangle card, float alpha, float ease)
		{
			WeDraw.Fill(spriteBatch, new Rectangle(card.X - 3, card.Y - 3, card.Width + 6, card.Height + 6), WeAccent.Mid * (0.16f * alpha));
			WeDraw.Fill(spriteBatch, card, PanelColor * alpha);
			WeDraw.Border(spriteBatch, card, WeAccent.Mid * alpha);
			if (ease < 0.38f)
				return;

			float textAlpha = MathHelper.Clamp((ease - 0.38f) / 0.4f, 0f, 1f) * alpha;
			LayoutCard(card, out Vector2 controls, out Rectangle seekHit, out Vector2 shuffle, out Vector2 loop, out float tx);
			MenuTrack track = WePlaylist.Current;
			var font = FontAssets.MouseText.Value;
			DrawCentered(spriteBatch, font, track.Title, new Vector2(card.X + card.Width * 0.5f, card.Y + 14f * (card.Height / CardHeight)), TextMain * textAlpha, 0.92f);
			DrawCentered(spriteBatch, font, track.Artist, new Vector2(card.X + card.Width * 0.5f, card.Y + 34f * (card.Height / CardHeight)), TextSub * textAlpha, 0.72f);

			float duration = Math.Max(WePlaylist.GetDuration(), 0.01f);
			float time = MathHelper.Clamp(WePlaylist.GetDisplayTime(), 0f, duration);
			float progress = time / duration;
			var bar = new Rectangle(seekHit.X + 4, seekHit.Y + 8, Math.Max(1, seekHit.Width - 8), 4);
			WeDraw.Fill(spriteBatch, bar, Color.White * (0.18f * textAlpha));
			WeDraw.Fill(spriteBatch, new Rectangle(bar.X, bar.Y, Math.Max(1, (int)(bar.Width * progress)), bar.Height), WeAccent.Mid * textAlpha);
			spriteBatch.Draw(WeDraw.Circle(), new Vector2(bar.X + bar.Width * progress, bar.Y + 2f), null, TextMain * textAlpha, 0f, WeDraw.Circle().Size() * 0.5f, 8f / WeDraw.Circle().Width, SpriteEffects.None, 0f);
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, WePlaylist.FormatTime(time), new Vector2(bar.X, bar.Y + 8f), TextSub * textAlpha, 0f, Vector2.Zero, new Vector2(0.7f));
			string end = WePlaylist.FormatTime(duration);
			Vector2 endSize = font.MeasureString(end) * 0.7f;
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, end, new Vector2(bar.Right - endSize.X, bar.Y + 8f), TextSub * textAlpha, 0f, Vector2.Zero, new Vector2(0.7f));

			DrawGlyph(spriteBatch, controls + new Vector2(-ExtraOffset * tx, 0f), SkipRadius * tx, WeIcons.Playlist, "P", textAlpha, false);
			DrawSkip(spriteBatch, controls + new Vector2(-SkipOffset * tx, 0f), -1, textAlpha, SkipRadius * tx);
			RoundButton.Draw(spriteBatch, controls, PlayRadius * tx, textAlpha);
			DrawPlayPause(spriteBatch, controls, 10f * tx, Paint(controls, PlayRadius * tx, textAlpha), WePlaylist.IsPaused);
			DrawSkip(spriteBatch, controls + new Vector2(SkipOffset * tx, 0f), 1, textAlpha, SkipRadius * tx);
			DrawGlyph(spriteBatch, controls + new Vector2(ExtraOffset * tx, 0f), SkipRadius * tx, WeIcons.Upload, "U", textAlpha, false);
			DrawGlyph(spriteBatch, shuffle, LoopRadius * tx, WeIcons.Shuffle, "S", textAlpha, WePlaylist.ShuffleEnabled);
			DrawGlyph(spriteBatch, loop, LoopRadius * tx, WeIcons.Loop, "L", textAlpha, WePlaylist.LoopEnabled);
		}

		private static void DrawSkip(SpriteBatch spriteBatch, Vector2 center, int direction, float alpha, float radius)
		{
			Texture2D icon = WeIcons.Get(direction < 0 ? WeIcons.Prev : WeIcons.Next);
			if (icon != null) {
				RoundButton.DrawIcon(spriteBatch, center, radius, icon, 0f, alpha);
				return;
			}

			RoundButton.Draw(spriteBatch, center, radius, alpha);
			Color color = Paint(center, radius, alpha);
			Texture2D pixel = WeDraw.Pixel;
			int halfH = Math.Max(4, (int)(radius * 0.46f));
			int triW = Math.Max(5, (int)(radius * 0.62f));
			int cy = (int)center.Y;
			int baseX = (int)center.X - direction * 2;
			for (int dy = -halfH; dy <= halfH; dy++) {
				float t = 1f - Math.Abs(dy) / (float)halfH;
				int width = Math.Max(1, (int)MathF.Round(triW * t));
				int x = direction > 0 ? baseX : baseX - width + 1;
				spriteBatch.Draw(pixel, new Rectangle(x, cy + dy, width, 1), color);
			}

			int barX = direction > 0 ? baseX + triW + 1 : baseX - triW - 2;
			spriteBatch.Draw(pixel, new Rectangle(barX, cy - halfH, 2, halfH * 2 + 1), color);
		}

		private static void DrawPlayPause(SpriteBatch spriteBatch, Vector2 center, float size, Color color, bool paused)
		{
			Texture2D icon = WeIcons.Get(paused ? WeIcons.Play : WeIcons.Pause);
			if (icon != null) {
				float scale = size * 2.15f / Math.Max(1, icon.Width);
				spriteBatch.Draw(icon, center, null, color, 0f, icon.Size() * 0.5f, scale, SpriteEffects.None, 0f);
				return;
			}

			Texture2D pixel = WeDraw.Pixel;
			if (paused) {
				int height = Math.Max(6, (int)(size * 1.35f));
				int originX = (int)(center.X - size * 0.38f);
				int originY = (int)center.Y;
				for (int dy = -height; dy <= height; dy++) {
					int width = Math.Max(1, (int)((height - Math.Abs(dy)) * 0.85f));
					spriteBatch.Draw(pixel, new Rectangle(originX, originY + dy, width, 1), color);
				}
			}
			else {
				int height = (int)(size * 1.4f);
				int y = (int)(center.Y - height * 0.5f);
				spriteBatch.Draw(pixel, new Rectangle((int)(center.X - 6f), y, 3, height), color);
				spriteBatch.Draw(pixel, new Rectangle((int)(center.X + 2f), y, 3, height), color);
			}
		}

		private static void DrawGlyph(SpriteBatch spriteBatch, Vector2 center, float radius, string icon, string letter, float alpha, bool active)
		{
			Texture2D tex = WeIcons.Get(icon);
			if (tex != null)
				RoundButton.DrawIcon(spriteBatch, center, radius, tex, 0f, alpha, active);
			else
				RoundButton.DrawLetter(spriteBatch, center, radius, letter, alpha, active);
		}

		private static void DrawCentered(SpriteBatch spriteBatch, ReLogic.Graphics.DynamicSpriteFont font, string text, Vector2 center, Color color, float scale)
		{
			Vector2 size = font.MeasureString(text ?? "") * scale;
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, text ?? "", center - new Vector2(size.X * 0.5f, 0f), color, 0f, Vector2.Zero, new Vector2(scale));
		}

		private static Color Paint(Vector2 center, float radius, float alpha) =>
			WeAccent.Glyph(RoundButton.Hover(center, radius)) * alpha;
	}
}
