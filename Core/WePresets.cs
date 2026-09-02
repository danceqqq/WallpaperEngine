using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Terraria;
using WallpaperEngine.Audio;
using WallpaperEngine.Chrome;
using WallpaperEngine.Grab;

namespace WallpaperEngine.Core
{
	internal sealed class WeLookFile
	{
		public string Name { get; set; } = "Look";
		public WeLookData Data { get; set; } = new();

		[JsonIgnore]
		internal string Path { get; set; } = "";
	}

	internal sealed class WeLookData
	{
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
		public List<WeElementRecord> Elements { get; set; } = new();
		public bool PlayerWidget { get; set; }
		public bool ClockWidget { get; set; }
		public bool QuoteWidget { get; set; }
		public bool MoonWidget { get; set; }
		public bool DiscordWidget { get; set; }
		public int DiscordStyle { get; set; }
		public bool CleanChrome { get; set; }
		public bool Clock24h { get; set; } = true;
		public bool ClockAnalog { get; set; }
		public bool ClockDate { get; set; } = true;
		public int WrenchStyle { get; set; }
		public bool DisableLogoPulse { get; set; }
		public bool MuteWhenUnfocused { get; set; }
		public bool MenuTextCustom { get; set; }
		public int MenuTextR { get; set; } = 255;
		public int MenuTextG { get; set; } = 255;
		public int MenuTextB { get; set; } = 255;
		public int ButtonStyle { get; set; }
		public string FontFile { get; set; } = "";
		public float FontScaleX { get; set; } = 1f;
		public float FontScaleY { get; set; } = 1f;
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
	}

	internal static class WePresets
	{
		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			WriteIndented = true,
			PropertyNameCaseInsensitive = true
		};

		private static readonly List<WeLookFile> Cached = new();

		internal static IReadOnlyList<WeLookFile> All => Cached;

		internal static WeLookFile[] Copy()
		{
			var copy = new WeLookFile[Cached.Count];
			Cached.CopyTo(copy);
			return copy;
		}

		internal static void Refresh()
		{
			Cached.Clear();
			try {
				WeSave.EnsureFolders();
				if (!Directory.Exists(WeSave.PresetFolder))
					return;

				foreach (string path in Directory.GetFiles(WeSave.PresetFolder, "*.json")) {
					try {
						WeLookFile file = JsonSerializer.Deserialize<WeLookFile>(File.ReadAllText(path), JsonOptions);
						if (file?.Data == null)
							continue;
						if (string.IsNullOrWhiteSpace(file.Name))
							file.Name = Path.GetFileNameWithoutExtension(path);
						file.Name = SafeName(file.Name);
						file.Path = path;
						Cached.Add(file);
					}
					catch {
					}
				}
			}
			catch {
			}

			Cached.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
		}

		internal static void SaveCurrent()
		{
			WeSave.EnsureFolders();
			string name = NextName();
			var file = new WeLookFile { Name = name, Data = Capture() };
			string path = WeFiles.UniquePath(WeSave.PresetFolder, name + ".json");
			File.WriteAllText(path, JsonSerializer.Serialize(file, JsonOptions));
			Refresh();
			WeToast.Show("ToastLookSaved");
		}

		internal static void Load(WeLookFile file)
		{
			if (file?.Data == null)
				return;

			int prevHub = WeSave.Data.WrenchStyle;
			Apply(file.Data);
			WeSave.Save();
			WeArt.Scan();
			WeCatalog.DropMissing();
			int hub = Math.Clamp(WeSave.Data.WrenchStyle, 0, 1);
			if (hub != prevHub) {
				if (hub == 0)
					WrenchToolbar.Collapse();
				else
					WrenchDock.Reset();
			}

			if (WeSave.Data.ChromeCustom)
				ClientChrome.Apply();

			WePlaylist.Silence();
			if (WeSave.Data.Music == MusicKind.Custom)
				WePlaylist.OnThemeSelected();

			WeType.Scan();
			WeToast.Show("ToastLookLoaded");
		}

		internal static void Delete(WeLookFile file)
		{
			if (file == null)
				return;
			try {
				string path = file.Path;
				if (string.IsNullOrEmpty(path) || !File.Exists(path))
					path = Path.Combine(WeSave.PresetFolder, file.Name + ".json");
				if (File.Exists(path))
					File.Delete(path);
				else {
					foreach (string found in Directory.GetFiles(WeSave.PresetFolder, "*.json")) {
						try {
							WeLookFile other = JsonSerializer.Deserialize<WeLookFile>(File.ReadAllText(found), JsonOptions);
							if (other != null && string.Equals(other.Name, file.Name, StringComparison.OrdinalIgnoreCase))
								File.Delete(found);
						}
						catch {
						}
					}
				}
			}
			catch {
			}

			Refresh();
			WeToast.Show("ToastLookGone");
		}

