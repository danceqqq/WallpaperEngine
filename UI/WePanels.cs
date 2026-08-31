using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.UI.Chat;
using WallpaperEngine.Chrome;
using WallpaperEngine.Content;
using WallpaperEngine.Core;
using WallpaperEngine.Layout;
using WallpaperEngine.Widgets;
using WallpaperEngine.Audio;
using WallpaperEngine.Grab;

namespace WallpaperEngine.UI
{
	internal enum WePanelId
	{
		None,
		Wallpaper,
		Music,
		Widgets,
		Logo,
		Client
	}

	internal static class WePanels
	{
		private static WePanelId _id;
		private static float _fade;
		private static float _scroll;
		private static bool _frameInput;
		private static bool _mouseHeld;
		private static int _lastWheel;
		private static string _dragSlider;

		internal static bool IsOpen => _id != WePanelId.None;
		internal static bool Is(WePanelId id) => _id == id;

		internal static void Open(WePanelId id)
		{
			LayoutEditor.Cancel(false);
			_id = id;
			_scroll = 0f;
			WeArt.Scan();
			WeLibrary.ScanIntoSave();
			WeCatalog.Refresh();
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		internal static void Close()
		{
			if (_id == WePanelId.None)
				return;
			_id = WePanelId.None;
			_dragSlider = null;
			SoundEngine.PlaySound(SoundID.MenuClose);
		}

		internal static void Update()
		{
			if (!WeModMenu.OnTitle)
				_id = WePanelId.None;
			_fade = MathHelper.Lerp(_fade, IsOpen ? 1f : 0f, 0.22f);
			if (!IsOpen && _fade < 0.02f)
				_fade = 0f;
		}

		internal static void HandleInput()
		{
			if (_frameInput)
				return;
			_frameInput = true;

			bool pressed = Main.mouseLeft && !_mouseHeld;
			_mouseHeld = Main.mouseLeft;
			if (!IsOpen)
				return;

			Main.blockMouse = true;
			Rectangle panel = PanelRect();

			int wheel = Mouse.GetState().ScrollWheelValue;
			if (panel.Contains(Main.mouseX, Main.mouseY))
				_scroll = MathHelper.Clamp(_scroll - (wheel - _lastWheel) / 120f * 42f, 0f, MaxScroll());
			_lastWheel = wheel;

			if (_dragSlider != null) {
				if (Main.mouseLeft)
					ApplySlider(_dragSlider);
				else
					_dragSlider = null;
				return;
			}

			if (!pressed)
				return;

			if (!panel.Contains(Main.mouseX, Main.mouseY)) {
				Close();
				Main.mouseLeftRelease = false;
				return;
			}

			HandleClicks(panel);
			Main.mouseLeftRelease = false;
		}

		internal static void EndFrame() => _frameInput = false;

		internal static void Draw(SpriteBatch spriteBatch)
		{
			if (_fade <= 0.02f)
				return;

			WeDraw.WithLinear(spriteBatch, () => {
				float dim = _id == WePanelId.Wallpaper ? 0.16f : 0.5f;
				WeDraw.Fill(spriteBatch, WeDraw.CoverRect, Color.Black * (dim * _fade));
				Rectangle panel = PanelRect();
				WeDraw.Fill(spriteBatch, panel, new Color(22, 24, 30) * (0.96f * _fade));
				WeDraw.Border(spriteBatch, panel, WeAccent.Mid * _fade);
				DrawHeader(spriteBatch, panel);
				WeDraw.WithClip(spriteBatch, View(panel), () => DrawBody(spriteBatch, panel));
			});
		}

		private static void DrawHeader(SpriteBatch spriteBatch, Rectangle panel)
		{
			string title = _id switch {
				WePanelId.Wallpaper => WeText.UI("BtnWallpaper"),
				WePanelId.Music => WeText.UI("BtnMusic"),
				WePanelId.Widgets => WeText.UI("BtnWidgets"),
				WePanelId.Logo => WeText.UI("BtnLogo"),
				WePanelId.Client => WeText.UI("BtnClient"),
				_ => ""
			};
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, title,
				new Vector2(panel.X + 20, panel.Y + 14), WeAccent.Light * _fade, 0f, Vector2.Zero, new Vector2(0.95f));
		}

		private static void DrawBody(SpriteBatch spriteBatch, Rectangle panel)
		{
			int y = View(panel).Y - (int)_scroll;
			switch (_id) {
				case WePanelId.Wallpaper:
					DrawWallpaper(spriteBatch, panel, ref y);
					break;
				case WePanelId.Logo:
					DrawLogo(spriteBatch, panel, ref y);
					break;
				case WePanelId.Music:
					DrawMusic(spriteBatch, panel, ref y);
					break;
				case WePanelId.Widgets:
					DrawWidgets(spriteBatch, panel, ref y);
					break;
				case WePanelId.Client:
					DrawClient(spriteBatch, panel, ref y);
					break;
			}
		}

		private static void HandleClicks(Rectangle panel)
		{
			int y = View(panel).Y - (int)_scroll;
			switch (_id) {
				case WePanelId.Wallpaper:
					ClickWallpaper(panel, ref y);
					break;
				case WePanelId.Logo:
					ClickLogo(panel, ref y);
					break;
				case WePanelId.Music:
					ClickMusic(panel, ref y);
					break;
				case WePanelId.Widgets:
					ClickWidgets(panel, ref y);
					break;
				case WePanelId.Client:
					ClickClient(panel, ref y);
					break;
			}
		}

