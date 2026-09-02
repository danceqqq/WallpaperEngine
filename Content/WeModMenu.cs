using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using WallpaperEngine.Chrome;
using WallpaperEngine.Core;
using WallpaperEngine.Layout;
using WallpaperEngine.UI;
using WallpaperEngine.Audio;
using WallpaperEngine.Grab;

namespace WallpaperEngine.Content
{
	public class WeModMenu : ModMenu
	{
		private Asset<Texture2D> _empty;
		private static bool _ticked;

		public override string DisplayName => Language.GetTextValue("Mods.WallpaperEngine.MenuTheme");

		internal static bool IsActive
		{
			get
			{
				WeModMenu menu = ModContent.GetInstance<WeModMenu>();
				return menu != null && MenuLoader.CurrentMenu == menu;
			}
		}

		internal static bool OnTitle => Main.gameMenu && Main.menuMode == 0 && IsActive;

		public override ModSurfaceBackgroundStyle MenuBackgroundStyle =>
			WeSettings.HasCustomSky ? ModContent.GetInstance<WeBackgroundStyle>() : null;

		public override Asset<Texture2D> SunTexture => HideSunMoon ? _empty : base.SunTexture;

		public override Asset<Texture2D> MoonTexture => HideSunMoon ? _empty : base.MoonTexture;

		public override int Music
		{
			get
			{
				if (WeSave.Data.Music == MusicKind.Silence)
					return 0;
				if (WeSave.Data.Music == MusicKind.Custom)
					return WePlaylist.MenuMusicId;
				return 50;
			}
		}

		private static bool HideSunMoon =>
			!SceneGraph.Visible(SceneGraph.SunMoon) || WeSettings.HasCustomSky;

		public override void Load()
		{
			_empty = ModContent.Request<Texture2D>("WallpaperEngine/Assets/Textures/UI/empty");
			WeIcons.Load();
			WePresetLogos.Load();
			WePlaylist.Load(Mod);
			WeSpectrum.Load();
		}

		public override void OnSelected()
		{
			WePersist.OnSelected();
			WeArt.Scan();
			WeCatalog.Refresh();
			WeCatalog.DropMissing();
			WeLibrary.ScanIntoSave();
			WeSplash.OnThemeSelected();
			WePlaylist.OnThemeSelected();
			WePlayerUI.Reset();
			WePanels.Close();
			LayoutEditor.Reset();
			WrenchToolbar.OnThemeSelected();
			WeType.Scan();
		}

		public override void OnDeselected()
		{
			if (Main.gameMenu && Main.menuMode != 0) {
				LayoutEditor.Cancel(false);
				WePanels.Close();
				WeSplash.Hide();
				return;
			}

			WePersist.OnDeselected();
			WePlaylist.Silence();
			LayoutEditor.Cancel(false);
			WePanels.Close();
			WeSplash.Hide();
		}

		public override void Update(bool isOnTitleScreen)
		{
			if (!Main.gameMenu)
				return;
			if (isOnTitleScreen)
				Tick();
		}

		internal void Tick()
		{
			if (!Main.gameMenu || _ticked)
				return;

			_ticked = true;
			WeMenuHost.TickLogic();
		}

		public override bool PreDrawLogo(
			SpriteBatch spriteBatch,
			ref Vector2 logoDrawCenter,
			ref float logoRotation,
			ref float logoScale,
			ref Color drawColor)
		{
			Tick();
			WeLook.StabilizeLogo(ref logoRotation, ref logoScale);
			WeBackgroundStyle.Draw(spriteBatch);
			WeBackgroundStyle.DrawAtmosphere(spriteBatch);
			if (WeSave.Data.Logo is LogoKind.Custom or LogoKind.Hidden or LogoKind.Borrowed or LogoKind.Preset || SceneGraph.Get(SceneGraph.Logo).Customized)
			{
				if (WeSave.Data.Logo != LogoKind.Hidden && SceneGraph.Visible(SceneGraph.Logo))
					WeLogo.DrawCustom(spriteBatch, 1f, logoRotation, logoScale);
				return false;
			}

			return WeLogo.ShouldDrawVanilla(ref logoDrawCenter, ref logoScale);
		}

		public override void PostDrawLogo(
			SpriteBatch spriteBatch,
			Vector2 logoDrawCenter,
			float logoRotation,
			float logoScale,
			Color drawColor)
		{
			Tick();
			WeMenuHost.DrawOverlay(spriteBatch);
			_ticked = false;
		}
	}
}
