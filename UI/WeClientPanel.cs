using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI.Chat;
using WallpaperEngine.Chrome;
using WallpaperEngine.Core;

namespace WallpaperEngine.UI
{
	internal static partial class WePanels
	{
		private static int LooksGroupH(int n) => 32 + 40 + 28 + n * 42 + 14;

		private static int MenuGroupH(int fonts, bool customRgb)
		{
			int h = 32 + 58 + 8 + 70 + 8 + 42 + 42 + 40 + 28 + fonts * 50 + 22 + 22 + 14;
			if (customRgb)
				h += 74;
			return h;
		}

		internal static float ClientContentHeight()
		{
			return LooksGroupH(WePresets.All.Count) + 10 + 140 + 10 + 356 + 10 +
			       MenuGroupH(WeType.All.Count, WeSave.Data.MenuTextCustom) + 10 + 118 + 16;
		}

		private static void DrawGroupShell(SpriteBatch spriteBatch, Rectangle panel, int y, int h, string title)
		{
			if (spriteBatch == null)
				return;
			var card = new Rectangle(panel.X + 10, y, panel.Width - 20, h);
			WeDraw.Fill(spriteBatch, card, new Color(16, 18, 24) * (0.94f * _fade));
			WeDraw.Border(spriteBatch, card, WeAccent.Mid * (0.5f * _fade));
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, title,
				new Vector2(card.X + 12, y + 8), WeAccent.Light * _fade, 0f, Vector2.Zero, new Vector2(0.8f));
		}

		private static void ClientLooks(SpriteBatch spriteBatch, Rectangle panel, ref int y, bool click)
		{
			WeLookFile[] looks = click ? WePresets.Copy() : null;
			int n = click ? looks.Length : WePresets.All.Count;
			int top = y;
			int h = LooksGroupH(n);
			DrawGroupShell(spriteBatch, panel, top, h, WeText.UI("Looks"));
			y = top + 32;
			if (click) {
				if (ClickRow(panel, ref y, out int which)) {
					if (which == 0)
						WePresets.SaveCurrent();
					else
						WeFiles.OpenFolder(WeSave.PresetFolder);
				}

				SkipHint(ref y);
				foreach (WeLookFile look in looks) {
					if (!ClickLook(panel, ref y, out bool trash))
						continue;
					if (trash)
						WePresets.Delete(look);
					else
						WePresets.Load(look);
				}

				y = top + h + 10;
				return;
			}

			DrawButtonRow(spriteBatch, panel, ref y, WeText.UI("SaveLook"), WeText.UI("OpenFolder"));
			DrawHint(spriteBatch, panel, ref y, WeText.UI("LookHint"));
			foreach (WeLookFile look in WePresets.All)
				DrawCard(spriteBatch, panel, ref y, look.Name, false);
			y = top + h + 10;
		}

		private static void ClientHub(SpriteBatch spriteBatch, Rectangle panel, ref int y, bool click)
		{
			const int h = 140;
			int top = y;
			DrawGroupShell(spriteBatch, panel, top, h, WeText.UI("HubStyle"));
			int inner = top + 32;
			int cellW = (panel.Width - 48) / 2;
			var left = new Rectangle(panel.X + 20, inner, cellW, 96);
			var right = new Rectangle(left.Right + 8, inner, cellW, 96);
			if (click) {
				if (left.Contains(Main.mouseX, Main.mouseY))
					WeSettings.SetWrenchStyle(0);
				if (right.Contains(Main.mouseX, Main.mouseY))
					WeSettings.SetWrenchStyle(1);
				y = top + h + 10;
				return;
			}

			DrawHubCell(spriteBatch, left, 0);
			DrawHubCell(spriteBatch, right, 1);
			y = top + h + 10;
		}

