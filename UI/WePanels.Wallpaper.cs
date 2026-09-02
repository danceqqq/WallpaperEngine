using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI.Chat;
using WallpaperEngine.Core;
using WallpaperEngine.Grab;

namespace WallpaperEngine.UI
{
	internal static partial class WePanels
	{
		private struct WallHit
		{
			internal Rectangle Rect;
			internal string Slider;
			internal Action Left;
			internal Action Right;
		}

		private static readonly List<WallHit> WallHits = new();
		private static float _wallHeight;
		private static Rectangle _wallView;

		private static void DrawWallpaper(SpriteBatch spriteBatch, Rectangle panel, ref int y)
		{
			WallHits.Clear();
			_wallView = View(panel);
			int start = y;

			DrawSection(spriteBatch, panel, ref y, WeText.UI("SectionSky"));
			DrawSkyChips(spriteBatch, panel, ref y);
			if (WeSave.Data.Wallpaper is WallpaperKind.Color or WallpaperKind.Gradient) {
				DrawRgbArmed(spriteBatch, panel, ref y, "wallA", WeSettings.WallpaperColorA);
				if (WeSave.Data.Wallpaper == WallpaperKind.Gradient)
					DrawRgbArmed(spriteBatch, panel, ref y, "wallB", WeSettings.WallpaperColorB);
			}

			DrawArmedSlider(spriteBatch, panel, ref y, "dim", WeSave.Data.WallpaperDim, WeText.UI("WallpaperDim"));
			DrawArmedSlider(spriteBatch, panel, ref y, "vignette", WeSave.Data.WallpaperVignette, WeText.UI("WallpaperVignette"));
			DrawHitCard(spriteBatch, panel, ref y, WeText.UI("WallpaperParallax"), WeSave.Data.WallpaperParallax, () => {
				WeSave.Data.WallpaperParallax = !WeSave.Data.WallpaperParallax;
				WeSave.Save();
			});

			DrawSection(spriteBatch, panel, ref y, WeText.UI("SectionLayers"));
			DrawTwin(spriteBatch, panel, ref y, WeText.UI("AddImageLayer"), WeText.UI("AddEffectLayer"),
				() => WeSettings.AddImageLayer(WeSave.Data.Wallpaper == WallpaperKind.Image ? WeSave.Data.WallpaperId : ""),
				() => WeSettings.AddEffectLayer(WeFxKind.Stars));

			if (WeSave.Data.Layers.Count == 0)
				DrawHint(spriteBatch, panel, ref y, WeText.UI("EmptyLayers"));
			else {
				foreach (WeLayerRecord layer in WeSave.Data.Layers)
					DrawLayerBlock(spriteBatch, panel, ref y, layer);
			}

			DrawSection(spriteBatch, panel, ref y, WeText.UI("SectionImages"));
			DrawHint(spriteBatch, panel, ref y, WeText.UI("AnimHint"));
			DrawTwin(spriteBatch, panel, ref y, WeText.UI("ImportImage"), WeText.UI("OpenFolder"),
				() => WeArt.TryImportWallpaper(),
				() => WeFiles.OpenFolder(WeSave.WallpaperFolder));

			WeLayerRecord selected = WeSettings.SelectedLayer();
			foreach (WeArtRecord record in WeSave.Data.Wallpapers)
				DrawLibRow(spriteBatch, panel, ref y, record, selected != null && selected.ArtId == record.Id);

			DrawBorrowSection(spriteBatch, panel, ref y, WeCatalog.Skies, WeOfferKind.Sky);
			_wallHeight = Math.Max(0, y - start);
		}

		private static void ClickWallpaper(Rectangle panel, ref int y)
		{
			_ = panel;
			_ = y;
			Point mouse = new(Main.mouseX, Main.mouseY);
			if (!_wallView.Contains(mouse) && _wallView.Width > 0)
				return;

			bool right = WeInput.RightDown;
			for (int i = WallHits.Count - 1; i >= 0; i--) {
				WallHit hit = WallHits[i];
				if (!hit.Rect.Contains(mouse))
					continue;
				if (!string.IsNullOrEmpty(hit.Slider)) {
					if (!right) {
						_dragSlider = hit.Slider;
						ApplySlider(hit.Slider);
					}

					return;
				}

				if (right && hit.Right != null) {
					hit.Right();
					return;
				}

				hit.Left?.Invoke();
				return;
			}
		}

