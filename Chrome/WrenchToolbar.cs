using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using WallpaperEngine.Content;
using WallpaperEngine.Core;
using WallpaperEngine.Layout;
using WallpaperEngine.UI;

namespace WallpaperEngine.Chrome
{
	internal static class WrenchToolbar
	{
		private const float Radius = 22f;
		private const float ChildRadius = 19f;

		private static readonly List<Spark> Sparks = new();
		private static float _open;
		private static float _spin;
		private static bool _expanded;
		private static bool _frameInput;
		private static bool _mouseHeld;
		private static bool _holdLock;
		private static bool _pulse = true;

		private struct Spark
		{
			public Vector2 Position;
			public Vector2 Velocity;
			public float Life;
			public float Max;
			public float Size;
		}

		internal static bool Expanded => !WrenchHub.UseDock && (_expanded || _open > 0.04f);
		internal static bool Busy => WrenchHub.UseDock
			? WrenchDock.Busy
			: Expanded || HoverAny() || (_open > 0.12f && InOrbit());

		internal static Vector2 Anchor => SceneGraph.Pixel(SceneGraph.Wrench);

		internal static float Scale => SceneGraph.ScaleOf(SceneGraph.Wrench);

		internal static Rectangle HitRect() => WrenchHub.UseDock
			? WrenchDock.HitRect()
			: RoundButton.Hit(Anchor, Radius * Scale + 4f);

		internal static void OnThemeSelected()
		{
			_pulse = !WeSave.Data.WrenchOpened;
			if (WeSplash.Visible)
				_pulse = true;
		}

		internal static void Update()
		{
			if (!WeModMenu.OnTitle) {
				_expanded = false;
				_open = 0f;
				Sparks.Clear();
				WrenchDock.Reset();
				return;
			}

			if (WrenchHub.UseDock) {
				_expanded = false;
				_open = MathHelper.Lerp(_open, 0f, 0.28f);
				WrenchDock.Update();
				TickSparks();
				return;
			}

			float target = _expanded ? 1f : 0f;
			_open = MathHelper.Lerp(_open, target, 0.2f);
			if (Math.Abs(_open - target) < 0.01f)
				_open = target;
			_spin = MathHelper.Lerp(_spin, _expanded ? MathHelper.Pi * 0.92f : 0f, 0.18f);
			TickSparks();
		}

		internal static void HandleInput()
		{
			if (_frameInput || LayoutEditor.Editing || WeSplash.Visible)
				return;

			_frameInput = true;
			bool pressed = WeInput.Edge(ref _mouseHeld, ref _holdLock);

			if (!WeModMenu.OnTitle)
				return;

			if (WePanels.IsOpen) {
				Main.blockMouse = true;
				if (WrenchHub.UseDock)
					WrenchDock.HandleInput();
				return;
			}

			if (WrenchHub.UseDock) {
				WrenchDock.HandleInput();
				return;
			}

			if (pressed && HitRect().Contains(Main.mouseX, Main.mouseY)) {
				if (_expanded)
					Collapse();
				else
					Expand();
				WeInput.LockHold(ref _holdLock);
				return;
			}

			if (!_expanded)
				return;

			if (pressed) {
				for (int i = 0; i < WrenchHub.Actions.Length; i++) {
					if (ChildHit(i).Contains(Main.mouseX, Main.mouseY) && ChildAlpha(i) > 0.55f) {
						WrenchHub.Activate(WrenchHub.Actions[i]);
						WeInput.LockHold(ref _holdLock);
						return;
					}
				}

				if (!WePanels.IsOpen && !HitRect().Contains(Main.mouseX, Main.mouseY))
					Collapse();
			}

			if (HoverAny() || InOrbit())
				Main.blockMouse = true;
		}

		internal static void EndFrame()
		{
			_frameInput = false;
			WrenchDock.EndFrame();
		}

