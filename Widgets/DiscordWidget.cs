using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.UI.Chat;
using WallpaperEngine.Core;
using WallpaperEngine.Layout;
using WallpaperEngine.UI;

namespace WallpaperEngine.Widgets
{
	internal static class DiscordWidget
	{
		private static readonly Color CardFill = new Color(28, 30, 36);
		private static readonly Color CardDeep = new Color(18, 20, 24);
		private static readonly Color Muted = new Color(168, 174, 184);
		private static readonly Color Online = new Color(35, 165, 89);
		private static readonly Color Idle = new Color(240, 178, 50);
		private static readonly Color Dnd = new Color(242, 63, 66);
		private static readonly Color Offline = new Color(128, 132, 142);
		private static readonly List<(Rectangle Rect, string Channel)> Joins = new();

		private static bool _mouseHeld;
		private static bool _hover;
		private static bool _hoverJoin;
		private static bool _editing;
		private static bool _enterHeld;
		private static string _buffer = "";
		private static string _joinChannel = "";
		private static int _caret;
		private static int _lastWheel;
		private static float _bodyScroll;
		private static float _bodyMax;
		private static Rectangle _card;

		internal static bool Enabled => WeSave.Data.DiscordWidget && SceneGraph.Visible(SceneGraph.Discord);

		internal static bool Editing => _editing;

		internal static bool Busy => Enabled && (_hover || _hoverJoin || _editing);

		internal static Vector2 Anchor => SceneGraph.Pixel(SceneGraph.Discord);

		internal static float Scale => SceneGraph.ScaleOf(SceneGraph.Discord);

		internal const int IdFieldHeight = 44;

		internal static Rectangle HitRect()
		{
			Vector2 size = CardSize();
			Vector2 pos = Anchor;
			return new Rectangle(
				(int)(pos.X - size.X * 0.5f),
				(int)(pos.Y - size.Y * 0.5f),
				(int)size.X,
				(int)size.Y);
		}

		internal static void OpenIdEditor()
		{
			if (!_editing)
				_buffer = DiscordFeed.ExtractId(WeSave.Data.DiscordGuildId ?? "");
			_editing = true;
			_caret = 0;
			_enterHeld = true;
			Main.clrInput();
			if (!WePanels.Is(WePanelId.Widgets))
				WePanels.Open(WePanelId.Widgets);
		}

		internal static void Unfocus()
		{
			if (!_editing)
				return;
			_editing = false;
			PlayerInput.WritingText = false;
			Main.drawingPlayerChat = false;
			CommitBuffer();
		}

		internal static void Tick()
		{
			int wheel = Mouse.GetState().ScrollWheelValue;
			if (!Enabled || WePanels.IsOpen || WePanels.AteInput || LayoutEditor.Editing || WeSplash.Visible) {
				_lastWheel = wheel;
				return;
			}

			bool over = _card.Width > 0 && _card.Contains(Main.mouseX, Main.mouseY);
			int style = Math.Clamp(WeSave.Data.DiscordStyle, 0, 2);
			if (over && (style == 1 || style == 2)) {
				float delta = (wheel - _lastWheel) / 120f * 32f;
				_bodyScroll = MathHelper.Clamp(_bodyScroll - delta, 0f, _bodyMax);
			}

			_lastWheel = wheel;
		}

		internal static void TickInput()
		{
			if (!_editing)
				return;

			if (!WeSave.Data.DiscordWidget || !WePanels.IsOpen || !WePanels.Is(WePanelId.Widgets)) {
				Unfocus();
				return;
			}

			PlayerInput.WritingText = true;
			Main.drawingPlayerChat = false;
			try {
				Main.instance.HandleIME();
			}
			catch {
			}

			string previous = _buffer;
			string next = _buffer ?? "";
			try {
				next = Main.GetInputText(_buffer ?? "") ?? "";
			}
			catch {
			}
			if (next.Length - previous.Length >= 8)
				_buffer = DiscordFeed.ExtractId(next);
			else
				_buffer = DiscordFeed.DigitsOnly(next, 20);

			if (_buffer != previous && DiscordFeed.IsSnowflake(_buffer))
				DiscordFeed.SetGuildId(_buffer);

			_caret++;
			bool enter = Main.inputTextEnter;
			if (enter && !_enterHeld) {
				CommitBuffer();
				_editing = false;
				PlayerInput.WritingText = false;
				SoundEngine.PlaySound(SoundID.MenuTick);
			}

			_enterHeld = enter;
			if (Main.inputTextEscape)
				Unfocus();
		}