		private static void DrawSection(SpriteBatch spriteBatch, Rectangle panel, ref int y, string text)
		{
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, text,
				new Vector2(panel.X + 18, y + 2), WeAccent.Light * _fade, 0f, Vector2.Zero, new Vector2(0.78f));
			y += 24;
		}

		private static void DrawSkyChips(SpriteBatch spriteBatch, Rectangle panel, ref int y)
		{
			int gap = 6;
			int w = Math.Max(40, (panel.Width - 32 - gap * 2) / 3);
			int x = panel.X + 16;
			DrawChip(spriteBatch, new Rectangle(x, y, w, 32), WeText.UI("SkyVanilla"), WeSave.Data.Wallpaper == WallpaperKind.Vanilla,
				WeSettings.SetWallpaperVanilla);
			DrawChip(spriteBatch, new Rectangle(x + w + gap, y, w, 32), WeText.UI("SkyColor"), WeSave.Data.Wallpaper == WallpaperKind.Color,
				() => WeSettings.SetWallpaperColor(false));
			DrawChip(spriteBatch, new Rectangle(x + (w + gap) * 2, y, w, 32), WeText.UI("SkyGradient"), WeSave.Data.Wallpaper == WallpaperKind.Gradient,
				() => WeSettings.SetWallpaperColor(true));
			y += 40;
		}