		private static void DrawHubCell(SpriteBatch spriteBatch, Rectangle hit, int style)
		{
			bool on = WeSave.Data.WrenchStyle == style;
			bool hover = hit.Contains(Main.mouseX, Main.mouseY);
			WeDraw.Fill(spriteBatch, hit, (on ? WeAccent.Deep : new Color(22, 24, 30)) * ((hover ? 0.95f : 0.82f) * _fade));
			WeDraw.Border(spriteBatch, hit, (on || hover ? WeAccent.Light : WeAccent.Mid) * _fade);
			var preview = new Rectangle(hit.X + 8, hit.Y + 8, hit.Width - 16, hit.Height - 30);
			WeDraw.Fill(spriteBatch, preview, new Color(12, 14, 18) * _fade);
			WrenchToolbar.DrawStylePreview(spriteBatch, preview, style, _fade, on);
			string title = WeText.UI(style == 1 ? "HubStyleDock" : "HubStyleRadial");
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, title,
				new Vector2(hit.X + 12, hit.Bottom - 20), Color.White * _fade, 0f, Vector2.Zero, new Vector2(0.72f));
		}

		private static void ClientWindow(SpriteBatch spriteBatch, Rectangle panel, ref int y, bool click)
		{
			const int h = 356;
			int top = y;
			DrawGroupShell(spriteBatch, panel, top, h, WeText.UI("GroupWindow"));
			int inner = top + 32;
			var preview = new Rectangle(panel.X + 20, inner, panel.Width - 40, 38);
			if (!click)
				DrawWindowPreview(spriteBatch, preview);

			int chipY = inner + 46;
			int chipW = (panel.Width - 56) / 3;
			Rectangle[] chips = {
				new(panel.X + 20, chipY, chipW, 30),
				new(panel.X + 28 + chipW, chipY, chipW, 30),
				new(panel.X + 36 + chipW * 2, chipY, chipW, 30)
			};
			if (click) {
				for (int i = 0; i < 3; i++) {
					if (chips[i].Contains(Main.mouseX, Main.mouseY))
						_clientChip = i;
				}

				y = chipY + 38;
				string key = _clientChip == 1 ? "border" : _clientChip == 2 ? "title" : "caption";
				ClickRgb(panel, ref y, key);
				if (ClickCard(panel, ref y)) {
					WeSave.Data.DarkTitleBar = !WeSave.Data.DarkTitleBar;
					WeSave.Data.ChromeCustom = true;
					WeSave.Save();
					ClientChrome.Apply();
					WeToast.Show("ToastChrome");
				}

				if (ClickRow(panel, ref y, out int which)) {
					if (which == 0) {
						if (WeFiles.TryPickIcon(out string path))
							ClientChrome.SetIcon(path);
					}
					else {
						ClientChrome.Reset();
						WeToast.Show("ToastReset");
					}
				}

				if (ClickCard(panel, ref y))
					WeSplash.Show();
				SkipHint(ref y);
				y = top + h + 10;
				return;
			}

			DrawWindowChip(spriteBatch, chips[0], 0, WeSettings.CaptionColor, WeText.UI("ChipCaption"));
			DrawWindowChip(spriteBatch, chips[1], 1, WeSettings.BorderColor, WeText.UI("ChipBorder"));
			DrawWindowChip(spriteBatch, chips[2], 2, WeSettings.TitleTextColor, WeText.UI("ChipTitle"));
			y = chipY + 38;
			string drawKey = _clientChip == 1 ? "border" : _clientChip == 2 ? "title" : "caption";
			Color drawColor = _clientChip == 1 ? WeSettings.BorderColor : _clientChip == 2 ? WeSettings.TitleTextColor : WeSettings.CaptionColor;
			DrawRgb(spriteBatch, panel, ref y, drawKey, drawColor);
			DrawCard(spriteBatch, panel, ref y, WeText.UI("DarkTitleBar"), WeSave.Data.DarkTitleBar);
			DrawButtonRow(spriteBatch, panel, ref y, WeText.UI("PickIcon"), WeText.UI("ResetChrome"));
			DrawCard(spriteBatch, panel, ref y, WeText.UI("ShowHelp"), false);
			DrawHint(spriteBatch, panel, ref y, WeText.UI("BorderlessHint"));
			y = top + h + 10;
		}

		private static void DrawWindowChip(SpriteBatch spriteBatch, Rectangle hit, int index, Color color, string label)
		{
			bool on = _clientChip == index;
			bool hover = hit.Contains(Main.mouseX, Main.mouseY);
			WeDraw.Fill(spriteBatch, hit, (on ? WeAccent.Deep : new Color(22, 24, 30)) * _fade);
			WeDraw.Border(spriteBatch, hit, (on || hover ? WeAccent.Light : WeAccent.Mid) * _fade);
			WeDraw.Fill(spriteBatch, new Rectangle(hit.X + 6, hit.Y + 7, 16, 16), color * _fade);
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, label,
				new Vector2(hit.X + 28, hit.Y + 7), Color.White * _fade, 0f, Vector2.Zero, new Vector2(0.62f));
		}

		private static void DrawWindowPreview(SpriteBatch spriteBatch, Rectangle bar)
		{
			Color cap = WeSettings.CaptionColor;
			if (!WeSave.Data.ChromeCustom)
				cap = WeSave.Data.DarkTitleBar ? new Color(32, 32, 32) : new Color(240, 240, 240);
			WeDraw.Fill(spriteBatch, bar, cap * _fade);
			WeDraw.Border(spriteBatch, bar, WeSettings.BorderColor * _fade);
			Color text = WeSave.Data.ChromeCustom ? WeSettings.TitleTextColor : (WeSave.Data.DarkTitleBar ? Color.White : new Color(32, 32, 32));
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, "Terraria",
				new Vector2(bar.X + 36, bar.Y + 10), text * _fade, 0f, Vector2.Zero, new Vector2(0.78f));
			WeDraw.Fill(spriteBatch, new Rectangle(bar.X + 10, bar.Y + 10, 16, 16), WeAccent.Mid * _fade);
			for (int i = 0; i < 3; i++)
				WeDraw.Fill(spriteBatch, new Rectangle(bar.Right - 54 + i * 16, bar.Y + 14, 10, 10), Color.White * (0.35f * _fade));
		}

		private static void ClientMenu(SpriteBatch spriteBatch, Rectangle panel, ref int y, bool click)
		{
			int fonts = WeType.All.Count;
			bool customRgb = WeSave.Data.MenuTextCustom;
			WeFontOffer[] offers = click ? SnapshotFonts() : null;
			int top = y;
			int h = MenuGroupH(fonts, customRgb);
			DrawGroupShell(spriteBatch, panel, top, h, WeText.UI("GroupMenu"));
			int inner = top + 32;
			var preview = new Rectangle(panel.X + 20, inner, panel.Width - 40, 54);
			if (!click)
				DrawMenuLive(spriteBatch, preview);

			int styleY = inner + 62;
			int styleW = (panel.Width - 68) / 4;
			if (click) {
				for (int i = 0; i < 4; i++) {
					var cell = new Rectangle(panel.X + 20 + i * (styleW + 8), styleY, styleW, 66);
					if (cell.Contains(Main.mouseX, Main.mouseY))
						WeSettings.SetButtonStyle(i);
				}

				y = styleY + 74;
				if (ClickCard(panel, ref y))
					WeSettings.ToggleMenuTextCustom();
				if (customRgb)
					ClickRgb(panel, ref y, "menu");
				if (ClickCard(panel, ref y))
					WeType.Clear();
				if (ClickRow(panel, ref y, out int which)) {
					if (which == 0)
						WeType.TryImport();
					else
						WeFiles.OpenFolder(WeSave.FontFolder);
				}

				SkipHint(ref y);
				foreach (WeFontOffer offer in offers) {
					if (!ClickFont(panel, ref y, out bool trash))
						continue;
					if (trash)
						WeType.Delete(offer);
					else
						WeType.Select(offer.FileName);
				}

				ClickSlider(panel, ref y, "fw");
				ClickSlider(panel, ref y, "fh");
				y = top + h + 10;
				return;
			}

			string sample = WeText.UI("MenuPreview");
			for (int i = 0; i < 4; i++) {
				var cell = new Rectangle(panel.X + 20 + i * (styleW + 8), styleY, styleW, 66);
				bool on = WeSave.Data.ButtonStyle == i;
				bool hover = cell.Contains(Main.mouseX, Main.mouseY);
				WeDraw.Fill(spriteBatch, cell, (on ? WeAccent.Deep : new Color(22, 24, 30)) * _fade);
				WeDraw.Border(spriteBatch, cell, (on || hover ? WeAccent.Light : WeAccent.Mid) * _fade);
				WeDraw.WithClip(spriteBatch, cell, () => WeLook.DrawPreview(
					spriteBatch, sample, new Vector2(cell.Center.X, cell.Y + 24),
					WeLook.MenuIdle, _fade, 0.28f, i));
				string key = i switch { 1 => "BtnOutline", 2 => "BtnAccent", 3 => "BtnPlate", _ => "BtnVanilla" };
				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch, FontAssets.MouseText.Value, WeText.UI(key),
					new Vector2(cell.X + 6, cell.Bottom - 18), Color.White * _fade, 0f, Vector2.Zero, new Vector2(0.58f));
			}

			y = styleY + 74;
			DrawCard(spriteBatch, panel, ref y, WeText.UI("MenuTextCustom"), WeSave.Data.MenuTextCustom);
			if (customRgb)
				DrawRgb(spriteBatch, panel, ref y, "menu", WeSettings.MenuTextColor);
			DrawCard(spriteBatch, panel, ref y, WeText.UI("FontVanilla"), string.IsNullOrEmpty(WeSave.Data.FontFile));
			DrawButtonRow(spriteBatch, panel, ref y, WeText.UI("ImportFont"), WeText.UI("OpenFolder"));
			DrawHint(spriteBatch, panel, ref y, WeText.UI("FontHint"));
			foreach (WeFontOffer offer in WeType.All)
				DrawFontRow(spriteBatch, panel, ref y, offer);
			DrawSlider(spriteBatch, panel, ref y, "fw", (WeLook.FontScaleX - 0.5f) / 1.3f, WeText.UI("FontWidth"));
			DrawSlider(spriteBatch, panel, ref y, "fh", (WeLook.FontScaleY - 0.5f) / 1.3f, WeText.UI("FontHeight"));
			y = top + h + 10;
		}

		private static void DrawMenuLive(SpriteBatch spriteBatch, Rectangle box)
		{
			WeDraw.Fill(spriteBatch, box, new Color(10, 12, 16) * _fade);
			WeDraw.Border(spriteBatch, box, WeAccent.Mid * (0.45f * _fade));
			string sample = WeText.UI("MenuPreview");
			float wave = (MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f) + 1f) * 0.5f;
			Color idle = WeLook.MenuIdle;
			Color hover = WeAccent.Hover;
			WeDraw.WithClip(spriteBatch, box, () => {
				WeLook.DrawPreview(
					spriteBatch, sample, new Vector2(box.X + box.Width * 0.28f, box.Center.Y),
					idle, _fade, 0.38f);
				WeLook.DrawPreview(
					spriteBatch, sample, new Vector2(box.X + box.Width * 0.72f, box.Center.Y),
					Color.Lerp(idle, hover, 0.35f + 0.65f * wave), _fade, 0.38f);
			});
		}

		private static void DrawFontRow(SpriteBatch spriteBatch, Rectangle panel, ref int y, WeFontOffer offer)
		{
			Rectangle hit = Row(panel, y, 44);
			bool on = string.Equals(WeSave.Data.FontFile, offer.FileName, StringComparison.OrdinalIgnoreCase);
			bool hover = hit.Contains(Main.mouseX, Main.mouseY);
			WeDraw.Fill(spriteBatch, hit, (on ? WeAccent.Deep : new Color(22, 24, 30)) * _fade);
			WeDraw.Border(spriteBatch, hit, (on || hover ? WeAccent.Light : WeAccent.Mid) * _fade);
			var aa = new Rectangle(hit.X + 8, hit.Y + 6, 52, 32);
			WeDraw.Fill(spriteBatch, aa, new Color(10, 12, 16) * _fade);
			Texture2D prev = WeType.PreviewOf(offer.FileName);
			if (prev != null) {
				float s = Math.Min((aa.Width - 4f) / Math.Max(1, prev.Width), (aa.Height - 4f) / Math.Max(1, prev.Height));
				spriteBatch.Draw(prev, aa.Center.ToVector2(), null, Color.White * _fade, 0f, prev.Size() * 0.5f, s, SpriteEffects.None, 0f);
			}

			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, offer.Family,
				new Vector2(aa.Right + 12, hit.Y + 13), Color.White * _fade, 0f, Vector2.Zero, new Vector2(0.78f));
			y += 50;
		}

		private static bool ClickFont(Rectangle panel, ref int y, out bool trash)
		{
			Rectangle hit = Row(panel, y, 44);
			y += 50;
			trash = false;
			if (!hit.Contains(Main.mouseX, Main.mouseY))
				return false;
			if (Main.mouseRight) {
				trash = true;
				return true;
			}

			return true;
		}

		private static WeFontOffer[] SnapshotFonts()
		{
			var copy = new WeFontOffer[WeType.All.Count];
			for (int i = 0; i < copy.Length; i++)
				copy[i] = WeType.All[i];
			return copy;
		}

		private static void ClientAccent(SpriteBatch spriteBatch, Rectangle panel, ref int y, bool click)
		{
			const int h = 118;
			int top = y;
			DrawGroupShell(spriteBatch, panel, top, h, WeText.UI("Accent"));
			int inner = top + 32;
			const int cols = 4;
			const int gap = 6;
			int cellW = (panel.Width - 40 - gap * (cols - 1)) / cols;
			const int cellH = 32;
			if (click) {
				for (int i = 0; i < WeAccent.Palettes.Length; i++) {
					int col = i % cols;
					int row = i / cols;
					var hit = new Rectangle(panel.X + 20 + col * (cellW + gap), inner + row * (cellH + gap), cellW, cellH);
					if (hit.Contains(Main.mouseX, Main.mouseY))
						WeAccent.Set(i);
				}

				y = top + h;
				return;
			}

			for (int i = 0; i < WeAccent.Palettes.Length; i++) {
				int col = i % cols;
				int row = i / cols;
				var hit = new Rectangle(panel.X + 20 + col * (cellW + gap), inner + row * (cellH + gap), cellW, cellH);
				AccentSwatch swatch = WeAccent.Palettes[i];
				bool on = i == WeAccent.Index;
				WeDraw.Fill(spriteBatch, hit, swatch.Mid * _fade);
				if (on)
					WeDraw.Border(spriteBatch, hit, Color.White * _fade);
				WeLook.DrawPreview(
					spriteBatch, "Aa", hit.Center.ToVector2(), Color.White, _fade * 0.9f, 0.22f, 0);
			}

			y = top + h;
		}
	}
}