		private static void DrawWallpaper(SpriteBatch spriteBatch, Rectangle panel, ref int y)
		{
			DrawHint(spriteBatch, panel, ref y, WeText.UI("LivePreview"));
			DrawHint(spriteBatch, panel, ref y, WeText.UI("BorrowMix"));
			DrawCard(spriteBatch, panel, ref y, WeText.UI("VanillaSky"), WeSave.Data.Wallpaper == WallpaperKind.Vanilla);
			DrawCard(spriteBatch, panel, ref y, WeText.UI("SolidColor"), WeSave.Data.Wallpaper == WallpaperKind.Color);
			DrawCard(spriteBatch, panel, ref y, WeText.UI("Gradient"), WeSave.Data.Wallpaper == WallpaperKind.Gradient);
			if (WeSave.Data.Wallpaper is WallpaperKind.Color or WallpaperKind.Gradient) {
				DrawRgb(spriteBatch, panel, ref y, "wallA", WeSettings.WallpaperColorA);
				if (WeSave.Data.Wallpaper == WallpaperKind.Gradient)
					DrawRgb(spriteBatch, panel, ref y, "wallB", WeSettings.WallpaperColorB);
			}

			DrawSlider(spriteBatch, panel, ref y, "dim", WeSave.Data.WallpaperDim, WeText.UI("WallpaperDim"));
			DrawSlider(spriteBatch, panel, ref y, "vignette", WeSave.Data.WallpaperVignette, WeText.UI("WallpaperVignette"));
			DrawCard(spriteBatch, panel, ref y, WeText.UI("WallpaperParallax"), WeSave.Data.WallpaperParallax);
			DrawButtonRow(spriteBatch, panel, ref y, WeText.UI("AddImageLayer"), WeText.UI("AddEffectLayer"));
			DrawButtonRow(spriteBatch, panel, ref y, WeText.UI("ImportImage"), WeText.UI("OpenFolder"));
			DrawHint(spriteBatch, panel, ref y, WeText.UI("SceneLayers"));
			foreach (WeLayerRecord layer in WeSave.Data.Layers)
				DrawCard(spriteBatch, panel, ref y, LayerTitle(layer), layer.Id == WeSave.Data.SelectedLayerId);

			WeLayerRecord selected = WeSettings.SelectedLayer();
			if (selected != null) {
				DrawCard(spriteBatch, panel, ref y, WeText.UI("LayerForeground"), selected.Foreground);
				if (selected.Kind == WeLayerKind.Image)
					DrawCard(spriteBatch, panel, ref y, WeText.UI(FitKey(selected.Fit)), true);
				else
					DrawCard(spriteBatch, panel, ref y, WeText.UI(FxKey(selected.Effect)), true);
				DrawSlider(spriteBatch, panel, ref y, "lop", selected.Opacity, WeText.UI("LayerOpacity"));
				DrawSlider(spriteBatch, panel, ref y, "lpar", selected.Parallax, WeText.UI("LayerParallax"));
				DrawSlider(spriteBatch, panel, ref y, "lzm", (selected.Zoom - 0.6f) / 1.2f, WeText.UI("LayerZoom"));
				DrawButtonRow(spriteBatch, panel, ref y, WeText.UI("LayerUp"), WeText.UI("LayerDown"));
				DrawCard(spriteBatch, panel, ref y, WeText.UI("RemoveLayer"), false);
			}

			foreach (WeArtRecord record in WeSave.Data.Wallpapers)
				DrawArtCard(spriteBatch, panel, ref y, record, selected != null && selected.ArtId == record.Id, logo: false);

			DrawBorrowSection(spriteBatch, panel, ref y, WeCatalog.Skies, WeOfferKind.Sky);
		}

		private static void ClickWallpaper(Rectangle panel, ref int y)
		{
			SkipHint(ref y);
			SkipHint(ref y);
			if (ClickCard(panel, ref y))
				WeSettings.SetWallpaperVanilla();
			if (ClickCard(panel, ref y))
				WeSettings.SetWallpaperColor(false);
			if (ClickCard(panel, ref y))
				WeSettings.SetWallpaperColor(true);
			if (WeSave.Data.Wallpaper is WallpaperKind.Color or WallpaperKind.Gradient) {
				ClickRgb(panel, ref y, "wallA");
				if (WeSave.Data.Wallpaper == WallpaperKind.Gradient)
					ClickRgb(panel, ref y, "wallB");
			}

			ClickSlider(panel, ref y, "dim");
			ClickSlider(panel, ref y, "vignette");
			if (ClickCard(panel, ref y)) {
				WeSave.Data.WallpaperParallax = !WeSave.Data.WallpaperParallax;
				WeSave.Save();
			}

			if (ClickRow(panel, ref y, out int addWhich)) {
				if (addWhich == 0)
					WeSettings.AddImageLayer(WeSave.Data.Wallpaper == WallpaperKind.Image ? WeSave.Data.WallpaperId : "");
				else
					WeSettings.AddEffectLayer(WeFxKind.Stars);
			}

			if (ClickRow(panel, ref y, out int importWhich)) {
				if (importWhich == 0)
					WeArt.TryImportWallpaper();
				else
					WeFiles.OpenFolder(WeSave.WallpaperFolder);
			}

			SkipHint(ref y);
			foreach (WeLayerRecord layer in WeSave.Data.Layers.ToArray()) {
				if (ClickCard(panel, ref y))
					WeSettings.SelectLayer(layer.Id);
			}

			WeLayerRecord selected = WeSettings.SelectedLayer();
			if (selected != null) {
				if (ClickCard(panel, ref y))
					WeSettings.ToggleSelectedForeground();
				if (selected.Kind == WeLayerKind.Image) {
					if (ClickCard(panel, ref y))
						WeSettings.CycleSelectedFit();
				}
				else if (ClickCard(panel, ref y))
					WeSettings.CycleSelectedEffect();

				ClickSlider(panel, ref y, "lop");
				ClickSlider(panel, ref y, "lpar");
				ClickSlider(panel, ref y, "lzm");
				if (ClickRow(panel, ref y, out int moveWhich)) {
					if (moveWhich == 0)
						WeSettings.MoveSelectedLayer(-1);
					else
						WeSettings.MoveSelectedLayer(1);
				}

				if (ClickCard(panel, ref y))
					WeSettings.RemoveSelectedLayer();
			}

			foreach (WeArtRecord record in WeSave.Data.Wallpapers.ToArray()) {
				if (ClickArt(panel, ref y, record, false))
					WeSettings.AssignSelectedImage(record.Id);
			}

			ClickBorrowSection(panel, ref y, WeCatalog.Skies, WeOfferKind.Sky);
		}

