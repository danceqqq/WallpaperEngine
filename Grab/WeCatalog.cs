using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using WallpaperEngine.Content;
using WallpaperEngine.Core;

namespace WallpaperEngine.Grab
{
	internal static class WeCatalog
	{
		private static readonly BindingFlags Flags =
			BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

		private static FieldInfo _menusField;
		private static readonly List<WeOffer> LogoList = new();
		private static readonly List<WeOffer> SkyList = new();
		private static readonly Dictionary<string, Asset<Texture2D>> ModIcons = new(StringComparer.Ordinal);
		private static readonly Dictionary<string, bool> LogoOverride = new(StringComparer.Ordinal);
		private static readonly Dictionary<string, Texture2D> StylePreviewCache = new(StringComparer.Ordinal);
		private static readonly Dictionary<string, Texture2D> MenuScenes = new(StringComparer.Ordinal);

		private static Texture2D _vanillaLogo;
		private static Texture2D _vanillaLogo2;
		private static int _modCount = -1;
		private static int _pulse;
		private static bool _scanned;

		internal static IReadOnlyList<WeOffer> Logos => LogoList;
		internal static IReadOnlyList<WeOffer> Skies => SkyList;
		internal static bool Ready => _scanned;

		internal static void Refresh()
		{
			try {
				Rebuild();
			}
			catch {
			}
		}

		internal static void Pulse()
		{
			if (!Main.gameMenu)
				return;

			_pulse++;
			int mods = 0;
			try {
				mods = ModLoader.Mods.Length;
			}
			catch {
			}

			bool pending = false;
			foreach (WeOffer offer in LogoList) {
				if (offer.Pending)
					pending = true;
			}

			foreach (WeOffer offer in SkyList) {
				if (offer.Pending)
					pending = true;
			}

			if (!_scanned || mods != _modCount || (pending && _pulse % 20 == 0))
				Refresh();

			if (_scanned && _pulse % 30 == 0)
				DropMissing();
		}

		internal static void Unload()
		{
			LogoList.Clear();
			SkyList.Clear();
			ModIcons.Clear();
			LogoOverride.Clear();
			StylePreviewCache.Clear();
			MenuScenes.Clear();
			WeInspect.Unload();
			WeModArt.Unload();
			_vanillaLogo = null;
			_vanillaLogo2 = null;
			_menusField = null;
			_modCount = -1;
			_scanned = false;
		}

		internal static WeOffer FindLogo(string id) => Find(LogoList, id);

		internal static WeOffer FindSky(string id) => Find(SkyList, id);

		internal static void DropMissing()
		{
			if (!_scanned)
				return;

			WeSaveData data = WeSave.Data;
			if (data.Logo == LogoKind.Borrowed && (SourceGone(data.LogoId) || Skipped(data.LogoId))) {
				data.Logo = LogoKind.Vanilla;
				data.LogoId = "";
				WeSave.Save();
				WeToast.Show("ToastBorrowGone");
			}

			if (data.Wallpaper == WallpaperKind.Borrowed && (SourceGone(data.WallpaperId) || Skipped(data.WallpaperId))) {
				data.Wallpaper = WallpaperKind.Vanilla;
				data.WallpaperId = "";
				WeSave.Save();
				WeToast.Show("ToastBorrowGone");
			}
		}

		private static bool SourceGone(string id)
		{
			if (string.IsNullOrEmpty(id))
				return true;
			if (FindMenu(id) != null)
				return false;
			return SnapshotMenus().Count > 0;
		}

		private static bool Skipped(string id)
		{
			ModMenu menu = FindMenu(id);
			return menu != null && WeModArt.SkipMenu(menu);
		}

		internal static Texture2D ReadMenuLogo(ModMenu menu) => ReadLogo(menu);

		internal static Texture2D LogoTexture(string id)
		{
			ModMenu menu = FindMenu(id);
			if (menu == null)
				return null;

			Texture2D fromProp = ReadLogo(menu);
			if (fromProp != null && !IsSharedLogo(fromProp) && !LooksPlaceholder(menu, fromProp) && !WeInspect.IsIcon(fromProp)) {
				string name = WeInspect.AssetName(LogoAsset(menu));
				if (!WeInspect.IsScene(fromProp, name) || WeInspect.IsLogo(fromProp, name))
					return fromProp;
			}

			return PickMenuLogoField(menu) ?? WeModArt.FindLogo(menu);
		}

