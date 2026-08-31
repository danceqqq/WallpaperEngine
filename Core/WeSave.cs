using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Terraria;

namespace WallpaperEngine.Core
{
	internal enum WallpaperKind
	{
		Vanilla = 0,
		Color = 1,
		Gradient = 2,
		Image = 3,
		Borrowed = 4
	}

	internal enum LogoKind
	{
		Vanilla = 0,
		Hidden = 1,
		Custom = 2,
		Borrowed = 3
	}

	internal enum MusicKind
	{
		Vanilla = 0,
		Silence = 1,
		Custom = 2
	}

	internal enum WallpaperFit
	{
		Cover = 0,
		Contain = 1,
		Stretch = 2
	}

	internal enum WeLayerKind
	{
		Image = 0,
		Effect = 1
	}

	internal enum WeFxKind
	{
		Stars = 0,
		Dust = 1,
		Fog = 2,
		Grain = 3,
		Scanlines = 4,
		Fireflies = 5,
		Clouds = 6,
		Rain = 7,
		Beat = 8
	}

	internal sealed class WeLayerRecord
	{
		public string Id { get; set; } = "";
		public WeLayerKind Kind { get; set; }
		public string ArtId { get; set; } = "";
		public WeFxKind Effect { get; set; }
		public float Parallax { get; set; } = 0.18f;
		public float Opacity { get; set; } = 1f;
		public float Zoom { get; set; } = 1f;
		public bool Foreground { get; set; }
		public bool Visible { get; set; } = true;
		public WallpaperFit Fit { get; set; }
		public float PanX { get; set; } = 0.5f;
		public float PanY { get; set; } = 0.5f;
	}

	internal sealed class WeArtRecord
	{
		public string Id { get; set; } = "";
		public string FileName { get; set; } = "";
		public float PanX { get; set; } = 0.5f;
		public float PanY { get; set; } = 0.5f;
	}

	internal sealed class WeTrackRecord
	{
		public string Id { get; set; } = "";
		public string FileName { get; set; } = "";
		public string Title { get; set; } = "";
		public string Artist { get; set; } = "Custom";
	}

	internal sealed class WeElementRecord
	{
		public string Id { get; set; } = "";
		public bool Visible { get; set; } = true;
		public bool Customized { get; set; }
		public float AnchorX { get; set; } = 0.5f;
		public float AnchorY { get; set; } = 0.5f;
		public float Scale { get; set; } = 1f;
	}

	internal sealed class WeSaveData
	{
		public bool SplashDismissed { get; set; }
		public bool WrenchOpened { get; set; }
		public bool KeepMenuSelected { get; set; }
		public int AccentIndex { get; set; }
		public WallpaperKind Wallpaper { get; set; }
		public string WallpaperId { get; set; } = "";
		public int WallpaperColorR { get; set; } = 18;
		public int WallpaperColorG { get; set; } = 22;
		public int WallpaperColorB { get; set; } = 38;
		public int WallpaperColor2R { get; set; } = 42;
		public int WallpaperColor2G { get; set; } = 18;
		public int WallpaperColor2B { get; set; } = 52;
		public float WallpaperDim { get; set; }
		public float WallpaperVignette { get; set; }
		public bool WallpaperParallax { get; set; }
		public WallpaperFit WallpaperFit { get; set; }
		public List<WeLayerRecord> Layers { get; set; } = new();
		public string SelectedLayerId { get; set; } = "";
		public LogoKind Logo { get; set; }
		public string LogoId { get; set; } = "";
		public MusicKind Music { get; set; }
		public bool LoopEnabled { get; set; }
		public bool ShuffleEnabled { get; set; }
		public string LoopedTrackId { get; set; } = "";
		public List<string> DisabledTrackIds { get; set; } = new();
		public List<WeTrackRecord> Tracks { get; set; } = new();
		public List<WeArtRecord> Wallpapers { get; set; } = new();
		public List<WeArtRecord> Logos { get; set; } = new();
		public List<WeElementRecord> Elements { get; set; } = new();
		public bool PlayerWidget { get; set; }
		public bool ClockWidget { get; set; }
		public bool QuoteWidget { get; set; }
		public bool MoonWidget { get; set; }
		public bool CleanChrome { get; set; }
		public bool Clock24h { get; set; } = true;
		public bool ClockAnalog { get; set; }
		public bool ClockDate { get; set; } = true;
		public bool ChromeCustom { get; set; }
		public int CaptionR { get; set; } = 32;
		public int CaptionG { get; set; } = 32;
		public int CaptionB { get; set; } = 32;
		public int BorderR { get; set; } = 32;
		public int BorderG { get; set; } = 32;
		public int BorderB { get; set; } = 32;
		public int TitleTextR { get; set; } = 240;
		public int TitleTextG { get; set; } = 240;
		public int TitleTextB { get; set; } = 240;
		public bool DarkTitleBar { get; set; } = true;
		public string WindowIconFile { get; set; } = "";
	}

	internal static class WeSave
	{
		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			WriteIndented = true,
			PropertyNameCaseInsensitive = true
		};

		private static WeSaveData _data = new();
		private static bool _loaded;

		internal static WeSaveData Data
		{
			get
			{
				EnsureLoaded();
				return _data;
			}
		}

		internal static string RootFolder => Path.Combine(Main.SavePath, "WallpaperEngine");
		internal static string MusicFolder => Path.Combine(RootFolder, "Music");
		internal static string BrokenFolder => Path.Combine(RootFolder, "Broken");
		internal static string LogoFolder => Path.Combine(RootFolder, "Logos");
		internal static string WallpaperFolder => Path.Combine(RootFolder, "Wallpapers");
		internal static string IconFolder => Path.Combine(RootFolder, "Icons");
		internal static string QuotePath => Path.Combine(RootFolder, "quote.txt");
		internal static string SettingsPath => Path.Combine(RootFolder, "settings.json");
		internal static string PlayGuardPath => Path.Combine(RootFolder, "playing.lock");

