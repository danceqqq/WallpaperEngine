using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;
using WallpaperEngine.Audio;
using WallpaperEngine.Core;
using WallpaperEngine.UI;

namespace WallpaperEngine.Content
{
	internal static class WePresetLogos
	{
		internal const string Watermelon1 = "watermelon1";
		internal const string Watermelon2 = "watermelon2";
		internal const string BlueArchive = "bluearchive";

		internal static readonly string[] Ids = { Watermelon1, Watermelon2, BlueArchive };

		private const float StickerLeft = 22f;
		private const float StickerTop = 23f;
		private const float StickerRight = 469f;
		private const float StickerBottom = 428f;
		private const float HandsLayerX = 241f;
		private const float HandsLayerY = 227f;
		private const float BaShineX = 1016f;
		private const float BaShineY = 398f;

		private static readonly Color Ruby = new(196, 48, 78);
		private static readonly Color ShinePink = new(255, 186, 198);

		private static Asset<Texture2D> _wm1Base;
		private static Asset<Texture2D> _wm1Sticker;
		private static Asset<Texture2D> _wm1Prev;
		private static Asset<Texture2D> _wm2Base;
		private static Asset<Texture2D> _wm2Hands;
		private static Asset<Texture2D> _wm2Prev;
		private static Asset<Texture2D> _ba;
		private static Asset<Texture2D> _packIcon;
		private static Texture2D _shine;

		internal static bool IsKnown(string id) =>
			id == Watermelon1 || id == Watermelon2 || id == BlueArchive;

		internal static string TitleKey(string id) => id switch {
			Watermelon1 => "LogoWatermelon1",
			Watermelon2 => "LogoWatermelon2",
			_ => "LogoBlueArchive"
		};

		internal static void Load()
		{
			_wm1Base = Req("Logo/WatermelonStickerStyle/withoutsticker");
			_wm1Sticker = Req("Logo/WatermelonStickerStyle/sticker");
			_wm1Prev = Req("Logo/WatermelonStickerStyle/prev");
			_wm2Base = Req("Logo/WatermelonStyle/withouthand");
			_wm2Hands = Req("Logo/WatermelonStyle/hand");
			_wm2Prev = Req("Logo/WatermelonStyle/Prev");
			_ba = Req("Logo/BlueArchiveStyleLogo");
			_packIcon = Req("Logo/LogoMod/DieWithASmile");
		}

		internal static void Unload()
		{
			_wm1Base = null;
			_wm1Sticker = null;
			_wm1Prev = null;
			_wm2Base = null;
			_wm2Hands = null;
			_wm2Prev = null;
			_ba = null;
			_packIcon = null;
			Texture2D shine = _shine;
			_shine = null;
			if (shine == null || shine.IsDisposed)
				return;

			Main.QueueMainThreadAction(() => {
				try {
					if (!shine.IsDisposed)
						shine.Dispose();
				}
				catch {
				}
			});
		}

		internal static Texture2D PackIcon() => Tex(_packIcon);

		internal static Texture2D BaseOf(string id) => id switch {
			Watermelon1 => Tex(_wm1Base),
			Watermelon2 => Tex(_wm2Base),
			BlueArchive => Tex(_ba),
			_ => null
		};

		internal static Texture2D PreviewOf(string id) => id switch {
			Watermelon1 => Tex(_wm1Prev) ?? Tex(_wm1Base),
			Watermelon2 => Tex(_wm2Prev) ?? Tex(_wm2Base),
			BlueArchive => Tex(_ba),
			_ => null
		};