		internal static void HandleInput()
		{
			if (!Enabled || WeSplash.Visible || LayoutEditor.Editing || WePanels.AteInput) {
				_mouseHeld = Main.mouseLeft;
				if (WePanels.IsOpen)
					return;
				_hover = _hoverJoin = false;
				return;
			}

			if (WePanels.IsOpen) {
				_mouseHeld = Main.mouseLeft;
				_hover = _hoverJoin = false;
				return;
			}

			bool pressed = Main.mouseLeft && !_mouseHeld;
			_mouseHeld = Main.mouseLeft;
			Point mouse = new(Main.mouseX, Main.mouseY);
			_hoverJoin = false;
			_joinChannel = "";
			for (int i = 0; i < Joins.Count; i++) {
				if (!Joins[i].Rect.Contains(mouse))
					continue;
				_hoverJoin = true;
				_joinChannel = Joins[i].Channel ?? "";
				break;
			}

			_hover = (_card.Width > 0 && _card.Contains(mouse)) || _hoverJoin;
			if (_hover)
				Main.blockMouse = true;
			if (!pressed)
				return;

			if (_hoverJoin) {
				SoundEngine.PlaySound(SoundID.MenuTick);
				DiscordFeed.OpenJoin(_joinChannel);
				return;
			}

			if (_hover) {
				SoundEngine.PlaySound(SoundID.MenuTick);
				OpenIdEditor();
			}
		}

		internal static void DrawIdField(SpriteBatch spriteBatch, Rectangle hit, float fade)
		{
			bool hover = hit.Contains(Main.mouseX, Main.mouseY);
			float pulse = (MathF.Sin(Main.GlobalTimeWrappedHourly * 3.4f) + 1f) * 0.5f;
			bool live = _editing || hover;
			Color fill = new Color(18, 20, 26) * ((live ? 0.96f : 0.82f) * fade);
			Color border = _editing
				? Color.Lerp(WeAccent.Mid, WeAccent.Light, pulse) * fade
				: (hover ? WeAccent.Light : WeAccent.Mid) * fade;
			WeDraw.Fill(spriteBatch, hit, fill);
			WeDraw.Border(spriteBatch, hit, border);

			string shownId = _editing ? (_buffer ?? "") : (WeSave.Data.DiscordGuildId ?? "");
			string shown;
			Color color;
			if (string.IsNullOrEmpty(shownId) && !_editing) {
				shown = WeText.UI("DiscordIdPlaceholder");
				color = Muted * fade;
			}
			else {
				shown = shownId;
				if (_editing && (_caret / 20) % 2 == 0)
					shown += "|";
				color = Color.White * fade;
			}

			var font = FontAssets.MouseText.Value;
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, font, Plain(shown),
				new Vector2(hit.X + 12, hit.Y + 12), color, 0f, Vector2.Zero, new Vector2(0.82f));

			if (DiscordFeed.IsSnowflake(shownId)) {
				Texture2D circle = WeDraw.Circle();
				Vector2 pip = new(hit.Right - 18, hit.Y + hit.Height * 0.5f);
				spriteBatch.Draw(circle, pip, null, Online * fade, 0f, circle.Size() * 0.5f, 10f / circle.Width, SpriteEffects.None, 0f);
			}
		}

		internal static string StatusLine()
		{
			if (_editing && !DiscordFeed.IsSnowflake(_buffer) && _buffer.Length > 0)
				return _buffer.Length < 17 ? WeText.UI("DiscordNeedDigits") : WeText.UI("DiscordBadId");

			return DiscordFeed.Status switch {
				DiscordFeedStatus.Loading => WeText.UI("DiscordLoading"),
				DiscordFeedStatus.NeedWidget => WeText.UI("DiscordNeedWidget"),
				DiscordFeedStatus.BadId => WeText.UI("DiscordBadId"),
				DiscordFeedStatus.NetError => WeText.UI("DiscordNetError"),
				DiscordFeedStatus.Typing => WeText.UI("DiscordNeedDigits"),
				DiscordFeedStatus.Ok => OnlineLabel(DiscordFeed.Snap.Presence),
				_ => WeText.UI("DiscordEmpty")
			};
		}

