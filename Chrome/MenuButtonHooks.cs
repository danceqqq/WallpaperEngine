using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using WallpaperEngine.Content;
using WallpaperEngine.Core;
using WallpaperEngine.Layout;
using WallpaperEngine.UI;

namespace WallpaperEngine.Chrome
{
	public class MenuButtonHooks : ModSystem
	{
		internal static Rectangle MenuDrawBounds;
		internal static float VanillaMenuY = 220f;
		internal static float LastMenuBottom;
		internal static int MouseRemapY;
		private static int _remapYNext;
		private static bool _drawWaveHover;
		private static bool _passStarted;

		public override void Load()
		{
			IL_Main.DrawMenu += PatchMenuButtonPositions;
			IL_Main.DrawMenu += PatchMenuHoverColor;
			IL_Main.DrawMenu += PatchMenuHoverDrawString;
			On_Utils.DrawBorderStringBig += DrawBorderStringBigHook;
		}

		public override void Unload()
		{
			On_Utils.DrawBorderStringBig -= DrawBorderStringBigHook;
		}

		internal static void BeginFrame()
		{
			MouseRemapY = _remapYNext;
			_remapYNext = 0;
			_passStarted = false;
		}

		internal static Rectangle MenuHit()
		{
			if (!MenuDrawBounds.IsEmpty) {
				Rectangle drawn = MenuDrawBounds;
				drawn.Inflate(12, 8);
				return drawn;
			}

			Vector2 origin = SceneGraph.Pixel(SceneGraph.MenuButtons);
			return new Rectangle((int)(origin.X - 250f), (int)(origin.Y - 28f), 500, 7 * 68 + 24);
		}

		private void PatchMenuButtonPositions(ILContext il)
		{
			ILCursor cursor = new ILCursor(il);
			int offY = -1;
			if (!cursor.TryGotoNext(MoveType.After,
				i => i.MatchLdcI4(250),
				i => i.MatchStloc(out offY),
				i => i.MatchLdsfld(typeof(Main), nameof(Main.screenWidth)),
				i => i.MatchLdcI4(2),
				i => i.MatchDiv())) {
				Mod.Logger.Warn("Could not patch main menu button X.");
				return;
			}

			cursor.EmitDelegate<Func<int, int>>(ApplyMainMenuButtonX);
			if (offY < 0)
				return;

			cursor.Index = 0;
			if (!cursor.TryGotoNext(MoveType.After,
				i => i.MatchLdcI4(250),
				i => i.MatchStloc(offY),
				i => i.MatchLdsfld(typeof(Main), nameof(Main.screenWidth)))) {
				Mod.Logger.Warn("Could not patch main menu button Y.");
				return;
			}

			cursor.Index--;
			cursor.EmitLdloc(offY);
			cursor.EmitDelegate<Func<int, int>>(ApplyMainMenuButtonY);
			cursor.EmitStloc(offY);

			cursor.Index = 0;
			if (cursor.TryGotoNext(MoveType.After,
				i => i.MatchLdcI4(220),
				i => i.MatchStloc(offY),
				i => i.MatchLdcI4(7))) {
				cursor.Index--;
				cursor.EmitLdloc(offY);
				cursor.EmitDelegate<Func<int, int>>(ApplyMainMenuButtonY);
				cursor.EmitStloc(offY);
			}

			cursor.Index = 0;
			while (cursor.TryGotoNext(MoveType.After, i =>
				(i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt) &&
				i.Operand is MethodReference method &&
				method.Name == "AddMenuButtons")) {
				cursor.EmitLdloc(offY);
				cursor.EmitDelegate<Func<int, int>>(ApplyMainMenuButtonY);
				cursor.EmitStloc(offY);
				break;
			}
		}

		private void PatchMenuHoverColor(ILContext il)
		{
			ILCursor cursor = new ILCursor(il);
			if (!cursor.TryGotoNext(MoveType.Before, i => i.MatchLdcR4(215f)))
				return;

			Instruction green = cursor.Next;
			Instruction red = FindLdcR4Before(il, cursor.Index, 255f, 20);
			Instruction blue = FindLdcR4After(il, cursor.Index, 0f, 20);
			if (red == null || blue == null)
				return;

			ReplaceLdcR4(cursor, blue, GetFocusHoverB);
			ReplaceLdcR4(cursor, green, GetFocusHoverG);
			ReplaceLdcR4(cursor, red, GetFocusHoverR);
		}