		private static void DrawLogo(SpriteBatch spriteBatch, Rectangle panel, ref int y)
		{
			DrawHint(spriteBatch, panel, ref y, WeText.UI("BorrowMix"));
			DrawCard(spriteBatch, panel, ref y, WeText.UI("VanillaLogo"), WeSave.Data.Logo == LogoKind.Vanilla);
			DrawCard(spriteBatch, panel, ref y, WeText.UI("HideLogo"), WeSave.Data.Logo == LogoKind.Hidden);
			DrawButtonRow(spriteBatch, panel, ref y, WeText.UI("ImportImage"), WeText.UI("OpenFolder"));
			foreach (WeArtRecord record in WeSave.Data.Logos)
				DrawArtCard(spriteBatch, panel, ref y, record, WeSave.Data.LogoId == record.Id, logo: true);

			DrawBorrowSection(spriteBatch, panel, ref y, WeCatalog.Logos, WeOfferKind.Logo);
		}

		private static void ClickLogo(Rectangle panel, ref int y)
		{
			SkipHint(ref y);
			if (ClickCard(panel, ref y))
				WeSettings.SetLogo(LogoKind.Vanilla);
			if (ClickCard(panel, ref y))
				WeSettings.SetLogo(LogoKind.Hidden);
			if (ClickRow(panel, ref y, out int which)) {
				if (which == 0)
					WeArt.TryImportLogo();
				else
					WeFiles.OpenFolder(WeSave.LogoFolder);
			}

			foreach (WeArtRecord record in WeSave.Data.Logos.ToArray()) {
				if (ClickArt(panel, ref y, record, true))
					WeSettings.SetLogo(LogoKind.Custom, record.Id);
			}

			ClickBorrowSection(panel, ref y, WeCatalog.Logos, WeOfferKind.Logo);
		}

		private static void DrawMusic(SpriteBatch spriteBatch, Rectangle panel, ref int y)
		{
			DrawCard(spriteBatch, panel, ref y, WeText.UI("VanillaMusic"), WeSave.Data.Music == MusicKind.Vanilla);
			DrawCard(spriteBatch, panel, ref y, WeText.UI("Silence"), WeSave.Data.Music == MusicKind.Silence);
			DrawButtonRow(spriteBatch, panel, ref y, WeText.UI("ImportSong"), WeText.UI("OpenFolder"));
			DrawHint(spriteBatch, panel, ref y, WeText.UI(WeSave.Data.Tracks.Count == 0 ? "NoTracks" : "TrackHint"));
			foreach (WeTrackRecord track in WeSave.Data.Tracks)
				DrawTrack(spriteBatch, panel, ref y, track);
		}

		private static void ClickMusic(Rectangle panel, ref int y)
		{
			if (ClickCard(panel, ref y)) {
				WeSettings.SetMusic(MusicKind.Vanilla);
				WePlaylist.Silence();
				WeToast.Show("ToastMusic");
			}

			if (ClickCard(panel, ref y)) {
				WeSettings.SetMusic(MusicKind.Silence);
				WePlaylist.Silence();
				WeToast.Show("ToastMusic");
			}

			if (ClickRow(panel, ref y, out int which)) {
				if (which == 0) {
					if (WeFiles.TryPickAudio(out string path))
						WeLibrary.Import(path);
				}
				else
					WeFiles.OpenFolder(WeSave.MusicFolder);
			}

			SkipHint(ref y);
			foreach (WeTrackRecord track in WeSave.Data.Tracks.ToArray())
				ClickTrack(panel, ref y, track);
		}

