using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using WallpaperEngine.Chrome;
using WallpaperEngine.Content;
using WallpaperEngine.Core;
using WallpaperEngine.Widgets;
using WallpaperEngine.Audio;

namespace WallpaperEngine.Layout
{
	internal static class SceneGraph
	{
		internal const string Logo = "logo";
		internal const string MenuButtons = "menuButtons";
		internal const string ThemeSwap = "themeSwap";
		internal const string SocialTerraria = "socialTerraria";
		internal const string SocialTml = "socialTml";
		internal const string Version = "version";
		internal const string News = "news";
		internal const string SunMoon = "sunMoon";
		internal const string Wrench = "wrench";
		internal const string Player = "widget.player";
		internal const string Clock = "widget.clock";
		internal const string Quote = "widget.quote";
		internal const string Moon = "widget.moon";
		internal const string Discord = "widget.discord";

		internal static readonly string[] Ids =
		{
			Logo, MenuButtons, ThemeSwap, SocialTerraria, SocialTml, Version, News, SunMoon, Wrench, Player, Clock, Quote, Moon, Discord
		};

		internal static void EnsureRecords(WeSaveData data)
		{
			foreach (string id in Ids) {
				if (data.Elements.Exists(item => item.Id == id))
					continue;
				data.Elements.Add(new WeElementRecord { Id = id, Visible = true, Scale = 1f });
			}
		}

		internal static WeElementRecord Get(string id)
		{
			if (LayoutEditor.Editing && LayoutEditor.TryWork(id, out WeElementRecord work))
				return work;

			WeElementRecord record = Find(id);
			return record ?? DefaultRecord(id);
		}

		internal static WeElementRecord Find(string id) =>
			WeSave.Data.Elements.Find(item => item.Id == id);

		internal static bool Visible(string id)
		{
			if (id == Player && !WeSave.Data.PlayerWidget)
				return false;
			if (id == Clock && !WeSave.Data.ClockWidget)
				return false;
			if (id == Quote && !WeSave.Data.QuoteWidget)
				return false;
			if (id == Moon && !WeSave.Data.MoonWidget)
				return false;
			if (id == Discord && !WeSave.Data.DiscordWidget)
				return false;
			if (!LayoutEditor.Editing && WeSave.Data.CleanChrome && IsClutter(id))
				return false;
			return Get(id).Visible;
		}

		internal static bool IsClutter(string id) =>
			id is Version or SocialTerraria or SocialTml or News;

		internal static bool CanHide(string id) => id is not Wrench and not ThemeSwap;

		internal static float ScaleOf(string id) => MathHelper.Clamp(Get(id).Scale <= 0f ? 1f : Get(id).Scale, 0.35f, 2.4f);

		internal static Vector2 Pixel(string id)
		{
			WeElementRecord record = Get(id);
			if (record.Customized)
				return new Vector2(record.AnchorX * Math.Max(1, Main.screenWidth), record.AnchorY * Math.Max(1, Main.screenHeight));
			return DefaultPixel(id);
		}

		internal static void SetPixel(string id, Vector2 pixel, float scale)
		{
			WeElementRecord record = Find(id) ?? DefaultRecord(id);
			if (Find(id) == null)
				WeSave.Data.Elements.Add(record);

			record.Customized = true;
			record.AnchorX = MathHelper.Clamp(pixel.X / Math.Max(1, Main.screenWidth), 0f, 1f);
			record.AnchorY = MathHelper.Clamp(pixel.Y / Math.Max(1, Main.screenHeight), 0f, 1f);
			record.Scale = MathHelper.Clamp(scale, 0.35f, 2.4f);
		}