		internal static Texture2D SkyPreview(WeOffer offer)
		{
			if (offer == null)
				return null;

			if (offer.UseStyle) {
				Texture2D fromStyle = StyleArt(StyleOf(offer.Id));
				if (fromStyle != null)
					return fromStyle;
			}

			Texture2D scene = MenuScene(offer.Id);
			if (scene != null)
				return scene;
			return WeModArt.FindPreview(FindMenu(offer.Id));
		}

		internal static Texture2D MenuScene(string id)
		{
			if (string.IsNullOrEmpty(id))
				return null;

			if (MenuScenes.TryGetValue(id, out Texture2D cached) && cached != null && !cached.IsDisposed)
				return cached;

			Texture2D live = PickMenuScene(FindMenu(id));
			if (live != null && !WeInspect.IsCoverSized(live))
				live = null;
			live ??= WeModArt.FindSky(FindMenu(id));
			if (live != null)
				MenuScenes[id] = live;
			return live;
		}

		internal static Texture2D ModIcon(string modName)
		{
			if (string.IsNullOrEmpty(modName))
				return null;

			if (ModIcons.TryGetValue(modName, out Asset<Texture2D> cached))
				return ReadyTexture(cached);

			Mod mod = FindMod(modName);
			Asset<Texture2D> asset = RequestModIcon(mod);
			ModIcons[modName] = asset;
			return ReadyTexture(asset);
		}

		internal static ModMenu FindMenu(string fullName)
		{
			if (string.IsNullOrEmpty(fullName))
				return null;

			foreach (ModMenu menu in SnapshotMenus()) {
				if (SafeFullName(menu) == fullName)
					return menu;
			}

			return null;
		}

		private static void Rebuild()
		{
			CacheVanillaLogos();
			try {
				_modCount = ModLoader.Mods.Length;
			}
			catch {
				_modCount = -1;
			}

			MenuScenes.Clear();
			var logos = new List<WeOffer>();
			var skies = new List<WeOffer>();
			var seenLogo = new HashSet<string>(StringComparer.Ordinal);
			var seenSky = new HashSet<string>(StringComparer.Ordinal);

			foreach (ModMenu menu in SnapshotMenus()) {
				if (!IsBorrowCandidate(menu))
					continue;
				if (WeModArt.SkipMenu(menu))
					continue;

				string id = SafeFullName(menu);
				if (string.IsNullOrEmpty(id))
					continue;

				ClassifyLogo(menu, id, logos, seenLogo);
				ClassifySky(menu, id, skies, seenSky);
			}

			logos.Sort(CompareOffers);
			skies.Sort(CompareOffers);
			LogoList.Clear();
			SkyList.Clear();
			LogoList.AddRange(logos);
			SkyList.AddRange(skies);
			_scanned = true;
			PrimeModIcons();
		}

		private static void ClassifyLogo(ModMenu menu, string id, List<WeOffer> logos, HashSet<string> seen)
		{
			if (!seen.Add(id))
				return;

			Asset<Texture2D> asset = LogoAsset(menu);
			Texture2D tex = ReadLogo(menu);
			bool fromProp = OverridesLogo(menu);
			bool pending = tex == null && (LogoAssetPending(menu) || WeModArt.LogoPending(menu));
			if (fromProp && !pending && (tex == null || IsSharedLogo(tex) || LooksPlaceholder(menu, tex) || WeInspect.IsIcon(tex)))
				fromProp = false;

			if (fromProp && !pending) {
				string name = WeInspect.AssetName(asset);
				if (WeInspect.IsScene(tex, name) && !WeInspect.IsLogo(tex, name))
					fromProp = false;
			}

			if (!fromProp) {
				tex = PickMenuLogoField(menu) ?? WeModArt.FindLogo(menu);
				pending = tex == null && WeModArt.LogoPending(menu);
				if (!pending && tex == null && !OverridesMethod(menu, nameof(ModMenu.PreDrawLogo)))
					return;
			}

			logos.Add(MakeOffer(menu, id, WeOfferKind.Logo, pending && tex == null));
		}