		private static void DrawWidgets(SpriteBatch spriteBatch, Rectangle panel, ref int y)
		{
			DrawCard(spriteBatch, panel, ref y, WeText.UI("AddPlayer"), WeSave.Data.PlayerWidget);
			DrawCard(spriteBatch, panel, ref y, WeText.UI("AddClock"), WeSave.Data.ClockWidget);
			if (WeSave.Data.ClockWidget) {
				DrawCard(spriteBatch, panel, ref y, WeText.UI(WeSave.Data.Clock24h ? "Clock24h" : "Clock12h"), WeSave.Data.Clock24h);
				DrawCard(spriteBatch, panel, ref y, WeText.UI(WeSave.Data.ClockAnalog ? "ClockAnalog" : "ClockDigital"), WeSave.Data.ClockAnalog);
				DrawCard(spriteBatch, panel, ref y, WeText.UI("ClockDate"), WeSave.Data.ClockDate);
			}

			DrawCard(spriteBatch, panel, ref y, WeText.UI("AddQuote"), WeSave.Data.QuoteWidget);
			if (WeSave.Data.QuoteWidget)
				DrawButtonRow(spriteBatch, panel, ref y, WeText.UI("OpenQuotes"), WeText.UI("OpenFolder"));

			DrawCard(spriteBatch, panel, ref y, WeText.UI("AddMoon"), WeSave.Data.MoonWidget);

			DrawCard(spriteBatch, panel, ref y, WeText.UI("BtnClean"), WeSave.Data.CleanChrome);
			DrawHint(spriteBatch, panel, ref y, WeText.UI("HiddenLayers"));
			foreach (WeElementRecord hidden in SceneGraph.Hidden())
				DrawCard(spriteBatch, panel, ref y, WeText.Layer(hidden.Id) + "  ·  " + WeText.UI("Restore"), false);
		}

		private static void ClickWidgets(Rectangle panel, ref int y)
		{
			if (ClickCard(panel, ref y)) {
				WeSettings.SetPlayerWidget(!WeSave.Data.PlayerWidget);
				WeToast.Show(WeSave.Data.PlayerWidget ? "ToastWidgetOn" : "ToastWidgetOff");
			}

			if (ClickCard(panel, ref y)) {
				WeSettings.SetClockWidget(!WeSave.Data.ClockWidget);
				WeToast.Show(WeSave.Data.ClockWidget ? "ToastWidgetOn" : "ToastWidgetOff");
			}

			if (WeSave.Data.ClockWidget) {
				if (ClickCard(panel, ref y)) {
					WeSave.Data.Clock24h = !WeSave.Data.Clock24h;
					WeSave.Save();
				}

				if (ClickCard(panel, ref y)) {
					WeSave.Data.ClockAnalog = !WeSave.Data.ClockAnalog;
					WeSave.Save();
				}

				if (ClickCard(panel, ref y)) {
					WeSave.Data.ClockDate = !WeSave.Data.ClockDate;
					WeSave.Save();
				}
			}

			if (ClickCard(panel, ref y)) {
				WeSettings.SetQuoteWidget(!WeSave.Data.QuoteWidget);
				if (WeSave.Data.QuoteWidget)
					QuoteWidget.EnsureFile();
				WeToast.Show(WeSave.Data.QuoteWidget ? "ToastWidgetOn" : "ToastWidgetOff");
			}

			if (WeSave.Data.QuoteWidget && ClickRow(panel, ref y, out int quoteWhich)) {
				if (quoteWhich == 0) {
					QuoteWidget.EnsureFile();
					WeFiles.OpenFile(WeSave.QuotePath);
				}
				else
					WeFiles.OpenFolder(WeSave.RootFolder);
			}

			if (ClickCard(panel, ref y)) {
				WeSettings.SetMoonWidget(!WeSave.Data.MoonWidget);
				WeToast.Show(WeSave.Data.MoonWidget ? "ToastWidgetOn" : "ToastWidgetOff");
			}

			if (ClickCard(panel, ref y)) {
				WeSettings.ToggleCleanChrome();
				WeToast.Show(WeSave.Data.CleanChrome ? "ToastCleanOn" : "ToastCleanOff");
			}

			SkipHint(ref y);
			foreach (WeElementRecord hidden in SceneGraph.Hidden()) {
				if (ClickCard(panel, ref y))
					LayoutEditor.RestoreHidden(hidden.Id);
			}
		}

		private static void DrawClient(SpriteBatch spriteBatch, Rectangle panel, ref int y)
		{
			DrawHint(spriteBatch, panel, ref y, WeText.UI("BorderlessHint"));
			DrawHint(spriteBatch, panel, ref y, WeText.UI("HubStyle"));
			DrawHubStyle(spriteBatch, panel, ref y, 0);
			DrawHubStyle(spriteBatch, panel, ref y, 1);
			DrawRgb(spriteBatch, panel, ref y, "caption", WeSettings.CaptionColor, WeText.UI("CaptionColor"));
			DrawRgb(spriteBatch, panel, ref y, "border", WeSettings.BorderColor, WeText.UI("BorderColor"));
			DrawRgb(spriteBatch, panel, ref y, "title", WeSettings.TitleTextColor, WeText.UI("TitleTextColor"));
			DrawCard(spriteBatch, panel, ref y, WeText.UI("DarkTitleBar"), WeSave.Data.DarkTitleBar);
			DrawButtonRow(spriteBatch, panel, ref y, WeText.UI("PickIcon"), WeText.UI("ResetChrome"));
			DrawCard(spriteBatch, panel, ref y, WeText.UI("ShowHelp"), false);
			DrawHint(spriteBatch, panel, ref y, WeText.UI("Accent"));
			for (int i = 0; i < WeAccent.Palettes.Length; i++)
				DrawAccent(spriteBatch, panel, ref y, i);
		}

