using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using WallpaperEngine.Core;
using WallpaperEngine.Grab;
using WallpaperEngine.Layout;
using WallpaperEngine.UI;

namespace WallpaperEngine.Content
{
	internal static class WeLogo
	{
		internal static Vector2 Anchor => SceneGraph.Pixel(SceneGraph.Logo);
		internal static float Scale => SceneGraph.ScaleOf(SceneGraph.Logo);

		internal static Rectangle HitRect()
		{
			Texture2D tex = CurrentTexture();
			if (tex == null)
				return Around(Anchor, 420f * Scale, 140f * Scale);

			float s = DrawScale(tex);
			return new Rectangle(
				(int)(Anchor.X - tex.Width * s * 0.5f),
				(int)(Anchor.Y - tex.Height * s * 0.5f),
				Math.Max(1, (int)(tex.Width * s)),
				Math.Max(1, (int)(tex.Height * s)));
		}

		internal static Texture2D CurrentTexture()
		{
			if (WeSave.Data.Logo == LogoKind.Hidden)
				return null;
			if (WeSave.Data.Logo == LogoKind.Preset)
				return WePresetLogos.BaseOf(WeSave.Data.LogoId);
			if (WeSave.Data.Logo == LogoKind.Custom && WeArt.TryGetLogo(out Texture2D custom))
				return custom;
			if (WeSave.Data.Logo == LogoKind.Borrowed)
				return WeCatalog.LogoTexture(WeSave.Data.LogoId);
			return ModContent.GetInstance<WeModMenu>()?.Logo?.Value;
		}

		internal static float DrawScale(Texture2D tex)
		{
			if (tex == null)
				return Scale;
			float cap = MathHelper.Min(520f, Main.screenWidth * 0.38f);
			return cap / Math.Max(1, tex.Width) * Scale;
		}

		internal static void DrawCustom(SpriteBatch spriteBatch, float fade, float rotation, float bounce)
		{
			if (!SceneGraph.Visible(SceneGraph.Logo))
				return;

			WeLook.StabilizeLogo(ref rotation, ref bounce);
			bounce = MathHelper.Clamp(bounce, 0.5f, 1.6f);
			if (WeSave.Data.Logo == LogoKind.Preset && WePresetLogos.Draw(spriteBatch, fade, rotation, bounce))
				return;

			if (WeSave.Data.Logo == LogoKind.Borrowed &&
			    WeBorrow.TryDrawLogo(spriteBatch, Anchor, Scale, fade, rotation, bounce))
				return;

			if (WeSave.Data.Logo == LogoKind.Borrowed) {
				Texture2D borrowed = CurrentTexture();
				if (borrowed == null)
					return;
				float borrowedScale = DrawScale(borrowed) * bounce;
				WeDraw.WithPoint(spriteBatch, () => {
					spriteBatch.Draw(borrowed, Anchor, null, Color.White * fade, rotation, borrowed.Size() * 0.5f, borrowedScale, SpriteEffects.None, 0f);
				});
				return;
			}

			WeDraw.WithLinear(spriteBatch, () => {
				Texture2D tex = CurrentTexture();
				if (tex == null)
					return;
				float scale = DrawScale(tex) * bounce;
				spriteBatch.Draw(tex, Anchor, null, Color.White * fade, rotation, tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
			}, WeSave.Data.Logo == LogoKind.Custom ? WeAnim.AdvanceActive : null);
		}

		internal static bool ShouldDrawVanilla(ref Vector2 logoDrawCenter, ref float logoScale)
		{
			if (!SceneGraph.Visible(SceneGraph.Logo) || WeSave.Data.Logo == LogoKind.Hidden)
				return false;
			if (WeSave.Data.Logo is LogoKind.Custom or LogoKind.Borrowed or LogoKind.Preset)
				return false;

			logoDrawCenter = Anchor;
			logoScale *= Scale;
			return true;
		}

		private static Rectangle Around(Vector2 pos, float w, float h) =>
			new((int)(pos.X - w * 0.5f), (int)(pos.Y - h * 0.5f), (int)w, (int)h);
	}
}