		internal static void Draw(SpriteBatch spriteBatch, float fade)
		{
			_card = Rectangle.Empty;
			Joins.Clear();
			if (!Enabled || fade <= 0f)
				return;

			Vector2 size = CardSize();
			Vector2 pos = Anchor;
			_card = new Rectangle(
				(int)(pos.X - size.X * 0.5f),
				(int)(pos.Y - size.Y * 0.5f),
				(int)size.X,
				(int)size.Y);
			WeDraw.WithLinear(spriteBatch, () =>
				DrawSkin(spriteBatch, _card, Scale, fade, Math.Clamp(WeSave.Data.DiscordStyle, 0, 2), true));
		}

		internal static void DrawPreview(SpriteBatch spriteBatch, Rectangle box, float fade, int style)
		{
			if (fade <= 0.02f || box.Width < 8 || box.Height < 8)
				return;

			style = Math.Clamp(style, 0, 2);
			Vector2 nat = NativeSize(style);
			float s = Math.Min(box.Width / nat.X, box.Height / nat.Y);
			if (s <= 0.02f)
				return;

			int w = Math.Max(12, (int)(nat.X * s));
			int h = Math.Max(12, (int)(nat.Y * s));
			var card = new Rectangle(
				box.X + (box.Width - w) / 2,
				box.Y + (box.Height - h) / 2,
				w, h);
			DrawSkin(spriteBatch, card, s, fade, style, false);
		}

		private static void CommitBuffer()
		{
			string id = DiscordFeed.ExtractId(_buffer);
			_buffer = id;
			DiscordFeed.SetGuildId(id);
		}

		private static Vector2 NativeSize(int style) => style switch {
			1 => new Vector2(412f, 264f),
			2 => new Vector2(296f, 328f),
			_ => new Vector2(220f, 188f)
		};

		private static Vector2 CardSize() => NativeSize(Math.Clamp(WeSave.Data.DiscordStyle, 0, 2)) * Scale;

		private static void DrawSkin(SpriteBatch spriteBatch, Rectangle card, float s, float fade, int style, bool hits)
		{
			DrawChrome(spriteBatch, card, s, fade);
			switch (style) {
				case 1:
					DrawBanner(spriteBatch, card, s, fade, hits);
					break;
				case 2:
					DrawRoster(spriteBatch, card, s, fade, hits);
					break;
				default:
					DrawCompact(spriteBatch, card, s, fade);
					break;
			}
		}

		private static void DrawChrome(SpriteBatch spriteBatch, Rectangle card, float s, float fade)
		{
			float radius = MathF.Min(18f * s, Math.Min(card.Width, card.Height) * 0.18f);
			FillRound(spriteBatch, card, CardFill * (0.96f * fade), radius);
			int inset = Math.Max(2, (int)(radius * 0.85f));
			WeDraw.Fill(spriteBatch, new Rectangle(card.X, card.Y + inset, Math.Max(2, (int)(3f * s)), Math.Max(4, card.Height - inset * 2)), WeAccent.Mid * fade);
			WeDraw.Fill(spriteBatch, new Rectangle(card.X + inset, card.Y + 1, Math.Max(1, card.Width - inset * 2), 1), Color.White * (0.14f * fade));
			WeDraw.Fill(spriteBatch, new Rectangle(card.X + inset, card.Bottom - 2, Math.Max(1, card.Width - inset * 2), 1), Color.Black * (0.35f * fade));
		}

