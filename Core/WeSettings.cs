using System;
using Microsoft.Xna.Framework;
using WallpaperEngine.Chrome;

namespace WallpaperEngine.Core
{
	internal static class WeSettings
	{
		internal static WeSaveData Current => WeSave.Data;

		internal static bool HasCustomSky
		{
			get
			{
				if (Current.Wallpaper is WallpaperKind.Color or WallpaperKind.Gradient or WallpaperKind.Image or WallpaperKind.Borrowed)
					return true;
				return Current.Layers.Exists(layer =>
					layer.Visible && !layer.Foreground && layer.Kind == WeLayerKind.Image && !string.IsNullOrEmpty(layer.ArtId));
			}
		}

		internal static Color WallpaperColorA => new(Current.WallpaperColorR, Current.WallpaperColorG, Current.WallpaperColorB);
		internal static Color WallpaperColorB => new(Current.WallpaperColor2R, Current.WallpaperColor2G, Current.WallpaperColor2B);

		internal static Vector2 WallpaperPan
		{
			get
			{
				WeLayerRecord layer = SelectedLayer();
				if (layer is { Kind: WeLayerKind.Image })
					return new Vector2(Math.Clamp(layer.PanX, 0f, 1f), Math.Clamp(layer.PanY, 0f, 1f));
				WeArtRecord record = CurrentWallpaper();
				if (record == null)
					return new Vector2(0.5f, 0.5f);
				return new Vector2(Math.Clamp(record.PanX, 0f, 1f), Math.Clamp(record.PanY, 0f, 1f));
			}
		}

		internal static WeArtRecord CurrentWallpaper()
		{
			if (string.IsNullOrEmpty(Current.WallpaperId))
				return null;
			return Current.Wallpapers.Find(item => item.Id == Current.WallpaperId);
		}

		internal static WeArtRecord CurrentLogo()
		{
			if (string.IsNullOrEmpty(Current.LogoId))
				return null;
			return Current.Logos.Find(item => item.Id == Current.LogoId);
		}

		internal static void SetWallpaperVanilla()
		{
			Current.Wallpaper = WallpaperKind.Vanilla;
			Current.WallpaperId = "";
			WeSave.Save();
		}

		internal static void SetWallpaperColor(bool gradient)
		{
			Current.Wallpaper = gradient ? WallpaperKind.Gradient : WallpaperKind.Color;
			Current.WallpaperId = "";
			WeSave.Save();
		}

		internal static void SetWallpaperImage(string id)
		{
			Current.Wallpaper = WallpaperKind.Image;
			Current.WallpaperId = id ?? "";
			AddOrSelectImageLayer(id);
			WeSave.Save();
		}

		internal static WeLayerRecord SelectedLayer()
		{
			if (Current.Layers.Count == 0)
				return null;
			WeLayerRecord selected = Current.Layers.Find(item => item.Id == Current.SelectedLayerId);
			return selected ?? Current.Layers[0];
		}

		internal static void SelectLayer(string id)
		{
			Current.SelectedLayerId = id ?? "";
			WeSave.Save();
		}

		internal static WeLayerRecord AddImageLayer(string artId)
		{
			if (Current.Layers.Count >= 6)
				return SelectedLayer();

			var layer = new WeLayerRecord {
				Id = "layer-" + Guid.NewGuid().ToString("N")[..8],
				Kind = WeLayerKind.Image,
				ArtId = artId ?? "",
				Parallax = 0.16f,
				Opacity = 1f,
				Zoom = 1f,
				Fit = Current.WallpaperFit
			};
			Current.Layers.Add(layer);
			Current.SelectedLayerId = layer.Id;
			if (IsLocalImage(artId) && Current.Wallpaper is WallpaperKind.Vanilla or WallpaperKind.Image) {
				Current.Wallpaper = WallpaperKind.Image;
				Current.WallpaperId = artId;
			}

			WeSave.Save();
			return layer;
		}

