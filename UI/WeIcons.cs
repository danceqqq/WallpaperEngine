using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.ModLoader;

namespace WallpaperEngine.UI
{
	internal static class WeIcons
	{
		internal const string Setting = "Setting";
		internal const string MoveLogo = "MoveLogo";
		internal const string Wallpaper = "ChangeWallpaper";
		internal const string Music = "ChangeMusic";
		internal const string Widget = "Widget";
		internal const string Logo = "ChangeLogo";
		internal const string Layout = "MoveElement";
		internal const string Client = "ClientSetting";
		internal const string Hide = "HideVersion";
		internal const string Play = "Play";
		internal const string Pause = "Pause";
		internal const string Prev = "Previous";
		internal const string Next = "Next";
		internal const string Playlist = "Playlist";
		internal const string Upload = "Upload";
		internal const string Shuffle = "Shuffle";
		internal const string Loop = "Loop";
		internal const string Trash = "Trash";

		private static readonly string[] Names =
		{
			Setting, MoveLogo, Wallpaper, Music, Widget, Logo, Layout, Client, Hide,
			Play, Pause, Prev, Next, Playlist, Upload, Shuffle, Loop, Trash
		};

		private static readonly Dictionary<string, Asset<Texture2D>> Assets = new();

		internal static void Load()
		{
			Assets.Clear();
			foreach (string name in Names)
				Request(name);
		}

		private static void Request(string name)
		{
			Assets[name] = ModContent.Request<Texture2D>(
				"WallpaperEngine/Assets/Textures/UI/Icons/" + name,
				AssetRequestMode.ImmediateLoad);
		}

		internal static Texture2D Get(string name)
		{
			if (string.IsNullOrEmpty(name) || !Assets.TryGetValue(name, out Asset<Texture2D> asset))
				return null;
			return asset?.Value;
		}
	}
}
