using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
		private static readonly Color Card = new Color(35, 36, 40) * 0.94f;
		private static readonly Color Muted = new Color(176, 180, 188);
		private static readonly Color Online = new Color(35, 165, 89);
		private static readonly Color Idle = new Color(240, 178, 50);
		private static readonly Color Dnd = new Color(242, 63, 66);
		private static readonly Color Offline = new Color(128, 132, 142);

		private static bool _mouseHeld;
		private static bool _hover;
		private static bool _hoverGear;
		private static bool _hoverJoin;
		private static bool _editing;
		private static bool _enterHeld;
		private static string _buffer = "";
		private static int _caret;
		private static Rectangle _card;
		private static Rectangle _gear;
		private static Rectangle _join;
		private static Rectangle _field;

		internal static bool Enabled => WeSave.Data.DiscordWidget && SceneGraph.Visible(SceneGraph.Discord);

		internal static bool Editing => _editing;

		internal static bool Busy => Enabled && (_hover || _hoverGear || _hoverJoin || _editing);

		internal static Vector2 Anchor => SceneGraph.Pixel(SceneGraph.Discord);

		internal static float Scale => SceneGraph.ScaleOf(SceneGraph.Discord);

		internal const int IdFieldHeight = 44;

		internal static Rectangle HitRect()
		{
			Vector2 size = CardSize();
			Vector2 pos = Anchor;
			var card = new Rectangle(
				(int)(pos.X - size.X * 0.5f),
				(int)(pos.Y - size.Y * 0.5f),
				(int)size.X,
				(int)size.Y);
			return Rectangle.Union(card, GearHit(card));
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
			if (!Enabled || WeSplash.Visible || LayoutEditor.Editing) {
				_mouseHeld = Main.mouseLeft;
				if (WePanels.IsOpen)
					return;
				_hover = _hoverGear = _hoverJoin = false;
				return;
			}

			if (WePanels.IsOpen) {
				_mouseHeld = Main.mouseLeft;
				_hover = _hoverGear = _hoverJoin = false;
				return;
			}

			bool pressed = Main.mouseLeft && !_mouseHeld;
			_mouseHeld = Main.mouseLeft;
			Point mouse = new(Main.mouseX, Main.mouseY);
			_hoverGear = _gear.Contains(mouse);
			_hoverJoin = !_hoverGear && _join.Width > 0 && _join.Contains(mouse);
			_hover = _card.Contains(mouse) || _hoverGear || _hoverJoin;
			if (_hover)
				Main.blockMouse = true;
			if (!pressed)
				return;

			if (_hoverJoin && !string.IsNullOrEmpty(DiscordFeed.Snap.Invite)) {
				SoundEngine.PlaySound(SoundID.MenuTick);
				try {
					Utils.OpenToURL(DiscordFeed.Snap.Invite);
				}
				catch {
				}

				return;
			}

			if (_hover || _hoverGear) {
				SoundEngine.PlaySound(SoundID.MenuTick);
				OpenIdEditor();
			}
		}

		internal static void DrawIdField(SpriteBatch spriteBatch, Rectangle hit, float fade)
		{
			_field = hit;
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
			_gear = Rectangle.Empty;
			_join = Rectangle.Empty;
			if (!Enabled || fade <= 0f)
				return;

			Vector2 size = CardSize();
			Vector2 pos = Anchor;
			_card = new Rectangle(
				(int)(pos.X - size.X * 0.5f),
				(int)(pos.Y - size.Y * 0.5f),
				(int)size.X,
				(int)size.Y);
			_gear = GearHit(_card);
			WeDraw.WithLinear(spriteBatch, () => {
				FillRound(spriteBatch, _card, Card * fade, 22f * Scale);
				switch (Math.Clamp(WeSave.Data.DiscordStyle, 0, 2)) {
					case 1:
						DrawBanner(spriteBatch, fade);
						break;
					case 2:
						DrawRoster(spriteBatch, fade);
						break;
					default:
						DrawCompact(spriteBatch, fade);
						break;
				}

				DrawGear(spriteBatch, fade);
			});
		}

		private static void CommitBuffer()
		{
			string id = DiscordFeed.ExtractId(_buffer);
			_buffer = id;
			DiscordFeed.SetGuildId(id);
		}

		private static Vector2 CardSize()
		{
			float s = Scale;
			return Math.Clamp(WeSave.Data.DiscordStyle, 0, 2) switch {
				1 => new Vector2(392f * s, 148f * s),
				2 => new Vector2(256f * s, 304f * s),
				_ => new Vector2(208f * s, 176f * s)
			};
		}

		private static Rectangle GearHit(Rectangle card)
		{
			float r = 11f * Scale;
			return new Rectangle(
				(int)(card.Right - r * 2f - 6f * Scale),
				(int)(card.Y + 6f * Scale),
				(int)(r * 2f + 2f),
				(int)(r * 2f + 2f));
		}

		private static void DrawGear(SpriteBatch spriteBatch, float fade)
		{
			Vector2 center = _gear.Center.ToVector2();
			float radius = 11f * Scale;
			float pulse = (MathF.Sin(Main.GlobalTimeWrappedHourly * 3.4f) + 1f) * 0.5f;
			Texture2D circle = WeDraw.Circle();
			Color glow = Color.Lerp(WeAccent.Mid, WeAccent.Light, pulse) * ((0.55f + pulse * 0.45f) * fade);
			spriteBatch.Draw(
				circle, center, null, glow * 0.45f, 0f,
				circle.Size() * 0.5f, (radius * 2.6f) / circle.Width, SpriteEffects.None, 0f);
			RoundButton.DrawIcon(
				spriteBatch, center, radius,
				WeIcons.Get(WeIcons.Setting), 0f, fade, _hoverGear || _editing);
		}

		private static void DrawCompact(SpriteBatch spriteBatch, float fade)
		{
			float s = Scale;
			Vector2 badge = new(_card.X + 22f * s, _card.Y + 28f * s);
			DrawBadge(spriteBatch, badge, 18f * s, fade);
			DrawOnlineLine(spriteBatch, new Vector2(_card.X + 18f * s, _card.Y + 78f * s), fade, 5);
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, Plain(StatusText()),
				new Vector2(_card.X + 18f * s, _card.Bottom - 28f * s),
				Muted * fade, 0f, Vector2.Zero, new Vector2(0.72f * s));
		}

		private static void DrawBanner(SpriteBatch spriteBatch, float fade)
		{
			float s = Scale;
			DrawBadge(spriteBatch, new Vector2(_card.X + 28f * s, _card.Y + 32f * s), 18f * s, fade);
			string name = DisplayName();
			var font = FontAssets.MouseText.Value;
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, font, Plain(Ellipsize(font, name, 168f * s, 0.9f * s)),
				new Vector2(_card.X + 54f * s, _card.Y + 16f * s),
				Color.White * fade, 0f, Vector2.Zero, new Vector2(0.9f * s));
			string channel = string.IsNullOrEmpty(DiscordFeed.Snap.Voice)
				? OnlineLabel(DiscordFeed.Snap.Presence)
				: "# " + DiscordFeed.Snap.Voice;
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, font, Plain(Ellipsize(font, channel, 176f * s, 0.72f * s)),
				new Vector2(_card.X + 54f * s, _card.Y + 38f * s),
				Muted * fade, 0f, Vector2.Zero, new Vector2(0.72f * s));

			if (!string.IsNullOrEmpty(DiscordFeed.Snap.Invite)) {
				string label = WeText.UI("DiscordJoin");
				Vector2 text = font.MeasureString(label) * (0.78f * s);
				int w = (int)(text.X + 28f * s);
				int h = (int)(28f * s);
				_join = new Rectangle(_card.Right - w - (int)(18f * s), _card.Y + (int)(18f * s), w, h);
				Color fill = _hoverJoin ? WeAccent.Light : WeAccent.Mid;
				FillRound(spriteBatch, _join, fill * fade, h * 0.5f);
				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch, font, Plain(label),
					new Vector2(_join.X + (_join.Width - text.X) * 0.5f, _join.Y + (_join.Height - text.Y) * 0.5f - 1f),
					Color.White * fade, 0f, Vector2.Zero, new Vector2(0.78f * s));
			}

			DrawOnlineLine(spriteBatch, new Vector2(_card.X + 20f * s, _card.Bottom - 38f * s), fade, 7);
		}

		private static void DrawRoster(SpriteBatch spriteBatch, float fade)
		{
			float s = Scale;
			DrawBadge(spriteBatch, new Vector2(_card.X + 26f * s, _card.Y + 28f * s), 16f * s, fade);
			var font = FontAssets.MouseText.Value;
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, font, Plain(Ellipsize(font, DisplayName(), 140f * s, 0.82f * s)),
				new Vector2(_card.X + 50f * s, _card.Y + 14f * s),
				Color.White * fade, 0f, Vector2.Zero, new Vector2(0.82f * s));
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, font, Plain(OnlineLabel(DiscordFeed.Snap.Presence)),
				new Vector2(_card.X + 50f * s, _card.Y + 34f * s),
				Muted * fade, 0f, Vector2.Zero, new Vector2(0.7f * s));

			DiscordMember[] members = DiscordFeed.Snap.Members ?? Array.Empty<DiscordMember>();
			int rows = Math.Min(members.Length, 6);
			if (rows == 0) {
				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch, font, Plain(StatusText()),
					new Vector2(_card.X + 18f * s, _card.Y + 70f * s),
					Muted * fade, 0f, Vector2.Zero, new Vector2(0.72f * s));
				return;
			}

			float y = _card.Y + 62f * s;
			for (int i = 0; i < rows; i++) {
				DrawPerson(spriteBatch, members[i], new Vector2(_card.X + 20f * s, y), 168f * s, fade);
				y += 34f * s;
			}
		}

		private static void DrawBadge(SpriteBatch spriteBatch, Vector2 center, float radius, float fade)
		{
			Texture2D circle = WeDraw.Circle();
			spriteBatch.Draw(
				circle, center, null, WeAccent.Deep * fade, 0f,
				circle.Size() * 0.5f, radius * 2f / circle.Width, SpriteEffects.None, 0f);
			spriteBatch.Draw(
				circle, center, null, WeAccent.Mid * (0.85f * fade), 0f,
				circle.Size() * 0.5f, (radius * 2f - 3f) / circle.Width, SpriteEffects.None, 0f);
			string letter = DisplayName();
			letter = letter.Length > 0 ? char.ToUpperInvariant(letter[0]).ToString() : "D";
			var font = FontAssets.DeathText.Value;
			float scale = radius / 22f;
			Vector2 size = font.MeasureString(letter) * scale;
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, font, Plain(letter), center - size * 0.5f + new Vector2(0f, 1f * scale),
				Color.White * fade, 0f, Vector2.Zero, new Vector2(scale));
		}

		private static void DrawOnlineLine(SpriteBatch spriteBatch, Vector2 origin, float fade, int max)
		{
			DiscordMember[] members = DiscordFeed.Snap.Members ?? Array.Empty<DiscordMember>();
			int shown = Math.Min(members.Length, max);
			float s = Scale;
			for (int i = shown - 1; i >= 0; i--)
				DrawAvatar(spriteBatch, members[i], new Vector2(origin.X + i * 18f * s, origin.Y), 13f * s, fade);

			float textX = shown == 0 ? origin.X : origin.X + shown * 18f * s + 10f * s;
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, Plain(OnlineLabel(DiscordFeed.Snap.Presence)),
				new Vector2(textX, origin.Y - 8f * s),
				Color.White * fade, 0f, Vector2.Zero, new Vector2(0.76f * s));
		}

		private static void DrawPerson(SpriteBatch spriteBatch, DiscordMember member, Vector2 pos, float maxWidth, float fade)
		{
			if (member == null)
				return;

			float s = Scale;
			DrawAvatar(spriteBatch, member, pos + new Vector2(12f * s, 10f * s), 12f * s, fade);
			string name = Ellipsize(FontAssets.MouseText.Value, member.Name, maxWidth - 36f * s, 0.74f * s);
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, Plain(name),
				pos + new Vector2(30f * s, 2f * s),
				Color.White * fade, 0f, Vector2.Zero, new Vector2(0.74f * s));
			if (!string.IsNullOrEmpty(member.Voice)) {
				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch, FontAssets.MouseText.Value, Plain("# " + member.Voice),
					pos + new Vector2(30f * s, 16f * s),
					Muted * fade, 0f, Vector2.Zero, new Vector2(0.62f * s));
			}
		}

		private static void DrawAvatar(SpriteBatch spriteBatch, DiscordMember member, Vector2 center, float radius, float fade)
		{
			Texture2D circle = WeDraw.Circle();
			spriteBatch.Draw(
				circle, center, null, new Color(22, 24, 28) * fade, 0f,
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
				circle, pipAt, null, new Color(22, 24, 28) * fade, 0f,
				circle.Size() * 0.5f, 9f * Scale / circle.Width, SpriteEffects.None, 0f);
			spriteBatch.Draw(
				circle, pipAt, null, pip * fade, 0f,
				circle.Size() * 0.5f, 6f * Scale / circle.Width, SpriteEffects.None, 0f);
		}

		private static string DisplayName()
		{
			if (!string.IsNullOrEmpty(DiscordFeed.Snap.Name))
				return DiscordFeed.Snap.Name;
			return WeText.UI("AddDiscord");
		}

		private static string StatusText()
		{
			if (!DiscordFeed.HasGuildId)
				return WeText.UI("DiscordTapToSet");
			return StatusLine();
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
	}
}