		internal static WeLayerRecord AddEffectLayer(WeFxKind effect)
		{
			if (Current.Layers.Count >= 6)
				return SelectedLayer();

			var layer = new WeLayerRecord {
				Id = "layer-" + Guid.NewGuid().ToString("N")[..8],
				Kind = WeLayerKind.Effect,
				Effect = effect,
				Parallax = effect is WeFxKind.Stars or WeFxKind.Clouds ? 0.08f : 0.22f,
				Opacity = effect is WeFxKind.Grain or WeFxKind.Scanlines ? 0.22f : 0.7f,
				Zoom = 1f
			};
			Current.Layers.Add(layer);
			Current.SelectedLayerId = layer.Id;
			WeSave.Save();
			return layer;
		}

		internal static void RemoveSelectedLayer() => RemoveLayer(SelectedLayer()?.Id);

		internal static void RemoveLayer(string id)
		{
			if (string.IsNullOrEmpty(id))
				return;
			Current.Layers.RemoveAll(item => item.Id == id);
			if (Current.SelectedLayerId == id)
				Current.SelectedLayerId = Current.Layers.Count > 0 ? Current.Layers[^1].Id : "";
			if (Current.Wallpaper == WallpaperKind.Image) {
				WeLayerRecord next = null;
				foreach (WeLayerRecord layer in Current.Layers) {
					if (layer.Kind == WeLayerKind.Image && !string.IsNullOrEmpty(layer.ArtId)) {
						next = layer;
						break;
					}
				}

				if (next != null)
					Current.WallpaperId = next.ArtId;
				else {
					Current.Wallpaper = WallpaperKind.Vanilla;
					Current.WallpaperId = "";
				}
			}

			WeSave.Save();
		}

		internal static void MoveSelectedLayer(int delta)
		{
			WeLayerRecord layer = SelectedLayer();
			if (layer == null)
				return;
			int index = Current.Layers.FindIndex(item => item.Id == layer.Id);
			int next = Math.Clamp(index + delta, 0, Current.Layers.Count - 1);
			if (index < 0 || next == index)
				return;
			Current.Layers.RemoveAt(index);
			Current.Layers.Insert(next, layer);
			WeSave.Save();
		}

		internal static void CycleSelectedEffect()
		{
			WeLayerRecord layer = SelectedLayer();
			if (layer == null || layer.Kind != WeLayerKind.Effect)
				return;
			layer.Effect = (WeFxKind)(((int)layer.Effect + 1) % 9);
			WeSave.Save();
		}

		internal static void CycleSelectedFit()
		{
			WeLayerRecord layer = SelectedLayer();
			if (layer == null)
				return;
			layer.Fit = (WallpaperFit)(((int)layer.Fit + 1) % 3);
			Current.WallpaperFit = layer.Fit;
			WeSave.Save();
		}

		internal static void ToggleSelectedForeground()
		{
			WeLayerRecord layer = SelectedLayer();
			if (layer == null)
				return;
			layer.Foreground = !layer.Foreground;
			WeSave.Save();
		}

		internal static void AssignSelectedImage(string artId)
		{
			if (string.IsNullOrEmpty(artId))
				return;
			WeLayerRecord layer = SelectedLayer();
			if (layer == null || layer.Kind != WeLayerKind.Image)
				AddImageLayer(artId);
			else {
				layer.ArtId = artId;
				layer.PanX = 0.5f;
				layer.PanY = 0.5f;
				if (Current.Wallpaper is WallpaperKind.Vanilla or WallpaperKind.Image) {
					Current.Wallpaper = WallpaperKind.Image;
					Current.WallpaperId = artId;
				}

				WeSave.Save();
			}
		}

		internal static void SetWallpaperBorrowed(string id)
		{
			if (string.IsNullOrEmpty(id))
				return;

			Current.Wallpaper = WallpaperKind.Borrowed;
			Current.WallpaperId = id;
			WeSave.Save();
		}