		private static void DrawCompact(SpriteBatch spriteBatch, Rectangle card, float s, float fade)
		{
			DrawHeader(spriteBatch, card, s, fade, 140f * s, false, false);
			Hairline(spriteBatch, card, s, card.Y + 58f * s, fade);
			DrawOnlineLine(spriteBatch, card, s, new Vector2(card.X + 18f * s, card.Y + 88f * s), fade, 7, false);
			string sub = CompactSub();
			if (!string.IsNullOrEmpty(DiscordFeed.Snap.Voice) && DiscordFeed.Status == DiscordFeedStatus.Ok)
				sub = WeText.UI("DiscordInVoice").Replace("%n", VoiceCount().ToString()) + "  ·  # " + DiscordFeed.Snap.Voice;
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, Plain(Ellipsize(FontAssets.MouseText.Value, sub, card.Width - 36f * s, 0.68f * s)),
				new Vector2(card.X + 18f * s, card.Bottom - 28f * s),
				Muted * fade, 0f, Vector2.Zero, new Vector2(0.68f * s));
		}

		private static void DrawBanner(SpriteBatch spriteBatch, Rectangle card, float s, float fade, bool hits)
		{
			DrawHeader(spriteBatch, card, s, fade, 176f * s, true, hits);
			Hairline(spriteBatch, card, s, card.Y + 62f * s, fade);
			var body = new Rectangle(
				card.X + (int)(10f * s),
				card.Y + (int)(70f * s),
				Math.Max(8, card.Width - (int)(20f * s)),
				Math.Max(8, card.Height - (int)(80f * s)));
			List<VoiceBand> bands = VoiceBands();
			if (bands.Count == 0) {
				DrawOnlineLine(spriteBatch, card, s, new Vector2(body.X + 8f * s, body.Y + 18f * s), fade, 10, false);
				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch, FontAssets.MouseText.Value, Plain(BannerEmptyLine()),
					new Vector2(body.X + 8f * s, body.Y + 40f * s),
					Muted * fade, 0f, Vector2.Zero, new Vector2(0.68f * s));
				if (hits)
					_bodyMax = 0f;
				return;
			}

			float content = 0f;
			for (int i = 0; i < bands.Count; i++)
				content += BandHeight(bands[i], s);
			float max = Math.Max(0f, content - body.Height);
			float scroll = hits ? ClampScroll(max) : 0f;
			void Body()
			{
				float y = body.Y - scroll;
				for (int i = 0; i < bands.Count; i++) {
					float h = BandHeight(bands[i], s);
					if (y + h >= body.Y && y <= body.Bottom)
						DrawVoiceBand(spriteBatch, bands[i], new Rectangle(body.X, (int)y, body.Width, (int)h), s, fade, hits);
					y += h;
				}

				if (hits && max > 4f)
					DrawScrollThumb(spriteBatch, body, scroll, max, fade);
			}

			if (hits)
				WeDraw.WithClip(spriteBatch, body, Body);
			else
				Body();
		}

		private static void DrawRoster(SpriteBatch spriteBatch, Rectangle card, float s, float fade, bool hits)
		{
			DrawHeader(spriteBatch, card, s, fade, 148f * s, false, false);
			Hairline(spriteBatch, card, s, card.Y + 58f * s, fade);
			DiscordMember[] members = DiscordFeed.Snap.Members ?? Array.Empty<DiscordMember>();
			var list = new Rectangle(
				card.X + (int)(10f * s),
				card.Y + (int)(66f * s),
				Math.Max(8, card.Width - (int)(20f * s)),
				Math.Max(8, card.Height - (int)(76f * s)));
			float rowH = 38f * s;
			if (members.Length == 0) {
				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch, FontAssets.MouseText.Value, Plain(ShortStatus()),
					new Vector2(list.X + 8f * s, list.Y + 8f * s),
					Muted * fade, 0f, Vector2.Zero, new Vector2(0.7f * s));
				if (hits)
					_bodyMax = 0f;
				return;
			}

			float content = members.Length * rowH;
			float max = Math.Max(0f, content - list.Height);
			float scroll = hits ? ClampScroll(max) : 0f;
			void People()
			{
				float y = list.Y - scroll;
				for (int i = 0; i < members.Length; i++) {
					if (y + rowH < list.Y) {
						y += rowH;
						continue;
					}

					if (y > list.Bottom)
						break;
					DrawPerson(spriteBatch, members[i], new Vector2(list.X + 4f * s, y), list.Width - 8f * s, s, fade, hits);
					y += rowH;
				}

				if (hits && max > 4f)
					DrawScrollThumb(spriteBatch, list, scroll, max, fade);
			}

			if (hits)
				WeDraw.WithClip(spriteBatch, list, People);
			else
				People();
		}

		private static void DrawHeader(SpriteBatch spriteBatch, Rectangle card, float s, float fade, float nameWidth, bool join, bool hits)
		{
			DrawBadge(spriteBatch, new Vector2(card.X + 28f * s, card.Y + 32f * s), 17f * s, fade);
			var font = FontAssets.MouseText.Value;
			float joinW = 0f;
			if (join && DiscordFeed.HasGuildId) {
				string label = WeText.UI("DiscordJoin");
				Vector2 text = font.MeasureString(label) * (0.72f * s);
				int w = (int)(text.X + 22f * s);
				int h = Math.Max(18, (int)(24f * s));
				var rect = new Rectangle(card.Right - w - (int)(14f * s), card.Y + (int)(18f * s), w, h);
				joinW = w + 10f * s;
				DrawJoinPill(spriteBatch, rect, label, s, fade, hits, "");
			}

			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, font, Plain(Ellipsize(font, DisplayName(), nameWidth - joinW * 0.15f, 0.88f * s)),
				new Vector2(card.X + 52f * s, card.Y + 14f * s),
				Color.White * fade, 0f, Vector2.Zero, new Vector2(0.88f * s));
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, font, Plain(Ellipsize(font, HeaderSub(), nameWidth, 0.68f * s)),
				new Vector2(card.X + 52f * s, card.Y + 36f * s),
				Muted * fade, 0f, Vector2.Zero, new Vector2(0.68f * s));
		}

		private static void DrawVoiceBand(SpriteBatch spriteBatch, VoiceBand band, Rectangle rect, float s, float fade, bool hits)
		{
			if (band == null)
				return;

			var font = FontAssets.MouseText.Value;
			DrawVoiceGlyph(spriteBatch, new Vector2(rect.X + 8f * s, rect.Y + 12f * s), s, fade);
			string title = "# " + (string.IsNullOrEmpty(band.Name) ? WeText.UI("DiscordVoice") : band.Name);
			float titleW = rect.Width - 72f * s;
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, font, Plain(Ellipsize(font, title, titleW, 0.7f * s)),
				new Vector2(rect.X + 18f * s, rect.Y + 2f * s),
				WeAccent.Light * fade, 0f, Vector2.Zero, new Vector2(0.7f * s));
			if (DiscordFeed.HasGuildId) {
				string label = WeText.UI("DiscordJoinShort");
				Vector2 text = font.MeasureString(label) * (0.64f * s);
				int w = (int)(text.X + 16f * s);
				int h = Math.Max(16, (int)(20f * s));
				var join = new Rectangle(rect.Right - w - 2, rect.Y + 2, w, h);
				DrawJoinPill(spriteBatch, join, label, s, fade, hits, band.Id);
			}

			if (band.People.Count == 0) {
				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch, font, Plain(WeText.UI("DiscordVoiceEmpty")),
					new Vector2(rect.X + 18f * s, rect.Y + 22f * s),
					Muted * (0.85f * fade), 0f, Vector2.Zero, new Vector2(0.6f * s));
				return;
			}

			int shown = Math.Min(band.People.Count, Math.Max(1, (int)((rect.Width - 16f * s) / (18f * s))));
			for (int i = shown - 1; i >= 0; i--)
				DrawAvatar(spriteBatch, band.People[i], new Vector2(rect.X + 16f * s + i * 18f * s, rect.Y + 38f * s), 11f * s, s, fade);
		}

		private static void DrawPerson(SpriteBatch spriteBatch, DiscordMember member, Vector2 pos, float maxWidth, float s, float fade, bool hits)
		{
			if (member == null)
				return;

			bool voice = !string.IsNullOrEmpty(member.Voice);
			float joinW = 0f;
			if (voice && DiscordFeed.HasGuildId) {
				string label = WeText.UI("DiscordJoinShort");
				Vector2 text = FontAssets.MouseText.Value.MeasureString(label) * (0.62f * s);
				int w = (int)(text.X + 14f * s);
				int h = Math.Max(16, (int)(20f * s));
				var join = new Rectangle((int)(pos.X + maxWidth - w), (int)(pos.Y + 8f * s), w, h);
				joinW = w + 8f * s;
				DrawJoinPill(spriteBatch, join, label, s, fade, hits, member.ChannelId);
			}

			DrawAvatar(spriteBatch, member, pos + new Vector2(12f * s, 12f * s), 12f * s, s, fade);
			string name = Ellipsize(FontAssets.MouseText.Value, member.Name, maxWidth - 36f * s - joinW, 0.74f * s);
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, Plain(name),
				pos + new Vector2(30f * s, voice ? 2f * s : 8f * s),
				(voice ? WeAccent.Light : Color.White) * fade, 0f, Vector2.Zero, new Vector2(0.74f * s));
			if (!voice)
				return;

			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, Plain("# " + member.Voice),
				pos + new Vector2(30f * s, 18f * s),
				Muted * fade, 0f, Vector2.Zero, new Vector2(0.6f * s));
		}

		private static void DrawJoinPill(SpriteBatch spriteBatch, Rectangle rect, string label, float s, float fade, bool hits, string channel)
		{
			if (rect.Width < 8 || rect.Height < 8)
				return;
			if (hits)
				Joins.Add((rect, channel ?? ""));
			bool hot = hits && rect.Contains(Main.mouseX, Main.mouseY);
			float pulse = (MathF.Sin(Main.GlobalTimeWrappedHourly * 3.2f) + 1f) * 0.5f;
			Color fill = Color.Lerp(WeAccent.Mid, WeAccent.Light, hot ? 0.55f + pulse * 0.2f : 0.08f);
			FillRound(spriteBatch, rect, fill * fade, rect.Height * 0.5f);
			if (hot)
				WeDraw.Fill(spriteBatch, new Rectangle(rect.X + 2, rect.Y + 1, Math.Max(1, rect.Width - 4), 1), Color.White * (0.22f * fade));
			var font = FontAssets.MouseText.Value;
			float scale = 0.66f * s;
			Vector2 text = font.MeasureString(label) * scale;
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, font, Plain(label),
				new Vector2(rect.X + (rect.Width - text.X) * 0.5f, rect.Y + (rect.Height - text.Y) * 0.5f - 1f),
				Color.White * fade, 0f, Vector2.Zero, new Vector2(scale));
		}

		private static void DrawVoiceGlyph(SpriteBatch spriteBatch, Vector2 center, float s, float fade)
		{
			Texture2D circle = WeDraw.Circle();
			spriteBatch.Draw(circle, center, null, WeAccent.Light * fade, 0f, circle.Size() * 0.5f, 5.5f * s / circle.Width, SpriteEffects.None, 0f);
			spriteBatch.Draw(circle, center + new Vector2(4.2f * s, 0f), null, WeAccent.Light * (0.55f * fade), 0f, circle.Size() * 0.5f, 3.4f * s / circle.Width, SpriteEffects.None, 0f);
		}

		private static void DrawScrollThumb(SpriteBatch spriteBatch, Rectangle list, float scroll, float max, float fade)
		{
			int h = Math.Max(10, (int)(list.Height * list.Height / (list.Height + max)));
			int travel = Math.Max(1, list.Height - h);
			int y = list.Y + (int)(travel * (scroll / max));
			FillRound(spriteBatch, new Rectangle(list.Right - 5, y, 3, h), WeAccent.Mid * (0.7f * fade), 1.5f);
		}

		private static void DrawBadge(SpriteBatch spriteBatch, Vector2 center, float radius, float fade)
		{
			Texture2D circle = WeDraw.Circle();
			spriteBatch.Draw(
				circle, center, null, WeAccent.Mid * (0.55f * fade), 0f,
				circle.Size() * 0.5f, (radius * 2f + 5f) / circle.Width, SpriteEffects.None, 0f);
			Texture2D icon = DiscordFeed.Snap.Icon;
			if (icon != null && !icon.IsDisposed) {
				spriteBatch.Draw(
					circle, center, null, CardDeep * fade, 0f,
					circle.Size() * 0.5f, (radius * 2f + 2f) / circle.Width, SpriteEffects.None, 0f);
				spriteBatch.Draw(
					icon, center, null, Color.White * fade, 0f,
					icon.Size() * 0.5f, radius * 2f / Math.Max(1, icon.Width), SpriteEffects.None, 0f);
				return;
			}

			spriteBatch.Draw(
				circle, center, null, WeAccent.Deep * fade, 0f,
				circle.Size() * 0.5f, radius * 2f / circle.Width, SpriteEffects.None, 0f);
			string letter = DisplayName();
			letter = letter.Length > 0 ? char.ToUpperInvariant(letter[0]).ToString() : "D";
			var font = FontAssets.DeathText.Value;
			float scale = radius / 22f;
			Vector2 size = font.MeasureString(letter) * scale;
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, font, Plain(letter), center - size * 0.5f + new Vector2(0f, 1f * scale),
				Color.White * fade, 0f, Vector2.Zero, new Vector2(scale));
		}

		private static void DrawOnlineLine(SpriteBatch spriteBatch, Rectangle card, float s, Vector2 origin, float fade, int max, bool showCount)
		{
			DiscordMember[] members = DiscordFeed.Snap.Members ?? Array.Empty<DiscordMember>();
			int shown = Math.Min(members.Length, max);
			float room = card.Right - origin.X - 12f * s;
			if (shown > 0)
				shown = Math.Min(shown, Math.Max(1, (int)(room / (18f * s))));
			for (int i = shown - 1; i >= 0; i--)
				DrawAvatar(spriteBatch, members[i], new Vector2(origin.X + i * 18f * s, origin.Y), 13f * s, s, fade);

			if (!showCount)
				return;

			float textX = shown == 0 ? origin.X : origin.X + shown * 18f * s + 10f * s;
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, Plain(OnlineLabel(DiscordFeed.Snap.Presence)),
				new Vector2(textX, origin.Y - 8f * s),
				Color.White * fade, 0f, Vector2.Zero, new Vector2(0.76f * s));
		}

		private static void DrawAvatar(SpriteBatch spriteBatch, DiscordMember member, Vector2 center, float radius, float s, float fade)
		{
			Texture2D circle = WeDraw.Circle();
			spriteBatch.Draw(
				circle, center, null, CardDeep * fade, 0f,
				circle.Size() * 0.5f, (radius * 2f + 3f) / circle.Width, SpriteEffects.None, 0f);
			if (member?.Avatar != null && !member.Avatar.IsDisposed) {
				spriteBatch.Draw(
					member.Avatar, center, null, Color.White * fade, 0f,
					member.Avatar.Size() * 0.5f, radius * 2f / Math.Max(1, member.Avatar.Width), SpriteEffects.None, 0f);
			}
			else {
				string letter = !string.IsNullOrEmpty(member?.Name) ? char.ToUpperInvariant(member.Name[0]).ToString() : "?";
				float scale = radius / 14f;
				Vector2 size = FontAssets.MouseText.Value.MeasureString(letter) * scale;
				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch, FontAssets.MouseText.Value, Plain(letter),
					center - size * 0.5f, Color.White * fade, 0f, Vector2.Zero, new Vector2(scale));
			}

			Color pip = member?.Status switch {
				"idle" => Idle,
				"dnd" => Dnd,
				"offline" => Offline,
				_ => Online
			};
			Vector2 pipAt = center + new Vector2(radius * 0.62f, radius * 0.62f);
			spriteBatch.Draw(
				circle, pipAt, null, CardDeep * fade, 0f,
				circle.Size() * 0.5f, 9f * s / circle.Width, SpriteEffects.None, 0f);
			spriteBatch.Draw(
				circle, pipAt, null, pip * fade, 0f,
				circle.Size() * 0.5f, 6f * s / circle.Width, SpriteEffects.None, 0f);
		}

		private static void Hairline(SpriteBatch spriteBatch, Rectangle card, float s, float y, float fade)
		{
			int x = card.X + (int)(14f * s);
			WeDraw.Fill(spriteBatch, new Rectangle(x, (int)y, Math.Max(1, card.Width - (int)(28f * s)), 1), WeAccent.Mid * (0.28f * fade));
		}

		private static float ClampScroll(float max)
		{
			_bodyMax = max;
			_bodyScroll = MathHelper.Clamp(_bodyScroll, 0f, _bodyMax);
			return _bodyScroll;
		}

		private static float BandHeight(VoiceBand band, float s) =>
			(band != null && band.People.Count > 0 ? 54f : 28f) * s;

		private static List<VoiceBand> VoiceBands()
		{
			var bands = new List<VoiceBand>();
			var index = new Dictionary<string, VoiceBand>(StringComparer.Ordinal);
			DiscordSnap snap = DiscordFeed.Snap;
			if (snap.Channels != null) {
				for (int i = 0; i < snap.Channels.Length; i++) {
					DiscordChan ch = snap.Channels[i];
					if (ch == null || string.IsNullOrEmpty(ch.Id) || index.ContainsKey(ch.Id))
						continue;
					var band = new VoiceBand {
						Id = ch.Id,
						Name = string.IsNullOrEmpty(ch.Name) ? WeText.UI("DiscordVoice") : ch.Name
					};
					index[ch.Id] = band;
					bands.Add(band);
				}
			}

			DiscordMember[] members = snap.Members ?? Array.Empty<DiscordMember>();
			for (int i = 0; i < members.Length; i++) {
				DiscordMember member = members[i];
				if (member == null || string.IsNullOrEmpty(member.ChannelId))
					continue;
				if (!index.TryGetValue(member.ChannelId, out VoiceBand band)) {
					band = new VoiceBand {
						Id = member.ChannelId,
						Name = string.IsNullOrEmpty(member.Voice) ? WeText.UI("DiscordVoice") : member.Voice
					};
					index[member.ChannelId] = band;
					bands.Add(band);
				}

				band.People.Add(member);
			}

			bands.Sort((a, b) => {
				int occ = (b.People.Count > 0 ? 1 : 0).CompareTo(a.People.Count > 0 ? 1 : 0);
				return occ != 0 ? occ : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
			});
			return bands;
		}

		private static string DisplayName()
		{
			if (!string.IsNullOrEmpty(DiscordFeed.Snap.Name))
				return DiscordFeed.Snap.Name;
			return WeText.UI("AddDiscord");
		}

		private static string HeaderSub()
		{
			if (DiscordFeed.Status != DiscordFeedStatus.Ok)
				return ShortStatus();
			int voice = VoiceCount();
			if (voice > 0)
				return OnlineLabel(DiscordFeed.Snap.Presence) + "  ·  " + WeText.UI("DiscordInVoice").Replace("%n", voice.ToString());
			return OnlineLabel(DiscordFeed.Snap.Presence);
		}

		private static string CompactSub()
		{
			if (DiscordFeed.Status == DiscordFeedStatus.Ok)
				return OnlineLabel(DiscordFeed.Snap.Presence);
			return ShortStatus();
		}

		private static string BannerEmptyLine()
		{
			if (DiscordFeed.Status == DiscordFeedStatus.Ok)
				return WeText.UI("DiscordVoiceEmpty");
			return ShortStatus();
		}

		private static int VoiceCount()
		{
			int n = 0;
			DiscordMember[] members = DiscordFeed.Snap.Members;
			if (members == null)
				return 0;
			for (int i = 0; i < members.Length; i++) {
				if (!string.IsNullOrEmpty(members[i]?.Voice))
					n++;
			}

			return n;
		}

		private static string ShortStatus()
		{
			if (!DiscordFeed.HasGuildId)
				return WeText.UI("DiscordTapToSet");
			return DiscordFeed.Status switch {
				DiscordFeedStatus.Loading => WeText.UI("DiscordLoading"),
				DiscordFeedStatus.NeedWidget => WeText.UI("DiscordNeedWidgetShort"),
				DiscordFeedStatus.BadId => WeText.UI("DiscordBadId"),
				DiscordFeedStatus.NetError => WeText.UI("DiscordNetError"),
				DiscordFeedStatus.Typing => WeText.UI("DiscordNeedDigits"),
				_ => WeText.UI("DiscordEmpty")
			};
		}

		private static string OnlineLabel(int count) =>
			WeText.UI("DiscordOnline").Replace("%n", count.ToString());

		private static string Plain(string text)
		{
			if (string.IsNullOrEmpty(text))
				return "";
			return text.Replace("[", "(").Replace("]", ")");
		}

		private static string Ellipsize(ReLogic.Graphics.DynamicSpriteFont font, string text, float max, float scale)
		{
			if (string.IsNullOrEmpty(text) || font.MeasureString(text).X * scale <= max)
				return text ?? "";

			string cut = text;
			while (cut.Length > 1 && font.MeasureString(cut + "...").X * scale > max)
				cut = cut[..^1];
			return cut + "...";
		}

		private static void FillRound(SpriteBatch spriteBatch, Rectangle rect, Color color, float radius)
		{
			radius = MathF.Min(radius, Math.Min(rect.Width, rect.Height) * 0.5f);
			int r = Math.Max(2, (int)radius);
			WeDraw.Fill(spriteBatch, new Rectangle(rect.X + r, rect.Y, Math.Max(1, rect.Width - r * 2), rect.Height), color);
			WeDraw.Fill(spriteBatch, new Rectangle(rect.X, rect.Y + r, r, Math.Max(1, rect.Height - r * 2)), color);
			WeDraw.Fill(spriteBatch, new Rectangle(rect.Right - r, rect.Y + r, r, Math.Max(1, rect.Height - r * 2)), color);
			Texture2D circle = WeDraw.Circle();
			float scale = r * 2f / circle.Width;
			Vector2 origin = circle.Size() * 0.5f;
			spriteBatch.Draw(circle, new Vector2(rect.X + r, rect.Y + r), null, color, 0f, origin, scale, SpriteEffects.None, 0f);
			spriteBatch.Draw(circle, new Vector2(rect.Right - r, rect.Y + r), null, color, 0f, origin, scale, SpriteEffects.None, 0f);
			spriteBatch.Draw(circle, new Vector2(rect.X + r, rect.Bottom - r), null, color, 0f, origin, scale, SpriteEffects.None, 0f);
			spriteBatch.Draw(circle, new Vector2(rect.Right - r, rect.Bottom - r), null, color, 0f, origin, scale, SpriteEffects.None, 0f);
		}

		private sealed class VoiceBand
		{
			internal string Id = "";
			internal string Name = "";
			internal readonly List<DiscordMember> People = new();
		}
	}
}
