using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;

namespace WallpaperEngine.Grab
{
	internal static class WeInspect
	{
		private static readonly Dictionary<int, byte> Corner = new();

		internal static void Unload() => Corner.Clear();

		internal static bool IsLogo(Texture2D tex, string assetName)
		{
			if (LooksLikeJunkName(assetName))
				return false;
			if (LooksLikeLogoName(assetName) && !LooksLikeSceneName(assetName))
				return true;
			if (tex == null || tex.IsDisposed)
				return false;
			return HasClearCorners(tex);
		}

		internal static bool IsScene(Texture2D tex, string assetName)
		{
			if (LooksLikeJunkName(assetName))
				return false;
			if (LooksLikeLogoName(assetName) && !LooksLikeSceneName(assetName))
				return false;
			if (LooksLikeSceneName(assetName) && !HasClearCorners(tex))
				return true;
			return IsOpaqueLandscape(tex);
		}

		internal static bool LooksLikeSceneName(string name)
		{
			if (string.IsNullOrEmpty(name))
				return false;
			return Contains(name, "Background") ||
			       Contains(name, "Backdrop") ||
			       Contains(name, "Wallpaper") ||
			       Contains(name, "TitleSky") ||
			       Contains(name, "MenuSky") ||
			       Contains(name, "MenuBG") ||
			       EndsWithToken(name, "Sky") ||
			       EndsWithToken(name, "Bg") ||
			       EndsWithToken(name, "BG");
		}

		internal static bool LooksLikeLogoName(string name)
		{
			if (string.IsNullOrEmpty(name))
				return false;
			return Contains(name, "Logo") ||
			       Contains(name, "TitleCard") ||
			       Contains(name, "Wordmark") ||
			       Contains(name, "WordMark");
		}

		internal static bool IsCoverSized(Texture2D tex) =>
			tex != null && !tex.IsDisposed && tex.Width >= 480 && tex.Height >= 270;

		internal static bool IsFillPixel(Texture2D tex) =>
			tex != null && !tex.IsDisposed && tex.Width <= 4 && tex.Height <= 4;

		internal static bool IsIcon(Texture2D tex)
		{
			if (tex == null || tex.IsDisposed || IsFillPixel(tex))
				return false;
			if (tex.Width > 256 || tex.Height > 256)
				return false;
			float aspect = tex.Width / (float)Math.Max(1, tex.Height);
			return aspect is >= 0.55f and <= 1.65f;
		}

		internal static bool IsWordmark(Texture2D tex)
		{
			if (tex == null || tex.IsDisposed || IsIcon(tex))
				return false;
			float aspect = tex.Width / (float)Math.Max(1, tex.Height);
			return tex.Width >= 140 && tex.Height is >= 40 and <= 480 && aspect >= 1.65f;
		}

		internal static bool LooksLikeStillName(string name)
		{
			if (string.IsNullOrEmpty(name))
				return false;
			return Contains(name, "ShaderSource") ||
			       Contains(name, "UniverseImager") ||
			       Contains(name, "MenuBackground") ||
			       Contains(name, "MapBackground");
		}

		internal static bool LooksLikeJunkName(string name)
		{
			if (string.IsNullOrEmpty(name))
				return false;
			if (LooksLikeStillName(name))
				return false;
			return Contains(name, "Noise") ||
			       Contains(name, "Mask") ||
			       Contains(name, "Palette") ||
			       Contains(name, "Displace") ||
			       Contains(name, "Shader") ||
			       Contains(name, "Gradient") ||
			       Contains(name, "Ember") ||
			       Contains(name, "Particle") ||
			       Contains(name, "Dust") ||
			       Contains(name, "Warble") ||
			       Contains(name, "Cracks") ||
			       Contains(name, "Bloom") ||
			       Contains(name, "Overlay") ||
			       Contains(name, "Glow");
		}

		internal static string AssetName(Asset<Texture2D> asset)
		{
			try {
				return asset?.Name ?? "";
			}
			catch {
				return "";
			}
		}

		internal static int SceneScore(Texture2D tex, string assetName)
		{
			if (tex == null || tex.IsDisposed || LooksLikeJunkName(assetName) || IsLogo(tex, assetName))
				return 0;

			int area = Math.Max(1, tex.Width * tex.Height);
			int score = area;
			if (LooksLikeSceneName(assetName))
				score *= 3;
			if (IsOpaqueLandscape(tex))
				score *= 2;
			if (LooksLikeLogoName(assetName))
				score /= 8;
			return score;
		}

		private static bool IsOpaqueLandscape(Texture2D tex)
		{
			if (tex == null || tex.IsDisposed)
				return false;
			if (tex.Width < 640 || tex.Height < 360)
				return false;

			float aspect = tex.Width / (float)Math.Max(1, tex.Height);
			if (aspect is < 1.2f or > 2.5f)
				return false;

			bool? opaque = CornersOpaque(tex);
			return opaque == true;
		}

		private static bool HasClearCorners(Texture2D tex)
		{
			bool? opaque = CornersOpaque(tex);
			return opaque == false;
		}

		private static bool? CornersOpaque(Texture2D tex)
		{
			if (tex == null || tex.IsDisposed)
				return null;

			int key = tex.GetHashCode();
			if (Corner.TryGetValue(key, out byte cached)) {
				if (cached == 1)
					return false;
				if (cached == 2)
					return true;
				return null;
			}

			if (tex.Format != SurfaceFormat.Color) {
				Corner[key] = 0;
				return null;
			}

			try {
				var pixel = new Color[1];
				int w = Math.Max(0, tex.Width - 1);
				int h = Math.Max(0, tex.Height - 1);
				int[] xs = { 0, w };
				int[] ys = { 0, h };
				bool allOpaque = true;
				foreach (int y in ys) {
					foreach (int x in xs) {
						tex.GetData(0, new Rectangle(x, y, 1, 1), pixel, 0, 1);
						if (pixel[0].A < 180)
							allOpaque = false;
					}
				}

				Corner[key] = (byte)(allOpaque ? 2 : 1);
				return allOpaque;
			}
			catch {
				Corner[key] = 0;
				return null;
			}
		}

		private static bool Contains(string name, string token) =>
			name.Contains(token, StringComparison.OrdinalIgnoreCase);

		private static bool EndsWithToken(string name, string token)
		{
			int i = name.LastIndexOf(token, StringComparison.OrdinalIgnoreCase);
			if (i < 0)
				return false;
			int end = i + token.Length;
			return end == name.Length || name[end] == '/' || name[end] == '_' || name[end] == '.';
		}
	}
}