		private static void AddOrSelectImageLayer(string id)
		{
			WeLayerRecord existing = Current.Layers.Find(item => item.Kind == WeLayerKind.Image && item.ArtId == id);
			if (existing != null) {
				Current.SelectedLayerId = existing.Id;
				return;
			}

			WeLayerRecord selected = SelectedLayer();
			if (selected is { Kind: WeLayerKind.Image }) {
				selected.ArtId = id ?? "";
				selected.PanX = 0.5f;
				selected.PanY = 0.5f;
				Current.SelectedLayerId = selected.Id;
				return;
			}

			AddImageLayer(id);
		}

		internal static void SetWallpaperRgb(bool second, int r, int g, int b)
		{
			if (second) {
				Current.WallpaperColor2R = Math.Clamp(r, 0, 255);
				Current.WallpaperColor2G = Math.Clamp(g, 0, 255);
				Current.WallpaperColor2B = Math.Clamp(b, 0, 255);
			}
			else {
				Current.WallpaperColorR = Math.Clamp(r, 0, 255);
				Current.WallpaperColorG = Math.Clamp(g, 0, 255);
				Current.WallpaperColorB = Math.Clamp(b, 0, 255);
			}

			WeSave.Save();
		}

		internal static void SaveWallpaperPan(Vector2 pan)
		{
			WeLayerRecord layer = SelectedLayer();
			if (layer != null && layer.Kind == WeLayerKind.Image) {
				layer.PanX = MathHelper.Clamp(pan.X, 0f, 1f);
				layer.PanY = MathHelper.Clamp(pan.Y, 0f, 1f);
			}

			WeArtRecord record = CurrentWallpaper();
			if (record == null) {
				WeSave.Save();
				return;
			}

			record.PanX = MathHelper.Clamp(pan.X, 0f, 1f);
			record.PanY = MathHelper.Clamp(pan.Y, 0f, 1f);
			WeSave.Save();
		}

		internal static void SetLogo(LogoKind kind, string id = "")
		{
			Current.Logo = kind;
			Current.LogoId = kind is LogoKind.Custom or LogoKind.Borrowed or LogoKind.Preset ? id ?? "" : "";
			WeSave.Save();
		}

		internal static void CenterWallpaperPan()
		{
			SaveWallpaperPan(new Vector2(0.5f, 0.5f));
		}

		private static bool IsLocalImage(string artId) =>
			!string.IsNullOrEmpty(artId) && Current.Wallpapers.Exists(item => item.Id == artId);

		internal static void SetMusic(MusicKind kind)
		{
			Current.Music = kind;
			WeSave.Save();
		}

		internal static void SetPlayerWidget(bool enabled)
		{
			Current.PlayerWidget = enabled;
			WeSave.Save();
		}

		internal static void SetClockWidget(bool enabled)
		{
			Current.ClockWidget = enabled;
			WeSave.Save();
		}

		internal static void SetQuoteWidget(bool enabled)
		{
			Current.QuoteWidget = enabled;
			WeSave.Save();
		}

		internal static void SetMoonWidget(bool enabled)
		{
			Current.MoonWidget = enabled;
			WeSave.Save();
		}

		internal static void SetDiscordWidget(bool enabled)
		{
			Current.DiscordWidget = enabled;
			WeSave.Save();
		}

		internal static void SetDiscordStyle(int style)
		{
			Current.DiscordStyle = Math.Clamp(style, 0, 2);
			WeSave.Save();
		}

		internal static void SetWrenchStyle(int style)
		{
			style = Math.Clamp(style, 0, 1);
			if (Current.WrenchStyle == style)
				return;

			Current.WrenchStyle = style;
			WeSave.Save();
			if (style == 0)
				WrenchToolbar.Collapse();
			else
				WrenchDock.Reset();
			WeToast.Show("ToastHubStyle");
		}

		internal static void ToggleCleanChrome()
		{
			Current.CleanChrome = !Current.CleanChrome;
			WeSave.Save();
		}

		internal static void CycleWallpaperFit()
		{
			Current.WallpaperFit = (WallpaperFit)(((int)Current.WallpaperFit + 1) % 3);
			WeSave.Save();
		}

		internal static void SetWallpaperParallax(bool enabled)
		{
			Current.WallpaperParallax = enabled;
			WeSave.Save();
		}

