using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.UI.Chat;
using WallpaperEngine.Chrome;
using WallpaperEngine.Content;
using WallpaperEngine.Core;
using WallpaperEngine.UI;
using WallpaperEngine.Audio;

namespace WallpaperEngine.Layout
{
	internal static class LayoutEditor
	{
		private static bool _editing;
		private static bool _dragging;
		private static bool _panning;
		private static bool _frameInput;
		private static bool _mouseHeld;
		private static string _selected = "";
		private static Vector2 _dragOffset;
		private static Vector2 _lastMouse;
		private static Vector2 _workPan = new(0.5f, 0.5f);
		private static readonly Dictionary<string, WeElementRecord> Work = new();
		private static readonly Dictionary<string, WeElementRecord> BeginSnap = new();
		private static readonly Dictionary<string, WeElementRecord> Undo = new();
		private static float _dim;
		private static int _lastWheel;
		private static bool _esc;
		private static bool _z;
		private static bool _del;
		private static int _blockTheme;

		internal static bool Editing => _editing;
		internal static bool Busy => _dragging || _panning;
		internal static float Dim => _dim;
		internal static string Selected => _selected;
		internal static Vector2 WorkPan => _workPan;
		internal static bool ShouldBlockThemeSwap => _editing || _blockTheme > 0;

		internal static bool TryWork(string id, out WeElementRecord record) => Work.TryGetValue(id, out record);

		internal static void Reset()
		{
			Cancel(restore: false);
			_dim = 0f;
		}