		private static void ClassifySky(ModMenu menu, string id, List<WeOffer> skies, HashSet<string> seen)
		{
			if (!seen.Add(id))
				return;

			bool useStyle = IsDrawableStyle(ReadStyle(menu));
			Texture2D scene = PickMenuScene(menu) ?? WeModArt.FindSky(menu);
			if (scene != null)
				MenuScenes[id] = scene;

			bool pending = scene == null && (HasPendingScene(menu) || WeModArt.SkyPending(menu));
			bool useFx = OverridesMethod(menu, nameof(ModMenu.PreDrawLogo));
			bool hostSky = WeModArt.HasHostSky(menu);
			if (!useStyle && scene == null && !pending && !useFx && !hostSky)
				return;

			WeOffer offer = MakeOffer(menu, id, WeOfferKind.Sky, pending);
			offer.UseStyle = useStyle;
			offer.UseMenuScene = scene != null || pending;
			offer.UseThemeFx = useFx;
			skies.Add(offer);
		}

		internal static bool HasThemeFx(ModMenu menu)
		{
			if (menu == null || !OverridesMethod(menu, nameof(ModMenu.PreDrawLogo)))
				return false;

			try {
				MethodInfo method = menu.GetType().GetMethod(nameof(ModMenu.PreDrawLogo), BindingFlags.Public | BindingFlags.Instance);
				System.Reflection.MethodBody body = method?.GetMethodBody();
				if (body == null)
					return true;

				int length = body.GetILAsByteArray()?.Length ?? 0;
				return length >= 28;
			}
			catch {
				return true;
			}
		}

		private static Texture2D PickMenuScene(ModMenu menu)
		{
			if (menu == null)
				return null;

			Texture2D logo = ReadLogo(menu);
			Texture2D best = null;
			int bestScore = 0;
			foreach ((Texture2D tex, string name) in MenuTextures(menu)) {
				if (tex == null || tex.IsDisposed || tex == logo)
					continue;

				int score = WeInspect.SceneScore(tex, name);
				if (score > bestScore) {
					best = tex;
					bestScore = score;
				}
			}

			return best;
		}

		private static Texture2D PickMenuLogoField(ModMenu menu)
		{
			if (menu == null)
				return null;

			Texture2D logo = ReadLogo(menu);
			foreach ((Texture2D tex, string name) in MenuTextures(menu)) {
				if (tex == null || tex.IsDisposed || tex == logo || IsSharedLogo(tex))
					continue;
				if (WeInspect.IsIcon(tex))
					continue;
				if (!WeInspect.LooksLikeLogoName(name))
					continue;
				if (WeInspect.IsScene(tex, name) && !WeInspect.IsLogo(tex, name))
					continue;
				return tex;
			}

			return null;
		}

		private static bool HasPendingScene(ModMenu menu)
		{
			foreach ((Texture2D tex, string name) in MenuTextures(menu, includeUnloaded: true)) {
				if (tex != null)
					continue;
				if (WeInspect.LooksLikeSceneName(name) && !WeInspect.LooksLikeJunkName(name) && !WeInspect.LooksLikeLogoName(name))
					return true;
			}

			return false;
		}

		private static IEnumerable<(Texture2D tex, string name)> MenuTextures(ModMenu menu, bool includeUnloaded = false)
		{
			if (menu == null)
				yield break;

			for (Type type = menu.GetType(); type != null && type != typeof(ModMenu) && type != typeof(object); type = type.BaseType) {
				FieldInfo[] fields;
				PropertyInfo[] props;
				try {
					fields = type.GetFields(Flags);
					props = type.GetProperties(Flags);
				}
				catch {
					yield break;
				}

				foreach (FieldInfo field in fields) {
					if (!IsTextureType(field.FieldType))
						continue;
					if (!TryReadPart(SafeGet(field, field.IsStatic ? null : menu), field.Name, includeUnloaded, out Texture2D tex, out string name))
						continue;
					yield return (tex, name);
				}

				foreach (PropertyInfo prop in props) {
					if (prop.GetIndexParameters().Length > 0 || IsSkippedPart(prop.Name) || !IsTextureType(prop.PropertyType))
						continue;
					object target = prop.GetGetMethod(true)?.IsStatic == true ? null : menu;
					if (!TryReadPart(SafeGet(prop, target), prop.Name, includeUnloaded, out Texture2D tex, out string name))
						continue;
					yield return (tex, name);
				}
			}
		}

		private static bool IsSkippedPart(string name) =>
			name is "Logo" or "SunTexture" or "MoonTexture" or "MenuBackgroundStyle";

