using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using WallpaperEngine.Audio;
using WallpaperEngine.Core;
using WallpaperEngine.UI;

namespace WallpaperEngine.Content
{
	internal static class WeFx
	{
		private struct Speck
		{
			public Vector2 Pos;
			public Vector2 Vel;
			public float Size;
			public float Phase;
		}

		private static Speck[] _stars;
		private static Speck[] _dust;
		private static Speck[] _flies;
		private static Speck[] _clouds;
		private static Speck[] _rain;
		private static bool _ready;

		internal static void Update()
		{
			Ensure();
			Tick(_stars, wrap: true, damp: 1f);
			Tick(_dust, wrap: true, damp: 1f);
			Tick(_flies, wrap: true, damp: 0.999f);
			Tick(_clouds, wrap: true, damp: 1f);
			Tick(_rain, wrap: true, damp: 1f);
			Wander(_flies, 0.04f);
		}

		internal static void Draw(SpriteBatch spriteBatch, WeLayerRecord layer)
		{
			if (layer == null || !layer.Visible)
				return;

			Ensure();
			float alpha = MathHelper.Clamp(layer.Opacity, 0f, 1f);
			if (alpha < 0.01f)
				return;

			Vector2 pan = MouseShift(layer.Parallax);
			switch (layer.Effect) {
				case WeFxKind.Stars:
					DrawSpecks(spriteBatch, _stars, WeDraw.Circle(), pan, alpha, twinkle: true, additive: false);
					break;
				case WeFxKind.Dust:
					DrawSpecks(spriteBatch, _dust, WeDraw.Circle(), pan, alpha * 0.7f, twinkle: false, additive: false);
					break;
				case WeFxKind.Fog:
					DrawFog(spriteBatch, pan, alpha);
					break;
				case WeFxKind.Grain:
					DrawGrain(spriteBatch, alpha);
					break;
				case WeFxKind.Scanlines:
					DrawScan(spriteBatch, alpha);
					break;
				case WeFxKind.Fireflies:
					DrawSpecks(spriteBatch, _flies, WeDraw.Circle(), pan, alpha, twinkle: true, additive: true);
					break;
				case WeFxKind.Clouds:
					DrawClouds(spriteBatch, pan, alpha);
					break;
				case WeFxKind.Rain:
					DrawRain(spriteBatch, pan, alpha);
					break;
				case WeFxKind.Beat:
					DrawBeat(spriteBatch, alpha);
					break;
			}
		}

		internal static Vector2 MouseShift(float parallax)
		{
			float p = MathHelper.Clamp(parallax, 0f, 1f);
			if (p < 0.01f)
				return Vector2.Zero;
			float mx = MathHelper.Clamp(Main.mouseX / (float)Math.Max(1, Main.screenWidth), 0f, 1f) - 0.5f;
			float my = MathHelper.Clamp(Main.mouseY / (float)Math.Max(1, Main.screenHeight), 0f, 1f) - 0.5f;
			return new Vector2(mx * p * 90f, my * p * 70f);
		}

		private static void Ensure()
		{
			if (_ready)
				return;
			_stars = Seed(80, 0.15f, 0.55f, 0.8f, 2.4f);
			_dust = Seed(55, 0.08f, 0.35f, 1.1f, 2.6f, up: true);
			_flies = Seed(16, 0.2f, 0.9f, 2.2f, 4.4f);
			_clouds = Seed(6, 0.04f, 0.18f, 48f, 90f);
			_rain = SeedRain(60);
			_ready = true;
		}

		private static Speck[] Seed(int count, float minSpeed, float maxSpeed, float minSize, float maxSize, bool up = false)
		{
			var list = new Speck[count];
			for (int i = 0; i < count; i++) {
				float ang = up ? -MathHelper.PiOver2 + Main.rand.NextFloat(-0.35f, 0.35f) : Main.rand.NextFloat(MathHelper.TwoPi);
				float speed = Main.rand.NextFloat(minSpeed, maxSpeed);
				list[i] = new Speck {
					Pos = new Vector2(Main.rand.NextFloat(Main.screenWidth), Main.rand.NextFloat(Main.screenHeight)),
					Vel = ang.ToRotationVector2() * speed,
					Size = Main.rand.NextFloat(minSize, maxSize),
					Phase = Main.rand.NextFloat(MathHelper.TwoPi)
				};
			}

			return list;
		}

		private static Speck[] SeedRain(int count)
		{
			var list = new Speck[count];
			for (int i = 0; i < count; i++) {
				list[i] = new Speck {
					Pos = new Vector2(Main.rand.NextFloat(Main.screenWidth + 80f) - 40f, Main.rand.NextFloat(Main.screenHeight)),
					Vel = new Vector2(Main.rand.NextFloat(-0.4f, 0.15f), Main.rand.NextFloat(7.5f, 13f)),
					Size = Main.rand.NextFloat(7f, 16f),
					Phase = Main.rand.NextFloat(MathHelper.TwoPi)
				};
			}

			return list;
		}