		internal static void Begin(string focus = "")
		{
			WePanels.Close();
			Work.Clear();
			BeginSnap.Clear();
			foreach (string id in SceneGraph.Ids) {
				WeElementRecord live = Clone(SceneGraph.Find(id) ?? SceneGraph.DefaultRecord(id));
				if (!live.Customized) {
					Vector2 px = SceneGraph.DefaultPixel(id);
					live.AnchorX = pixelX(px.X);
					live.AnchorY = pixelY(px.Y);
				}

				Work[id] = Clone(live);
				BeginSnap[id] = Clone(live);
			}

			_workPan = WeSettings.WallpaperPan;
			_editing = true;
			_selected = SceneGraph.Visible(focus) ? focus : "";
			_blockTheme = 12;
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		internal static void Save()
		{
			if (!_editing)
				return;

			FinishDrag();
			foreach (WeElementRecord record in Work.Values) {
				WeElementRecord live = SceneGraph.Find(record.Id);
				if (live == null) {
					WeSave.Data.Elements.Add(Clone(record));
					continue;
				}

				live.Visible = record.Visible;
				live.Customized = record.Customized;
				live.AnchorX = record.AnchorX;
				live.AnchorY = record.AnchorY;
				live.Scale = record.Scale;
			}

			WeSettings.SaveWallpaperPan(_workPan);
			WeSave.Save();
			_editing = false;
			_blockTheme = 12;
			Main.mouseLeftRelease = false;
			SoundEngine.PlaySound(SoundID.MenuClose);
		}

		internal static void Cancel(bool restore)
		{
			FinishDrag();
			if (_editing && restore)
				SoundEngine.PlaySound(SoundID.MenuClose);
			_editing = false;
			_selected = "";
			Work.Clear();
		}

		internal static void Update()
		{
			if (!WeModMenu.OnTitle) {
				Cancel(false);
				_dim = 0f;
				return;
			}

			_dim = MathHelper.Lerp(_dim, _editing ? 1f : 0f, 0.18f);
			if (_blockTheme > 0)
				_blockTheme--;
			if (_dim < 0.004f && !_editing)
				_dim = 0f;
			else if (_dim > 0.996f && _editing)
				_dim = 1f;
		}

		internal static void HandleInput()
		{
			if (_frameInput)
				return;
			_frameInput = true;

			bool esc = Main.keyState.IsKeyDown(Keys.Escape);
			if (esc && !_esc && _editing && !WeSplash.Visible && !WePanels.IsOpen) {
				Cancel(true);
				_esc = true;
				return;
			}

			_esc = esc;
			if (!_editing)
				return;

			bool ctrl = Main.keyState.IsKeyDown(Keys.LeftControl) || Main.keyState.IsKeyDown(Keys.RightControl);
			bool z = Main.keyState.IsKeyDown(Keys.Z);
			if (ctrl && z && !_z)
				Restore(Undo);
			_z = z;

			bool del = Main.keyState.IsKeyDown(Keys.Delete);
			if (del && !_del)
				HideSelected();
			_del = del;

			if (Main.keyState.IsKeyDown(Keys.Enter)) {
				Save();
				return;
			}

			int wheel = Mouse.GetState().ScrollWheelValue;
			float delta = (wheel - _lastWheel) / 120f;
			_lastWheel = wheel;
			if (Math.Abs(delta) > 0.01f && !string.IsNullOrEmpty(_selected) && Work.TryGetValue(_selected, out WeElementRecord scaled)) {
				PushUndo();
				scaled.Scale = MathHelper.Clamp(scaled.Scale + delta * 0.08f, 0.35f, 2.4f);
				scaled.Customized = true;
			}

			bool pressed = Main.mouseLeft && !_mouseHeld;
			_mouseHeld = Main.mouseLeft;

			if (ToolbarHit()) {
				Main.blockMouse = true;
				HandleToolbar(pressed);
				return;
			}

			if (_dragging) {
				UpdateDrag();
				return;
			}

			if (_panning) {
				UpdatePan();
				return;
			}

			if (WePanels.IsOpen || WeSplash.Visible)
				return;

			if (pressed) {
				string hit = HitTest();
				if (!string.IsNullOrEmpty(hit)) {
					_selected = hit;
					BeginDrag(hit);
					return;
				}

			if (WeSettings.Current.Wallpaper == WallpaperKind.Image && WeArt.TryGetWallpaper(out _)) {
					_panning = true;
					_lastMouse = new Vector2(Main.mouseX, Main.mouseY);
					Main.mouseLeftRelease = false;
					Main.blockMouse = true;
				}
			}
		}

		internal static void EndFrame() => _frameInput = false;

		internal static void Draw(SpriteBatch spriteBatch, float fade)
		{
			if (_dim <= 0.01f)
				return;

			WeDraw.Fill(spriteBatch, WeDraw.CoverRect, Color.Black * (0.45f * _dim));
			if (!_editing)
				return;

			Color idle = WeAccent.Mid * (0.75f * fade);
			Color hot = WeAccent.Hover * fade;
			foreach (string id in SceneGraph.VisibleIds()) {
				Rectangle hit = SceneGraph.Hit(id);
				DrawBox(spriteBatch, hit, id == _selected || id == HitTest() ? hot : idle);
			}

			DrawCenterGuides(spriteBatch, fade);
			DrawToolbar(spriteBatch, fade);

			if (WeSettings.Current.Wallpaper == WallpaperKind.Image || WeSettings.SelectedLayer() is { Kind: WeLayerKind.Image }) {
				var font = FontAssets.MouseText.Value;
				string pan = WeText.UI("DragToPan");
				Vector2 size = font.MeasureString(pan) * 0.72f;
				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch, font, pan,
					new Vector2((Main.screenWidth - size.X) * 0.5f, 22f),
					new Color(255, 236, 236) * fade, 0f, Vector2.Zero, new Vector2(0.72f));
			}

			if (!string.IsNullOrEmpty(_selected) && Work.TryGetValue(_selected, out WeElementRecord rec)) {
				Rectangle hit = SceneGraph.Hit(_selected);
				string scale = $"{MathF.Round(rec.Scale * 100f)}%  ·  {WeText.UI("ScrollToResize")}";
				ChatManager.DrawColorCodedStringWithShadow(
					spriteBatch, FontAssets.MouseText.Value, scale,
					new Vector2(hit.X, hit.Bottom + 6),
					Color.White * fade, 0f, Vector2.Zero, new Vector2(0.72f));
			}
		}