		internal static void EnsureFolders()
		{
			Directory.CreateDirectory(MusicFolder);
			Directory.CreateDirectory(BrokenFolder);
			Directory.CreateDirectory(LogoFolder);
			Directory.CreateDirectory(WallpaperFolder);
			Directory.CreateDirectory(IconFolder);
		}

		internal static void EnsureLoaded()
		{
			if (_loaded)
				return;

			_loaded = true;
			EnsureFolders();
			Load();
		}

		internal static void Load()
		{
			try {
				if (File.Exists(SettingsPath)) {
					WeSaveData parsed = JsonSerializer.Deserialize<WeSaveData>(File.ReadAllText(SettingsPath), JsonOptions);
					if (parsed != null)
						_data = parsed;
				}
			}
			catch {
			}

			Normalize();
		}

		internal static void Normalize()
		{
			_data.DisabledTrackIds ??= new List<string>();
			_data.Tracks ??= new List<WeTrackRecord>();
			_data.Wallpapers ??= new List<WeArtRecord>();
			_data.Logos ??= new List<WeArtRecord>();
			_data.Elements ??= new List<WeElementRecord>();
			_data.Layers ??= new List<WeLayerRecord>();
			_data.WallpaperId ??= "";
			_data.LogoId ??= "";
			_data.LoopedTrackId ??= "";
			_data.WindowIconFile ??= "";
			_data.SelectedLayerId ??= "";
			_data.WallpaperDim = Math.Clamp(_data.WallpaperDim, 0f, 1f);
			_data.WallpaperVignette = Math.Clamp(_data.WallpaperVignette, 0f, 1f);
			if ((int)_data.WallpaperFit < 0 || (int)_data.WallpaperFit > 2)
				_data.WallpaperFit = WallpaperFit.Cover;
			if ((int)_data.Wallpaper < 0 || (int)_data.Wallpaper > 4)
				_data.Wallpaper = WallpaperKind.Vanilla;
			if ((int)_data.Logo < 0 || (int)_data.Logo > 3)
				_data.Logo = LogoKind.Vanilla;
			if (_data.Logo == LogoKind.Borrowed && string.IsNullOrEmpty(_data.LogoId))
				_data.Logo = LogoKind.Vanilla;
			if (_data.Wallpaper == WallpaperKind.Borrowed && string.IsNullOrEmpty(_data.WallpaperId))
				_data.Wallpaper = WallpaperKind.Vanilla;
			if (_data.AccentIndex < 0 || _data.AccentIndex >= WeAccent.Palettes.Length)
				_data.AccentIndex = 0;

			foreach (WeArtRecord wall in _data.Wallpapers) {
				if (wall == null)
					continue;
				wall.PanX = Math.Clamp(wall.PanX, 0f, 1f);
				wall.PanY = Math.Clamp(wall.PanY, 0f, 1f);
			}

			foreach (WeLayerRecord layer in _data.Layers) {
				if (layer == null)
					continue;
				layer.Id ??= "";
				layer.ArtId ??= "";
				layer.Opacity = Math.Clamp(layer.Opacity, 0f, 1f);
				layer.Parallax = Math.Clamp(layer.Parallax, 0f, 1f);
				layer.Zoom = Math.Clamp(layer.Zoom <= 0.01f ? 1f : layer.Zoom, 0.6f, 1.8f);
				layer.PanX = Math.Clamp(layer.PanX, 0f, 1f);
				layer.PanY = Math.Clamp(layer.PanY, 0f, 1f);
				if ((int)layer.Effect < 0 || (int)layer.Effect > 8)
					layer.Effect = WeFxKind.Stars;
			}

			_data.Layers.RemoveAll(item => item == null || string.IsNullOrEmpty(item.Id));
			if (_data.Layers.Count == 0 && _data.Wallpaper == WallpaperKind.Image && !string.IsNullOrEmpty(_data.WallpaperId)) {
				_data.Layers.Add(new WeLayerRecord {
					Id = "layer-base",
					Kind = WeLayerKind.Image,
					ArtId = _data.WallpaperId,
					Parallax = _data.WallpaperParallax ? 0.22f : 0f,
					Fit = _data.WallpaperFit,
					Opacity = 1f,
					Zoom = 1f
				});
			}

			if (!string.IsNullOrEmpty(_data.SelectedLayerId) && _data.Layers.TrueForAll(item => item.Id != _data.SelectedLayerId))
				_data.SelectedLayerId = _data.Layers.Count > 0 ? _data.Layers[0].Id : "";

			Layout.SceneGraph.EnsureRecords(_data);
		}

		internal static void Save()
		{
			try {
				EnsureFolders();
				File.WriteAllText(SettingsPath, JsonSerializer.Serialize(_data, JsonOptions));
			}
			catch {
			}
		}

		internal static void WritePlayGuard(string fileName)
		{
			try {
				EnsureFolders();
				File.WriteAllText(PlayGuardPath, fileName ?? "");
			}
			catch {
			}
		}

		internal static string ReadPlayGuard()
		{
			try {
				return File.Exists(PlayGuardPath) ? File.ReadAllText(PlayGuardPath).Trim() : "";
			}
			catch {
				return "";
			}
		}

		internal static void ClearPlayGuard()
		{
			try {
				if (File.Exists(PlayGuardPath))
					File.Delete(PlayGuardPath);
			}
			catch {
			}
		}
	}
}
