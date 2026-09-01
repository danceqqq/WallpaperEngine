using System.Reflection;
using Terraria;
using Terraria.ModLoader;
using WallpaperEngine.Core;
using WallpaperEngine.Audio;
using WallpaperEngine.Grab;

namespace WallpaperEngine.Content
{
	public class WePersist : ModSystem
	{
		private static FieldInfo _switchToMenu;
		private static FieldInfo _lastSelected;
		private static FieldInfo _loading;
		private static int _restoreCooldown;

		internal static bool MenuStillLoading => _loading?.GetValue(null) is true;

		public override void Load()
		{
			const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			_switchToMenu = typeof(MenuLoader).GetField("switchToMenu", flags);
			_lastSelected = typeof(MenuLoader).GetField("LastSelectedModMenu", flags);
			_loading = typeof(MenuLoader).GetField("loading", flags);
		}

		public override void PostSetupContent()
		{
			WeSave.EnsureLoaded();
			WeCatalog.Refresh();
			Chrome.ClientChrome.Apply();
			if (WeSave.Data.KeepMenuSelected)
				_restoreCooldown = 30;
		}

		public override void PreUpdatePlayers()
		{
			WePlaylist.HandleMenuLifecycle();
		}

		public override void UpdateUI(Microsoft.Xna.Framework.GameTime gameTime)
		{
			WePlaylist.HandleMenuLifecycle();
			if (!Main.gameMenu)
				return;

			if (_restoreCooldown > 0) {
				_restoreCooldown--;
				Restore();
			}
			else if (WeSave.Data.KeepMenuSelected && MenuLoader.CurrentMenu is not WeModMenu)
				Restore();
		}

		internal static void OnSelected()
		{
			WeSave.Data.KeepMenuSelected = true;
			WeSave.Save();
			Remember();
		}

		internal static void OnDeselected()
		{
			if (MenuStillLoading)
				return;

			WeSave.Data.KeepMenuSelected = false;
			WeSave.Save();
		}

		private static void Restore()
		{
			if (!WeSave.Data.KeepMenuSelected)
				return;

			WeModMenu menu = ModContent.GetInstance<WeModMenu>();
			if (menu == null)
				return;

			if (Main.instance != null)
				Main.instance.playOldTile = false;
			Remember();
			if (MenuLoader.CurrentMenu == menu)
				return;

			_switchToMenu?.SetValue(null, menu);
		}

		private static void Remember()
		{
			WeModMenu menu = ModContent.GetInstance<WeModMenu>();
			if (menu == null || _lastSelected == null)
				return;

			try {
				_lastSelected.SetValue(null, menu.FullName);
				Main.SaveSettings();
			}
			catch {
			}
		}
	}
}
