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

		internal static void Show(string key)
		{
			_text = WeText.UI(key);
			_life = 2.4f;
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
			if (_life > 2f)
				alpha = MathHelper.Clamp((2.4f - _life) / 0.25f, 0f, 1f);

			var font = FontAssets.MouseText.Value;
			Vector2 size = font.MeasureString(_text);
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
				Vector2.One);
		}
	}
}
