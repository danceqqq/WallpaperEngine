using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace WallpaperEngine.Grab
{
	internal static class WeModArt
	{
		private static readonly Dictionary<string, Asset<Texture2D>> Assets = new(StringComparer.Ordinal);
		private static readonly Dictionary<string, string[]> FilesByMod = new(StringComparer.Ordinal);
		private static readonly HashSet<string> Missing = new(StringComparer.Ordinal);

		internal static void Unload()
		{
			Assets.Clear();
			FilesByMod.Clear();
			Missing.Clear();
		}

		internal static bool SkipMenu(ModMenu menu)
		{
			string type = TypeName(menu);
			if (type is "XNamelessDeityDimensionMainMenu")
				return true;
			return NameHas(SafeTitle(menu), "Paradise's Shining", "Paradise Shining");
		}

		internal static bool HasHostSky(ModMenu menu) =>
			TypeName(menu) is "AvatarRiftSkyMainMenu";

		internal static void PrimeLiveSky(ModMenu menu)
		{
			if (menu == null || TypeName(menu) is not "XAscentMainNenu")
				return;

			try {
				if (!ModLoader.TryGetMod("NoxusBoss", out Mod wotg) || wotg.Code == null)
					return;

				Type sky = wotg.Code.GetType("NoxusBoss.Core.World.GameScenes.TerminusStairway.TerminusStairwaySky");
				FieldInfo intensity = sky?.GetField("intensity", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				intensity?.SetValue(null, 1f);
			}
			catch {
			}
		}

		internal static void TickHostSky(ModMenu menu)
		{
			if (menu == null || !HasHostSky(menu))
				return;

			try {
				if (RiftSkyInstance() is not Terraria.Graphics.Effects.CustomSky sky)
					return;
				sky.Activate(Vector2.Zero);
				sky.Update(new GameTime());
			}
			catch {
			}
		}

		internal static bool TryDrawHostSky(SpriteBatch spriteBatch, ModMenu menu)
		{
			if (menu == null || !HasHostSky(menu))
				return false;

			try {
				object target = RiftSkyTarget();
				MethodInfo render = target?.GetType().GetMethod("Render", new[] { typeof(Color) });
				if (render == null)
					return false;

				render.Invoke(target, new object[] { Color.White });
				return true;
			}
			catch {
				return false;
			}
		}

		private static object RiftSkyInstance()
		{
			try {
				var sky = Terraria.Graphics.Effects.SkyManager.Instance["NoxusBoss:AvatarRiftSky"];
				if (sky != null)
					return sky;
			}
			catch {
			}

			return null;
		}

		private static object RiftSkyTarget()
		{
			try {
				if (!ModLoader.TryGetMod("NoxusBoss", out Mod wotg) || wotg.Code == null)
					return null;

				Type skyType = wotg.Code.GetType("NoxusBoss.Content.NPCs.Bosses.Avatar.SpecificEffectManagers.AvatarRiftSky");
				PropertyInfo prop = skyType?.GetProperty("SkyTarget", BindingFlags.Public | BindingFlags.Static);
				return prop?.GetValue(null);
			}
			catch {
				return null;
			}
		}

		internal static Texture2D FindSky(ModMenu menu)
		{
			if (menu == null)
				return null;

			foreach (string path in SkyPaths(menu)) {
				Texture2D tex = Request(SafeMod(menu), path);
				if (WeInspect.IsCoverSized(tex))
					return tex;
			}

			return FirstPacked(menu, sky: true, preview: false);
		}

		internal static Texture2D FindPreview(ModMenu menu)
		{
			if (menu == null)
				return null;

			Texture2D cover = FindSky(menu);
			if (cover != null)
				return cover;

			foreach (string path in SkyPaths(menu)) {
				Texture2D tex = Request(SafeMod(menu), path);
				if (tex != null)
					return tex;
			}

			return FirstPacked(menu, sky: true, preview: true);
		}

		internal static Texture2D FindLogo(ModMenu menu)
		{
			if (menu == null)
				return null;

			foreach (string path in LogoPaths(menu)) {
				Texture2D tex = Request(SafeMod(menu), path);
				if (IsUsableLogo(tex, path))
					return tex;
			}

			return FirstPacked(menu, sky: false, preview: false);
		}

		internal static bool SkyPending(ModMenu menu)
		{
			if (menu == null)
				return false;
			foreach (string path in SkyPaths(menu)) {
				if (Pending(SafeMod(menu), path))
					return true;
			}

			return PackedPending(menu, sky: true);
		}

		internal static bool LogoPending(ModMenu menu)
		{
			if (menu == null)
				return false;
			foreach (string path in LogoPaths(menu)) {
				if (Pending(SafeMod(menu), path))
					return true;
			}

			return PackedPending(menu, sky: false);
		}

		private static IEnumerable<string> SkyPaths(ModMenu menu)
		{
			string type = TypeName(menu);
			string display = SafeTitle(menu);
			foreach (string path in KnownSky(type, display))
				yield return path;

			if (IsCatalyst(menu)) {
				yield return "Assets/Backgrounds/MainMenu/AstrageldonBackground";
				yield return "Assets/Backgrounds/Astrageldon/Background";
				yield return "Assets/Backgrounds/Astrageldon/Sky";
			}

			if (!string.IsNullOrEmpty(type)) {
				yield return "MainMenu/" + type;
				yield return "MainMenu/" + type + "Background";
				yield return "Assets/Textures/MainMenuThemes/" + type;
			}
		}

		private static IEnumerable<string> LogoPaths(ModMenu menu)
		{
			string type = TypeName(menu);
			if (type is "CalamityMainMenu" or "CalamityMainMenu_Classic")
				yield return "MainMenu/Logo";

			if (IsCatalyst(menu)) {
				yield return "Assets/Backgrounds/MainMenu/CatstrageldonLogo";
				yield return "Assets/Backgrounds/MainMenu/AstrageldonLogo";
			}

			string stem = Token(type);
			if (!string.IsNullOrEmpty(stem) && !IsCatalyst(menu)) {
				yield return "Assets/Textures/UI/" + stem + "Logo";
				yield return "Assets/Textures/Menu/" + stem + "Logo";
				yield return "MainMenu/" + stem + "Logo";
			}
		}

		private static IEnumerable<string> KnownSky(string type, string display)
		{
			switch (type) {
				case "XNamelessDeityDimensionMainMenu":
					yield return "Assets/Textures/Skies/NamelessDeity/NamelessDeitySky";
					yield return "Assets/Textures/Skies/NamelessDeity/TheOriginalLight/Background";
					yield return "Assets/Textures/Skies/NamelessDeity/BackgroundPattern";
					yield break;
				case "AvatarRiftSkyMainMenu":
					yield return "Assets/Textures/UI/GraphicalUniverseImager/ShaderSource_Rift";
					yield break;
				case "AvatarWindMainMenu":
					yield return "Assets/Textures/UI/GraphicalUniverseImager/ShaderSource_Avatar";
					yield break;
				case "XAscentMainNenu":
					yield return "Assets/Textures/Map/AvatarUniverseExplorationMapBackground";
					yield return "NoxusBoss/Assets/Textures/Map/AvatarUniverseExplorationMapBackground";
					yield break;
				case "CalamityMainMenu":
					yield return "MainMenu/ModernMenuBackground";
					yield break;
				case "CalamityMainMenu_Classic":
					yield return "MainMenu/ClassicMenuBackground";
					yield break;
			}

			if (NameHas(display, "Paradise", "Nameless")) {
				yield return "Assets/Textures/Skies/NamelessDeity/NamelessDeitySky";
				yield return "Assets/Textures/Skies/NamelessDeity/TheOriginalLight/Background";
			}
			else if (NameHas(display, "Carmine", "Insouciant", "Rift"))
				yield return "Assets/Textures/UI/GraphicalUniverseImager/ShaderSource_Rift";
			else if (NameHas(display, "Turbulent", "Expanse"))
				yield return "Assets/Textures/UI/GraphicalUniverseImager/ShaderSource_Avatar";
			else if (NameHas(display, "Ascent", "Terminus", "Stair"))
				yield return "Assets/Textures/Map/AvatarUniverseExplorationMapBackground";
			else if (NameHas(display, "Classic") && NameHas(type, "Calamity"))
				yield return "MainMenu/ClassicMenuBackground";
			else if (NameHas(type, "Calamity") && NameHas(display, "Calamity", "Style"))
				yield return "MainMenu/ModernMenuBackground";
		}

		private static Texture2D FirstPacked(ModMenu menu, bool sky, bool preview)
		{
			Mod mod = SafeMod(menu);
			if (mod == null)
				return null;

			string token = Token(TypeName(menu));
			string display = SafeTitle(menu);
			string type = TypeName(menu);
			Texture2D best = null;
			int bestScore = 0;
			foreach (string file in FilesOf(mod)) {
				string asset = ToAssetPath(file);
				if (sky) {
					if (!IsSkyFile(asset))
						continue;
				}
				else if (!IsLogoFile(asset)) {
					continue;
				}

				if (!FileFits(asset, token, display, type) && !(IsCatalyst(menu) && Contains(asset, "MainMenu")))
					continue;

				int score = PackedScore(asset, sky, token);
				if (Contains(asset, "CatstrageldonLogo"))
					score += 24;
				if (Contains(asset, "AstrageldonLogo") && !sky)
					score += 12;
				if (Contains(asset, "AstrageldonBackground"))
					score += 16;
				if (score <= bestScore)
					continue;

				Texture2D tex = Request(mod, asset);
				if (tex == null)
					continue;
				if (sky && !preview && !WeInspect.IsCoverSized(tex))
					continue;
				if (!sky && !IsUsableLogo(tex, asset))
					continue;
				best = tex;
				bestScore = score;
			}

			return best;
		}

		private static bool IsUsableLogo(Texture2D tex, string path)
		{
			if (tex == null || tex.IsDisposed)
				return false;
			if (WeInspect.IsIcon(tex) || WeInspect.IsFillPixel(tex))
				return false;
			if (tex.Width < 80 || tex.Height < 32)
				return false;
			return WeInspect.LooksLikeLogoName(path) || WeInspect.IsWordmark(tex) || WeInspect.IsLogo(tex, path);
		}

		private static int PackedScore(string path, bool sky, string token)
		{
			int score = 1;
			if (!string.IsNullOrEmpty(token) && Contains(path, token))
				score += 8;
			if (sky) {
				if (WeInspect.LooksLikeStillName(path))
					score += 6;
				if (WeInspect.LooksLikeSceneName(path))
					score += 4;
				if (Contains(path, "MainMenu"))
					score += 5;
			}
			else {
				if (Contains(path, "MainMenu"))
					score += 6;
				if (Contains(path, "/Menu"))
					score += 4;
				if (Contains(path, "Astrageldon") || Contains(path, "Catalyst"))
					score += 8;
			}

			return score;
		}

		private static bool PackedPending(ModMenu menu, bool sky)
		{
			Mod mod = SafeMod(menu);
			if (mod == null)
				return false;

			string token = Token(TypeName(menu));
			string display = SafeTitle(menu);
			foreach (string file in FilesOf(mod)) {
				string asset = ToAssetPath(file);
				if (sky) {
					if (!IsSkyFile(asset))
						continue;
				}
				else if (!IsLogoFile(asset)) {
					continue;
				}

				if (!FileFits(asset, token, display, TypeName(menu)) && !(IsCatalyst(menu) && Contains(asset, "MainMenu")))
					continue;
				if (Pending(mod, asset))
					return true;
			}

			return false;
		}

		private static bool IsSkyFile(string path)
		{
			if (WeInspect.LooksLikeJunkName(path) || WeInspect.LooksLikeLogoName(path))
				return false;
			return WeInspect.LooksLikeSceneName(path) || WeInspect.LooksLikeStillName(path);
		}

		private static bool IsLogoFile(string path)
		{
			if (!WeInspect.LooksLikeLogoName(path) || WeInspect.LooksLikeSceneName(path) || WeInspect.LooksLikeJunkName(path))
				return false;
			if (Contains(path, "/Items/") || Contains(path, "/NPCs/") || Contains(path, "/Projectiles/") ||
			    Contains(path, "/Buffs/") || Contains(path, "/Tiles/") || Contains(path, "/Gore"))
				return false;
			return Contains(path, "Menu") || Contains(path, "/UI/") || Contains(path, "Title") || Contains(path, "MainMenu");
		}

		private static bool FileFits(string path, string token, string display, string typeName)
		{
			if (!string.IsNullOrEmpty(token) && token.Length >= 5 && Contains(path, token))
				return true;
			if (!string.IsNullOrEmpty(typeName) && Contains(path, typeName))
				return true;
			if (Contains(typeName, "Classic") && Contains(path, "Classic"))
				return true;
			if (Contains(typeName, "Calamity") && !Contains(typeName, "Classic") && Contains(path, "Modern"))
				return true;
			if (NameHas(display, "Paradise", "Nameless") && Contains(path, "Nameless"))
				return true;
			if (NameHas(display, "Carmine", "Insouciant") && Contains(path, "Rift"))
				return true;
			if (NameHas(display, "Turbulent", "Expanse") && Contains(path, "Avatar"))
				return true;
			if (NameHas(display, "Ascent") && (Contains(path, "Ascent") || Contains(path, "AvatarUniverse")))
				return true;
			if (Contains(path, "AstrageldonBackground") || Contains(path, "AstrageldonLogo"))
				return true;
			return false;
		}

		private static Texture2D Request(Mod mod, string path)
		{
			if (mod == null || string.IsNullOrEmpty(path))
				return null;

			string trimmed = Trim(path);
			if (trimmed.StartsWith(mod.Name + "/", StringComparison.OrdinalIgnoreCase))
				trimmed = trimmed[(mod.Name.Length + 1)..];

			string key = mod.Name + "/" + trimmed;
			if (Assets.TryGetValue(key, out Asset<Texture2D> cached))
				return Ready(cached);
			if (Missing.Contains(key))
				return null;

			if (!Exists(mod, trimmed)) {
				Missing.Add(key);
				return null;
			}

			try {
				Asset<Texture2D> asset = mod.Assets.Request<Texture2D>(trimmed, AssetRequestMode.AsyncLoad);
				Assets[key] = asset;
				return Ready(asset);
			}
			catch {
				try {
					Asset<Texture2D> full = ModContent.Request<Texture2D>(mod.Name + "/" + trimmed, AssetRequestMode.AsyncLoad);
					Assets[key] = full;
					return Ready(full);
				}
				catch {
					Missing.Add(key);
					return null;
				}
			}
		}

		private static bool Exists(Mod mod, string path)
		{
			string trimmed = Trim(path);
			if (trimmed.StartsWith(mod.Name + "/", StringComparison.OrdinalIgnoreCase))
				trimmed = trimmed[(mod.Name.Length + 1)..];

			foreach (string candidate in FileNames(trimmed)) {
				try {
					if (mod.FileExists(candidate))
						return true;
				}
				catch {
				}
			}

			foreach (string file in FilesOf(mod)) {
				if (ToAssetPath(file).Equals(trimmed, StringComparison.OrdinalIgnoreCase))
					return true;
			}

			return false;
		}

		private static bool Pending(Mod mod, string path)
		{
			if (mod == null || string.IsNullOrEmpty(path))
				return false;

			string key = mod.Name + "/" + Trim(path);
			if (Assets.TryGetValue(key, out Asset<Texture2D> asset))
				return asset != null && !asset.IsLoaded;
			if (Missing.Contains(key))
				return false;
			_ = Request(mod, path);
			return Assets.TryGetValue(key, out asset) && asset != null && !asset.IsLoaded;
		}

		private static IEnumerable<string> FileNames(string path)
		{
			yield return path;
			yield return path + ".png";
			yield return path + ".rawimg";
			if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
				yield return path[..^4] + ".rawimg";
		}

		private static string[] FilesOf(Mod mod)
		{
			if (FilesByMod.TryGetValue(mod.Name, out string[] cached))
				return cached;

			try {
				IEnumerable<string> names = mod.GetFileNames();
				if (names == null) {
					FilesByMod[mod.Name] = Array.Empty<string>();
					return FilesByMod[mod.Name];
				}

				var list = new List<string>();
				foreach (string name in names) {
					if (string.IsNullOrEmpty(name))
						continue;
					if (!(name.EndsWith(".rawimg", StringComparison.OrdinalIgnoreCase) ||
					      name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
						continue;
					if (Contains(name, "Sounds/") || Contains(name, "Music/") || Contains(name, "Localization/"))
						continue;
					list.Add(name);
					if (list.Count >= 12000)
						break;
				}

				cached = list.ToArray();
			}
			catch {
				cached = Array.Empty<string>();
			}

			FilesByMod[mod.Name] = cached;
			return cached;
		}

		private static Texture2D Ready(Asset<Texture2D> asset)
		{
			try {
				if (asset == null || !asset.IsLoaded)
					return null;
				Texture2D tex = asset.Value;
				return tex == null || tex.IsDisposed ? null : tex;
			}
			catch {
				return null;
			}
		}

		private static string Trim(string path)
		{
			string trimmed = (path ?? "").Replace('\\', '/').Trim().TrimStart('/');
			if (trimmed.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
			    trimmed.EndsWith(".rawimg", StringComparison.OrdinalIgnoreCase))
				trimmed = trimmed[..trimmed.LastIndexOf('.')];
			return trimmed;
		}

		private static string ToAssetPath(string file) => Trim(file);

		private static string TypeName(ModMenu menu)
		{
			try {
				return menu.GetType().Name ?? "";
			}
			catch {
				return "";
			}
		}

		private static string Token(string type)
		{
			if (string.IsNullOrEmpty(type))
				return "";
			string t = type;
			if (t.StartsWith("X", StringComparison.Ordinal) && t.Length > 2 && char.IsUpper(t[1]))
				t = t[1..];
			t = t.Replace("MainNenu", "", StringComparison.Ordinal)
				.Replace("SkyMainMenu", "", StringComparison.Ordinal)
				.Replace("MainMenu", "", StringComparison.Ordinal)
				.Replace("ModMenu", "", StringComparison.Ordinal);
			if (t.EndsWith("Menu", StringComparison.Ordinal) && t.Length > 4)
				t = t[..^4];
			if (t.EndsWith("Style", StringComparison.Ordinal) && t.Length > 5)
				t = t[..^5];
			return t;
		}

		private static bool IsCatalyst(ModMenu menu)
		{
			string hay = TypeName(menu) + " " + SafeModName(menu) + " " + SafeTitle(menu);
			return NameHas(hay, "Catalyst", "Astrageldon");
		}

		private static Mod SafeMod(ModMenu menu)
		{
			try {
				return menu.Mod;
			}
			catch {
				return null;
			}
		}

		private static string SafeModName(ModMenu menu)
		{
			try {
				return menu.Mod?.Name ?? "";
			}
			catch {
				return "";
			}
		}

		private static string SafeTitle(ModMenu menu)
		{
			try {
				return menu.DisplayName ?? "";
			}
			catch {
				return "";
			}
		}

		private static bool NameHas(string hay, params string[] needles)
		{
			if (string.IsNullOrEmpty(hay))
				return false;
			foreach (string needle in needles) {
				if (Contains(hay, needle))
					return true;
			}

			return false;
		}

		private static bool Contains(string hay, string needle) =>
			!string.IsNullOrEmpty(hay) && hay.Contains(needle, StringComparison.OrdinalIgnoreCase);
	}
}