		private static string NextName()
		{
			var used = new HashSet<string>(Cached.Select(item => item.Name), StringComparer.OrdinalIgnoreCase);
			if (used.Add("Look"))
				return "Look";
			for (int i = 2; i < 100; i++) {
				string name = "Look " + i;
				if (used.Add(name))
					return name;
			}

			return "Look " + DateTime.Now.ToString("HHmmss");
		}

		internal static string SafeName(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
				return "Look";
			var chars = name.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_').ToArray();
			string clean = new string(chars).Trim();
			if (clean.Length == 0)
				return "Look";
			return clean.Length > 40 ? clean[..40] : clean;
		}

		private static WeLookData Capture()
		{
			WeSaveData cur = WeSave.Data;
			return new WeLookData {
				AccentIndex = cur.AccentIndex,
				Wallpaper = cur.Wallpaper,
				WallpaperId = cur.WallpaperId ?? "",
				WallpaperColorR = cur.WallpaperColorR,
				WallpaperColorG = cur.WallpaperColorG,
				WallpaperColorB = cur.WallpaperColorB,
				WallpaperColor2R = cur.WallpaperColor2R,
				WallpaperColor2G = cur.WallpaperColor2G,
				WallpaperColor2B = cur.WallpaperColor2B,
				WallpaperDim = cur.WallpaperDim,
				WallpaperVignette = cur.WallpaperVignette,
				WallpaperParallax = cur.WallpaperParallax,
				WallpaperFit = cur.WallpaperFit,
				Layers = CloneLayers(cur.Layers),
				SelectedLayerId = cur.SelectedLayerId ?? "",
				Logo = cur.Logo,
				LogoId = cur.LogoId ?? "",
				Music = cur.Music,
				LoopEnabled = cur.LoopEnabled,
				ShuffleEnabled = cur.ShuffleEnabled,
				LoopedTrackId = cur.LoopedTrackId ?? "",
				Elements = CloneElements(cur.Elements),
				PlayerWidget = cur.PlayerWidget,
				ClockWidget = cur.ClockWidget,
				QuoteWidget = cur.QuoteWidget,
				MoonWidget = cur.MoonWidget,
				DiscordWidget = cur.DiscordWidget,
				DiscordStyle = cur.DiscordStyle,
				CleanChrome = cur.CleanChrome,
				Clock24h = cur.Clock24h,
				ClockAnalog = cur.ClockAnalog,
				ClockDate = cur.ClockDate,
				WrenchStyle = cur.WrenchStyle,
				DisableLogoPulse = cur.DisableLogoPulse,
				MuteWhenUnfocused = cur.MuteWhenUnfocused,
				MenuTextCustom = cur.MenuTextCustom,
				MenuTextR = cur.MenuTextR,
				MenuTextG = cur.MenuTextG,
				MenuTextB = cur.MenuTextB,
				ButtonStyle = cur.ButtonStyle,
				FontFile = cur.FontFile ?? "",
				FontScaleX = cur.FontScaleX,
				FontScaleY = cur.FontScaleY,
				ChromeCustom = cur.ChromeCustom,
				CaptionR = cur.CaptionR,
				CaptionG = cur.CaptionG,
				CaptionB = cur.CaptionB,
				BorderR = cur.BorderR,
				BorderG = cur.BorderG,
				BorderB = cur.BorderB,
				TitleTextR = cur.TitleTextR,
				TitleTextG = cur.TitleTextG,
				TitleTextB = cur.TitleTextB,
				DarkTitleBar = cur.DarkTitleBar
			};
		}

