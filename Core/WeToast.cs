using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI.Chat;
using WallpaperEngine.UI;

namespace WallpaperEngine.Core
{
	internal static class WeToast
	{
		private static string _text = "";
		private static float _life;
		private static float _max = 2.4f;

		internal static void Show(string key, float life = 2.4f)
		{
			_text = WeText.UI(key);
			_max = MathHelper.Max(0.8f, life);
			_life = _max;
		}

		internal static void Update()
		{
			if (_life > 0f)
				_life -= 1f / 60f;
		}

		internal static void Draw(SpriteBatch spriteBatch)
		{
			if (_life <= 0f || string.IsNullOrEmpty(_text))
				return;

			float alpha = MathHelper.Clamp(_life / 0.35f, 0f, 1f);
			if (_life > _max - 0.25f)
				alpha = MathHelper.Clamp((_max - _life) / 0.25f, 0f, 1f);

			var font = FontAssets.MouseText.Value;
			float scale = 1f;
			Vector2 size = font.MeasureString(_text);
			float max = Math.Max(160f, Main.screenWidth - 80f);
			if (size.X > max) {
				scale = max / size.X;
				size *= scale;
			}
			var rect = new Rectangle(
				(int)((Main.screenWidth - size.X - 36) * 0.5f),
				18,
				(int)size.X + 36,
				36);
			WeDraw.Fill(spriteBatch, rect, new Color(24, 26, 32) * (0.88f * alpha));
			WeDraw.Border(spriteBatch, rect, WeAccent.Mid * alpha);
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch,
				font,
				_text,
				new Vector2(rect.X + 18, rect.Y + (rect.Height - size.Y) * 0.5f),
				Color.White * alpha,
				0f,
				Vector2.Zero,
				new Vector2(scale));
		}
	}
}