		internal static bool Draw(SpriteBatch spriteBatch, float fade, float rotation, float bounce)
		{
			if (WeSave.Data.Logo != LogoKind.Preset || !IsKnown(WeSave.Data.LogoId))
				return false;

			Texture2D logo = BaseOf(WeSave.Data.LogoId);
			if (logo == null)
				return false;

			bounce = MathHelper.Clamp(bounce, 0.5f, 1.6f);
			float scale = WeLogo.DrawScale(logo) * bounce;
			Vector2 center = WeLogo.Anchor;
			string id = WeSave.Data.LogoId;

			WeDraw.WithLinear(spriteBatch, () => {
				if (id == BlueArchive)
					DrawHalo(spriteBatch, center, logo, scale, fade, rotation);

				spriteBatch.Draw(
					logo, center, null, Color.White * fade, rotation,
					logo.Size() * 0.5f, scale, SpriteEffects.None, 0f);

				if (id == Watermelon1)
					DrawSticker(spriteBatch, center, logo, scale, fade, rotation);
				else if (id == Watermelon2)
					DrawHands(spriteBatch, center, logo, scale, fade, rotation);
				else if (id == BlueArchive)
					DrawShine(spriteBatch, center, logo, scale, fade, rotation);
			});
			return true;
		}

		private static void DrawSticker(
			SpriteBatch spriteBatch, Vector2 center, Texture2D logo, float scale, float fade, float rotation)
		{
			Texture2D sticker = Tex(_wm1Sticker);
			if (sticker == null)
				return;

			float slotW = StickerRight - StickerLeft;
			float slotH = StickerBottom - StickerTop;
			var slotCenter = new Vector2((StickerLeft + StickerRight) * 0.5f, (StickerTop + StickerBottom) * 0.5f);
			float fit = Math.Min(slotW / Math.Max(1, sticker.Width), slotH / Math.Max(1, sticker.Height));
			float pulse = 1f - 0.12f * MathF.Abs(MathF.Sin(Main.GlobalTimeWrappedHourly * 2.4f));
			Vector2 pos = LocalToWorld(center, logo, scale, rotation, slotCenter);
			spriteBatch.Draw(
				sticker, pos, null, Color.White * fade, rotation,
				sticker.Size() * 0.5f, scale * fit * pulse, SpriteEffects.None, 0f);
		}

		private static void DrawHands(
			SpriteBatch spriteBatch, Vector2 center, Texture2D logo, float scale, float fade, float rotation)
		{
			Texture2D hands = Tex(_wm2Hands);
			if (hands == null)
				return;

			float bob = MathF.Sin(Main.GlobalTimeWrappedHourly * 1.05f) * 16f;
			var local = new Vector2(HandsLayerX + hands.Width * 0.5f, HandsLayerY + bob + hands.Height * 0.5f);
			Vector2 pos = LocalToWorld(center, logo, scale, rotation, local);
			spriteBatch.Draw(
				hands, pos, null, Color.White * fade, rotation,
				hands.Size() * 0.5f, scale, SpriteEffects.None, 0f);
		}

		private static void DrawHalo(
			SpriteBatch spriteBatch, Vector2 center, Texture2D logo, float scale, float fade, float rotation)
		{
			Texture2D halo = ShineTexture();
			if (halo == null)
				return;

			float beat = Beat();
			float haloPulse = 0.07f + 0.12f * beat;
			var haloScale = new Vector2(
				logo.Width * scale * 1.15f / halo.Width,
				logo.Height * scale * 1.55f / halo.Height);
			spriteBatch.Draw(
				halo, center, null, Ruby * (haloPulse * fade), rotation,
				halo.Size() * 0.5f, haloScale, SpriteEffects.None, 0f);
		}

		private static void DrawShine(
			SpriteBatch spriteBatch, Vector2 center, Texture2D logo, float scale, float fade, float rotation)
		{
			float beat = Beat();
			float pulse = 0.58f + 0.42f * beat;
			Vector2 pos = LocalToWorld(center, logo, scale, rotation, new Vector2(BaShineX, BaShineY));
			DrawShineAt(spriteBatch, pos, scale * 5.8f, fade, pulse);
		}

