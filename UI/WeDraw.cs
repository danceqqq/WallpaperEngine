using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using WallpaperEngine.Core;

namespace WallpaperEngine.UI
{
	internal static class WeDraw
	{
		private static readonly RasterizerState ClipRast = new()
		{
			CullMode = CullMode.CullCounterClockwiseFace,
			ScissorTestEnable = true
		};

		private static Texture2D _circle;
		internal static Rectangle? Scissor { get; private set; }

		internal static Point UiSize =>
			new(Math.Max(1, Main.screenWidth), Math.Max(1, Main.screenHeight));

		internal static Rectangle UiRect
		{
			get
			{
				Point size = UiSize;
				return new Rectangle(0, 0, size.X, size.Y);
			}
		}

		internal static Point CoverSize
		{
			get
			{
				int w = Math.Max(1, Main.screenWidth);
				int h = Math.Max(1, Main.screenHeight);
				GraphicsDevice gd = Main.instance?.GraphicsDevice;
				if (gd != null) {
					if (gd.Viewport.Width > 0)
						w = Math.Max(w, gd.Viewport.Width);
					if (gd.Viewport.Height > 0)
						h = Math.Max(h, gd.Viewport.Height);
					PresentationParameters pp = gd.PresentationParameters;
					if (pp != null) {
						if (pp.BackBufferWidth > 0)
							w = Math.Max(w, pp.BackBufferWidth);
						if (pp.BackBufferHeight > 0)
							h = Math.Max(h, pp.BackBufferHeight);
					}
				}

				return new Point(w, h);
			}
		}

		internal static Rectangle CoverRect
		{
			get
			{
				Point size = CoverSize;
				return new Rectangle(0, 0, size.X, size.Y);
			}
		}

		internal static Texture2D Pixel => TextureAssets.MagicPixel.Value;

		internal static Texture2D Circle()
		{
			if (_circle != null && !_circle.IsDisposed)
				return _circle;

			const int size = 64;
			_circle = new Texture2D(Main.graphics.GraphicsDevice, size, size);
			var data = new Color[size * size];
			float c = (size - 1) * 0.5f;
			for (int y = 0; y < size; y++) {
				for (int x = 0; x < size; x++) {
					float dist = MathF.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / (size * 0.5f);
					float a = MathHelper.Clamp((1f - dist) * 8f, 0f, 1f);
					byte v = (byte)(a * 255f);
					data[y * size + x] = new Color(v, v, v, v);
				}
			}

			_circle.SetData(data);
			return _circle;
		}

		internal static Rectangle CoverDestination(Texture2D tex, Vector2 pan, float overdraw = 1.02f)
		{
			Point cover = UiSize;
			float scale = Math.Max(
				cover.X / (float)Math.Max(1, tex.Width),
				cover.Y / (float)Math.Max(1, tex.Height)) * overdraw;
			int w = Math.Max(1, (int)(tex.Width * scale));
			int h = Math.Max(1, (int)(tex.Height * scale));
			float extraX = Math.Max(0, w - cover.X);
			float extraY = Math.Max(0, h - cover.Y);
			pan.X = MathHelper.Clamp(pan.X, 0f, 1f);
			pan.Y = MathHelper.Clamp(pan.Y, 0f, 1f);
			return new Rectangle((int)(-extraX * pan.X), (int)(-extraY * pan.Y), w, h);
		}

		internal static Rectangle ContainDestination(Texture2D tex, Vector2 pan)
		{
			Point cover = UiSize;
			float scale = Math.Min(
				cover.X / (float)Math.Max(1, tex.Width),
				cover.Y / (float)Math.Max(1, tex.Height));
			int w = Math.Max(1, (int)(tex.Width * scale));
			int h = Math.Max(1, (int)(tex.Height * scale));
			pan.X = MathHelper.Clamp(pan.X, 0f, 1f);
			pan.Y = MathHelper.Clamp(pan.Y, 0f, 1f);
			int leftoverX = cover.X - w;
			int leftoverY = cover.Y - h;
			return new Rectangle((int)(leftoverX * pan.X), (int)(leftoverY * pan.Y), w, h);
		}

		internal static Rectangle ImageDestination(Texture2D tex, Vector2 pan, WallpaperFit fit) => fit switch {
			WallpaperFit.Contain => ContainDestination(tex, pan),
			WallpaperFit.Stretch => UiRect,
			_ => CoverDestination(tex, pan)
		};

