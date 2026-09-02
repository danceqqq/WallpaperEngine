using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace WallpaperEngine.Core
{
	internal sealed class WeAnimFrame
	{
		internal int DelayMs;
		internal byte Disposal;
		internal int X;
		internal int Y;
		internal int W;
		internal int H;
		internal Color[] Pixels;
		internal bool BlendOver;
	}

	internal sealed class WeClip
	{
		internal int Width;
		internal int Height;
		internal DateTime Write;
		internal WeAnimFrame[] Frames;

		private Texture2D[] _gpu;
		private Color[][] _baked;
		private Color[] _canvas;
		private Color[] _backup;
		private int[] _delays;

		internal Texture2D Current()
		{
			if (_gpu == null || _gpu.Length < 2)
				return null;
			int want = IndexAt(Tick());
			want = Math.Clamp(want, 0, _gpu.Length - 1);
			Texture2D tex = _gpu[want];
			return tex != null && !tex.IsDisposed ? tex : null;
		}

		internal void Present()
		{
			KeepDelays();
			Bake();
			Upload();
		}

		internal void KeepDelays()
		{
			if (_delays != null || Frames == null)
				return;
			_delays = new int[Frames.Length];
			for (int i = 0; i < Frames.Length; i++)
				_delays[i] = Math.Max(20, Frames[i].DelayMs);
		}

		private void Bake()
		{
			if (_baked != null || Frames == null || Frames.Length < 2 || Width < 1 || Height < 1)
				return;
			_canvas = new Color[Width * Height];
			_baked = new Color[Frames.Length][];
			for (int i = 0; i < Frames.Length; i++) {
				Paint(i);
				_baked[i] = (Color[])_canvas.Clone();
			}

			_canvas = null;
			_backup = null;
			Frames = null;
		}

		private void Upload()
		{
			if (_gpu != null || _baked == null || _baked.Length < 2 || !WeAnim.CanUpload)
				return;
			GraphicsDevice device = Main.instance?.GraphicsDevice ?? Main.graphics?.GraphicsDevice;
			if (device == null)
				return;
			Unbind(device);
			_gpu = new Texture2D[_baked.Length];
			try {
				for (int i = 0; i < _baked.Length; i++) {
					var tex = new Texture2D(device, Width, Height, false, SurfaceFormat.Color);
					tex.SetData(_baked[i]);
					_gpu[i] = tex;
				}
			}
			catch {
				DisposeGpu();
			}
		}

		private static void Unbind(GraphicsDevice device)
		{
			if (device == null)
				return;
			try {
				for (int i = 0; i < 16; i++)
					device.Textures[i] = null;
			}
			catch {
			}
		}

		private void Paint(int index)
		{
			if (index > 0)
				DisposePrevious(Frames[index - 1]);

			WeAnimFrame frame = Frames[index];
			if (frame.Disposal == 3)
				Snapshot();
			Blit(frame);
		}

		private void DisposePrevious(WeAnimFrame prev)
		{
			if (prev.Disposal == 2)
				ClearRect(prev.X, prev.Y, prev.W, prev.H);
			else if (prev.Disposal == 3 && _backup != null && _backup.Length == _canvas.Length)
				Array.Copy(_backup, _canvas, _canvas.Length);
		}

		private void Snapshot()
		{
			if (_backup == null || _backup.Length != _canvas.Length)
				_backup = new Color[_canvas.Length];
			Array.Copy(_canvas, _backup, _canvas.Length);
		}

		private void ClearRect(int x, int y, int w, int h)
		{
			int x0 = Math.Max(0, x);
			int y0 = Math.Max(0, y);
			int x1 = Math.Min(Width, x + w);
			int y1 = Math.Min(Height, y + h);
			for (int row = y0; row < y1; row++)
				Array.Clear(_canvas, row * Width + x0, x1 - x0);
		}

		private void Blit(WeAnimFrame frame)
		{
			if (frame.Pixels == null || frame.W < 1 || frame.H < 1)
				return;

			for (int row = 0; row < frame.H; row++) {
				int dy = frame.Y + row;
				if ((uint)dy >= (uint)Height)
					continue;
				for (int col = 0; col < frame.W; col++) {
					int dx = frame.X + col;
					if ((uint)dx >= (uint)Width)
						continue;
					Color src = frame.Pixels[row * frame.W + col];
					int di = dy * Width + dx;
					if (frame.BlendOver)
						_canvas[di] = Over(_canvas[di], src);
					else if (src.A != 0)
						_canvas[di] = src;
				}
			}
		}

		private static Color Over(Color dst, Color src)
		{
			if (src.A == 0)
				return dst;
			if (src.A == 255 || dst.A == 0)
				return src;
			float sa = src.A / 255f;
			float da = dst.A / 255f;
			float ia = 1f - sa;
			float oa = sa + da * ia;
			if (oa < 0.001f)
				return Color.Transparent;
			float r = (src.R * sa + dst.R * da * ia) / oa;
			float g = (src.G * sa + dst.G * da * ia) / oa;
			float b = (src.B * sa + dst.B * da * ia) / oa;
			return new Color(
				(byte)Math.Clamp(r, 0f, 255f),
				(byte)Math.Clamp(g, 0f, 255f),
				(byte)Math.Clamp(b, 0f, 255f),
				(byte)Math.Clamp(oa * 255f, 0f, 255f));
		}

		private int IndexAt(long nowMs)
		{
			int n = _gpu?.Length ?? _baked?.Length ?? Frames?.Length ?? _delays?.Length ?? 0;
			if (n < 2)
				return 0;
			int total = 0;
			for (int i = 0; i < n; i++)
				total += DelayOf(i);
			if (total < 1)
				return 0;
			int t = (int)(nowMs % total);
			int acc = 0;
			for (int i = 0; i < n; i++) {
				acc += DelayOf(i);
				if (t < acc)
					return i;
			}

			return n - 1;
		}

		private int DelayOf(int i)
		{
			if (_delays != null && (uint)i < (uint)_delays.Length)
				return _delays[i];
			if (Frames != null && (uint)i < (uint)Frames.Length)
				return Math.Max(20, Frames[i].DelayMs);
			return 80;
		}

		private static long Tick()
		{
			try {
				return Environment.TickCount64;
			}
			catch {
				return (long)(Main.GlobalTimeWrappedHourly * 1000f);
			}
		}

		internal void Dispose()
		{
			DisposeGpu();
			Frames = null;
			_baked = null;
			_delays = null;
			_canvas = null;
			_backup = null;
		}

		private void DisposeGpu()
		{
			if (_gpu == null)
				return;
			foreach (Texture2D tex in _gpu)
				DisposeTex(tex);
			_gpu = null;
		}

		internal static void DisposeTex(Texture2D tex)
		{
			if (tex == null || tex.IsDisposed)
				return;
			Texture2D hold = tex;
			Main.QueueMainThreadAction(() => {
				try {
					if (!hold.IsDisposed)
						hold.Dispose();
				}
				catch {
				}
			});
		}
	}

	internal static class WeAnim
	{
		private const int MaxSide = 4096;
		private const int MaxFrames = 256;

		private static readonly Dictionary<string, WeClip> Clips = new(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, DateTime> KnownStill = new(StringComparer.OrdinalIgnoreCase);

		internal static bool CanUpload;

		internal static bool Fits(int w, int h, int frames) =>
			w >= 1 && h >= 1 && w <= MaxSide && h <= MaxSide && frames >= 1 && frames <= MaxFrames;

		internal static Texture2D Play(string path, DateTime write) => Frame(path, write);

		internal static Texture2D Frame(string path, DateTime write)
		{
			WeClip clip = GetOrLoad(path, write);
			return clip?.Current();
		}

		internal static bool Loaded(string path) =>
			!string.IsNullOrEmpty(path) && Clips.ContainsKey(path);

		internal static void Advance(string path, DateTime write)
		{
			GetOrLoad(path, write)?.Present();
		}

		internal static void Pulse()
		{
			CanUpload = true;
			try {
				AdvanceActive();
			}
			finally {
				CanUpload = false;
			}
		}

		internal static void AdvanceActive()
		{
			foreach (WeLayerRecord layer in WeSave.Data.Layers) {
				if (layer == null || layer.Kind != WeLayerKind.Image || string.IsNullOrEmpty(layer.ArtId))
					continue;
				AdvanceArt(WeSave.WallpaperFolder, layer.ArtId, WeSave.Data.Wallpapers);
			}

			if (WeSave.Data.Wallpaper == WallpaperKind.Image)
				AdvanceArt(WeSave.WallpaperFolder, WeSave.Data.WallpaperId, WeSave.Data.Wallpapers);

			if (WeSave.Data.Logo == LogoKind.Custom)
				AdvanceArt(WeSave.LogoFolder, WeSave.Data.LogoId, WeSave.Data.Logos);
		}

		private static void AdvanceArt(string folder, string id, List<WeArtRecord> records)
		{
			if (string.IsNullOrEmpty(id) || records == null)
				return;
			WeArtRecord record = null;
			foreach (WeArtRecord item in records) {
				if (item.Id == id) {
					record = item;
					break;
				}
			}

			if (record == null || string.IsNullOrEmpty(record.FileName))
				return;
			string path = Path.Combine(folder, record.FileName);
			if (!File.Exists(path))
				return;
			Advance(path, File.GetLastWriteTimeUtc(path));
		}

		private static WeClip GetOrLoad(string path, DateTime write)
		{
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
				return null;

			if (Clips.TryGetValue(path, out WeClip clip)) {
				if (clip != null && clip.Write == write)
					return clip;
				Drop(path);
			}

			if (KnownStill.TryGetValue(path, out DateTime stillAt) && stillAt == write)
				return null;

			if (!LooksAnimated(path)) {
				KnownStill[path] = write;
				return null;
			}

			clip = Load(path);
			if (clip == null)
				return null;
			if (clip.Frames == null || clip.Frames.Length < 2) {
				if (clip.Frames is { Length: 1 })
					KnownStill[path] = write;
				clip.Dispose();
				return null;
			}

			clip.Write = write;
			clip.KeepDelays();
			Clips[path] = clip;
			return clip;
		}

		private static bool LooksAnimated(string path)
		{
			try {
				using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
				Span<byte> head = stackalloc byte[8];
				int n = stream.Read(head);
				if (n >= 6 && head[0] == (byte)'G' && head[1] == (byte)'I' && head[2] == (byte)'F')
					return true;
				if (n < 8 || head[0] != 137 || head[1] != 80 || head[2] != 78 || head[3] != 71)
					return false;

				var buf = new byte[8];
				while (true) {
					if (stream.Read(buf, 0, 8) < 8)
						return false;
					int len = (buf[0] << 24) | (buf[1] << 16) | (buf[2] << 8) | buf[3];
					if (len < 0)
						return false;
					char a = (char)buf[4];
					char b = (char)buf[5];
					char c = (char)buf[6];
					char d = (char)buf[7];
					if (a == 'a' && b == 'c' && c == 'T' && d == 'L')
						return true;
					if (a == 'I' && b == 'D' && c == 'A' && d == 'T')
						return false;
					if (a == 'I' && b == 'E' && c == 'N' && d == 'D')
						return false;
					long skip = (long)len + 4;
					if (skip < 4 || stream.Position + skip > stream.Length)
						return false;
					stream.Seek(skip, SeekOrigin.Current);
				}
			}
			catch {
				return false;
			}
		}

		internal static void Drop(string path)
		{
			if (string.IsNullOrEmpty(path))
				return;
			KnownStill.Remove(path);
			if (!Clips.Remove(path, out WeClip clip) || clip == null)
				return;
			clip.Dispose();
		}

		internal static void Unload()
		{
			foreach (WeClip clip in Clips.Values)
				clip?.Dispose();
			Clips.Clear();
			KnownStill.Clear();
		}

		private static WeClip Load(string path)
		{
			try {
				var info = new FileInfo(path);
				if (!info.Exists || info.Length < 16 || info.Length > 48L * 1024 * 1024)
					return null;
				byte[] data = File.ReadAllBytes(path);
				if (data.Length < 16)
					return null;
				if (WeGif.LooksLike(data))
					return WeGif.Decode(data);
				if (WeApng.LooksLike(data))
					return WeApng.Decode(data);
			}
			catch {
			}

			return null;
		}
	}
}
