using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;

namespace WallpaperEngine.Chrome
{
	internal static class WeModListLook
	{
		private static readonly Color Accent = new(0, 153, 255);
		private static readonly List<Asset<Texture2D>> Frames = new();
		private static bool _hooks;

		internal static void Load(Mod mod)
		{
			if (_hooks || Main.dedServ)
				return;

			Frames.Clear();
			for (int i = 0; i < 8; i++) {
				string file = "Assets/Textures/UI/ModList/Frame" + i.ToString("D2") + ".png";
				if (mod == null || !mod.FileExists(file))
					break;
				Frames.Add(ModContent.Request<Texture2D>(
					"WallpaperEngine/Assets/Textures/UI/ModList/Frame" + i.ToString("D2"),
					i == 0 ? AssetRequestMode.ImmediateLoad : AssetRequestMode.AsyncLoad));
			}

			MethodInfo drawSelf = typeof(UIPanel).GetMethod("DrawSelf", BindingFlags.NonPublic | BindingFlags.Instance);
			if (drawSelf != null)
				MonoModHooks.Add(drawSelf, DrawSelf);
			_hooks = true;
		}

		internal static void Unload()
		{
			Frames.Clear();
		}

		private static void DrawSelf(Action<UIPanel, SpriteBatch> orig, UIPanel self, SpriteBatch spriteBatch)
		{
			if (!IsOurs(self)) {
				orig(self, spriteBatch);
				return;
			}

			self.BorderColor = self.IsMouseHovering ? Accent : Accent * 0.78f;
			Color saved = self.BackgroundColor;
			self.BackgroundColor = Color.Transparent;
			DrawBackdrop(spriteBatch, self);
			orig(self, spriteBatch);
			self.BackgroundColor = saved;
		}

		private static void DrawBackdrop(SpriteBatch spriteBatch, UIPanel panel)
		{
			Texture2D tex = CurrentFrame();
			if (tex == null)
				return;

			CalculatedStyle dim = panel.GetDimensions();
			var dest = new Rectangle((int)dim.X, (int)dim.Y, (int)dim.Width, (int)dim.Height);
			if (dest.Width < 2 || dest.Height < 2)
				return;

			float scale = Math.Max(dest.Width / (float)Math.Max(1, tex.Width), dest.Height / (float)Math.Max(1, tex.Height));
			int srcW = Math.Min(tex.Width, Math.Max(1, (int)(dest.Width / scale)));
			int srcH = Math.Min(tex.Height, Math.Max(1, (int)(dest.Height / scale)));
			int srcX = Math.Max(0, (tex.Width - srcW) / 2);
			int srcY = Math.Max(0, (tex.Height - srcH) / 2);
			spriteBatch.Draw(tex, dest, new Rectangle(srcX, srcY, srcW, srcH), Color.White * 0.92f);
		}

		private static Texture2D CurrentFrame()
		{
			if (Frames.Count == 0)
				return null;

			int index = (int)(Main.GameUpdateCount / 18u) % Frames.Count;
			try {
				Asset<Texture2D> asset = Frames[index];
				if (asset == null)
					return null;
				if (!asset.IsLoaded)
					asset = Frames[0];
				Texture2D tex = asset?.Value;
				return tex == null || tex.IsDisposed ? null : tex;
			}
			catch {
				return null;
			}
		}

		private static bool IsOurs(UIElement self)
		{
			if (self == null || self.GetType().Name != "UIModItem")
				return false;
			try {
				PropertyInfo prop = self.GetType().GetProperty("ModName", BindingFlags.Public | BindingFlags.Instance);
				return prop?.GetValue(self) as string == "WallpaperEngine";
			}
			catch {
				return false;
			}
		}
	}
}