		private static void Apply(WeLookData data)
		{
			WeSaveData cur = WeSave.Data;
			cur.AccentIndex = data.AccentIndex;
			cur.Wallpaper = data.Wallpaper;
			cur.WallpaperId = data.WallpaperId ?? "";
			cur.WallpaperColorR = data.WallpaperColorR;
			cur.WallpaperColorG = data.WallpaperColorG;
			cur.WallpaperColorB = data.WallpaperColorB;
			cur.WallpaperColor2R = data.WallpaperColor2R;
			cur.WallpaperColor2G = data.WallpaperColor2G;
			cur.WallpaperColor2B = data.WallpaperColor2B;
			cur.WallpaperDim = data.WallpaperDim;
			cur.WallpaperVignette = data.WallpaperVignette;
			cur.WallpaperParallax = data.WallpaperParallax;
			cur.WallpaperFit = data.WallpaperFit;
			cur.Layers = CloneLayers(data.Layers);
			cur.SelectedLayerId = data.SelectedLayerId ?? "";
			cur.Logo = data.Logo;
			cur.LogoId = data.LogoId ?? "";
			cur.Music = data.Music;
			cur.LoopEnabled = data.LoopEnabled;
			cur.ShuffleEnabled = data.ShuffleEnabled;
			cur.LoopedTrackId = data.LoopedTrackId ?? "";
			MergeElements(data.Elements);
			cur.PlayerWidget = data.PlayerWidget;
			cur.ClockWidget = data.ClockWidget;
			cur.QuoteWidget = data.QuoteWidget;
			cur.MoonWidget = data.MoonWidget;
			cur.DiscordWidget = data.DiscordWidget;
			cur.DiscordStyle = data.DiscordStyle;
			cur.CleanChrome = data.CleanChrome;
			cur.Clock24h = data.Clock24h;
			cur.ClockAnalog = data.ClockAnalog;
			cur.ClockDate = data.ClockDate;
			cur.WrenchStyle = data.WrenchStyle;
			cur.DisableLogoPulse = data.DisableLogoPulse;
			cur.MuteWhenUnfocused = data.MuteWhenUnfocused;
			cur.MenuTextCustom = data.MenuTextCustom;
			cur.MenuTextR = data.MenuTextR;
			cur.MenuTextG = data.MenuTextG;
			cur.MenuTextB = data.MenuTextB;
			cur.ButtonStyle = data.ButtonStyle;
			cur.FontFile = data.FontFile ?? "";
			cur.FontScaleX = data.FontScaleX;
			cur.FontScaleY = data.FontScaleY;
			cur.ChromeCustom = data.ChromeCustom;
			cur.CaptionR = data.CaptionR;
			cur.CaptionG = data.CaptionG;
			cur.CaptionB = data.CaptionB;
			cur.BorderR = data.BorderR;
			cur.BorderG = data.BorderG;
			cur.BorderB = data.BorderB;
			cur.TitleTextR = data.TitleTextR;
			cur.TitleTextG = data.TitleTextG;
			cur.TitleTextB = data.TitleTextB;
			cur.DarkTitleBar = data.DarkTitleBar;
			WeSave.Normalize();
		}

		private static void MergeElements(List<WeElementRecord> incoming)
		{
			if (incoming == null)
				return;
			foreach (WeElementRecord src in incoming) {
				if (src == null || string.IsNullOrEmpty(src.Id))
					continue;
				WeElementRecord live = WeSave.Data.Elements.Find(item => item.Id == src.Id);
				if (live == null) {
					WeSave.Data.Elements.Add(CloneElement(src));
					continue;
				}

				live.Visible = src.Visible;
				live.Customized = src.Customized;
				live.AnchorX = src.AnchorX;
				live.AnchorY = src.AnchorY;
				live.Scale = src.Scale;
			}
		}

		private static List<WeLayerRecord> CloneLayers(List<WeLayerRecord> source)
		{
			var list = new List<WeLayerRecord>();
			if (source == null)
				return list;
			foreach (WeLayerRecord item in source) {
				if (item == null)
					continue;
				list.Add(new WeLayerRecord {
					Id = item.Id ?? "",
					Kind = item.Kind,
					ArtId = item.ArtId ?? "",
					Effect = item.Effect,
					Parallax = item.Parallax,
					Opacity = item.Opacity,
					Zoom = item.Zoom,
					Foreground = item.Foreground,
					Visible = item.Visible,
					Fit = item.Fit,
					PanX = item.PanX,
					PanY = item.PanY
				});
			}

			return list;
		}

		private static List<WeElementRecord> CloneElements(List<WeElementRecord> source)
		{
			var list = new List<WeElementRecord>();
			if (source == null)
				return list;
			foreach (WeElementRecord item in source) {
				if (item == null)
					continue;
				list.Add(CloneElement(item));
			}

			return list;
		}

		private static WeElementRecord CloneElement(WeElementRecord item) => new() {
			Id = item.Id ?? "",
			Visible = item.Visible,
			Customized = item.Customized,
			AnchorX = item.AnchorX,
			AnchorY = item.AnchorY,
			Scale = item.Scale
		};
	}
}
