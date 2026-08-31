using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using WallpaperEngine.Core;
using WallpaperEngine.Layout;
using WallpaperEngine.UI;

namespace WallpaperEngine.Widgets
{
	internal static class MoonWidget
	{
		internal static bool Enabled => WeSave.Data.MoonWidget && SceneGraph.Visible(SceneGraph.Moon);

		internal static Vector2 Anchor => SceneGraph.Pixel(SceneGraph.Moon);

		internal static float Scale => SceneGraph.ScaleOf(SceneGraph.Moon);

		internal static Rectangle HitRect() => RoundButton.Hit(Anchor, 26f * Scale);

		internal static void Draw(SpriteBatch spriteBatch, float fade)
		{
			if (!Enabled || fade <= 0f)
				return;

			Vector2 center = Anchor;
			float radius = 26f * Scale;
			WeDraw.WithLinear(spriteBatch, () => RoundButton.Draw(spriteBatch, center, radius, fade));

			Texture2D moon = MoonTexture();
			if (moon != null) {
				Rectangle src = Frame(moon);
				WeDraw.WithPoint(spriteBatch, () => {
					float size = radius * 1.5f;
					float scale = size / Math.Max(1, Math.Max(src.Width, src.Height));
					spriteBatch.Draw(
						moon, center, src, Color.White * fade, 0f,
						new Vector2(src.Width, src.Height) * 0.5f, scale, SpriteEffects.None, 0f);
				});
			}

			WeDraw.WithLinear(spriteBatch, () =>
				RoundButton.Tooltip(spriteBatch, center, radius, WeText.UI("MoonPhase" + Math.Clamp(Main.moonPhase, 0, 7)), fade));
		}

		internal static void DrawPreview(SpriteBatch spriteBatch, Rectangle box, float fade)
		{
			if (fade <= 0.02f || box.Width < 8 || box.Height < 8)
				return;

			Vector2 center = box.Center.ToVector2();
			float radius = Math.Min(box.Width, box.Height) * 0.36f;
			RoundButton.Draw(spriteBatch, center, radius, fade);
			Texture2D moon = MoonTexture();
			if (moon == null || moon.IsDisposed)
				return;

			Rectangle src = Frame(moon);
			float size = radius * 1.5f;
			float scale = size / Math.Max(1, Math.Max(src.Width, src.Height));
			spriteBatch.Draw(
				moon, center, src, Color.White * fade, 0f,
				new Vector2(src.Width, src.Height) * 0.5f, scale, SpriteEffects.None, 0f);
		}

		private static Texture2D MoonTexture()
		{
			try {
				if (TextureAssets.Moon == null || TextureAssets.Moon.Length == 0)
					return null;
				int type = Math.Clamp(Main.moonType, 0, TextureAssets.Moon.Length - 1);
				Texture2D tex = TextureAssets.Moon[type]?.Value;
				return tex == null || tex.IsDisposed ? null : tex;
			}
			catch {
				return null;
			}
		}

		private static Rectangle Frame(Texture2D tex)
		{
			int phase = Math.Clamp(Main.moonPhase, 0, 7);
			if (tex.Height >= tex.Width * 4) {
				int h = Math.Max(1, tex.Height / 8);
				return new Rectangle(0, h * phase, tex.Width, h);
			}

			if (tex.Width >= tex.Height * 4) {
				int w = Math.Max(1, tex.Width / 8);
				return new Rectangle(w * phase, 0, w, tex.Height);
			}

			return tex.Bounds;
		}
	}
}
