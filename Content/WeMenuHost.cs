using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ModLoader;
using WallpaperEngine.Chrome;
using WallpaperEngine.Core;
using WallpaperEngine.Layout;
using WallpaperEngine.UI;
using WallpaperEngine.Widgets;
using WallpaperEngine.Audio;
using WallpaperEngine.Grab;

namespace WallpaperEngine.Content
{
	public class WeMenuHost : ModSystem
	{
		private static bool _esc;

		public override void Load()
		{
			if (Main.dedServ)
				return;

			WeSave.EnsureLoaded();
			On_Main.DrawMenu += DrawMenuHook;
			WeBorrowFx.Load();
			WeModListLook.Load(Mod);
		}

		public override void Unload()
		{
			On_Main.DrawMenu -= DrawMenuHook;
			WeCatalog.Unload();
			WeBorrow.Unload();
			WeBorrowFx.Unload();
			WeModListLook.Unload();
			WeArt.Unload();
			WePlayerUI.Unload();
			DiscordFeed.Unload();
			WePlaylist.Unload();
			WeDraw.Unload();
			ClientChrome.Unload();
		}

		internal static void TickLogic()
		{
			WeToast.Update();
			LayoutEditor.Update();
			WrenchToolbar.Update();
			WePanels.Update();
			WeSplash.Update();
			WidgetHost.Update();
			WeFx.Update();
			WeCatalog.Pulse();
			WeBorrowFx.Tick();
			WeBackgroundStyle.DrewThisFrame = false;
		}

		internal static void DrawOverlay(SpriteBatch spriteBatch)
		{
			WeWallpaper.DrawFore(spriteBatch);
			WidgetHost.Draw(spriteBatch, 1f);
			LayoutEditor.Draw(spriteBatch, 1f);
			WrenchToolbar.Draw(spriteBatch);
			WePanels.Draw(spriteBatch);
			WeSplash.Draw(spriteBatch);
			WeToast.Draw(spriteBatch);
			WeBackgroundStyle.EndFrame();
			LayoutEditor.EndFrame();
			WePlayerUI.EndFrame();
			WrenchToolbar.EndFrame();
			WePanels.EndFrame();
		}

		private static void DrawMenuHook(On_Main.orig_DrawMenu orig, Main self, GameTime time)
		{
			bool steal = false;
			bool savedRelease = Main.mouseLeftRelease;
			int savedMouseY = Main.mouseY;
			bool remapY = false;

			if (WeModMenu.OnTitle) {
				HandleInput();
				steal = WeSplash.Visible || WePanels.IsOpen || WePanels.AteInput || WrenchToolbar.Busy || LayoutEditor.Busy || WidgetHost.Busy;
				if (steal) {
					Main.blockMouse = true;
					Main.mouseLeftRelease = false;
				}

				MenuButtonHooks.BeginFrame();
				int dy = MenuButtonHooks.MouseRemapY;
				if (dy != 0 && !WePanels.IsOpen && !WeSplash.Visible && SceneGraph.Visible(SceneGraph.MenuButtons)) {
					Main.mouseY -= dy;
					remapY = true;
				}
			}

			orig(self, time);

			if (steal)
				Main.mouseLeftRelease = savedRelease;
			if (remapY)
				Main.mouseY = savedMouseY;
		}

		private static void HandleInput()
		{
			bool esc = Main.keyState.IsKeyDown(Keys.Escape);
			if (esc && !_esc) {
				if (WeSplash.Visible)
					WeSplash.Dismiss(savePreference: false);
				else if (WePanels.IsOpen)
					WePanels.Close();
				else if (LayoutEditor.Editing)
					LayoutEditor.Cancel(true);
				else if (WrenchToolbar.Expanded)
					WrenchToolbar.Collapse();
			}

			_esc = esc;
			WeSplash.HandleInput();
			if (WeSplash.Visible)
				return;

			WePanels.HandleInput();
			WrenchToolbar.HandleInput();
			LayoutEditor.HandleInput();
			WidgetHost.HandleInput();
		}
	}
}