		internal static void ResetVanillaTheme()
		{
			Current.Wallpaper = WallpaperKind.Vanilla;
			Current.WallpaperId = "";
			Current.WallpaperDim = 0f;
			Current.WallpaperVignette = 0f;
			Current.WallpaperParallax = false;
			Current.WallpaperFit = WallpaperFit.Cover;
			Current.Logo = LogoKind.Vanilla;
			Current.LogoId = "";
			Current.Music = MusicKind.Vanilla;
			Current.PlayerWidget = false;
			Current.ClockWidget = false;
			Current.QuoteWidget = false;
			Current.MoonWidget = false;
			Current.DiscordWidget = false;
			Current.DiscordStyle = 0;
			Current.CleanChrome = false;
			Current.WrenchStyle = 0;
			Current.DisableLogoPulse = false;
			Current.MuteWhenUnfocused = false;
			Current.MenuTextCustom = false;
			Current.MenuTextR = 255;
			Current.MenuTextG = 255;
			Current.MenuTextB = 255;
			Current.ButtonStyle = 0;
			Current.FontFile = "";
			Current.FontScaleX = 1f;
			Current.FontScaleY = 1f;
			Current.Layers.Clear();
			Current.SelectedLayerId = "";
			foreach (WeElementRecord element in Current.Elements) {
				element.Customized = false;
				element.Visible = true;
				element.Scale = 1f;
			}

			WeSave.Save();
			WeType.Scan();
		}

		internal static Color CaptionColor => new(Current.CaptionR, Current.CaptionG, Current.CaptionB);
		internal static Color BorderColor => new(Current.BorderR, Current.BorderG, Current.BorderB);
		internal static Color TitleTextColor => new(Current.TitleTextR, Current.TitleTextG, Current.TitleTextB);
		internal static Color MenuTextColor => new(Current.MenuTextR, Current.MenuTextG, Current.MenuTextB);

		internal static void ToggleLogoPulse()
		{
			Current.DisableLogoPulse = !Current.DisableLogoPulse;
			WeSave.Save();
		}

		internal static void ToggleMuteUnfocused()
		{
			Current.MuteWhenUnfocused = !Current.MuteWhenUnfocused;
			WeSave.Save();
		}

		internal static void ToggleMenuTextCustom()
		{
			Current.MenuTextCustom = !Current.MenuTextCustom;
			WeSave.Save();
		}

		internal static void CycleButtonStyle()
		{
			Current.ButtonStyle = (Current.ButtonStyle + 1) % 4;
			WeSave.Save();
		}

		internal static void SetButtonStyle(int style)
		{
			Current.ButtonStyle = Math.Clamp(style, 0, 3);
			WeSave.Save();
		}

		internal static void SetFontScale(bool width, float value)
		{
			value = Math.Clamp(value, 0.5f, 1.8f);
			if (width)
				Current.FontScaleX = value;
			else
				Current.FontScaleY = value;
			WeSave.Save();
		}

		internal static void SetMenuTextRgb(int r, int g, int b)
		{
			Current.MenuTextR = Math.Clamp(r, 0, 255);
			Current.MenuTextG = Math.Clamp(g, 0, 255);
			Current.MenuTextB = Math.Clamp(b, 0, 255);
			Current.MenuTextCustom = true;
			WeSave.Save();
		}

		internal static void SetChromeRgb(string which, int r, int g, int b)
		{
			r = Math.Clamp(r, 0, 255);
			g = Math.Clamp(g, 0, 255);
			b = Math.Clamp(b, 0, 255);
			switch (which) {
				case "caption":
					Current.CaptionR = r;
					Current.CaptionG = g;
					Current.CaptionB = b;
					break;
				case "border":
					Current.BorderR = r;
					Current.BorderG = g;
					Current.BorderB = b;
					break;
				default:
					Current.TitleTextR = r;
					Current.TitleTextG = g;
					Current.TitleTextB = b;
					break;
			}

			Current.ChromeCustom = true;
			WeSave.Save();
			ClientChrome.Apply();
		}
	}
}