		internal static Vector2 DefaultPixel(string id)
		{
			int w = Math.Max(1, Main.screenWidth);
			int h = Math.Max(1, Main.screenHeight);
			return id switch {
				Logo => new Vector2(w * 0.5f, 100f),
				MenuButtons => new Vector2(w * 0.5f, 220f),
				ThemeSwap => new Vector2(w * 0.5f, h - 22f),
				SocialTml => NativeSocialCenter(true, h),
				SocialTerraria => NativeSocialCenter(false, h),
				Version => new Vector2(w - 18f, 36f),
				News => new Vector2(w - 18f, h - 86f),
				SunMoon => new Vector2(w * 0.5f, 80f),
				Wrench => CenterWrench(w, h),
				Player => new Vector2(340f, h - 106f),
				Clock => new Vector2(w - 150f, 72f),
				Quote => new Vector2(48f + 160f, h * 0.42f),
				Moon => new Vector2(72f, 88f),
				Discord => new Vector2(170f, 220f),
				_ => new Vector2(w * 0.5f, h * 0.5f)
			};
		}

		private static Vector2 CenterWrench(int w, int h)
		{
			float menuBottom = MenuButtonHooks.LastMenuBottom > 8f
				? MenuButtonHooks.LastMenuBottom
				: 220f + 6f * 68f + 40f;
			float themeY = h - 36f;
			WeElementRecord theme = Find(ThemeSwap);
			if (theme is { Customized: true })
				themeY = theme.AnchorY * h;
			float y = MathHelper.Clamp((menuBottom + themeY) * 0.5f, 72f, h - 40f);
			return new Vector2(w * 0.5f, y);
		}

		internal static WeElementRecord DefaultRecord(string id) => new()
		{
			Id = id,
			Visible = true,
			Scale = 1f
		};

		internal static Rectangle Hit(string id)
		{
			Vector2 pos = Pixel(id);
			float scale = ScaleOf(id);
			return id switch {
				Logo => WeLogo.HitRect(),
				MenuButtons => MenuButtonHooks.MenuHit(),
				Wrench => WrenchToolbar.HitRect(),
				Player => WePlayerUI.HitRect(),
				Clock => ClockWidget.HitRect(),
				Quote => QuoteWidget.HitRect(),
				Moon => MoonWidget.HitRect(),
				Discord => DiscordWidget.HitRect(),
				ThemeSwap => Around(pos, 220, 28),
				SocialTml => SocialHit(pos, true),
				SocialTerraria => SocialHit(pos, false),
				Version => Around(pos, 180, 28),
				News => Around(pos, 280, 28),
				SunMoon => Around(pos, 80 * scale, 80 * scale),
				_ => Around(pos, 80, 28)
			};
		}

		internal static IEnumerable<string> VisibleIds()
		{
			foreach (string id in Ids) {
				if (Visible(id))
					yield return id;
			}
		}

		internal static IEnumerable<WeElementRecord> Hidden()
		{
			foreach (string id in Ids) {
				WeElementRecord record = Get(id);
				if (!record.Visible && CanHide(id))
					yield return record;
			}
		}

		internal static bool RestoreNativeSocialIfMisplaced(WeSaveData data)
		{
			bool dirty = false;
			dirty |= UnstickTopRightSocial(data, SocialTml);
			dirty |= UnstickTopRightSocial(data, SocialTerraria);
			return dirty;
		}

		private static bool UnstickTopRightSocial(WeSaveData data, string id)
		{
			WeElementRecord el = data?.Elements?.Find(item => item.Id == id);
			if (el == null || !el.Customized)
				return false;
			if (el.AnchorX <= 0.80f || el.AnchorY >= 0.18f)
				return false;
			el.Customized = false;
			return true;
		}

		private static Vector2 NativeSocialCenter(bool tml, int h)
		{
			int n = SocialCount(tml);
			Vector2 origin = tml
				? new Vector2(18f, h - 26f - 22f)
				: new Vector2(18f, 12f);
			return origin + new Vector2(n * 15f, 11f);
		}

		private static Rectangle SocialHit(Vector2 center, bool tml)
		{
			int n = SocialCount(tml);
			return Around(center, n * 30f + 8f, 28f);
		}

		private static int SocialCount(bool tml)
		{
			try {
				var links = tml ? Main.tModLoaderTitleLinks : Main.TitleLinks;
				if (links != null && links.Count > 0)
					return links.Count;
			}
			catch {
			}

			return 4;
		}

		private static Rectangle Around(Vector2 pos, float w, float h) =>
			new((int)(pos.X - w * 0.5f), (int)(pos.Y - h * 0.5f), (int)w, (int)h);
	}
}