		private static void Tick(Speck[] specks, bool wrap, float damp)
		{
			if (specks == null)
				return;
			int w = Math.Max(1, Main.screenWidth);
			int h = Math.Max(1, Main.screenHeight);
			for (int i = 0; i < specks.Length; i++) {
				Speck s = specks[i];
				s.Pos += s.Vel;
				s.Vel *= damp;
				s.Phase += 0.03f + s.Size * 0.004f;
				if (wrap) {
					if (s.Pos.X < -40f)
						s.Pos.X = w + 40f;
					if (s.Pos.X > w + 40f)
						s.Pos.X = -40f;
					if (s.Pos.Y < -40f)
						s.Pos.Y = h + 40f;
					if (s.Pos.Y > h + 40f)
						s.Pos.Y = -40f;
				}

				specks[i] = s;
			}
		}

		private static void Wander(Speck[] specks, float amount)
		{
			if (specks == null)
				return;
			for (int i = 0; i < specks.Length; i++) {
				Speck s = specks[i];
				s.Vel += new Vector2(Main.rand.NextFloat(-amount, amount), Main.rand.NextFloat(-amount, amount));
				if (s.Vel.LengthSquared() > 1.4f)
					s.Vel *= 0.92f;
				specks[i] = s;
			}
		}

		private static void DrawSpecks(SpriteBatch spriteBatch, Speck[] specks, Texture2D circle, Vector2 pan, float alpha, bool twinkle, bool additive)
		{
			if (specks == null)
				return;
			for (int i = 0; i < specks.Length; i++) {
				Speck s = specks[i];
				float tw = twinkle ? 0.55f + 0.45f * (0.5f + 0.5f * MathF.Sin(s.Phase)) : 1f;
				Color color = (additive ? WeAccent.Light : Color.White) * (alpha * tw);
				float scale = s.Size * 2f / circle.Width;
				spriteBatch.Draw(circle, s.Pos + pan, null, color, 0f, circle.Size() * 0.5f, scale, SpriteEffects.None, 0f);
			}
		}

		private static void DrawFog(SpriteBatch spriteBatch, Vector2 pan, float alpha)
		{
			float t = Main.GlobalTimeWrappedHourly * 8f;
			Rectangle cover = WeDraw.CoverRect;
			for (int i = 0; i < 5; i++) {
				float y = ((t * (4 + i) + i * 70f) % (cover.Height + 160f)) - 80f + pan.Y * 0.25f;
				int h = 48 + i * 18;
				WeDraw.Fill(spriteBatch, new Rectangle(cover.X, (int)y, cover.Width, h), Color.White * (0.045f * alpha * (1f - i * 0.12f)));
			}
		}

		private static void DrawGrain(SpriteBatch spriteBatch, float alpha)
		{
			Texture2D pixel = WeDraw.Pixel;
			int w = Main.screenWidth;
			int h = Main.screenHeight;
			for (int i = 0; i < 280; i++) {
				int x = Main.rand.Next(w);
				int y = Main.rand.Next(h);
				float a = Main.rand.NextFloat(0.08f, 0.28f) * alpha;
				spriteBatch.Draw(pixel, new Rectangle(x, y, 1, 1), Color.White * a);
			}
		}

		private static void DrawScan(SpriteBatch spriteBatch, float alpha)
		{
			int h = Main.screenHeight;
			int w = Main.screenWidth;
			for (int y = 0; y < h; y += 3)
				WeDraw.Fill(spriteBatch, new Rectangle(0, y, w, 1), Color.Black * (0.22f * alpha));
		}

		private static void DrawClouds(SpriteBatch spriteBatch, Vector2 pan, float alpha)
		{
			Texture2D circle = WeDraw.Circle();
			if (_clouds == null)
				return;
			for (int i = 0; i < _clouds.Length; i++) {
				Speck s = _clouds[i];
				Color color = Color.White * (0.07f * alpha);
				spriteBatch.Draw(circle, s.Pos + pan, null, color, 0f, circle.Size() * 0.5f, s.Size * 2.4f / circle.Width, SpriteEffects.None, 0f);
				spriteBatch.Draw(circle, s.Pos + pan + new Vector2(s.Size * 0.45f, 8f), null, color, 0f, circle.Size() * 0.5f, s.Size * 1.8f / circle.Width, SpriteEffects.None, 0f);
			}
		}

		private static void DrawRain(SpriteBatch spriteBatch, Vector2 pan, float alpha)
		{
			if (_rain == null)
				return;
			Texture2D pixel = WeDraw.Pixel;
			Color color = new Color(180, 200, 220) * (0.28f * alpha);
			for (int i = 0; i < _rain.Length; i++) {
				Speck s = _rain[i];
				Vector2 from = s.Pos + pan;
				Vector2 to = from + Vector2.Normalize(s.Vel) * s.Size;
				RoundButton.DrawThick(spriteBatch, pixel, from, to, 1.2f, color);
			}
		}

		private static void DrawBeat(SpriteBatch spriteBatch, float alpha)
		{
			float beat = WeSpectrum.SmoothBeat;
			if (beat < 0.02f)
				return;
			WeDraw.Fill(spriteBatch, WeDraw.CoverRect, WeAccent.Light * (0.16f * alpha * beat));
			WeDraw.DrawVignette(spriteBatch, 0.35f * alpha * beat);
		}
	}
}