		private static bool IsTextureType(Type type)
		{
			if (type == typeof(Texture2D) || type == typeof(Asset<Texture2D>))
				return true;
			return type != null && type.IsGenericType &&
			       type.GetGenericTypeDefinition() == typeof(Asset<>) &&
			       type.GetGenericArguments()[0] == typeof(Texture2D);
		}

		private static bool TryReadPart(object value, string memberName, bool includeUnloaded, out Texture2D tex, out string name)
		{
			tex = null;
			name = memberName ?? "";
			if (value == null)
				return false;

			try {
				if (value is Asset<Texture2D> asset) {
					string assetName = WeInspect.AssetName(asset);
					if (!string.IsNullOrEmpty(assetName))
						name = memberName + " " + assetName;
					if (WeInspect.LooksLikeJunkName(memberName) || WeInspect.LooksLikeJunkName(assetName))
						return false;
					tex = ReadyTexture(asset);
					return tex != null || includeUnloaded;
				}

				if (value is Texture2D direct && !direct.IsDisposed) {
					if (WeInspect.LooksLikeJunkName(memberName))
						return false;
					tex = direct;
					return true;
				}
			}
			catch {
				return false;
			}

			return false;
		}

		private static WeOffer MakeOffer(ModMenu menu, string id, WeOfferKind kind, bool pending)
		{
			Mod mod = SafeMod(menu);
			return new WeOffer {
				Id = id,
				Kind = kind,
				ModName = SafeModName(mod),
				ModTitle = SafeModTitle(mod),
				MenuTitle = SafeMenuTitle(menu),
				Pending = pending
			};
		}

		private static bool IsBorrowCandidate(ModMenu menu)
		{
			if (menu is WeModMenu)
				return false;

			Type type = menu.GetType();
			if (type.Namespace != null && type.Namespace.StartsWith("Terraria.ModLoader", StringComparison.Ordinal))
				return false;

			Mod mod = SafeMod(menu);
			if (mod == null || mod is WallpaperEngineMod)
				return false;

			return true;
		}

