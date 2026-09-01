using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using WallpaperEngine.Content;
using WallpaperEngine.Core;
using WallpaperEngine.UI;

namespace WallpaperEngine.Grab
{
	internal static class WeBorrow
	{
		private static readonly HashSet<string> FailedHooks = new(StringComparer.Ordinal);
		private static bool _drawing;

		internal static void Unload()
		{
			FailedHooks.Clear();
			_drawing = false;
		}

		internal static bool TryDrawSky(SpriteBatch spriteBatch, string id)
		{
			if (_drawing || string.IsNullOrEmpty(id) || spriteBatch == null)
				return false;

			WeOffer offer = WeCatalog.FindSky(id);
			if (offer == null)
				return false;

			ModMenu menu = WeCatalog.FindMenu(id);
			if (menu == null)
				return false;

			bool live = offer.UseThemeFx || WeModArt.HasHostSky(menu);
			if (offer.Pending && !live)
				return false;

			_drawing = true;
			bool painted = false;
			try {
				WeModArt.PrimeLiveSky(menu);
				if (WeModArt.HasHostSky(menu))
					painted = WeModArt.TryDrawHostSky(spriteBatch, menu);
				else if (offer.UseThemeFx)
					painted |= WeBorrowFx.TryRunSky(spriteBatch, id);

				if (!painted && offer.UseStyle)
					painted = DrawStyle(spriteBatch, id, WeCatalog.StyleOf(id));
				if (!painted && !live && offer.UseMenuScene)
					painted = DrawCover(spriteBatch, WeCatalog.MenuScene(id));
			}
			catch {
				painted = false;
			}
			finally {
				RestoreUi(spriteBatch);
				_drawing = false;
			}

			return painted;
		}

		internal static bool TryDrawLogo(SpriteBatch spriteBatch, Vector2 anchor, float layoutScale, float fade, float rotation, float bounce)
		{
			if (_drawing || spriteBatch == null)
				return false;
			if (WeSave.Data.Logo != LogoKind.Borrowed)
				return false;

			string id = WeSave.Data.LogoId;
			if (string.IsNullOrEmpty(id) || WeCatalog.FindMenu(id) == null)
				return false;

			_drawing = true;
			bool drew;
			try {
				drew = WeBorrowFx.TryRunLogo(spriteBatch, id, anchor, layoutScale, fade, rotation, bounce);
			}
			catch {
				drew = false;
			}
			finally {
				RestoreUi(spriteBatch);
				_drawing = false;
			}

			return drew;
		}

		private static bool DrawStyle(SpriteBatch spriteBatch, string id, ModSurfaceBackgroundStyle style)
		{
			if (style == null || !WeCatalog.IsDrawableStyle(style))
				return false;

			bool drew = false;
			drew |= DrawSlot(spriteBatch, CallFar(style), horizon: false);
			drew |= DrawSlot(spriteBatch, CallMid(style), horizon: false);

			bool wantClose = true;
			if (!FailedHooks.Contains(id) &&
			    WeCatalog.OverridesMethod(style, nameof(ModSurfaceBackgroundStyle.PreDrawCloseBackground))) {
				try {
					wantClose = style.PreDrawCloseBackground(spriteBatch);
					drew = true;
				}
				catch {
					FailedHooks.Add(id);
					wantClose = true;
				}
			}

			if (wantClose)
				drew |= DrawSlot(spriteBatch, CallClose(style), horizon: true);

			if (!drew) {
				Texture2D preview = WeCatalog.StyleArt(style);
				if (preview != null)
					drew = DrawCover(spriteBatch, preview);
			}

			return drew;
		}

		private static int CallFar(ModSurfaceBackgroundStyle style)
		{
			try {
				return style.ChooseFarTexture();
			}
			catch {
				return 0;
			}
		}

		private static int CallMid(ModSurfaceBackgroundStyle style)
		{
			try {
				return style.ChooseMiddleTexture();
			}
			catch {
				return 0;
			}
		}

		private static int CallClose(ModSurfaceBackgroundStyle style)
		{
			try {
				float scale = 1f;
				double parallax = 0d;
				float a = 0f;
				float b = 0f;
				return style.ChooseCloseTexture(ref scale, ref parallax, ref a, ref b);
			}
			catch {
				return 0;
			}
		}

		private static bool DrawSlot(SpriteBatch spriteBatch, int slot, bool horizon)
		{
			Texture2D tex = WeCatalog.TextureOfSlot(slot);
			if (tex == null)
				return false;
			return horizon ? DrawHorizon(spriteBatch, tex) : DrawCover(spriteBatch, tex);
		}

		private static bool DrawCover(SpriteBatch spriteBatch, Texture2D tex)
		{
			if (tex == null || tex.IsDisposed)
				return false;

			Vector2 pan = new(0.5f, 0.5f);
			if (WeSave.Data.WallpaperParallax) {
				Vector2 shift = WeFx.MouseShift(0.12f);
				pan.X = MathHelper.Clamp(0.5f + shift.X / Math.Max(1, Main.screenWidth), 0f, 1f);
				pan.Y = MathHelper.Clamp(0.5f + shift.Y / Math.Max(1, Main.screenHeight), 0f, 1f);
			}

			Rectangle dest = WeDraw.CoverDestination(tex, pan);
			spriteBatch.Draw(tex, dest, Color.White);
			return true;
		}

		private static bool DrawHorizon(SpriteBatch spriteBatch, Texture2D tex)
		{
			if (tex == null || tex.IsDisposed)
				return false;

			Rectangle cover = WeDraw.UiRect;
			float scale = cover.Width / (float)Math.Max(1, tex.Width);
			int w = Math.Max(1, (int)(tex.Width * scale));
			int h = Math.Max(1, (int)(tex.Height * scale));
			spriteBatch.Draw(tex, new Rectangle((cover.Width - w) / 2, cover.Height - h, w, h), Color.White);
			return true;
		}

		private static void RestoreUi(SpriteBatch spriteBatch)
		{
			try {
				spriteBatch.End();
			}
			catch {
			}

			try {
				WeDraw.BeginUi(spriteBatch);
			}
			catch {
			}
		}
	}
}