		internal static void HideSelected()
		{
			if (string.IsNullOrEmpty(_selected) || !SceneGraph.CanHide(_selected))
				return;

			PushUndo();
			if (Work.TryGetValue(_selected, out WeElementRecord record))
				record.Visible = false;
			_selected = "";
		}

		internal static void RestoreHidden(string id)
		{
			WeElementRecord live = SceneGraph.Find(id);
			if (live != null)
				live.Visible = true;
			if (Work.TryGetValue(id, out WeElementRecord work))
				work.Visible = true;
			WeSave.Save();
		}

		private static void HandleToolbar(bool pressed)
		{
			Rectangle save = SaveHit();
			Rectangle cancel = CancelHit();
			Rectangle reset = ResetHit();
			if (!pressed)
				return;

			if (save.Contains(Main.mouseX, Main.mouseY))
				Save();
			else if (cancel.Contains(Main.mouseX, Main.mouseY))
				Cancel(true);
			else if (reset.Contains(Main.mouseX, Main.mouseY)) {
				WeSettings.ResetVanillaTheme();
				WePlaylist.OnThemeSelected();
				WeToast.Show("ToastReset");
				Begin();
			}

			Main.mouseLeftRelease = false;
		}

		private static bool ToolbarHit() =>
			SaveHit().Contains(Main.mouseX, Main.mouseY) ||
			CancelHit().Contains(Main.mouseX, Main.mouseY) ||
			ResetHit().Contains(Main.mouseX, Main.mouseY);

		private static void DrawToolbar(SpriteBatch spriteBatch, float fade)
		{
			DrawChip(spriteBatch, SaveHit(), WeText.UI("SaveLayout"), fade);
			DrawChip(spriteBatch, CancelHit(), WeText.UI("CancelLayout"), fade);
			DrawChip(spriteBatch, ResetHit(), WeText.UI("ResetVanilla"), fade);
		}

		private static void DrawChip(SpriteBatch spriteBatch, Rectangle hit, string text, float fade)
		{
			bool hover = hit.Contains(Main.mouseX, Main.mouseY);
			WeDraw.Fill(spriteBatch, hit, new Color(24, 26, 32) * ((hover ? 0.94f : 0.8f) * fade));
			WeDraw.Border(spriteBatch, hit, (hover ? WeAccent.Hover : WeAccent.Mid) * fade);
			var font = FontAssets.MouseText.Value;
			Vector2 size = font.MeasureString(text) * 0.78f;
			ChatManager.DrawColorCodedStringWithShadow(
				spriteBatch, font, text,
				new Vector2(hit.X + (hit.Width - size.X) * 0.5f, hit.Y + (hit.Height - size.Y) * 0.5f),
				Color.White * fade, 0f, Vector2.Zero, new Vector2(0.78f));
		}

		private static Rectangle SaveHit() => Chip(0);
		private static Rectangle CancelHit() => Chip(1);
		private static Rectangle ResetHit() => Chip(2);

		private static Rectangle Chip(int index)
		{
			int width = 148;
			int y = Main.screenHeight - 58;
			int x = (Main.screenWidth - (width * 3 + 16)) / 2 + index * (width + 8);
			return new Rectangle(x, y, width, 34);
		}

		private static string HitTest()
		{
			foreach (string id in SceneGraph.VisibleIds()) {
				if (SceneGraph.Hit(id).Contains(Main.mouseX, Main.mouseY))
					return id;
			}

			return "";
		}

		private static void BeginDrag(string id)
		{
			PushUndo();
			_dragging = true;
			Rectangle hit = SceneGraph.Hit(id);
			_dragOffset = new Vector2(Main.mouseX, Main.mouseY) - hit.Center.ToVector2();
			Main.mouseLeftRelease = false;
			Main.blockMouse = true;
		}

