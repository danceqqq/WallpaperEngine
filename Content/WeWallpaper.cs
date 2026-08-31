using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using WallpaperEngine.Core;
using WallpaperEngine.Grab;
using WallpaperEngine.Layout;
using WallpaperEngine.UI;

namespace WallpaperEngine.Content
{
	internal static class WeWallpaper
	{
		internal static void DrawBack(SpriteBatch spriteBatch)
		{
			WeDraw.WithLinear(spriteBatch, () => {
				if (WeSettings.HasCustomSky)
					DrawBackplate(spriteBatch);

				foreach (WeLayerRecord layer in WeSave.Data.Layers) {
					if (layer == null || !layer.Visible || layer.Foreground)
						continue;
					DrawLayer(spriteBatch, layer);
				}
			});
		}

		internal static void DrawFore(SpriteBatch spriteBatch)
		{
			bool any = false;
			foreach (WeLayerRecord layer in WeSave.Data.Layers) {
				if (layer != null && layer.Visible && layer.Foreground)
					any = true;
			}

			if (!any)
				return;

			WeDraw.WithLinear(spriteBatch, () => {
				foreach (WeLayerRecord layer in WeSave.Data.Layers) {
					if (layer == null || !layer.Visible || !layer.Foreground)
						continue;
					DrawLayer(spriteBatch, layer);
				}
			});
		}

		private static void DrawBackplate(SpriteBatch spriteBatch)
		{
			switch (WeSave.Data.Wallpaper) {
				case WallpaperKind.Color:
					WeDraw.Fill(spriteBatch, WeDraw.CoverRect, WeSettings.WallpaperColorA);
					break;
				case WallpaperKind.Gradient:
					WeDraw.DrawVerticalGradient(spriteBatch, WeDraw.CoverRect, WeSettings.WallpaperColorA, WeSettings.WallpaperColorB, 1f);
					break;
				case WallpaperKind.Image:
					if (!HasImageBack())
						WeDraw.Fill(spriteBatch, WeDraw.CoverRect, WeSettings.WallpaperColorA);
					break;
				case WallpaperKind.Borrowed:
					if (!WeBorrow.TryDrawSky(spriteBatch, WeSave.Data.WallpaperId))
						WeDraw.Fill(spriteBatch, WeDraw.CoverRect, WeSettings.WallpaperColorA);
					break;
				default:
					WeDraw.Fill(spriteBatch, WeDraw.CoverRect, WeSettings.WallpaperColorA);
					break;
			}
		}

		private static bool HasImageBack()
		{
			foreach (WeLayerRecord layer in WeSave.Data.Layers) {
				if (layer.Visible && !layer.Foreground && layer.Kind == WeLayerKind.Image && !string.IsNullOrEmpty(layer.ArtId))
					return true;
			}

			return WeArt.TryGetWallpaper(out _);
		}

		private static void DrawLayer(SpriteBatch spriteBatch, WeLayerRecord layer)
		{
			if (layer.Kind == WeLayerKind.Effect) {
				WeFx.Draw(spriteBatch, layer);
				return;
			}

			if (!WeArt.TryGetWallpaper(layer.ArtId, out Texture2D tex))
				return;

			Vector2 pan = LayoutEditor.Editing && layer.Id == WeSave.Data.SelectedLayerId
				? LayoutEditor.WorkPan
				: new Vector2(layer.PanX, layer.PanY);
			Vector2 shift = WeFx.MouseShift(layer.Parallax);
			pan.X = MathHelper.Clamp(pan.X + shift.X / Math.Max(1, Main.screenWidth), 0f, 1f);
			pan.Y = MathHelper.Clamp(pan.Y + shift.Y / Math.Max(1, Main.screenHeight), 0f, 1f);
			Rectangle dest = WeDraw.ImageDestination(tex, pan, layer.Fit);
			if (layer.Zoom > 1.01f || layer.Zoom < 0.99f) {
				int cx = dest.X + dest.Width / 2;
				int cy = dest.Y + dest.Height / 2;
				int w = (int)(dest.Width * layer.Zoom);
				int h = (int)(dest.Height * layer.Zoom);
				dest = new Rectangle(cx - w / 2, cy - h / 2, w, h);
			}

			spriteBatch.Draw(tex, dest, Color.White * MathHelper.Clamp(layer.Opacity, 0f, 1f));
		}
	}
}