		private static void ClickClient(Rectangle panel, ref int y)
		{
			SkipHint(ref y);
			SkipHint(ref y);
			if (ClickHubStyle(panel, ref y, 0))
				WeSettings.SetWrenchStyle(0);
			if (ClickHubStyle(panel, ref y, 1))
				WeSettings.SetWrenchStyle(1);
			ClickRgb(panel, ref y, "caption", true);
			ClickRgb(panel, ref y, "border", true);
			ClickRgb(panel, ref y, "title", true);
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
			for (int i = 0; i < WeAccent.Palettes.Length; i++) {
				if (ClickAccent(panel, ref y))
					WeAccent.Set(i);
			}
		}

		private static void DrawHubStyle(SpriteBatch spriteBatch, Rectangle panel, ref int y, int style)
		{
			Rectangle hit = Row(panel, y, 92);
			bool on = WeSave.Data.WrenchStyle == style;
			bool hover = hit.Contains(Main.mouseX, Main.mouseY);
			WeDraw.Fill(spriteBatch, hit, (on ? WeAccent.Deep : new Color(28, 30, 38)) * ((hover ? 0.95f : 0.8f) * _fade));
			WeDraw.Border(spriteBatch, hit, (on || hover ? WeAccent.Light : WeAccent.Mid) * _fade);
			var preview = new Rectangle(hit.X + 8, hit.Y + 8, 168, hit.Height - 16);
			WeDraw.Fill(spriteBatch, preview, new Color(16, 18, 24) * _fade);
			WrenchToolbar.DrawStylePreview(spriteBatch, preview, style, _fade, on);
			string title = WeText.UI(style == 1 ? "HubStyleDock" : "HubStyleRadial");
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, title,
				new Vector2(preview.Right + 14, hit.Y + 34), Color.White * _fade, 0f, Vector2.Zero, new Vector2(0.82f));
			y += 100;
		}

		private static bool ClickHubStyle(Rectangle panel, ref int y, int style)
		{
			Rectangle hit = Row(panel, y, 92);
			y += 100;
			return hit.Contains(Main.mouseX, Main.mouseY);
		}