		internal static void DrawVignette(SpriteBatch spriteBatch, float amount)
		{
			if (amount < 0.01f)
				return;

			Rectangle r = CoverRect;
			int band = Math.Max(40, (int)(Math.Min(r.Width, r.Height) * 0.26f));
			const int slices = 16;
			for (int i = 0; i < slices; i++) {
				float a = amount * (1f - i / (float)slices);
				a *= a;
				Color c = Color.Black * a;
				int t = (int)(band * i / (float)slices);
				int th = Math.Max(1, (int)(band / (float)slices) + 1);
				Fill(spriteBatch, new Rectangle(r.X, r.Y + t, r.Width, th), c);
				Fill(spriteBatch, new Rectangle(r.X, r.Bottom - t - th, r.Width, th), c);
				Fill(spriteBatch, new Rectangle(r.X + t, r.Y, th, r.Height), c);
				Fill(spriteBatch, new Rectangle(r.Right - t - th, r.Y, th, r.Height), c);
			}
		}

		internal static void Fill(SpriteBatch spriteBatch, Rectangle rect, Color color) =>
			spriteBatch.Draw(Pixel, rect, color);

		internal static void Border(SpriteBatch spriteBatch, Rectangle rect, Color color)
		{
			spriteBatch.Draw(Pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), color);
			spriteBatch.Draw(Pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), color);
			spriteBatch.Draw(Pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), color);
			spriteBatch.Draw(Pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), color);
		}

		internal static void DrawVerticalGradient(SpriteBatch spriteBatch, Rectangle rect, Color top, Color bottom, float alpha)
		{
			const int slices = 36;
			for (int i = 0; i < slices; i++) {
				float t0 = i / (float)slices;
				int y = rect.Y + (int)(rect.Height * t0);
				int next = rect.Y + (int)(rect.Height * ((i + 1) / (float)slices));
				spriteBatch.Draw(Pixel, new Rectangle(rect.X, y, rect.Width, Math.Max(1, next - y + 1)), Color.Lerp(top, bottom, t0) * alpha);
			}
		}

		internal static void BeginUi(SpriteBatch spriteBatch)
		{
			RasterizerState rast = Scissor.HasValue ? ClipRast : RasterizerState.CullCounterClockwise;
			spriteBatch.Begin(
				SpriteSortMode.Deferred,
				BlendState.AlphaBlend,
				SamplerState.LinearClamp,
				DepthStencilState.None,
				rast,
				null,
				Main.UIScaleMatrix);
			if (Scissor.HasValue)
				Main.instance.GraphicsDevice.ScissorRectangle = Scissor.Value;
		}

		internal static void WithLinear(SpriteBatch spriteBatch, Action draw)
		{
			spriteBatch.End();
			BeginUi(spriteBatch);
			draw();
			spriteBatch.End();
			BeginUi(spriteBatch);
		}

		internal static void WithPoint(SpriteBatch spriteBatch, Action draw)
		{
			spriteBatch.End();
			RasterizerState rast = Scissor.HasValue ? ClipRast : RasterizerState.CullCounterClockwise;
			spriteBatch.Begin(
				SpriteSortMode.Deferred,
				BlendState.AlphaBlend,
				SamplerState.PointClamp,
				DepthStencilState.None,
				rast,
				null,
				Main.UIScaleMatrix);
			if (Scissor.HasValue)
				Main.instance.GraphicsDevice.ScissorRectangle = Scissor.Value;
			draw();
			spriteBatch.End();
			BeginUi(spriteBatch);
		}

		internal static void WithClip(SpriteBatch spriteBatch, Rectangle uiClip, Action draw)
		{
			GraphicsDevice gd = Main.instance.GraphicsDevice;
			Rectangle scaled = ToNative(uiClip);
			scaled = Rectangle.Intersect(scaled, gd.Viewport.Bounds);
			if (Scissor.HasValue)
				scaled = Rectangle.Intersect(scaled, Scissor.Value);
			if (scaled.Width < 2 || scaled.Height < 2)
				return;

			Rectangle old = gd.ScissorRectangle;
			Rectangle? previous = Scissor;
			spriteBatch.End();
			Scissor = scaled;
			gd.ScissorRectangle = scaled;
			BeginUi(spriteBatch);
			draw();
			spriteBatch.End();
			Scissor = previous;
			gd.ScissorRectangle = old;
			BeginUi(spriteBatch);
		}

		internal static void Unload()
		{
			Texture2D tex = _circle;
			_circle = null;
			if (tex == null || tex.IsDisposed)
				return;

			Main.QueueMainThreadAction(() => {
				try {
					if (!tex.IsDisposed)
						tex.Dispose();
				}
				catch {
				}
			});
		}

		private static Rectangle ToNative(Rectangle rect)
		{
			float scale = Main.UIScale;
			return new Rectangle(
				(int)MathF.Floor(rect.X * scale),
				(int)MathF.Floor(rect.Y * scale),
				Math.Max(1, (int)MathF.Ceiling(rect.Width * scale)),
				Math.Max(1, (int)MathF.Ceiling(rect.Height * scale)));
		}
	}
}
