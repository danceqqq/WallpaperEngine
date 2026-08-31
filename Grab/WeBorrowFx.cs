using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using WallpaperEngine.Core;
using WallpaperEngine.UI;

namespace WallpaperEngine.Grab
{
	internal static class WeBorrowFx
	{
		private enum Pass
		{
			Off,
			Sky,
			Logo
		}

		private static bool _hooks;
		private static Pass _pass;
		private static bool _painted;
		private static bool _defaultLogo;
		private static Texture2D _menuLogo;
		private static Texture2D _wordmark;
		private static Texture2D _vanilla;
		private static Texture2D _vanilla2;
		private static FieldInfo _current;
		private static ModMenu _savedMenu;
		private static int _swapDepth;
		private static Vector2 _vanillaCenter;
		private static Vector2 _anchor;
		private static float _layout = 1f;
		private static float _fade = 1f;

		internal static void Load()
		{
			if (_hooks || Main.dedServ)
				return;

			_current = typeof(MenuLoader).GetField("currentMenu", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			TryHook(new[] { typeof(Texture2D), typeof(Vector2), typeof(Color) }, DrawVecColor);
			TryHook(new[] { typeof(Texture2D), typeof(Vector2), typeof(Rectangle?), typeof(Color) }, DrawVecRect);
			TryHook(
				new[] { typeof(Texture2D), typeof(Vector2), typeof(Rectangle?), typeof(Color), typeof(float), typeof(Vector2), typeof(float), typeof(SpriteEffects), typeof(float) },
				DrawVecScale);
			TryHook(
				new[] { typeof(Texture2D), typeof(Vector2), typeof(Rectangle?), typeof(Color), typeof(float), typeof(Vector2), typeof(Vector2), typeof(SpriteEffects), typeof(float) },
				DrawVecScale2);
			TryHook(new[] { typeof(Texture2D), typeof(Rectangle), typeof(Color) }, DrawRect);
			TryHook(new[] { typeof(Texture2D), typeof(Rectangle), typeof(Rectangle?), typeof(Color) }, DrawRectSrc);
			TryHook(
				new[] { typeof(Texture2D), typeof(Rectangle), typeof(Rectangle?), typeof(Color), typeof(float), typeof(Vector2), typeof(SpriteEffects), typeof(float) },
				DrawRectFull);
			_hooks = true;
		}

		internal static void Unload()
		{
			_pass = Pass.Off;
			_menuLogo = null;
			_wordmark = null;
			_vanilla = null;
			_vanilla2 = null;
			_savedMenu = null;
			_swapDepth = 0;
			_current = null;
		}

		internal static void Tick()
		{
			if (!Main.gameMenu)
				return;

			if (WeSave.Data.Wallpaper == WallpaperKind.Borrowed)
				TickMenu(WeCatalog.FindMenu(WeSave.Data.WallpaperId));
			if (WeSave.Data.Logo == LogoKind.Borrowed)
				TickMenu(WeCatalog.FindMenu(WeSave.Data.LogoId));
		}

		internal static bool TryRunSky(SpriteBatch spriteBatch, string id) =>
			Run(spriteBatch, id, Pass.Sky, new Vector2(Main.screenWidth * 0.5f, 100f), 1f, 1f);

		internal static bool TryRunLogo(SpriteBatch spriteBatch, string id, Vector2 anchor, float layoutScale, float fade)
		{
			if (!Run(spriteBatch, id, Pass.Logo, anchor, layoutScale, fade))
				return false;
			if (_painted && !_defaultLogo)
				return true;
			return BlitMenuLogo(spriteBatch, id, anchor, layoutScale, fade);
		}

		private static void TickMenu(ModMenu menu)
		{
			if (menu == null)
				return;

			try {
				menu.Update(true);
			}
			catch {
			}

			WeModArt.TickHostSky(menu);
		}

		private static bool Run(SpriteBatch spriteBatch, string id, Pass pass, Vector2 anchor, float layoutScale, float fade)
		{
			Load();
			ModMenu menu = WeCatalog.FindMenu(id);
			if (menu == null || spriteBatch == null)
				return false;

			CacheLogos(id, menu);
			WeModArt.PrimeLiveSky(menu);
			_vanillaCenter = new Vector2(Main.screenWidth * 0.5f, 100f);
			_anchor = anchor;
			_layout = Math.Max(0.05f, layoutScale);
			_fade = MathHelper.Clamp(fade, 0f, 1f);
			_painted = false;
			_defaultLogo = true;
			_pass = pass;

			double time = Main.time;
			bool day = Main.dayTime;
			PushMenu(menu);
			try {
				Vector2 center = _vanillaCenter;
				float rotation = 0f;
				float scale = 1f;
				Color color = Color.White * _fade;
				_defaultLogo = menu.PreDrawLogo(spriteBatch, ref center, ref rotation, ref scale, ref color);
				if (pass == Pass.Logo) {
					try {
						menu.PostDrawLogo(spriteBatch, center, rotation, scale, color);
					}
					catch {
					}
				}

				return true;
			}
			catch {
				return false;
			}
			finally {
				_pass = Pass.Off;
				PopMenu();
				Main.time = time;
				Main.dayTime = day;
				_menuLogo = null;
				_wordmark = null;
			}
		}

		private static bool BlitMenuLogo(SpriteBatch spriteBatch, string id, Vector2 anchor, float layoutScale, float fade)
		{
			Texture2D tex = WeCatalog.LogoTexture(id);
			if (tex == null || WeInspect.IsIcon(tex))
				return _painted;

			float cap = MathHelper.Min(520f, Main.screenWidth * 0.38f);
			float scale = cap / Math.Max(1, tex.Width) * layoutScale;
			WeDraw.WithPoint(spriteBatch, () => {
				spriteBatch.Draw(tex, anchor, null, Color.White * fade, 0f, tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
			});
			return true;
		}

		private static void CacheLogos(string id, ModMenu menu)
		{
			_menuLogo = WeCatalog.ReadMenuLogo(menu);
			_wordmark = WeCatalog.LogoTexture(id);
			try {
				_vanilla = TextureAssets.Logo?.Value;
			}
			catch {
			}

			try {
				_vanilla2 = TextureAssets.Logo2?.Value;
			}
			catch {
			}
		}

		private static void PushMenu(ModMenu menu)
		{
			if (_current == null || menu == null)
				return;
			try {
				if (_swapDepth == 0)
					_savedMenu = _current.GetValue(null) as ModMenu;
				_swapDepth++;
				_current.SetValue(null, menu);
			}
			catch {
			}
		}

		private static void PopMenu()
		{
			if (_current == null || _swapDepth <= 0)
				return;
			_swapDepth--;
			if (_swapDepth > 0)
				return;
			try {
				_current.SetValue(null, _savedMenu);
			}
			catch {
			}

			_savedMenu = null;
		}

		private static bool Allow(Texture2D tex, ref Vector2 pos, ref Vector2 scale, ref Rectangle dest, bool hasDest, ref Color color)
		{
			if (_pass == Pass.Off)
				return true;
			if (tex == null)
				return true;

			if (WeInspect.IsFillPixel(tex) && !hasDest) {
				bool fillCover = IsCover(tex, pos, scale, null);
				if (_pass == Pass.Sky) {
					_painted = true;
					return true;
				}

				return !fillCover;
			}

			bool screen = tex is RenderTarget2D || IsCover(tex, pos, scale, hasDest ? dest : null);
			if (screen) {
				if (_pass == Pass.Sky) {
					_painted = true;
					return true;
				}

				return false;
			}

			if (IsThemeLogo(tex)) {
				if (_pass == Pass.Sky)
					return false;
				KeepLogo(ref pos, ref scale, ref dest, hasDest, ref color);
				return true;
			}

			if (_pass == Pass.Logo && NearLogo(pos, hasDest ? dest : null)) {
				KeepLogo(ref pos, ref scale, ref dest, hasDest, ref color);
				return true;
			}

			if (_pass == Pass.Sky) {
				_painted = true;
				return true;
			}

			return false;
		}

		private static bool IsThemeLogo(Texture2D tex)
		{
			if (tex == null)
				return false;
			if (tex == _menuLogo || tex == _wordmark || tex == _vanilla || tex == _vanilla2)
				return true;
			if (WeInspect.IsFillPixel(tex) || tex is RenderTarget2D)
				return false;
			string name = "";
			try {
				name = tex.Name ?? "";
			}
			catch {
			}

			return WeInspect.LooksLikeLogoName(name) && !WeInspect.LooksLikeSceneName(name);
		}

		private static void KeepLogo(ref Vector2 pos, ref Vector2 scale, ref Rectangle dest, bool hasDest, ref Color color)
		{
			if (hasDest)
				Remap(ref dest);
			else
				Remap(ref pos, ref scale);
			color *= _fade;
			_painted = true;
			_defaultLogo = false;
		}

		private static bool IsCover(Texture2D tex, Vector2 pos, Vector2 scale, Rectangle? dest)
		{
			if (dest.HasValue)
				return dest.Value.Width >= Main.screenWidth * 0.42f && dest.Value.Height >= Main.screenHeight * 0.42f;

			int w = Math.Max(1, (int)(tex.Width * Math.Abs(scale.X)));
			int h = Math.Max(1, (int)(tex.Height * Math.Abs(scale.Y)));
			return w >= Main.screenWidth * 0.42f && h >= Main.screenHeight * 0.42f;
		}

		private static bool NearLogo(Vector2 pos, Rectangle? dest)
		{
			Vector2 at = dest?.Center.ToVector2() ?? pos;
			return Vector2.Distance(at, _vanillaCenter) < 280f;
		}

		private static void Remap(ref Vector2 pos, ref Vector2 scale)
		{
			pos = (pos - _vanillaCenter) * _layout + _anchor;
			scale *= _layout;
		}

		private static void Remap(ref Rectangle dest)
		{
			Vector2 center = dest.Center.ToVector2();
			center = (center - _vanillaCenter) * _layout + _anchor;
			int w = Math.Max(1, (int)(dest.Width * _layout));
			int h = Math.Max(1, (int)(dest.Height * _layout));
			dest = new Rectangle((int)(center.X - w * 0.5f), (int)(center.Y - h * 0.5f), w, h);
		}

		private static void TryHook(Type[] types, Delegate hook)
		{
			MethodInfo method = FindDraw(types);
			if (method == null)
				return;

			try {
				MonoModHooks.Add(method, hook);
			}
			catch {
			}
		}

		private static MethodInfo FindDraw(Type[] types)
		{
			foreach (MethodInfo method in typeof(SpriteBatch).GetMethods(BindingFlags.Public | BindingFlags.Instance)) {
				if (method.Name != nameof(SpriteBatch.Draw))
					continue;

				ParameterInfo[] pars = method.GetParameters();
				if (pars.Length != types.Length)
					continue;

				bool match = true;
				for (int i = 0; i < types.Length; i++) {
					if (pars[i].ParameterType != types[i]) {
						match = false;
						break;
					}
				}

				if (match)
					return method;
			}

			return null;
		}

		private static void DrawVecColor(Action<SpriteBatch, Texture2D, Vector2, Color> orig, SpriteBatch sb, Texture2D tex, Vector2 pos, Color color)
		{
			Vector2 scale = Vector2.One;
			Rectangle dest = Rectangle.Empty;
			if (Allow(tex, ref pos, ref scale, ref dest, false, ref color))
				orig(sb, tex, pos, color);
		}

		private static void DrawVecRect(Action<SpriteBatch, Texture2D, Vector2, Rectangle?, Color> orig, SpriteBatch sb, Texture2D tex, Vector2 pos, Rectangle? src, Color color)
		{
			Vector2 scale = Vector2.One;
			Rectangle dest = Rectangle.Empty;
			if (Allow(tex, ref pos, ref scale, ref dest, false, ref color))
				orig(sb, tex, pos, src, color);
		}

		private static void DrawVecScale(
			Action<SpriteBatch, Texture2D, Vector2, Rectangle?, Color, float, Vector2, float, SpriteEffects, float> orig,
			SpriteBatch sb, Texture2D tex, Vector2 pos, Rectangle? src, Color color, float rot, Vector2 origin, float scale, SpriteEffects fx, float depth)
		{
			Vector2 sc = new(scale, scale);
			Rectangle dest = Rectangle.Empty;
			if (Allow(tex, ref pos, ref sc, ref dest, false, ref color))
				orig(sb, tex, pos, src, color, rot, origin, sc.X, fx, depth);
		}

		private static void DrawVecScale2(
			Action<SpriteBatch, Texture2D, Vector2, Rectangle?, Color, float, Vector2, Vector2, SpriteEffects, float> orig,
			SpriteBatch sb, Texture2D tex, Vector2 pos, Rectangle? src, Color color, float rot, Vector2 origin, Vector2 scale, SpriteEffects fx, float depth)
		{
			Rectangle dest = Rectangle.Empty;
			if (Allow(tex, ref pos, ref scale, ref dest, false, ref color))
				orig(sb, tex, pos, src, color, rot, origin, scale, fx, depth);
		}

		private static void DrawRect(Action<SpriteBatch, Texture2D, Rectangle, Color> orig, SpriteBatch sb, Texture2D tex, Rectangle dest, Color color)
		{
			Vector2 pos = dest.Center.ToVector2();
			Vector2 scale = Vector2.One;
			if (Allow(tex, ref pos, ref scale, ref dest, true, ref color))
				orig(sb, tex, dest, color);
		}

		private static void DrawRectSrc(Action<SpriteBatch, Texture2D, Rectangle, Rectangle?, Color> orig, SpriteBatch sb, Texture2D tex, Rectangle dest, Rectangle? src, Color color)
		{
			Vector2 pos = dest.Center.ToVector2();
			Vector2 scale = Vector2.One;
			if (Allow(tex, ref pos, ref scale, ref dest, true, ref color))
				orig(sb, tex, dest, src, color);
		}

		private static void DrawRectFull(
			Action<SpriteBatch, Texture2D, Rectangle, Rectangle?, Color, float, Vector2, SpriteEffects, float> orig,
			SpriteBatch sb, Texture2D tex, Rectangle dest, Rectangle? src, Color color, float rot, Vector2 origin, SpriteEffects fx, float depth)
		{
			Vector2 pos = dest.Center.ToVector2();
			Vector2 scale = Vector2.One;
			if (Allow(tex, ref pos, ref scale, ref dest, true, ref color))
				orig(sb, tex, dest, src, color, rot, origin, fx, depth);
		}
	}
}
