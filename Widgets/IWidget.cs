namespace WallpaperEngine.Widgets
{
	internal interface IWidget
	{
		string Id { get; }
		bool Enabled { get; }
		Microsoft.Xna.Framework.Rectangle HitRect();
		void Update();
		void HandleInput();
		void Draw(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch, float fade);
	}
}