		private static void UpdateDrag()
		{
			if (!Main.mouseLeft) {
				FinishDrag();
				return;
			}

			Vector2 mouse = new(Main.mouseX, Main.mouseY);
			Vector2 next = mouse - _dragOffset;
			if (Math.Abs(next.X - Main.screenWidth * 0.5f) < 8f)
				next.X = Main.screenWidth * 0.5f;
			if (Math.Abs(next.Y - Main.screenHeight * 0.5f) < 8f)
				next.Y = Main.screenHeight * 0.5f;

			next.X = MathHelper.Clamp(next.X, 24f, Main.screenWidth - 24f);
			next.Y = MathHelper.Clamp(next.Y, 24f, Main.screenHeight - 24f);
			if (Work.TryGetValue(_selected, out WeElementRecord record)) {
				record.Customized = true;
				record.AnchorX = pixelX(next.X);
				record.AnchorY = pixelY(next.Y);
			}

			Main.blockMouse = true;
		}

		private static void UpdatePan()
		{
			if (!Main.mouseLeft) {
				_panning = false;
				return;
			}

			if (WeArt.TryGetWallpaper(out Texture2D tex) && (WeSettings.SelectedLayer()?.Fit ?? WeSave.Data.WallpaperFit) == WallpaperFit.Cover) {
				Rectangle dest = WeDraw.CoverDestination(tex, _workPan);
				float extraX = Math.Max(1, dest.Width - Main.screenWidth);
				float extraY = Math.Max(1, dest.Height - Main.screenHeight);
				Vector2 mouse = new(Main.mouseX, Main.mouseY);
				Vector2 delta = mouse - _lastMouse;
				_workPan.X = MathHelper.Clamp(_workPan.X - delta.X / extraX, 0f, 1f);
				_workPan.Y = MathHelper.Clamp(_workPan.Y - delta.Y / extraY, 0f, 1f);
				_lastMouse = mouse;
			}

			Main.blockMouse = true;
		}

		private static void FinishDrag()
		{
			_dragging = false;
			_panning = false;
		}

		private static void PushUndo()
		{
			Undo.Clear();
			foreach (var pair in Work)
				Undo[pair.Key] = Clone(pair.Value);
		}

		private static void Restore(Dictionary<string, WeElementRecord> source)
		{
			if (source.Count == 0)
				return;
			foreach (var pair in source)
				Work[pair.Key] = Clone(pair.Value);
		}

		private static void DrawCenterGuides(SpriteBatch spriteBatch, float fade)
		{
			if (string.IsNullOrEmpty(_selected) || !Work.TryGetValue(_selected, out WeElementRecord record))
				return;

			Vector2 pos = new(record.AnchorX * Main.screenWidth, record.AnchorY * Main.screenHeight);
			if (Math.Abs(pos.X - Main.screenWidth * 0.5f) < 10f)
				WeDraw.Fill(spriteBatch, new Rectangle(Main.screenWidth / 2, 0, 1, Main.screenHeight), WeAccent.Light * (0.55f * fade));
			if (Math.Abs(pos.Y - Main.screenHeight * 0.5f) < 10f)
				WeDraw.Fill(spriteBatch, new Rectangle(0, Main.screenHeight / 2, Main.screenWidth, 1), WeAccent.Light * (0.55f * fade));
		}

		private static void DrawBox(SpriteBatch spriteBatch, Rectangle rect, Color color)
		{
			if (rect.IsEmpty)
				return;
			WeDraw.Border(spriteBatch, rect, color);
		}

		private static WeElementRecord Clone(WeElementRecord src) => new()
		{
			Id = src.Id,
			Visible = src.Visible,
			Customized = src.Customized,
			AnchorX = src.AnchorX,
			AnchorY = src.AnchorY,
			Scale = src.Scale
		};

		private static float pixelX(float x) => MathHelper.Clamp(x / Math.Max(1, Main.screenWidth), 0f, 1f);
		private static float pixelY(float y) => MathHelper.Clamp(y / Math.Max(1, Main.screenHeight), 0f, 1f);
	}
}