		private static void DrawShineAt(SpriteBatch spriteBatch, Vector2 position, float size, float fade, float pulse)
		{
			Texture2D shine = ShineTexture();
			if (shine == null || fade <= 0f)
				return;

			Vector2 origin = shine.Size() * 0.5f;
			float time = Main.GlobalTimeWrappedHourly;
			pulse = MathHelper.Clamp(pulse, 0.15f, 1f);

			spriteBatch.Draw(shine, position, null, Ruby * (0.42f * pulse * fade), 0f, origin, 0.72f * size, SpriteEffects.None, 0f);
			spriteBatch.Draw(shine, position, null, ShinePink * (0.38f * pulse * fade), 0f, origin, 0.28f * size * (0.85f + 0.2f * pulse), SpriteEffects.None, 0f);
			spriteBatch.Draw(shine, position, null, Color.White * (0.22f * pulse * fade), 0f, origin, new Vector2(1.55f, 0.11f) * size, SpriteEffects.None, 0f);
			spriteBatch.Draw(shine, position, null, Color.White * (0.18f * pulse * fade), 0f, origin, new Vector2(0.11f, 1.35f) * size, SpriteEffects.None, 0f);

			const int rings = 4;
			for (int i = 0; i < rings; i++) {
				float cycle = (time * 0.48f + i / (float)rings) % 1f;
				float ringScale = (0.22f + cycle * 1.35f) * size;
				float ringAlpha = MathF.Sin(cycle * MathF.PI) * (1f - cycle) * 0.38f * fade;
				spriteBatch.Draw(shine, position, null, Ruby * ringAlpha, 0f, origin, ringScale, SpriteEffects.None, 0f);
				spriteBatch.Draw(shine, position, null, ShinePink * (ringAlpha * 0.45f), 0f, origin, ringScale * 0.55f, SpriteEffects.None, 0f);
			}

			const int sparks = 5;
			for (int i = 0; i < sparks; i++) {
				float ang = time * 1.7f + i * MathHelper.TwoPi / sparks;
				float radius = (10f + 7f * MathF.Sin(time * 2.8f + i)) * (size / 1.05f);
				Vector2 pos = position + ang.ToRotationVector2() * radius;
				float sparkAlpha = (0.18f + 0.16f * MathF.Sin(time * 4.5f + i * 1.3f)) * fade;
				spriteBatch.Draw(shine, pos, null, ShinePink * sparkAlpha, ang, origin, 0.08f * size, SpriteEffects.None, 0f);
			}
		}

		private static float Beat()
		{
			float beat = WeSpectrum.SmoothBeat;
			if (beat > 0.04f)
				return beat;
			return 0.45f + 0.25f * (0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.1f));
		}

		private static Vector2 LocalToWorld(Vector2 center, Texture2D logo, float scale, float rotation, Vector2 local)
		{
			Vector2 fromCenter = (local - logo.Size() * 0.5f) * scale;
			float c = MathF.Cos(rotation);
			float s = MathF.Sin(rotation);
			return center + new Vector2(fromCenter.X * c - fromCenter.Y * s, fromCenter.X * s + fromCenter.Y * c);
		}

		private static Texture2D ShineTexture()
		{
			if (_shine != null && !_shine.IsDisposed)
				return _shine;

			const int size = 128;
			_shine = new Texture2D(Main.graphics.GraphicsDevice, size, size);
			var data = new Color[size * size];
			float mid = (size - 1) * 0.5f;
			float maxDist = size * 0.5f;
			for (int y = 0; y < size; y++) {
				for (int x = 0; x < size; x++) {
					float dx = x - mid;
					float dy = y - mid;
					float dist = MathF.Sqrt(dx * dx + dy * dy) / maxDist;
					float a = MathHelper.Clamp(1f - dist, 0f, 1f);
					a = a * a * (3f - 2f * a);
					a *= a;
					byte v = (byte)(a * 255f);
					data[y * size + x] = new Color(v, v, v, v);
				}
			}

			_shine.SetData(data);
			return _shine;
		}

		private static Asset<Texture2D> Req(string path) =>
			ModContent.Request<Texture2D>("WallpaperEngine/" + path);

		private static Texture2D Tex(Asset<Texture2D> asset)
		{
			Texture2D tex = asset?.Value;
			return tex == null || tex.IsDisposed ? null : tex;
		}
	}
}
