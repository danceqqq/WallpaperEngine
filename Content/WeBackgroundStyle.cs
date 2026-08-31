using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader;
using WallpaperEngine.Core;
using WallpaperEngine.UI;

namespace WallpaperEngine.Content
{
	public class WeBackgroundStyle : ModSurfaceBackgroundStyle
	{
		internal static bool DrewThisFrame { get; set; }

		public override void ModifyFarFades(float[] fades, float transitionSpeed)
		{
			for (int i = 0; i < fades.Length; i++)
				fades[i] = 0f;
		}

		public override bool PreDrawCloseBackground(SpriteBatch spriteBatch)
		{
			if (!WeSettings.HasCustomSky)
				return true;

			Draw(spriteBatch);
			return false;
		}

		internal static void Draw(SpriteBatch spriteBatch)
		{
			if (DrewThisFrame)
				return;

			DrewThisFrame = true;
			WeWallpaper.DrawBack(spriteBatch);
		}

		internal static void DrawAtmosphere(SpriteBatch spriteBatch)
		{
			float dim = MathHelper.Clamp(WeSave.Data.WallpaperDim, 0f, 1f);
			float vignette = MathHelper.Clamp(WeSave.Data.WallpaperVignette, 0f, 1f);
			if (dim < 0.01f && vignette < 0.01f)
				return;

			WeDraw.WithLinear(spriteBatch, () => {
				if (dim >= 0.01f)
					WeDraw.Fill(spriteBatch, WeDraw.CoverRect, Color.Black * dim);
				if (vignette >= 0.01f)
					WeDraw.DrawVignette(spriteBatch, vignette);
			});
		}

		internal static void EndFrame() => DrewThisFrame = false;
	}
}