		private void PatchMenuHoverDrawString(ILContext il)
		{
			MethodInfo drawString = typeof(DynamicSpriteFontExtensionMethods).GetMethod(
				nameof(DynamicSpriteFontExtensionMethods.DrawString),
				new[] {
					typeof(SpriteBatch), typeof(DynamicSpriteFont), typeof(string), typeof(Vector2),
					typeof(Color), typeof(float), typeof(Vector2), typeof(float), typeof(SpriteEffects), typeof(float)
				});
			if (drawString == null)
				return;

			ILCursor cursor = new ILCursor(il);
			while (cursor.TryGotoNext(MoveType.Before, i => i.MatchLdsfld(typeof(FontAssets), nameof(FontAssets.DeathText)))) {
				int at = cursor.Index;
				if (!cursor.TryGotoNext(MoveType.Before, i => i.MatchCall(drawString)))
					break;
				if (cursor.Index - at > 60) {
					cursor.Index = at + 1;
					continue;
				}

				cursor.Remove();
				cursor.EmitDelegate(DrawMenuItemString);
			}
		}

		private static Instruction FindLdcR4Before(ILContext il, int fromIndex, float value, int lookback)
		{
			int start = Math.Max(0, fromIndex - lookback);
			for (int i = fromIndex - 1; i >= start; i--) {
				if (il.Instrs[i].MatchLdcR4(value))
					return il.Instrs[i];
			}

			return null;
		}

		private static Instruction FindLdcR4After(ILContext il, int fromIndex, float value, int lookahead)
		{
			int end = Math.Min(il.Instrs.Count, fromIndex + lookahead);
			for (int i = fromIndex + 1; i < end; i++) {
				if (il.Instrs[i].MatchLdcR4(value))
					return il.Instrs[i];
			}

			return null;
		}

		private static void ReplaceLdcR4(ILCursor cursor, Instruction target, Func<float> getter)
		{
			cursor.Goto(target, MoveType.Before);
			cursor.Remove();
			cursor.EmitDelegate(getter);
		}

		private static float GetFocusHoverR()
		{
			if (!ShouldShift())
				return 255f;
			_drawWaveHover = true;
			return WeAccent.Hover.R;
		}

		private static float GetFocusHoverG() => ShouldShift() ? WeAccent.Hover.G : 215f;
		private static float GetFocusHoverB() => ShouldShift() ? WeAccent.Hover.B : 0f;

		private static void DrawMenuItemString(
			SpriteBatch spriteBatch,
			DynamicSpriteFont spriteFont,
			string text,
			Vector2 position,
			Color color,
			float rotation,
			Vector2 origin,
			float scale,
			SpriteEffects effects,
			float layerDepth)
		{
			bool wave = _drawWaveHover;
			_drawWaveHover = false;
			if (ShouldShift() && (WePanels.IsOpen || WeSplash.Visible || !SceneGraph.Visible(SceneGraph.MenuButtons)))
				return;

			if (ShouldShift()) {
				ShiftMenuItem(spriteFont, text, ref position, origin, scale);
				scale *= SceneGraph.ScaleOf(SceneGraph.MenuButtons);
			}

			if (!wave || string.IsNullOrEmpty(text) || color.A < 16) {
				spriteBatch.DrawString(spriteFont, text, position, color, rotation, origin, scale, effects, layerDepth);
				return;
			}

			spriteBatch.DrawString(spriteFont, text, position, WeAccent.Hover, rotation, origin, scale, effects, layerDepth);
		}