		private static void DrawChip(SpriteBatch spriteBatch, Rectangle hit, string text, bool on, Action click)
		{
			bool hover = hit.Contains(Main.mouseX, Main.mouseY);
			WeDraw.Fill(spriteBatch, hit, (on ? WeAccent.Deep : new Color(32, 36, 44)) * ((hover ? 0.95f : 0.8f) * _fade));
			WeDraw.Border(spriteBatch, hit, (on || hover ? WeAccent.Light : WeAccent.Mid) * _fade);
			var font = FontAssets.MouseText.Value;
			Vector2 size = font.MeasureString(text) * 0.68f;
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, font, text,
				new Vector2(hit.X + (hit.Width - size.X) * 0.5f, hit.Y + (hit.Height - size.Y) * 0.5f),
				Color.White * _fade, 0f, Vector2.Zero, new Vector2(0.68f));
			Arm(hit, click);
		}

		private static void DrawTwin(SpriteBatch spriteBatch, Rectangle panel, ref int y, string a, string b, Action left, Action right)
		{
			Rectangle l = new(panel.X + 16, y, (panel.Width - 40) / 2, 32);
			Rectangle r = new(l.Right + 8, y, l.Width, 32);
			DrawMini(spriteBatch, l, a);
			DrawMini(spriteBatch, r, b);
			Arm(l, left);
			Arm(r, right);
			y += 40;
		}

		private static void DrawHitCard(SpriteBatch spriteBatch, Rectangle panel, ref int y, string text, bool on, Action click)
		{
			Rectangle hit = Row(panel, y, 36);
			bool hover = hit.Contains(Main.mouseX, Main.mouseY);
			WeDraw.Fill(spriteBatch, hit, (on ? WeAccent.Deep : new Color(28, 30, 38)) * ((hover ? 0.95f : 0.8f) * _fade));
			WeDraw.Border(spriteBatch, hit, (on || hover ? WeAccent.Light : WeAccent.Mid) * _fade);
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, text,
				new Vector2(hit.X + 12, hit.Y + 8), Color.White * _fade, 0f, Vector2.Zero, new Vector2(0.82f));
			Arm(hit, click);
			y += 42;
		}

		private static void DrawLayerBlock(SpriteBatch spriteBatch, Rectangle panel, ref int y, WeLayerRecord layer)
		{
			Rectangle hit = Row(panel, y, 40);
			Rectangle trash = TrashRect(hit);
			Rectangle body = new(hit.X, hit.Y, Math.Max(8, trash.X - hit.X - 8), hit.Height);
			bool on = layer.Id == WeSave.Data.SelectedLayerId;
			bool hover = body.Contains(Main.mouseX, Main.mouseY);
			WeDraw.Fill(spriteBatch, body, (on ? WeAccent.Deep : new Color(28, 30, 38)) * ((hover ? 0.95f : 0.8f) * _fade));
			WeDraw.Border(spriteBatch, body, (on || hover ? WeAccent.Light : WeAccent.Mid) * _fade);

			int textX = body.X + 12;
			if (layer.Kind == WeLayerKind.Image) {
				WeArtRecord art = WeSave.Data.Wallpapers.Find(item => item.Id == layer.ArtId);
				Texture2D thumb = WeArt.Preview(art, false);
				if (thumb != null) {
					var dest = new Rectangle(body.X + 8, hit.Y + 4, 48, 32);
					float scale = Math.Min(dest.Width / (float)Math.Max(1, thumb.Width), dest.Height / (float)Math.Max(1, thumb.Height));
					spriteBatch.Draw(thumb, dest.Center.ToVector2(), null, Color.White * _fade, 0f, thumb.Size() * 0.5f, scale, SpriteEffects.None, 0f);
					textX = dest.Right + 10;
				}
			}

			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, Ellipsize(LayerPretty(layer), 22),
				new Vector2(textX, hit.Y + 10), Color.White * _fade, 0f, Vector2.Zero, new Vector2(0.8f));
			RoundButton.DrawIcon(spriteBatch, trash.Center.ToVector2(), 13f, WeIcons.Get(WeIcons.Trash), 0f, _fade);
			RoundButton.Tooltip(spriteBatch, trash.Center.ToVector2(), 13f, WeText.UI("RemoveLayer"), _fade);
			Arm(body, () => WeSettings.SelectLayer(layer.Id));
			string id = layer.Id;
			Arm(trash, () => {
				WeSettings.RemoveLayer(id);
				WeToast.Show("ToastLayerGone");
			});
			y += 46;

			if (!on)
				return;

			DrawHitCard(spriteBatch, panel, ref y, WeText.UI("LayerForeground"), layer.Foreground, WeSettings.ToggleSelectedForeground);
			if (layer.Kind == WeLayerKind.Image) {
				DrawHitCard(spriteBatch, panel, ref y, WeText.UI(FitKey(layer.Fit)), true, WeSettings.CycleSelectedFit);
				DrawHitCard(spriteBatch, panel, ref y, WeText.UI("CenterImage"), false, WeSettings.CenterWallpaperPan);
			}
			else
				DrawHitCard(spriteBatch, panel, ref y, WeText.UI(FxKey(layer.Effect)), true, WeSettings.CycleSelectedEffect);

			DrawArmedSlider(spriteBatch, panel, ref y, "lop", layer.Opacity, WeText.UI("LayerOpacity"));
			DrawArmedSlider(spriteBatch, panel, ref y, "lpar", layer.Parallax, WeText.UI("LayerParallax"));
			DrawArmedSlider(spriteBatch, panel, ref y, "lzm", (layer.Zoom - 0.6f) / 1.2f, WeText.UI("LayerZoom"));
			DrawTwin(spriteBatch, panel, ref y, WeText.UI("LayerUp"), WeText.UI("LayerDown"),
				() => WeSettings.MoveSelectedLayer(-1),
				() => WeSettings.MoveSelectedLayer(1));
		}

		private static void DrawLibRow(SpriteBatch spriteBatch, Rectangle panel, ref int y, WeArtRecord record, bool on)
		{
			Rectangle hit = Row(panel, y, 52);
			bool hover = hit.Contains(Main.mouseX, Main.mouseY);
			WeDraw.Fill(spriteBatch, hit, (on ? WeAccent.Deep : new Color(28, 30, 38)) * _fade);
			WeDraw.Border(spriteBatch, hit, (on || hover ? WeAccent.Light : WeAccent.Mid) * _fade);
			Texture2D tex = WeArt.Preview(record, false);
			if (tex != null) {
				var dest = new Rectangle(hit.X + 8, hit.Y + 6, 64, 40);
				float scale = Math.Min(dest.Width / (float)tex.Width, dest.Height / (float)tex.Height);
				spriteBatch.Draw(tex, dest.Center.ToVector2(), null, Color.White * _fade, 0f, tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
			}

			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, PrettyFile(record.FileName),
				new Vector2(hit.X + 82, hit.Y + 16), Color.White * _fade, 0f, Vector2.Zero, new Vector2(0.75f));
			string artId = record.Id;
			Arm(hit,
				() => WeSettings.AssignSelectedImage(artId),
				() => WeArt.Delete(record, false));
			y += 58;
		}

		private static void DrawArmedSlider(SpriteBatch spriteBatch, Rectangle panel, ref int y, string key, float value, string label)
		{
			Rectangle bar = new(panel.X + 90, y + 8, panel.Width - 160, 8);
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, FontAssets.MouseText.Value, label,
				new Vector2(panel.X + 18, y), Color.White * (0.8f * _fade), 0f, Vector2.Zero, new Vector2(0.7f));
			WeDraw.Fill(spriteBatch, bar, Color.White * (0.15f * _fade));
			WeDraw.Fill(spriteBatch, new Rectangle(bar.X, bar.Y, Math.Max(1, (int)(bar.Width * MathHelper.Clamp(value, 0f, 1f))), bar.Height), WeAccent.Mid * _fade);
			ArmSlider(new Rectangle(panel.X + 90, y + 4, panel.Width - 160, 16), key);
			y += 22;
		}

		private static void DrawRgbArmed(SpriteBatch spriteBatch, Rectangle panel, ref int y, string key, Color color)
		{
			DrawArmedSlider(spriteBatch, panel, ref y, key + "R", color.R / 255f, WeText.UI("Red"));
			DrawArmedSlider(spriteBatch, panel, ref y, key + "G", color.G / 255f, WeText.UI("Green"));
			DrawArmedSlider(spriteBatch, panel, ref y, key + "B", color.B / 255f, WeText.UI("Blue"));
			WeDraw.Fill(spriteBatch, new Rectangle(panel.Right - 52, y - 70, 28, 62), color * _fade);
			y += 8;
		}

		private static void Arm(Rectangle rect, Action left, Action right = null)
		{
			rect = Rectangle.Intersect(rect, _wallView);
			if (rect.Width < 2 || rect.Height < 2)
				return;
			WallHits.Add(new WallHit { Rect = rect, Left = left, Right = right });
		}

		private static void ArmSlider(Rectangle rect, string key)
		{
			rect = Rectangle.Intersect(rect, _wallView);
			if (rect.Width < 2 || rect.Height < 2)
				return;
			WallHits.Add(new WallHit { Rect = rect, Slider = key });
		}

		private static string LayerPretty(WeLayerRecord layer)
		{
			if (layer.Kind == WeLayerKind.Effect)
				return WeText.UI(FxKey(layer.Effect));
			return PrettyFile(FileNameOf(layer.ArtId));
		}

		private static string PrettyFile(string file)
		{
			if (string.IsNullOrEmpty(file))
				return WeText.UI("ImageLayer");
			string name = Path.GetFileNameWithoutExtension(file);
			string ext = Path.GetExtension(file);
			if (name.Length >= 24 && HexName(name)) {
				string kind = string.IsNullOrEmpty(ext) ? "IMG" : ext.Trim('.').ToUpperInvariant();
				return kind + "  ·  " + name[..8];
			}

			return Ellipsize(file, 24);
		}

		private static bool HexName(string name)
		{
			foreach (char c in name) {
				bool hex = c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
				if (!hex)
					return false;
			}

			return true;
		}
	}
}