		private static void DrawCard(SpriteBatch spriteBatch, Rectangle panel, ref int y, string text, bool on)
		{
			Rectangle hit = Row(panel, y, 36);
			bool hover = hit.Contains(Main.mouseX, Main.mouseY);
			WeDraw.Fill(spriteBatch, hit, (on ? WeAccent.Deep : new Color(28, 30, 38)) * ((hover ? 0.95f : 0.8f) * _fade));
			WeDraw.Border(spriteBatch, hit, (on || hover ? WeAccent.Light : WeAccent.Mid) * _fade);
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, text,
				new Vector2(hit.X + 12, hit.Y + 8), Color.White * _fade, 0f, Vector2.Zero, new Vector2(0.82f));
			y += 42;
		}

		private static bool ClickCard(Rectangle panel, ref int y)
		{
			Rectangle hit = Row(panel, y, 36);
			y += 42;
			return hit.Contains(Main.mouseX, Main.mouseY);
		}

		private static void DrawButtonRow(SpriteBatch spriteBatch, Rectangle panel, ref int y, string a, string b)
		{
			Rectangle left = new(panel.X + 16, y, (panel.Width - 40) / 2, 32);
			Rectangle right = new(left.Right + 8, y, left.Width, 32);
			DrawMini(spriteBatch, left, a);
			DrawMini(spriteBatch, right, b);
			y += 40;
		}

		private static bool ClickRow(Rectangle panel, ref int y, out int which)
		{
			Rectangle left = new(panel.X + 16, y, (panel.Width - 40) / 2, 32);
			Rectangle right = new(left.Right + 8, y, left.Width, 32);
			y += 40;
			which = left.Contains(Main.mouseX, Main.mouseY) ? 0 : right.Contains(Main.mouseX, Main.mouseY) ? 1 : -1;
			return which >= 0;
		}

		private static void DrawMini(SpriteBatch spriteBatch, Rectangle hit, string text)
		{
			bool hover = hit.Contains(Main.mouseX, Main.mouseY);
			WeDraw.Fill(spriteBatch, hit, new Color(32, 36, 44) * ((hover ? 0.95f : 0.8f) * _fade));
			WeDraw.Border(spriteBatch, hit, WeAccent.Mid * _fade);
			var font = FontAssets.MouseText.Value;
			Vector2 size = font.MeasureString(text) * 0.72f;
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, font, text,
				new Vector2(hit.X + (hit.Width - size.X) * 0.5f, hit.Y + (hit.Height - size.Y) * 0.5f),
				Color.White * _fade, 0f, Vector2.Zero, new Vector2(0.72f));
		}

		private static void DrawHint(SpriteBatch spriteBatch, Rectangle panel, ref int y, string text)
		{
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, text,
				new Vector2(panel.X + 18, y + 4), Color.White * (0.7f * _fade), 0f, Vector2.Zero, new Vector2(0.72f));
			y += 28;
		}

		private static void SkipHint(ref int y) => y += 28;

		private static void DrawArtCard(SpriteBatch spriteBatch, Rectangle panel, ref int y, WeArtRecord record, bool on, bool logo)
		{
			Rectangle hit = Row(panel, y, 52);
			bool hover = hit.Contains(Main.mouseX, Main.mouseY);
			WeDraw.Fill(spriteBatch, hit, (on ? WeAccent.Deep : new Color(28, 30, 38)) * _fade);
			WeDraw.Border(spriteBatch, hit, (on || hover ? WeAccent.Light : WeAccent.Mid) * _fade);
			Texture2D tex = WeArt.Preview(record, logo);
			if (tex != null) {
				var dest = new Rectangle(hit.X + 8, hit.Y + 6, 64, 40);
				float scale = Math.Min(dest.Width / (float)tex.Width, dest.Height / (float)tex.Height);
				spriteBatch.Draw(tex, dest.Center.ToVector2(), null, Color.White * _fade, 0f, tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
			}

			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, record.FileName,
				new Vector2(hit.X + 82, hit.Y + 16), Color.White * _fade, 0f, Vector2.Zero, new Vector2(0.75f));
			y += 58;
		}

		private static bool ClickArt(Rectangle panel, ref int y, WeArtRecord record, bool logo)
		{
			Rectangle hit = Row(panel, y, 52);
			y += 58;
			if (Main.mouseRight && hit.Contains(Main.mouseX, Main.mouseY)) {
				WeArt.Delete(record, logo);
				return false;
			}

			return hit.Contains(Main.mouseX, Main.mouseY);
		}

		private static void DrawBorrowSection(SpriteBatch spriteBatch, Rectangle panel, ref int y, IReadOnlyList<WeOffer> offers, WeOfferKind kind)
		{
			DrawHint(spriteBatch, panel, ref y, WeText.UI(offers.Count == 0 ? "BorrowEmpty" : "FromMods"));
			foreach (WeOffer offer in offers)
				DrawBorrowCard(spriteBatch, panel, ref y, offer, IsBorrowedOn(offer, kind));
		}

		private static void ClickBorrowSection(Rectangle panel, ref int y, IReadOnlyList<WeOffer> offers, WeOfferKind kind)
		{
			SkipHint(ref y);
			foreach (WeOffer offer in offers) {
				if (!ClickBorrow(panel, ref y))
					continue;
				if (kind == WeOfferKind.Logo) {
					WeSettings.SetLogo(LogoKind.Borrowed, offer.Id);
					WeToast.Show("ToastLogo");
				}
				else {
					WeSettings.SetWallpaperBorrowed(offer.Id);
					WeToast.Show("ToastWallpaper");
				}
			}
		}

		private static bool IsBorrowedOn(WeOffer offer, WeOfferKind kind)
		{
			if (kind == WeOfferKind.Logo)
				return WeSave.Data.Logo == LogoKind.Borrowed && WeSave.Data.LogoId == offer.Id;
			return WeSave.Data.Wallpaper == WallpaperKind.Borrowed && WeSave.Data.WallpaperId == offer.Id;
		}

		private static void DrawBorrowCard(SpriteBatch spriteBatch, Rectangle panel, ref int y, WeOffer offer, bool on)
		{
			Rectangle hit = Row(panel, y, 56);
			bool hover = hit.Contains(Main.mouseX, Main.mouseY);
			WeDraw.Fill(spriteBatch, hit, (on ? WeAccent.Deep : new Color(28, 30, 38)) * ((hover ? 0.95f : 0.8f) * _fade));
			WeDraw.Border(spriteBatch, hit, (on || hover ? WeAccent.Light : WeAccent.Mid) * _fade);

			var badge = new Rectangle(hit.X + 8, hit.Y + 14, 28, 28);
			Texture2D icon = WeCatalog.ModIcon(offer.ModName);
			if (icon != null) {
				float scale = Math.Min(badge.Width / (float)icon.Width, badge.Height / (float)icon.Height);
				spriteBatch.Draw(icon, badge.Center.ToVector2(), null, Color.White * _fade, 0f, icon.Size() * 0.5f, scale, SpriteEffects.None, 0f);
			}
			else {
				WeDraw.Fill(spriteBatch, badge, BadgeColor(offer.ModName) * _fade);
				WeDraw.Border(spriteBatch, badge, WeAccent.Mid * _fade);
			}

			Texture2D preview = offer.Kind == WeOfferKind.Logo
				? WeCatalog.LogoTexture(offer.Id)
				: WeCatalog.SkyPreview(offer);
			var thumb = new Rectangle(hit.X + 44, hit.Y + 8, 72, 40);
			WeDraw.Fill(spriteBatch, thumb, Color.Black * (0.35f * _fade));
			if (preview != null) {
				float scale = Math.Min(thumb.Width / (float)preview.Width, thumb.Height / (float)preview.Height);
				spriteBatch.Draw(preview, thumb.Center.ToVector2(), null, Color.White * _fade, 0f, preview.Size() * 0.5f, scale, SpriteEffects.None, 0f);
			}
			else if (icon != null) {
				float scale = Math.Min(thumb.Width / (float)icon.Width, thumb.Height / (float)icon.Height) * 0.72f;
				spriteBatch.Draw(icon, thumb.Center.ToVector2(), null, Color.White * (0.55f * _fade), 0f, icon.Size() * 0.5f, scale, SpriteEffects.None, 0f);
			}

			string title = offer.Pending ? WeText.UI("BorrowPending") : Ellipsize(offer.MenuTitle, 28);
			string kind = offer.Kind == WeOfferKind.Logo
				? "BorrowKindLogo"
				: offer.UseThemeFx ? "BorrowKindSky" : "BorrowKindStill";
			string sub = Ellipsize(offer.ModTitle + "  ·  " + WeText.UI(kind), 34);
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, title,
				new Vector2(hit.X + 126, hit.Y + 8), Color.White * _fade, 0f, Vector2.Zero, new Vector2(0.75f));
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, sub,
				new Vector2(hit.X + 126, hit.Y + 28), Color.White * (0.62f * _fade), 0f, Vector2.Zero, new Vector2(0.68f));
			y += 62;
		}

		private static bool ClickBorrow(Rectangle panel, ref int y)
		{
			Rectangle hit = Row(panel, y, 56);
			y += 62;
			return hit.Contains(Main.mouseX, Main.mouseY);
		}

		private static Color BadgeColor(string name)
		{
			int h = name?.GetHashCode() ?? 0;
			return new Color(
				(byte)(70 + (h & 0x5F)),
				(byte)(70 + ((h >> 7) & 0x5F)),
				(byte)(80 + ((h >> 14) & 0x5F)));
		}

		private static string Ellipsize(string text, int max)
		{
			if (string.IsNullOrEmpty(text) || text.Length <= max)
				return text ?? "";
			return text[..Math.Max(1, max - 1)] + "...";
		}

		private static void DrawTrack(SpriteBatch spriteBatch, Rectangle panel, ref int y, WeTrackRecord track)
		{
			Rectangle hit = Row(panel, y, 36);
			Rectangle trash = TrashRect(hit);
			Rectangle body = new(hit.X, hit.Y, Math.Max(8, trash.X - hit.X - 8), hit.Height);
			bool on = !WeSave.Data.DisabledTrackIds.Contains(track.Id);
			bool playing = WeSave.Data.Music == MusicKind.Custom && WePlaylist.Current?.Id == track.Id;
			bool hover = body.Contains(Main.mouseX, Main.mouseY);
			WeDraw.Fill(spriteBatch, body, ((playing || on) ? WeAccent.Deep : new Color(28, 30, 38)) * ((hover || playing ? 0.95f : 0.8f) * _fade));
			WeDraw.Border(spriteBatch, body, (playing || on || hover ? WeAccent.Light : WeAccent.Mid) * _fade);
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, Ellipsize(track.Title + "  ·  " + track.Artist, 28),
				new Vector2(body.X + 12, hit.Y + 8), Color.White * _fade, 0f, Vector2.Zero, new Vector2(0.82f));
			RoundButton.DrawIcon(spriteBatch, trash.Center.ToVector2(), 12f, WeIcons.Get(WeIcons.Trash), 0f, _fade);
			RoundButton.Tooltip(spriteBatch, trash.Center.ToVector2(), 12f, WeText.UI("DeleteTrack"), _fade);
			y += 42;
		}

		private static void ClickTrack(Rectangle panel, ref int y, WeTrackRecord track)
		{
			Rectangle hit = Row(panel, y, 36);
			Rectangle trash = TrashRect(hit);
			Rectangle body = new(hit.X, hit.Y, Math.Max(8, trash.X - hit.X - 8), hit.Height);
			y += 42;
			if (trash.Contains(Main.mouseX, Main.mouseY)) {
				WePlaylist.DeleteCustom(track);
				WeToast.Show("ToastTrackDeleted");
				return;
			}

			if (!body.Contains(Main.mouseX, Main.mouseY))
				return;

			WePlaylist.PlayTrack(track);
		}

		private static Rectangle TrashRect(Rectangle hit) =>
			new(hit.Right - 34, hit.Y + 4, 28, 28);

		private static void DrawRgb(SpriteBatch spriteBatch, Rectangle panel, ref int y, string key, Color color, string label = null)
		{
			if (!string.IsNullOrEmpty(label)) {
				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch, FontAssets.MouseText.Value, label,
					new Vector2(panel.X + 18, y), Color.White * _fade, 0f, Vector2.Zero, new Vector2(0.72f));
				y += 20;
			}

			DrawSlider(spriteBatch, panel, ref y, key + "R", color.R / 255f, WeText.UI("Red"));
			DrawSlider(spriteBatch, panel, ref y, key + "G", color.G / 255f, WeText.UI("Green"));
			DrawSlider(spriteBatch, panel, ref y, key + "B", color.B / 255f, WeText.UI("Blue"));
			WeDraw.Fill(spriteBatch, new Rectangle(panel.Right - 52, y - 70, 28, 62), color * _fade);
			y += 8;
		}

		private static void ClickRgb(Rectangle panel, ref int y, string key, bool labeled = false)
		{
			if (labeled)
				y += 20;
			ClickSlider(panel, ref y, key + "R");
			ClickSlider(panel, ref y, key + "G");
			ClickSlider(panel, ref y, key + "B");
			y += 8;
		}

		private static void DrawSlider(SpriteBatch spriteBatch, Rectangle panel, ref int y, string key, float value, string label)
		{
			Rectangle bar = new(panel.X + 90, y + 8, panel.Width - 160, 8);
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, label,
				new Vector2(panel.X + 18, y), Color.White * (0.8f * _fade), 0f, Vector2.Zero, new Vector2(0.7f));
			WeDraw.Fill(spriteBatch, bar, Color.White * (0.15f * _fade));
			WeDraw.Fill(spriteBatch, new Rectangle(bar.X, bar.Y, Math.Max(1, (int)(bar.Width * value)), bar.Height), WeAccent.Mid * _fade);
			y += 22;
		}

		private static void ClickSlider(Rectangle panel, ref int y, string key)
		{
			Rectangle bar = new(panel.X + 90, y + 4, panel.Width - 160, 16);
			y += 22;
			if (bar.Contains(Main.mouseX, Main.mouseY)) {
				_dragSlider = key;
				ApplySlider(key);
			}
		}

		private static void ApplySlider(string key)
		{
			Rectangle panel = PanelRect();
			Rectangle bar = new(panel.X + 90, 0, panel.Width - 160, 8);
			float t = MathHelper.Clamp((Main.mouseX - bar.X) / (float)bar.Width, 0f, 1f);
			if (key == "dim") {
				WeSave.Data.WallpaperDim = t;
				WeSave.Save();
				return;
			}

			if (key == "vignette") {
				WeSave.Data.WallpaperVignette = t;
				WeSave.Save();
				return;
			}

			if (key is "lop" or "lpar" or "lzm") {
				WeLayerRecord layer = WeSettings.SelectedLayer();
				if (layer == null)
					return;
				if (key == "lop")
					layer.Opacity = t;
				else if (key == "lpar")
					layer.Parallax = t;
				else
					layer.Zoom = 0.6f + t * 1.2f;
				WeSave.Save();
				return;
			}

			int v = (int)(t * 255f);
			if (key.StartsWith("wallA") || key.StartsWith("wallB")) {
				Color a = WeSettings.WallpaperColorA;
				Color b = WeSettings.WallpaperColorB;
				bool second = key.StartsWith("wallB");
				Color c = second ? b : a;
				if (key.EndsWith("R"))
					c.R = (byte)v;
				else if (key.EndsWith("G"))
					c.G = (byte)v;
				else
					c.B = (byte)v;
				WeSettings.SetWallpaperRgb(second, c.R, c.G, c.B);
				return;
			}

			string which = key.StartsWith("caption") ? "caption" : key.StartsWith("border") ? "border" : "title";
			Color cur = which == "caption" ? WeSettings.CaptionColor : which == "border" ? WeSettings.BorderColor : WeSettings.TitleTextColor;
			if (key.EndsWith("R"))
				cur.R = (byte)v;
			else if (key.EndsWith("G"))
				cur.G = (byte)v;
			else
				cur.B = (byte)v;
			WeSettings.SetChromeRgb(which, cur.R, cur.G, cur.B);
		}

		private static void DrawAccent(SpriteBatch spriteBatch, Rectangle panel, ref int y, int index)
		{
			Rectangle hit = Row(panel, y, 28);
			WeDraw.Fill(spriteBatch, hit, WeAccent.Palettes[index].Mid * _fade);
			if (index == WeAccent.Index)
				WeDraw.Border(spriteBatch, hit, Color.White * _fade);
			y += 34;
		}

		private static bool ClickAccent(Rectangle panel, ref int y)
		{
			Rectangle hit = Row(panel, y, 28);
			y += 34;
			return hit.Contains(Main.mouseX, Main.mouseY);
		}

		private static Rectangle PanelRect()
		{
			if (_id == WePanelId.Wallpaper) {
				int w = Math.Min(400, Math.Max(300, Main.screenWidth / 3));
				int h = Main.screenHeight - 40;
				return new Rectangle(Main.screenWidth - w - 16, 20, w, h);
			}

			int cw = Math.Min(560, Main.screenWidth - 80);
			int ch = Math.Min(520, Main.screenHeight - 80);
			return new Rectangle((Main.screenWidth - cw) / 2, (Main.screenHeight - ch) / 2, cw, ch);
		}

		private static Rectangle View(Rectangle panel) =>
			new(panel.X + 8, panel.Y + 48, panel.Width - 16, panel.Height - 60);

		private static Rectangle Row(Rectangle panel, int y, int h) =>
			new(panel.X + 16, y, panel.Width - 32, h);

		private static string LayerTitle(WeLayerRecord layer)
		{
			string name = layer.Kind == WeLayerKind.Effect
				? WeText.UI(FxKey(layer.Effect))
				: FileNameOf(layer.ArtId);
			if (layer.Foreground)
				name += "  ·  " + WeText.UI("LayerForeground");
			return name;
		}

		private static string FileNameOf(string artId)
		{
			if (string.IsNullOrEmpty(artId))
				return WeText.UI("ImageLayer");
			WeArtRecord record = WeSave.Data.Wallpapers.Find(item => item.Id == artId);
			return record == null ? WeText.UI("ImageLayer") : record.FileName;
		}

		private static string FitKey() => FitKey(WeSave.Data.WallpaperFit);

		private static string FitKey(WallpaperFit fit) => fit switch {
			WallpaperFit.Contain => "FitContain",
			WallpaperFit.Stretch => "FitStretch",
			_ => "FitCover"
		};

		private static string FxKey(WeFxKind effect) => effect switch {
			WeFxKind.Dust => "FxDust",
			WeFxKind.Fog => "FxFog",
			WeFxKind.Grain => "FxGrain",
			WeFxKind.Scanlines => "FxScan",
			WeFxKind.Fireflies => "FxFlies",
			WeFxKind.Clouds => "FxClouds",
			WeFxKind.Rain => "FxRain",
			WeFxKind.Beat => "FxBeat",
			_ => "FxStars"
		};

		private static float MaxScroll()
		{
			float extra = WeSave.Data.Layers.Count * 42f + WeSave.Data.Wallpapers.Count * 58f + WeSave.Data.Logos.Count * 58f +
			              WeSave.Data.Tracks.Count * 42f + WeCatalog.Skies.Count * 62f + WeCatalog.Logos.Count * 62f;
			return 720f + extra;
		}
	}
}