		private static Vector2 DrawBorderStringBigHook(
			On_Utils.orig_DrawBorderStringBig orig,
			SpriteBatch spriteBatch,
			string text,
			Vector2 pos,
			Color color,
			float scale,
			float anchorx,
			float anchory,
			int maxCharactersDisplayed)
		{
			if (ShouldShift() && IsTitleMenuButton(text, pos)) {
				if (WePanels.IsOpen || WeSplash.Visible || !SceneGraph.Visible(SceneGraph.MenuButtons))
					return Vector2.Zero;

				ShiftLayout(ref pos, text, scale, anchorx, anchory);
				scale *= SceneGraph.ScaleOf(SceneGraph.MenuButtons);
			}

			return orig(spriteBatch, text, pos, color, scale, anchorx, anchory, maxCharactersDisplayed);
		}

		private static bool IsTitleMenuButton(string text, Vector2 pos)
		{
			if (string.IsNullOrEmpty(text) || Main.menuMode != 0)
				return false;
			Vector2 menu = SceneGraph.Pixel(SceneGraph.MenuButtons);
			return Math.Abs(pos.X - menu.X) <= 360f || Math.Abs(pos.X - Main.screenWidth * 0.5f) <= 360f;
		}

		private static void ShiftMenuItem(DynamicSpriteFont font, string text, ref Vector2 position, Vector2 origin, float scale)
		{
			if (string.IsNullOrEmpty(text))
				return;

			Vector2 menu = SceneGraph.Pixel(SceneGraph.MenuButtons);
			if (Math.Abs(position.X - menu.X) > 360f && Math.Abs(position.X - Main.screenWidth * 0.5f) > 360f)
				return;

			float layoutY = position.Y - origin.Y * scale;
			BeginPass(layoutY);
			position.Y += menu.Y - VanillaMenuY;
			_remapYNext = (int)Math.Round(menu.Y - VanillaMenuY);
			Vector2 size = font.MeasureString(text) * scale;
			NoteBounds(
				(int)MathF.Round(position.X - origin.X * scale),
				(int)MathF.Round(position.Y - origin.Y * scale),
				Math.Max(1, (int)MathF.Ceiling(size.X)),
				Math.Max(1, (int)MathF.Ceiling(size.Y)));
		}

		private static void ShiftLayout(ref Vector2 pos, string text, float scale, float anchorx, float anchory)
		{
			Vector2 menu = SceneGraph.Pixel(SceneGraph.MenuButtons);
			BeginPass(pos.Y);
			pos.Y += menu.Y - VanillaMenuY;
			_remapYNext = (int)Math.Round(menu.Y - VanillaMenuY);
			var font = FontAssets.DeathText.Value;
			Vector2 size = font.MeasureString(text) * scale;
			NoteBounds(
				(int)MathF.Round(pos.X - size.X * anchorx),
				(int)MathF.Round(pos.Y - size.Y * anchory),
				Math.Max(1, (int)MathF.Ceiling(size.X)),
				Math.Max(1, (int)MathF.Ceiling(size.Y)));
		}

		private static void BeginPass(float layoutY)
		{
			if (_passStarted)
				return;
			_passStarted = true;
			MenuDrawBounds = Rectangle.Empty;
			VanillaMenuY = layoutY;
		}

		private static void NoteBounds(int x, int y, int w, int h)
		{
			var rect = new Rectangle(x, y, w, h);
			MenuDrawBounds = MenuDrawBounds.IsEmpty ? rect : Rectangle.Union(MenuDrawBounds, rect);
			LastMenuBottom = MenuDrawBounds.Bottom;
		}

		private static int ApplyMainMenuButtonY(int offY)
		{
			if (!ShouldShift())
				return offY;
			if (WePanels.IsOpen || WeSplash.Visible || !SceneGraph.Visible(SceneGraph.MenuButtons))
				return 5000;
			MenuDrawBounds = Rectangle.Empty;
			return (int)Math.Round(SceneGraph.Pixel(SceneGraph.MenuButtons).Y);
		}

		private static int ApplyMainMenuButtonX(int menuCenterX)
		{
			if (!ShouldShift())
				return menuCenterX;
			return (int)Math.Round(SceneGraph.Pixel(SceneGraph.MenuButtons).X);
		}

		private static bool ShouldShift() => WeModMenu.OnTitle;
	}
}