		private static List<ModMenu> SnapshotMenus()
		{
			try {
				_menusField ??= typeof(MenuLoader).GetField("menus", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				object raw = _menusField?.GetValue(null);
				if (raw is IEnumerable<ModMenu> menus) {
					var copy = new List<ModMenu>();
					foreach (ModMenu menu in menus) {
						if (menu != null)
							copy.Add(menu);
					}

					return copy;
				}
			}
			catch {
			}

			return new List<ModMenu>();
		}

		private static void CacheVanillaLogos()
		{
			try {
				_vanillaLogo = TextureAssets.Logo?.Value;
			}
			catch {
			}

			try {
				_vanillaLogo2 = TextureAssets.Logo2?.Value;
			}
			catch {
			}
		}

		private static void PrimeModIcons()
		{
			foreach (WeOffer offer in LogoList)
				_ = ModIcon(offer.ModName);
			foreach (WeOffer offer in SkyList)
				_ = ModIcon(offer.ModName);
		}

		private static bool OverridesLogo(ModMenu menu)
		{
			string id = SafeFullName(menu);
			if (string.IsNullOrEmpty(id))
				return false;
			if (LogoOverride.TryGetValue(id, out bool known))
				return known;

			bool value = OverridesProperty(menu, nameof(ModMenu.Logo));
			LogoOverride[id] = value;
			return value;
		}

		private static Asset<Texture2D> LogoAsset(ModMenu menu)
		{
			try {
				return menu.Logo;
			}
			catch {
				return null;
			}
		}

		private static Texture2D ReadLogo(ModMenu menu)
		{
			try {
				Asset<Texture2D> asset = LogoAsset(menu);
				Texture2D tex = ReadyTexture(asset);
				return tex == null || tex.IsDisposed ? null : tex;
			}
			catch {
				return null;
			}
		}

		private static bool LogoAssetPending(ModMenu menu)
		{
			try {
				Asset<Texture2D> asset = LogoAsset(menu);
				return asset != null && !asset.IsLoaded;
			}
			catch {
				return false;
			}
		}

		private static ModSurfaceBackgroundStyle ReadStyle(ModMenu menu)
		{
			if (menu == null)
				return null;

			try {
				ModSurfaceBackgroundStyle style = menu.MenuBackgroundStyle;
				if (style is WeBackgroundStyle)
					return null;
				return style;
			}
			catch {
				return null;
			}
		}

		internal static ModSurfaceBackgroundStyle StyleOf(string id) => ReadStyle(FindMenu(id));

		internal static bool IsDrawableStyle(ModSurfaceBackgroundStyle style)
		{
			if (style == null || style is WeBackgroundStyle)
				return false;

			if (FirstStyleTexture(style) != null)
				return true;

			try {
				if (TextureOfSlot(style.ChooseFarTexture()) != null)
					return true;
				if (TextureOfSlot(style.ChooseMiddleTexture()) != null)
					return true;

				float scale = 1f;
				double parallax = 0d;
				float a = 0f;
				float b = 0f;
				if (TextureOfSlot(style.ChooseCloseTexture(ref scale, ref parallax, ref a, ref b)) != null)
					return true;
			}
			catch {
			}

			return OverridesMethod(style, nameof(ModSurfaceBackgroundStyle.PreDrawCloseBackground));
		}

		internal static Texture2D StyleArt(ModSurfaceBackgroundStyle style)
		{
			if (style == null)
				return null;

			string key = StyleKey(style);
			if (!string.IsNullOrEmpty(key) &&
			    StylePreviewCache.TryGetValue(key, out Texture2D cached) &&
			    cached != null && !cached.IsDisposed)
				return cached;

			Texture2D art = FirstStyleTexture(style);
			if (art == null) {
				try {
					art = TextureOfSlot(style.ChooseFarTexture()) ??
					      TextureOfSlot(style.ChooseMiddleTexture());
					if (art == null) {
						float scale = 1f;
						double parallax = 0d;
						float a = 0f;
						float b = 0f;
						art = TextureOfSlot(style.ChooseCloseTexture(ref scale, ref parallax, ref a, ref b));
					}
				}
				catch {
					art = null;
				}
			}

			if (art != null && !string.IsNullOrEmpty(key))
				StylePreviewCache[key] = art;
			return art;
		}

		private static string StyleKey(ModSurfaceBackgroundStyle style)
		{
			try {
				if (!string.IsNullOrEmpty(style.FullName))
					return style.FullName;
			}
			catch {
			}

			return style.GetType().FullName ?? "";
		}

		internal static Texture2D FirstStyleTexture(ModSurfaceBackgroundStyle style)
		{
			if (style == null)
				return null;

			Texture2D best = null;
			int bestArea = 0;
			try {
				foreach (FieldInfo field in style.GetType().GetFields(Flags)) {
					if (!TryArt(SafeGet(field, field.IsStatic ? null : style), field.Name, out Texture2D tex))
						continue;
					int score = WeInspect.SceneScore(tex, field.Name);
					if (score > bestArea) {
						best = tex;
						bestArea = score;
					}
				}

				foreach (PropertyInfo prop in style.GetType().GetProperties(Flags)) {
					if (prop.GetIndexParameters().Length > 0 || IsSkippedPart(prop.Name))
						continue;
					if (!TryArt(SafeGet(prop, style), prop.Name, out Texture2D tex))
						continue;
					int score = WeInspect.SceneScore(tex, prop.Name);
					if (score > bestArea) {
						best = tex;
						bestArea = score;
					}
				}
			}
			catch {
			}

			return best;
		}

		internal static Texture2D TextureOfSlot(int slot)
		{
			if (slot <= 0)
				return null;

			try {
				if (TextureAssets.Background == null || slot >= TextureAssets.Background.Length)
					return null;

				try {
					Main.instance.LoadBackground(slot);
				}
				catch {
				}

				Texture2D tex = TextureAssets.Background[slot]?.Value;
				if (tex == null || tex.IsDisposed || tex.Width < 32 || tex.Height < 32)
					return null;
				return tex;
			}
			catch {
				return null;
			}
		}

		internal static bool OverridesMethod(object instance, string name)
		{
			if (instance == null)
				return false;

			try {
				MethodInfo method = instance.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
				if (method == null)
					return false;
				return method.DeclaringType != method.GetBaseDefinition().DeclaringType;
			}
			catch {
				return false;
			}
		}

		internal static bool OverridesProperty(object instance, string name)
		{
			if (instance == null)
				return false;

			try {
				PropertyInfo prop = instance.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
				MethodInfo get = prop?.GetGetMethod();
				if (get == null)
					return false;
				return get.DeclaringType != get.GetBaseDefinition().DeclaringType;
			}
			catch {
				return false;
			}
		}

		private static bool TryArt(object value, string name, out Texture2D tex)
		{
			tex = null;
			try {
				if (value is Asset<Texture2D> asset)
					tex = ReadyTexture(asset);
				else if (value is Texture2D direct)
					tex = direct;
			}
			catch {
				return false;
			}

			return tex != null && !tex.IsDisposed && WeInspect.SceneScore(tex, name) > 0 && !IsSharedLogo(tex);
		}

		private static object SafeGet(FieldInfo field, object instance)
		{
			try {
				return field.GetValue(instance);
			}
			catch {
				return null;
			}
		}

		private static object SafeGet(PropertyInfo prop, object instance)
		{
			try {
				return prop.GetValue(instance);
			}
			catch {
				return null;
			}
		}

		private static Texture2D ReadyTexture(Asset<Texture2D> asset)
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

		private static bool IsSharedLogo(Texture2D tex) =>
			tex != null && (tex == _vanillaLogo || tex == _vanillaLogo2);

		private static bool LooksPlaceholder(ModMenu menu, Texture2D tex)
		{
			if (tex.Width < 24 || tex.Height < 16)
				return true;

			string name = "";
			try {
				name = menu.Logo?.Name ?? "";
			}
			catch {
			}

			if (string.IsNullOrEmpty(name))
				return false;

			return name.Contains("Blank", StringComparison.OrdinalIgnoreCase) ||
			       name.Contains("Empty", StringComparison.OrdinalIgnoreCase) ||
			       name.Contains("Placeholder", StringComparison.OrdinalIgnoreCase) ||
			       name.Contains("Null", StringComparison.OrdinalIgnoreCase) ||
			       (name.Contains("Pixel", StringComparison.OrdinalIgnoreCase) && tex.Width <= 32);
		}

		private static Asset<Texture2D> RequestModIcon(Mod mod)
		{
			if (mod == null)
				return null;

			try {
				foreach (string propName in new[] { "WorkshopIcon", "Icon", "SmallModIcon" }) {
					PropertyInfo prop = typeof(Mod).GetProperty(propName, Flags);
					if (prop?.GetValue(mod) is Asset<Texture2D> fromProp && fromProp != null)
						return fromProp;
				}
			}
			catch {
			}

			try {
				if (mod.FileExists("icon.png"))
					return mod.Assets.Request<Texture2D>("icon", AssetRequestMode.AsyncLoad);
			}
			catch {
			}

			return null;
		}

		private static Mod FindMod(string name)
		{
			if (string.IsNullOrEmpty(name))
				return null;

			try {
				return ModLoader.TryGetMod(name, out Mod mod) ? mod : null;
			}
			catch {
				return null;
			}
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

		private static string SafeFullName(ModMenu menu)
		{
			try {
				return menu.FullName ?? "";
			}
			catch {
				return "";
			}
		}

		private static string SafeModName(Mod mod)
		{
			try {
				return mod?.Name ?? "";
			}
			catch {
				return "";
			}
		}

		private static string SafeModTitle(Mod mod)
		{
			try {
				string title = mod?.DisplayName;
				return string.IsNullOrWhiteSpace(title) ? SafeModName(mod) : title;
			}
			catch {
				return SafeModName(mod);
			}
		}

		private static string SafeMenuTitle(ModMenu menu)
		{
			try {
				string title = menu.DisplayName;
				if (!string.IsNullOrWhiteSpace(title))
					return title;
			}
			catch {
			}

			try {
				return menu.Name ?? "";
			}
			catch {
				return "";
			}
		}

		private static WeOffer Find(List<WeOffer> list, string id)
		{
			if (string.IsNullOrEmpty(id))
				return null;
			return list.Find(item => item.Id == id);
		}

		private static int CompareOffers(WeOffer a, WeOffer b)
		{
			int mod = string.Compare(a.ModTitle, b.ModTitle, StringComparison.CurrentCultureIgnoreCase);
			if (mod != 0)
				return mod;
			return string.Compare(a.MenuTitle, b.MenuTitle, StringComparison.CurrentCultureIgnoreCase);
		}
	}
}