		internal static void Expand()
		{
			_expanded = true;
			_pulse = false;
			if (!WeSave.Data.WrenchOpened) {
				WeSave.Data.WrenchOpened = true;
				WeSave.Save();
			}

			Burst();
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		internal static void Collapse()
		{
			_expanded = false;
			SoundEngine.PlaySound(SoundID.MenuClose);
		}

		internal static void Draw(SpriteBatch spriteBatch)
		{
			if (!WeModMenu.OnTitle || !SceneGraph.Visible(SceneGraph.Wrench))
				return;

			if (WrenchHub.UseDock) {
				WrenchDock.Draw(spriteBatch);
				return;
			}

			WeDraw.WithLinear(spriteBatch, () => {
				Vector2 origin = Anchor;
				float scale = Scale;
				float pulse = _pulse ? 1f + MathF.Sin(Main.GlobalTimeWrappedHourly * 4.2f) * 0.08f : 1f;
				float radius = Radius * scale * pulse;
				Texture2D circle = WeDraw.Circle();

				if (_open > 0.02f) {
					float ring = OrbitRadius * 2f / circle.Width;
					spriteBatch.Draw(circle, origin, null, WeAccent.Mid * (0.14f * _open), 0f, circle.Size() * 0.5f, ring, SpriteEffects.None, 0f);
					spriteBatch.Draw(circle, origin, null, WeAccent.Light * (0.22f * _open), 0f, circle.Size() * 0.5f, (OrbitRadius * 2f + 6f) / circle.Width, SpriteEffects.None, 0f);
				}

				spriteBatch.Draw(circle, origin, null, WeAccent.Mid * 0.18f, 0f, circle.Size() * 0.5f, (radius * 2f + 18f) / circle.Width, SpriteEffects.None, 0f);
				Texture2D hub = WeIcons.Get(WeIcons.Setting);
				if (hub != null)
					RoundButton.DrawIcon(spriteBatch, origin, radius, hub, _spin, 1f, _expanded);
				else
					RoundButton.DrawWrench(spriteBatch, origin, radius, _spin, 1f, _expanded);
				RoundButton.Tooltip(spriteBatch, origin, radius, WeText.UI("Wrench"), 1f - _open);

				for (int i = 0; i < WrenchHub.Actions.Length; i++) {
					float alpha = ChildAlpha(i);
					if (alpha < 0.02f)
						continue;

					Vector2 pos = ChildPos(i);
					float childR = ChildRadius * scale * MathHelper.Lerp(0.5f, 1f, alpha);
					if (_open > 0.08f) {
						Color line = WeAccent.Mid * (0.35f * alpha);
						RoundButton.DrawThick(spriteBatch, WeDraw.Pixel, origin, pos, 1.4f, line);
					}

					bool on = WrenchHub.IsOn(WrenchHub.Actions[i]);
					Texture2D icon = WeIcons.Get(WrenchHub.IconName(WrenchHub.Actions[i]));
					if (icon != null)
						RoundButton.DrawIcon(spriteBatch, pos, childR, icon, 0f, alpha, on);
					else
						RoundButton.DrawLetter(spriteBatch, pos, childR, Letter(i), alpha, on);
					RoundButton.TooltipRadial(spriteBatch, pos, origin, childR, WeText.UI(WrenchHub.TipKey(WrenchHub.Actions[i])), alpha);
				}

				DrawSparks(spriteBatch);
			});
		}

		private static string Letter(int index) => index switch {
			0 => "L",
			1 => "W",
			2 => "M",
			3 => "+",
			4 => "G",
			5 => "E",
			6 => "C",
			7 => "H",
			_ => "?"
		};

		private static float OrbitRadius
		{
			get
			{
				float scale = Scale;
				float menuBottom = MenuButtonHooks.LastMenuBottom > 8f ? MenuButtonHooks.LastMenuBottom : Anchor.Y - 90f;
				float themeY = Main.screenHeight - 36f;
				float room = Math.Max(8f, Math.Min(Anchor.Y - menuBottom, themeY - Anchor.Y) - 6f);
				return MathHelper.Clamp(room + ChildRadius, 52f, 94f) * scale;
			}
		}

		private static Vector2 ChildDir(int index)
		{
			float n = WrenchHub.Actions.Length;
			float angle = -MathHelper.PiOver2 + index * MathHelper.TwoPi / n;
			return angle.ToRotationVector2();
		}

		private static Vector2 ChildPos(int index)
		{
			float t = MathHelper.Clamp((_open - index * 0.045f) / 0.42f, 0f, 1f);
			t = t * t * (3f - 2f * t);
			return Anchor + ChildDir(index) * (OrbitRadius * t);
		}

		private static float ChildAlpha(int index) =>
			MathHelper.Clamp((_open - index * 0.045f) / 0.32f, 0f, 1f);

		private static Rectangle ChildHit(int index) =>
			RoundButton.Hit(ChildPos(index), ChildRadius * Scale * MathHelper.Lerp(0.5f, 1f, ChildAlpha(index)));

		private static bool InOrbit()
		{
			float reach = OrbitRadius + ChildRadius * Scale + 10f;
			return Vector2.DistanceSquared(new Vector2(Main.mouseX, Main.mouseY), Anchor) <= reach * reach;
		}

		private static bool HoverAny()
		{
			if (HitRect().Contains(Main.mouseX, Main.mouseY))
				return true;
			if (_open < 0.2f)
				return false;
			for (int i = 0; i < WrenchHub.Actions.Length; i++) {
				if (ChildAlpha(i) > 0.4f && ChildHit(i).Contains(Main.mouseX, Main.mouseY))
					return true;
			}

			return false;
		}

		private static void Burst()
		{
			Vector2 origin = Anchor;
			for (int i = 0; i < 14; i++) {
				float angle = MathHelper.TwoPi * i / 14f + Main.rand.NextFloat(-0.12f, 0.12f);
				Vector2 dir = angle.ToRotationVector2();
				Sparks.Add(new Spark {
					Position = origin,
					Velocity = dir * Main.rand.NextFloat(1.6f, 3.4f),
					Life = 0f,
					Max = Main.rand.NextFloat(0.35f, 0.7f),
					Size = Main.rand.NextFloat(2.2f, 4.6f)
				});
			}
		}

		private static void TickSparks()
		{
			for (int i = Sparks.Count - 1; i >= 0; i--) {
				Spark spark = Sparks[i];
				spark.Life += 1f / 60f;
				spark.Position += spark.Velocity;
				spark.Velocity *= 0.94f;
				if (spark.Life >= spark.Max)
					Sparks.RemoveAt(i);
				else
					Sparks[i] = spark;
			}
		}

		private static void DrawSparks(SpriteBatch spriteBatch)
		{
			Texture2D circle = WeDraw.Circle();
			for (int i = 0; i < Sparks.Count; i++) {
				Spark spark = Sparks[i];
				float t = 1f - spark.Life / spark.Max;
				spriteBatch.Draw(
					circle,
					spark.Position,
					null,
					WeAccent.Light * (0.85f * t),
					0f,
					circle.Size() * 0.5f,
					spark.Size * 2f / circle.Width,
					SpriteEffects.None,
					0f);
			}
		}

		internal static void DrawStylePreview(SpriteBatch spriteBatch, Rectangle dest, int style, float fade, bool selected)
		{
			if (style == (int)WrenchStyle.Dock) {
				WrenchDock.DrawPreview(spriteBatch, dest, fade, selected);
				return;
			}

			Vector2 origin = dest.Center.ToVector2();
			float radius = Math.Min(dest.Width, dest.Height) * 0.16f;
			Texture2D circle = WeDraw.Circle();
			spriteBatch.Draw(circle, origin, null, WeAccent.Mid * (0.22f * fade), 0f, circle.Size() * 0.5f, (radius * 2.8f) / circle.Width, SpriteEffects.None, 0f);
			RoundButton.Draw(spriteBatch, origin, radius, fade, selected);
			Texture2D hub = WeIcons.Get(WeIcons.Setting);
			if (hub != null) {
				float iconScale = radius * 1.1f / Math.Max(1, Math.Max(hub.Width, hub.Height));
				spriteBatch.Draw(hub, origin, null, WeAccent.Icon(false, selected) * fade, 0f, hub.Size() * 0.5f, iconScale, SpriteEffects.None, 0f);
			}

			int n = WrenchHub.Actions.Length;
			float orbit = Math.Min(dest.Width, dest.Height) * 0.36f;
			for (int i = 0; i < n; i++) {
				float angle = -MathHelper.PiOver2 + i * MathHelper.TwoPi / n;
				Vector2 pos = origin + angle.ToRotationVector2() * orbit;
				RoundButton.Draw(spriteBatch, pos, radius * 0.42f, fade * 0.9f, selected && i == 2);
			}
		}
	}
}
