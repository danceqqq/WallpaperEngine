using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using ReLogic.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI.Chat;
using WallpaperEngine.Content;
using WallpaperEngine.Layout;
using WallpaperEngine.UI;

namespace WallpaperEngine.Chrome
{
	public class MenuChrome : ModSystem
	{
		private static MethodInfo _offsetModMenu;
		private static MethodInfo _handleNews;
		private static FieldInfo _newsText;
		private static FieldInfo _newsURL;
		private static Rectangle _themeHitbox;

		public override void Load()
		{
			const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
			_offsetModMenu = typeof(MenuLoader).GetMethod("OffsetModMenu", flags);
			_handleNews = typeof(Main).GetMethod("HandleNews", flags);
			_newsText = typeof(Main).GetField("newsText", flags);
			_newsURL = typeof(Main).GetField("newsURL", flags);

			TryHook(typeof(Main).GetMethod("DrawSocialMediaButtons", flags), DrawTerrariaSocialHook);
			TryHook(typeof(Main).GetMethod("DrawtModLoaderSocialMediaButtons", flags), DrawTmlSocialHook);
			TryHook(typeof(Main).GetMethod("DrawVersionNumber", flags), DrawVersionNumberHook);
			if (_handleNews != null) {
				System.Reflection.ParameterInfo[] args = _handleNews.GetParameters();
				if (_handleNews.IsStatic && args.Length == 1 && args[0].ParameterType == typeof(Color))
					TryHook(_handleNews, HandleNewsHook);
			}
			if (_offsetModMenu != null)
				MonoModHooks.Add(_offsetModMenu, OffsetModMenuHook);

			MethodInfo themeInner = typeof(MenuLoader).GetMethod("UpdateAndDrawModMenuInner", flags);
			if (themeInner != null)
				MonoModHooks.Modify(themeInner, PatchThemeSwap);
		}

		private void TryHook(MethodInfo method, Delegate hook)
		{
			if (method != null)
				MonoModHooks.Add(method, hook);
		}

		private static bool Active => WeModMenu.OnTitle;

		private static void DrawTerrariaSocialHook(Action<Color, float> orig, Color color, float upBump)
		{
			if (!Active) {
				orig(color, upBump);
				return;
			}

			if (!SceneGraph.Visible(SceneGraph.SocialTerraria) || LayoutEditor.Editing)
				return;

			if (SceneGraph.Get(SceneGraph.SocialTerraria).Customized)
				DrawIconRow(false, SceneGraph.Pixel(SceneGraph.SocialTerraria));
			else
				orig(color, upBump);
		}

		private static void DrawTmlSocialHook(Action<Color, float> orig, Color color, float upBump)
		{
			if (!Active) {
				orig(color, upBump);
				return;
			}

			if (!SceneGraph.Visible(SceneGraph.SocialTml) || LayoutEditor.Editing)
				return;

			if (SceneGraph.Get(SceneGraph.SocialTml).Customized)
				DrawIconRow(true, SceneGraph.Pixel(SceneGraph.SocialTml));
			else
				orig(color, upBump);
		}

		private static void DrawVersionNumberHook(Action<Color, float> orig, Color color, float upBump)
		{
			if (!Active) {
				orig(color, upBump);
				return;
			}

			if (!SceneGraph.Visible(SceneGraph.Version) || LayoutEditor.Editing)
				return;

			if (SceneGraph.Get(SceneGraph.Version).Customized) {
				var font = FontAssets.MouseText.Value;
				Vector2 pos = SceneGraph.Pixel(SceneGraph.Version);
				string text = Terraria.ModLoader.ModLoader.versionedName;
				Vector2 size = font.MeasureString(text);
				ChatManager.DrawColorCodedStringWithShadow(
					Main.spriteBatch, font, text, pos - new Vector2(size.X, size.Y), color, 0f, Vector2.Zero, Vector2.One);
				return;
			}

			orig(color, upBump);
		}

		private static void HandleNewsHook(Action<Color> orig, Color color)
		{
			if (!Active) {
				orig(color);
				return;
			}

			if (!SceneGraph.Visible(SceneGraph.News) || LayoutEditor.Editing)
				return;

			if (SceneGraph.Get(SceneGraph.News).Customized)
				DrawNews(SceneGraph.Pixel(SceneGraph.News), color);
			else
				orig(color);
		}

