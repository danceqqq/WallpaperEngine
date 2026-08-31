using Terraria.Audio;
using Terraria.ID;
using WallpaperEngine.Content;
using WallpaperEngine.Core;
using WallpaperEngine.Layout;
using WallpaperEngine.UI;

namespace WallpaperEngine.Chrome
{
	internal enum WrenchAction
	{
		LogoPos,
		Wallpaper,
		Music,
		Widgets,
		Logo,
		Layout,
		Client,
		Clean
	}

	internal enum WrenchStyle
	{
		Radial = 0,
		Dock = 1
	}

	internal static class WrenchHub
	{
		internal static readonly WrenchAction[] Actions =
		{
			WrenchAction.LogoPos, WrenchAction.Wallpaper, WrenchAction.Music,
			WrenchAction.Widgets, WrenchAction.Logo, WrenchAction.Layout,
			WrenchAction.Client, WrenchAction.Clean
		};

		internal static bool UseDock => WeSave.Data.WrenchStyle == (int)WrenchStyle.Dock;

		internal static void Activate(WrenchAction action)
		{
			SoundEngine.PlaySound(SoundID.MenuTick);
			switch (action) {
				case WrenchAction.LogoPos:
					LayoutEditor.Begin(SceneGraph.Logo);
					WrenchToolbar.Collapse();
					break;
				case WrenchAction.Wallpaper:
					WePanels.Open(WePanelId.Wallpaper);
					break;
				case WrenchAction.Music:
					WePanels.Open(WePanelId.Music);
					break;
				case WrenchAction.Widgets:
					WePanels.Open(WePanelId.Widgets);
					break;
				case WrenchAction.Logo:
					WePanels.Open(WePanelId.Logo);
					break;
				case WrenchAction.Layout:
					LayoutEditor.Begin();
					WrenchToolbar.Collapse();
					break;
				case WrenchAction.Client:
					WePanels.Open(WePanelId.Client);
					break;
				case WrenchAction.Clean:
					WeSettings.ToggleCleanChrome();
					WeToast.Show(WeSave.Data.CleanChrome ? "ToastCleanOn" : "ToastCleanOff");
					break;
			}
		}

		internal static bool IsOn(WrenchAction action) => action switch {
			WrenchAction.Clean => WeSave.Data.CleanChrome,
			_ => WePanels.Is(PanelOf(action))
		};

		internal static WePanelId PanelOf(WrenchAction action) => action switch {
			WrenchAction.Wallpaper => WePanelId.Wallpaper,
			WrenchAction.Music => WePanelId.Music,
			WrenchAction.Widgets => WePanelId.Widgets,
			WrenchAction.Logo => WePanelId.Logo,
			WrenchAction.Client => WePanelId.Client,
			_ => WePanelId.None
		};

		internal static string IconName(WrenchAction action) => action switch {
			WrenchAction.LogoPos => WeIcons.MoveLogo,
			WrenchAction.Wallpaper => WeIcons.Wallpaper,
			WrenchAction.Music => WeIcons.Music,
			WrenchAction.Widgets => WeIcons.Widget,
			WrenchAction.Logo => WeIcons.Logo,
			WrenchAction.Layout => WeIcons.Layout,
			WrenchAction.Client => WeIcons.Client,
			WrenchAction.Clean => WeIcons.Hide,
			_ => WeIcons.Setting
		};

		internal static string TipKey(WrenchAction action) => action switch {
			WrenchAction.LogoPos => "BtnLogoPos",
			WrenchAction.Wallpaper => "BtnWallpaper",
			WrenchAction.Music => "BtnMusic",
			WrenchAction.Widgets => "BtnWidgets",
			WrenchAction.Logo => "BtnLogo",
			WrenchAction.Layout => "BtnLayout",
			WrenchAction.Client => "BtnClient",
			WrenchAction.Clean => "BtnClean",
			_ => "Wrench"
		};

		internal static int ActiveIndex()
		{
			for (int i = 0; i < Actions.Length; i++) {
				if (IsOn(Actions[i]))
					return i;
			}

			return -1;
		}
	}
}
