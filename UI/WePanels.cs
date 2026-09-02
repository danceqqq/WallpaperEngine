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

	internal static partial class WePanels
	{
		private static WePanelId _id;
		private static float _fade;
		private static float _scroll;
		private static bool _frameInput;
		private static bool _mouseHeld;
		private static bool _holdLock;
		private static bool _rightHeld;
		private static bool _rightLock;
		private static int _lastWheel;
		private static string _dragSlider;
		private static bool _ateInput;
		private static int _clientChip;
		private const int SlotCount = 5;
		private const int SlotPlayer = 0;
		private const int SlotClock = 1;
		private const int SlotQuote = 2;
		private const int SlotMoon = 3;
		private const int SlotDiscord = 4;
		private static readonly float[] TileOpen = new float[SlotCount];
		private static int _tileHover = -1;
		private const int WidgetTileH = 72;
		private const int WidgetTileGap = 8;
		private const int DiscordStyleH = 80;
		private const int DiscordStyleStep = 86;

		internal static bool IsOpen => _id != WePanelId.None;
		internal static bool Is(WePanelId id) => _id == id;
		internal static bool AteInput => _ateInput;

		internal static void Open(WePanelId id)
		{
			LayoutEditor.Cancel(false);
			_id = id;
			_scroll = 0f;
			WeArt.Scan();
			WeLibrary.ScanIntoSave();
			WeCatalog.Refresh();
			WePresets.Refresh();
			WeType.Scan();
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		internal static void Close()
		{
			if (_id == WePanelId.None)
				return;
			_id = WePanelId.None;
			_dragSlider = null;
			DiscordWidget.Unfocus();
			SoundEngine.PlaySound(SoundID.MenuClose);
		}

		internal static void Update()
		{
			if (!WeModMenu.OnTitle) {
				if (IsOpen)
					DiscordWidget.Unfocus();
				_id = WePanelId.None;
			}
			_fade = MathHelper.Lerp(_fade, IsOpen ? 1f : 0f, 0.22f);
			if (!IsOpen && _fade < 0.02f)
				_fade = 0f;
			TickWidgetTiles();
		}

		internal static void HandleInput()
		{
			if (_frameInput)
				return;
			_frameInput = true;

			bool pressed = WeInput.Edge(ref _mouseHeld, ref _holdLock);
			bool rightPressed = WeInput.Edge(WeInput.RightDown, ref _rightHeld, ref _rightLock);
			if (!IsOpen)
				return;

			_ateInput = true;
			Main.blockMouse = true;
			Rectangle panel = PanelRect();

			int wheel = Mouse.GetState().ScrollWheelValue;
			if (panel.Contains(Main.mouseX, Main.mouseY))
				_scroll = MathHelper.Clamp(_scroll - (wheel - _lastWheel) / 120f * 42f, 0f, MaxScroll());
			_lastWheel = wheel;

			if (_dragSlider != null) {
				if (WeInput.LeftDown)
					ApplySlider(_dragSlider);
				else
					_dragSlider = null;
				return;
			}

			if (!pressed && !rightPressed)
				return;

			if (!panel.Contains(Main.mouseX, Main.mouseY)) {
				if (pressed)
					Close();
				if (pressed)
					WeInput.LockHold(ref _holdLock);
				if (rightPressed)
					WeInput.LockHold(ref _rightLock);
				return;
			}

			HandleClicks(panel);
			if (pressed)
				WeInput.LockHold(ref _holdLock);
			if (rightPressed)
				WeInput.LockHold(ref _rightLock);
		}

		internal static void EndFrame()
		{
			_frameInput = false;
			_ateInput = false;
		}

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
				if (selected.Kind == WeLayerKind.Image) {
					DrawCard(spriteBatch, panel, ref y, WeText.UI(FitKey(selected.Fit)), true);
					DrawCard(spriteBatch, panel, ref y, WeText.UI("CenterImage"), false);
				}
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
					if (ClickCard(panel, ref y))
						WeSettings.CenterWallpaperPan();
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
			DrawCard(spriteBatch, panel, ref y, WeText.UI("LogoPulse"), WeLook.LogoPulse);
			foreach (string id in WePresetLogos.Ids)
				DrawPresetCard(spriteBatch, panel, ref y, id);
			DrawButtonRow(spriteBatch, panel, ref y, WeText.UI("ImportImage"), WeText.UI("OpenFolder"));
			foreach (WeArtRecord record in WeSave.Data.Logos)
				DrawArtCard(spriteBatch, panel, ref y, record, WeSave.Data.Logo == LogoKind.Custom && WeSave.Data.LogoId == record.Id, logo: true);

			DrawBorrowSection(spriteBatch, panel, ref y, WeCatalog.Logos, WeOfferKind.Logo);
		}

		private static void ClickLogo(Rectangle panel, ref int y)
		{
			SkipHint(ref y);
			if (ClickCard(panel, ref y))
				WeSettings.SetLogo(LogoKind.Vanilla);
			if (ClickCard(panel, ref y))
				WeSettings.SetLogo(LogoKind.Hidden);
			if (ClickCard(panel, ref y))
				WeSettings.ToggleLogoPulse();
			foreach (string id in WePresetLogos.Ids) {
				if (!ClickBorrow(panel, ref y))
					continue;
				WeSettings.SetLogo(LogoKind.Preset, id);
				WeToast.Show("ToastLogo");
			}
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
			DrawCard(spriteBatch, panel, ref y, WeText.UI("MuteUnfocused"), WeSave.Data.MuteWhenUnfocused);
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

			if (ClickCard(panel, ref y))
				WeSettings.ToggleMuteUnfocused();

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
			DrawWidgetBand(spriteBatch, panel, ref y, SlotPlayer, WeText.UI("AddPlayer"), WeSave.Data.PlayerWidget, WePlayerUI.DrawPreview);
			DrawWidgetBand(spriteBatch, panel, ref y, SlotClock, WeText.UI("AddClock"), WeSave.Data.ClockWidget, ClockWidget.DrawPreview);
			DrawWidgetBand(spriteBatch, panel, ref y, SlotQuote, WeText.UI("AddQuote"), WeSave.Data.QuoteWidget, QuoteWidget.DrawPreview);
			DrawWidgetBand(spriteBatch, panel, ref y, SlotMoon, WeText.UI("AddMoon"), WeSave.Data.MoonWidget, MoonWidget.DrawPreview);
			DrawWidgetBand(spriteBatch, panel, ref y, SlotDiscord, WeText.UI("AddDiscord"), WeSave.Data.DiscordWidget,
				(sb, box, fade) => DiscordWidget.DrawPreview(sb, box, fade, WeSave.Data.DiscordStyle));

			DrawCard(spriteBatch, panel, ref y, WeText.UI("BtnClean"), WeSave.Data.CleanChrome);
			DrawHint(spriteBatch, panel, ref y, WeText.UI("HiddenLayers"));
			foreach (WeElementRecord hidden in SceneGraph.Hidden())
				DrawCard(spriteBatch, panel, ref y, WeText.Layer(hidden.Id) + "  ·  " + WeText.UI("Restore"), false);
		}

		private static void ClickWidgets(Rectangle panel, ref int y)
		{
			if (ClickWidgetBand(panel, ref y, SlotPlayer)) {
				WeSettings.SetPlayerWidget(!WeSave.Data.PlayerWidget);
				WeToast.Show(WeSave.Data.PlayerWidget ? "ToastWidgetOn" : "ToastWidgetOff");
			}

			if (ClickWidgetBand(panel, ref y, SlotClock)) {
				WeSettings.SetClockWidget(!WeSave.Data.ClockWidget);
				WeToast.Show(WeSave.Data.ClockWidget ? "ToastWidgetOn" : "ToastWidgetOff");
			}

			if (ClickWidgetBand(panel, ref y, SlotQuote)) {
				WeSettings.SetQuoteWidget(!WeSave.Data.QuoteWidget);
				if (WeSave.Data.QuoteWidget)
					QuoteWidget.EnsureFile();
				WeToast.Show(WeSave.Data.QuoteWidget ? "ToastWidgetOn" : "ToastWidgetOff");
			}

			if (ClickWidgetBand(panel, ref y, SlotMoon)) {
				WeSettings.SetMoonWidget(!WeSave.Data.MoonWidget);
				WeToast.Show(WeSave.Data.MoonWidget ? "ToastWidgetOn" : "ToastWidgetOff");
			}

			bool discordWasOn = WeSave.Data.DiscordWidget;
			if (ClickWidgetBand(panel, ref y, SlotDiscord, discordWasOn)) {
				if (discordWasOn) {
					WeSettings.SetDiscordWidget(false);
					DiscordWidget.Unfocus();
					TileOpen[SlotDiscord] = 0f;
					WeToast.Show("ToastWidgetOff");
				}
				else {
					WeSettings.SetDiscordWidget(true);
					DiscordWidget.OpenIdEditor();
					DiscordFeed.RefreshNow();
					TileOpen[SlotDiscord] = 1f;
					_tileHover = SlotDiscord;
					WeToast.Show("ToastDiscordId", 3.6f);
				}
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
			ClientLooks(spriteBatch, panel, ref y, false);
			ClientHub(spriteBatch, panel, ref y, false);
			ClientWindow(spriteBatch, panel, ref y, false);
			ClientMenu(spriteBatch, panel, ref y, false);
			ClientAccent(spriteBatch, panel, ref y, false);
		}

		private static void ClickClient(Rectangle panel, ref int y)
		{
			ClientLooks(null, panel, ref y, true);
			ClientHub(null, panel, ref y, true);
			ClientWindow(null, panel, ref y, true);
			ClientMenu(null, panel, ref y, true);
			ClientAccent(null, panel, ref y, true);
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

		private static void DrawWidgetBand(SpriteBatch spriteBatch, Rectangle panel, ref int y, int slot, string title, bool on, Action<SpriteBatch, Rectangle, float> preview)
		{
			DrawWidgetTile(spriteBatch, panel, ref y, title, on, preview, slot);
			int extra = SettingsExtra(slot);
			if (on && extra > 0) {
				var well = Row(panel, y, extra);
				WeDraw.Fill(spriteBatch, well, new Color(14, 16, 20) * (0.94f * _fade));
				WeDraw.Border(spriteBatch, well, WeAccent.Mid * (0.45f * _fade));
				WeDraw.WithClip(spriteBatch, well, () => {
					int inner = well.Y;
					DrawSlotSettings(spriteBatch, panel, ref inner, slot);
				});
				y += extra;
			}

			y += WidgetTileGap;
		}

		private static bool ClickWidgetBand(Rectangle panel, ref int y, int slot, bool discordWasOn = false)
		{
			bool tile = ClickWidgetTile(panel, ref y);
			int extra = SettingsExtra(slot);
			if (TileOn(slot) && SettingsNatural(slot) > 0) {
				int start = y;
				if (!tile && TileOpen[slot] > 0.82f) {
					int inner = start;
					ClickSlotSettings(panel, ref inner, slot, discordWasOn);
				}

				y = start + extra;
			}

			y += WidgetTileGap;
			return tile;
		}

		private static void DrawWidgetTile(SpriteBatch spriteBatch, Rectangle panel, ref int y, string title, bool on, Action<SpriteBatch, Rectangle, float> preview, int slot)
		{
			Rectangle hit = Row(panel, y, WidgetTileH);
			bool hover = hit.Contains(Main.mouseX, Main.mouseY) || _tileHover == slot;
			Color fill = on ? WeAccent.Deep : new Color(22, 24, 30);
			Color border = on || hover ? WeAccent.Light : new Color(72, 76, 84);
			WeDraw.Fill(spriteBatch, hit, fill * ((hover ? 0.96f : on ? 0.88f : 0.78f) * _fade));
			WeDraw.Border(spriteBatch, hit, border * _fade);
			if (on)
				WeDraw.Fill(spriteBatch, new Rectangle(hit.X, hit.Y + 8, 3, hit.Height - 16), WeAccent.Mid * _fade);
			var previewBox = new Rectangle(hit.X + 10, hit.Y + 8, 148, hit.Height - 16);
			WeDraw.Fill(spriteBatch, previewBox, new Color(14, 16, 20) * _fade);
			preview?.Invoke(spriteBatch, previewBox, _fade * (on ? 1f : 0.42f));
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, title,
				new Vector2(previewBox.Right + 14, hit.Y + 16),
				(on ? Color.White : new Color(168, 172, 180)) * _fade, 0f, Vector2.Zero, new Vector2(0.84f));
			if (on && SettingsNatural(slot) > 0) {
				string hint = WeText.UI("WidgetHoverSettings");
				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch, FontAssets.MouseText.Value, hint,
					new Vector2(previewBox.Right + 14, hit.Y + 40),
					MutedHint() * _fade, 0f, Vector2.Zero, new Vector2(0.68f));
			}

			y += WidgetTileH;
		}

		private static bool ClickWidgetTile(Rectangle panel, ref int y)
		{
			Rectangle hit = Row(panel, y, WidgetTileH);
			y += WidgetTileH;
			return hit.Contains(Main.mouseX, Main.mouseY);
		}

		private static Color MutedHint() => new Color(168, 172, 180);

		private static void DrawSlotSettings(SpriteBatch spriteBatch, Rectangle panel, ref int y, int slot)
		{
			switch (slot) {
				case SlotClock:
					DrawCard(spriteBatch, panel, ref y, WeText.UI(WeSave.Data.Clock24h ? "Clock24h" : "Clock12h"), WeSave.Data.Clock24h);
					DrawCard(spriteBatch, panel, ref y, WeText.UI(WeSave.Data.ClockAnalog ? "ClockAnalog" : "ClockDigital"), WeSave.Data.ClockAnalog);
					DrawCard(spriteBatch, panel, ref y, WeText.UI("ClockDate"), WeSave.Data.ClockDate);
					break;
				case SlotQuote:
					DrawButtonRow(spriteBatch, panel, ref y, WeText.UI("OpenQuotes"), WeText.UI("OpenFolder"));
					break;
				case SlotDiscord:
					DrawDiscordIdField(spriteBatch, panel, ref y);
					DrawHint(spriteBatch, panel, ref y, DiscordWidget.StatusLine());
					DrawHint(spriteBatch, panel, ref y, WeText.UI("DiscordHint1"));
					DrawHint(spriteBatch, panel, ref y, WeText.UI("DiscordHint2"));
					DrawDiscordStyle(spriteBatch, panel, ref y, 0);
					DrawDiscordStyle(spriteBatch, panel, ref y, 1);
					DrawDiscordStyle(spriteBatch, panel, ref y, 2);
					break;
			}
		}

		private static void ClickSlotSettings(Rectangle panel, ref int y, int slot, bool discordWasOn)
		{
			switch (slot) {
				case SlotClock:
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

					break;
				case SlotQuote:
					if (ClickRow(panel, ref y, out int quoteWhich)) {
						if (quoteWhich == 0) {
							QuoteWidget.EnsureFile();
							WeFiles.OpenFile(WeSave.QuotePath);
						}
						else
							WeFiles.OpenFolder(WeSave.RootFolder);
					}

					break;
				case SlotDiscord:
					if (ClickDiscordIdField(panel, ref y))
						DiscordWidget.OpenIdEditor();
					else if (discordWasOn && DiscordWidget.Editing)
						DiscordWidget.Unfocus();
					SkipHint(ref y);
					SkipHint(ref y);
					SkipHint(ref y);
					if (ClickDiscordStyle(panel, ref y))
						WeSettings.SetDiscordStyle(0);
					if (ClickDiscordStyle(panel, ref y))
						WeSettings.SetDiscordStyle(1);
					if (ClickDiscordStyle(panel, ref y))
						WeSettings.SetDiscordStyle(2);
					break;
			}
		}

		private static void TickWidgetTiles()
		{
			if (!IsOpen || _id != WePanelId.Widgets) {
				for (int i = 0; i < SlotCount; i++)
					TileOpen[i] = MathHelper.Lerp(TileOpen[i], 0f, 0.28f);
				_tileHover = -1;
				return;
			}

			Rectangle panel = PanelRect();
			Rectangle view = View(panel);
			int y = view.Y - (int)_scroll;
			int hover = -1;
			Point mouse = new(Main.mouseX, Main.mouseY);
			if (view.Contains(mouse)) {
				for (int slot = 0; slot < SlotCount; slot++) {
					int height = BandHeight(slot);
					var band = new Rectangle(panel.X + 16, y, panel.Width - 32, Math.Max(1, height - WidgetTileGap));
					if (band.Contains(mouse))
						hover = slot;
					y += height;
				}
			}

			_tileHover = hover;
			for (int slot = 0; slot < SlotCount; slot++) {
				float prev = TileOpen[slot];
				float target = TileOn(slot) && SettingsNatural(slot) > 0f && hover == slot ? 1f : 0f;
				TileOpen[slot] = MathHelper.Lerp(prev, target, 0.2f);
				if (Math.Abs(TileOpen[slot] - target) < 0.012f)
					TileOpen[slot] = target;
				if (TileOpen[slot] > prev + 0.004f)
					NudgeScrollToSlot(slot);
			}

			_scroll = MathHelper.Clamp(_scroll, 0f, MaxScroll());
		}

		private static void NudgeScrollToSlot(int slot)
		{
			Rectangle view = View(PanelRect());
			int y = view.Y - (int)_scroll;
			for (int i = 0; i < slot; i++)
				y += BandHeight(i);
			int bottom = y + BandHeight(slot) - WidgetTileGap;
			if (bottom > view.Bottom - 8)
				_scroll += bottom - (view.Bottom - 8);
			if (y < view.Y + 4)
				_scroll -= view.Y + 4 - y;
			_scroll = MathHelper.Clamp(_scroll, 0f, MaxScroll());
		}

		private static int BandHeight(int slot) =>
			WidgetTileH + WidgetTileGap + (TileOn(slot) ? SettingsExtra(slot) : 0);

		private static int SettingsExtra(int slot)
		{
			if (!TileOn(slot))
				return 0;
			return (int)MathF.Round(SettingsNatural(slot) * TileOpen[slot]);
		}

		private static float SettingsNatural(int slot) => slot switch {
			SlotClock => 126f,
			SlotQuote => 40f,
			SlotDiscord => 52f + 84f + DiscordStyleStep * 3,
			_ => 0f
		};

		private static bool TileOn(int slot) => slot switch {
			SlotPlayer => WeSave.Data.PlayerWidget,
			SlotClock => WeSave.Data.ClockWidget,
			SlotQuote => WeSave.Data.QuoteWidget,
			SlotMoon => WeSave.Data.MoonWidget,
			SlotDiscord => WeSave.Data.DiscordWidget,
			_ => false
		};

		private static void DrawDiscordStyle(SpriteBatch spriteBatch, Rectangle panel, ref int y, int style)
		{
			Rectangle hit = Row(panel, y, DiscordStyleH);
			bool on = WeSave.Data.DiscordStyle == style;
			bool hover = hit.Contains(Main.mouseX, Main.mouseY);
			Color fill = on ? WeAccent.Deep : new Color(22, 24, 30);
			Color border = on || hover ? WeAccent.Light : new Color(72, 76, 84);
			WeDraw.Fill(spriteBatch, hit, fill * ((hover ? 0.96f : on ? 0.88f : 0.78f) * _fade));
			WeDraw.Border(spriteBatch, hit, border * _fade);
			var preview = new Rectangle(hit.X + 8, hit.Y + 6, hit.Width - 16, 46);
			WeDraw.Fill(spriteBatch, preview, new Color(14, 16, 20) * _fade);
			DiscordWidget.DrawPreview(spriteBatch, preview, _fade * (on ? 1f : 0.45f), style);
			string key = style == 1 ? "DiscordStyleBanner" : style == 2 ? "DiscordStyleRoster" : "DiscordStyleCompact";
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, WeText.UI(key),
				new Vector2(hit.X + 12, hit.Bottom - 22),
				(on ? Color.White : new Color(168, 172, 180)) * _fade, 0f, Vector2.Zero, new Vector2(0.76f));
			y += DiscordStyleStep;
		}

		private static bool ClickDiscordStyle(Rectangle panel, ref int y)
		{
			Rectangle hit = Row(panel, y, DiscordStyleH);
			y += DiscordStyleStep;
			return hit.Contains(Main.mouseX, Main.mouseY);
		}

		private static void DrawDiscordIdField(SpriteBatch spriteBatch, Rectangle panel, ref int y)
		{
			Rectangle hit = Row(panel, y, DiscordWidget.IdFieldHeight);
			DiscordWidget.DrawIdField(spriteBatch, hit, _fade);
			y += 52;
		}

		private static bool ClickDiscordIdField(Rectangle panel, ref int y)
		{
			Rectangle hit = Row(panel, y, DiscordWidget.IdFieldHeight);
			y += 52;
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

		private static bool ClickLook(Rectangle panel, ref int y, out bool trash)
		{
			Rectangle hit = Row(panel, y, 36);
			y += 42;
			trash = false;
			if (!hit.Contains(Main.mouseX, Main.mouseY))
				return false;
			if (Main.mouseRight) {
				trash = true;
				return true;
			}

			return true;
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

		private static void DrawPresetCard(SpriteBatch spriteBatch, Rectangle panel, ref int y, string id)
		{
			Rectangle hit = Row(panel, y, 56);
			bool on = WeSave.Data.Logo == LogoKind.Preset && WeSave.Data.LogoId == id;
			bool hover = hit.Contains(Main.mouseX, Main.mouseY);
			WeDraw.Fill(spriteBatch, hit, (on ? WeAccent.Deep : new Color(28, 30, 38)) * ((hover ? 0.95f : 0.8f) * _fade));
			WeDraw.Border(spriteBatch, hit, (on || hover ? WeAccent.Light : WeAccent.Mid) * _fade);

			var badge = new Rectangle(hit.X + 8, hit.Y + 14, 28, 28);
			Texture2D icon = WePresetLogos.PackIcon();
			if (icon != null) {
				float scale = Math.Min(badge.Width / (float)icon.Width, badge.Height / (float)icon.Height);
				spriteBatch.Draw(icon, badge.Center.ToVector2(), null, Color.White * _fade, 0f, icon.Size() * 0.5f, scale, SpriteEffects.None, 0f);
			}
			else {
				WeDraw.Fill(spriteBatch, badge, WeAccent.Deep * _fade);
				WeDraw.Border(spriteBatch, badge, WeAccent.Mid * _fade);
			}

			Texture2D preview = WePresetLogos.PreviewOf(id);
			var thumb = new Rectangle(hit.X + 44, hit.Y + 8, 72, 40);
			WeDraw.Fill(spriteBatch, thumb, Color.Black * (0.35f * _fade));
			if (preview != null) {
				float scale = Math.Min(thumb.Width / (float)preview.Width, thumb.Height / (float)preview.Height);
				spriteBatch.Draw(preview, thumb.Center.ToVector2(), null, Color.White * _fade, 0f, preview.Size() * 0.5f, scale, SpriteEffects.None, 0f);
			}

			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, WeText.UI(WePresetLogos.TitleKey(id)),
				new Vector2(hit.X + 126, hit.Y + 8), Color.White * _fade, 0f, Vector2.Zero, new Vector2(0.75f));
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value,
				WeText.UI("LogoFromPack") + "  ·  " + WeText.UI("BorrowKindLogo"),
				new Vector2(hit.X + 126, hit.Y + 28), Color.White * (0.62f * _fade), 0f, Vector2.Zero, new Vector2(0.68f));
			y += 62;
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

			if (key == "fw" || key == "fh") {
				WeSettings.SetFontScale(key == "fw", 0.5f + t * 1.3f);
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

			if (key.StartsWith("menu")) {
				Color c = WeSettings.MenuTextColor;
				if (key.EndsWith("R"))
					c.R = (byte)v;
				else if (key.EndsWith("G"))
					c.G = (byte)v;
				else
					c.B = (byte)v;
				WeSettings.SetMenuTextRgb(c.R, c.G, c.B);
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

			if (_id == WePanelId.Widgets || _id == WePanelId.Client) {
				int ww = Math.Min(600, Main.screenWidth - 72);
				int wh = Math.Min(660, Main.screenHeight - 56);
				return new Rectangle((Main.screenWidth - ww) / 2, (Main.screenHeight - wh) / 2, ww, wh);
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
			if (_id == WePanelId.Widgets) {
				float content = 0f;
				for (int i = 0; i < SlotCount; i++)
					content += BandHeight(i);
				int hidden = 0;
				foreach (WeElementRecord _ in SceneGraph.Hidden())
					hidden++;
				content += 42f + 28f + hidden * 42f + 16f;
				return Math.Max(0f, content - View(PanelRect()).Height);
			}

			if (_id == WePanelId.Client)
				return Math.Max(0f, ClientContentHeight() - View(PanelRect()).Height);

			float extra = WeSave.Data.Layers.Count * 42f + WeSave.Data.Wallpapers.Count * 58f + WeSave.Data.Logos.Count * 58f +
			              WeSave.Data.Tracks.Count * 42f + WeCatalog.Skies.Count * 62f + WeCatalog.Logos.Count * 62f +
			              (_id == WePanelId.Logo ? WePresetLogos.Ids.Length * 62f + 42f : 0f) +
			              (_id == WePanelId.Wallpaper ? 42f : 0f) +
			              (_id == WePanelId.Music ? 42f : 0f);
			return 720f + extra;
		}
	}
}