		private static void DrawNews(Vector2 anchor, Color color)
		{
			string news = _newsText?.GetValue(null) as string ?? "";
			string text = Language.GetTextValue("tModLoader.LatestNews", news);
			var font = FontAssets.MouseText.Value;
			const float scale = 1.2f;
			Vector2 size = ChatManager.GetStringSize(font, text, new Vector2(scale));
			var hit = new Rectangle(
				(int)(anchor.X - size.X),
				(int)(anchor.Y - size.Y),
				(int)size.X,
				(int)size.Y);
			bool hover = hit.Contains(Main.mouseX, Main.mouseY);
			string url = _newsURL?.GetValue(null) as string;
			Color draw = hover && !string.IsNullOrEmpty(url) ? Main.highVersionColor : color;
			ChatManager.DrawColorCodedStringWithShadow(
				Main.spriteBatch, font, text, new Vector2(hit.X, hit.Y), draw, 0f, Vector2.Zero, new Vector2(scale));
			if (hover && Main.mouseLeftRelease && Main.mouseLeft && !string.IsNullOrEmpty(url))
				Utils.OpenToURL(url);
		}

		private static void DrawIconRow(bool tml, Vector2 anchor)
		{
			var links = tml ? Main.tModLoaderTitleLinks : Main.TitleLinks;
			if (links == null)
				return;

			Vector2 pos = new((int)MathF.Round(anchor.X), (int)MathF.Round(anchor.Y));
			foreach (var link in links) {
				link.Draw(Main.spriteBatch, pos);
				pos.X += 30f;
			}
		}

		private static void OffsetModMenuHook(Action<int> orig, int offset)
		{
			if (Active && (LayoutEditor.ShouldBlockThemeSwap || WeSplash.Visible || WePanels.IsOpen))
				return;
			orig(offset);
		}

		private static void PatchThemeSwap(ILContext il)
		{
			ILCursor cursor = new ILCursor(il);
			if (!cursor.TryGotoNext(MoveType.After, i => i.MatchNewobj<Rectangle>(), i => i.MatchStloc(out _)))
				return;

			if (cursor.Prev.MatchStloc(out int rectIndex)) {
				cursor.Emit(OpCodes.Ldloca, il.Body.Variables[rectIndex]);
				cursor.EmitDelegate<AdjustRect>(AdjustThemeRect);
			}

			MethodInfo draw = typeof(ChatManager).GetMethods(BindingFlags.Public | BindingFlags.Static)
				.FirstOrDefault(m =>
					m.Name == "DrawColorCodedStringWithShadow" &&
					m.GetParameters().Length == 10 &&
					m.GetParameters()[2].ParameterType == typeof(string));
			if (draw == null || !cursor.TryGotoNext(MoveType.Before, i => i.MatchCall(draw)))
				return;

			cursor.Remove();
			cursor.EmitDelegate(DrawThemeSwap);
		}

		private delegate void AdjustRect(ref Rectangle rectangle);

		private static void AdjustThemeRect(ref Rectangle rectangle)
		{
			if (!Active)
				return;

			if (!SceneGraph.Visible(SceneGraph.ThemeSwap) || LayoutEditor.ShouldBlockThemeSwap) {
				rectangle = Rectangle.Empty;
				_themeHitbox = rectangle;
				return;
			}

			if (!SceneGraph.Get(SceneGraph.ThemeSwap).Customized)
				return;

			Vector2 pos = SceneGraph.Pixel(SceneGraph.ThemeSwap);
			DynamicSpriteFont font = FontAssets.MouseText.Value;
			string text = Language.GetTextValue("tModLoader.ModMenuSwap") + ": " + (MenuLoader.CurrentMenu?.DisplayName ?? "");
			Vector2 size = ChatManager.GetStringSize(font, text, Vector2.One);
			rectangle = new Rectangle((int)(pos.X - size.X * 0.5f), (int)(pos.Y - size.Y * 0.5f), (int)size.X, (int)size.Y);
			_themeHitbox = rectangle;
		}

		private static Vector2 DrawThemeSwap(
			SpriteBatch spriteBatch,
			DynamicSpriteFont font,
			string text,
			Vector2 position,
			Color color,
			float rotation,
			Vector2 origin,
			Vector2 baseScale,
			float maxWidth,
			float spread)
		{
			if (!Active || LayoutEditor.ShouldBlockThemeSwap || !SceneGraph.Visible(SceneGraph.ThemeSwap)) {
				if (!Active)
					return ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, text, position, color, rotation, origin, baseScale, maxWidth, spread);
				return position;
			}

			Vector2 pos = SceneGraph.Get(SceneGraph.ThemeSwap).Customized
				? new Vector2(_themeHitbox.X, _themeHitbox.Y)
				: position;
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, text, pos, color, 0f, Vector2.Zero, baseScale, maxWidth, spread);
			return position;
		}
	}
}
