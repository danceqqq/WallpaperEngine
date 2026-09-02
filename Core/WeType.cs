using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace WallpaperEngine.Core
{
	internal sealed class WeFontOffer
	{
		internal string FileName;
		internal string Family;
		internal string Path;
	}

	internal static class WeType
	{
		private const int RasterPx = 48;
		private const uint FrPrivate = 0x10;
		private const int FwNormal = 400;
		private const byte DefaultCharset = 1;
		private const byte ClearTypeQuality = 5;
		private const byte OutTtOnly = 3;

		private static readonly List<WeFontOffer> Offers = new();
		private static readonly Dictionary<int, Glyph> Glyphs = new();
		private static readonly Dictionary<string, Texture2D> Previews = new(StringComparer.OrdinalIgnoreCase);

		private static string _loadedPath = "";
		private static string _face = "";
		private static bool _resourceAdded;
		private static Texture2D _atlas;
		private static Color[] _pixels;
		private static int _atlasW;
		private static int _atlasH;
		private static int _packX;
		private static int _packY;
		private static int _rowH;
		private static float _line = RasterPx;
		private static float _ascent = RasterPx * 0.8f;
		private static IntPtr _hdc;
		private static IntPtr _hfont;
		private static IntPtr _oldFont;
		private static string _deadFile = "";

		private struct Glyph
		{
			internal Rectangle Source;
			internal Vector2 Offset;
			internal float Advance;
			internal bool Empty;
		}

		internal static IReadOnlyList<WeFontOffer> All => Offers;
		internal static bool Active => _atlas != null && !_atlas.IsDisposed && !string.IsNullOrEmpty(_loadedPath);
		internal static float Line => _line;
		internal static string LoadedFile => string.IsNullOrEmpty(_loadedPath) ? "" : Path.GetFileName(_loadedPath);

		internal static void Ensure()
		{
			if (Active)
				return;
			string want = WeSave.Data.FontFile ?? "";
			if (string.IsNullOrEmpty(want))
				return;
			if (Main.graphics?.GraphicsDevice == null)
				return;
			if (string.Equals(_deadFile, want, StringComparison.OrdinalIgnoreCase))
				return;
			Scan();
			if (!Active)
				_deadFile = want;
		}

		internal static void Scan()
		{
			_deadFile = "";
			Offers.Clear();
			try {
				WeSave.EnsureFolders();
				if (Directory.Exists(WeSave.FontFolder)) {
					foreach (string path in Directory.GetFiles(WeSave.FontFolder)) {
						string ext = Path.GetExtension(path);
						if (!ext.Equals(".ttf", StringComparison.OrdinalIgnoreCase) &&
						    !ext.Equals(".otf", StringComparison.OrdinalIgnoreCase))
							continue;
						Offers.Add(new WeFontOffer {
							FileName = Path.GetFileName(path),
							Family = ReadFamily(path) ?? Path.GetFileNameWithoutExtension(path),
							Path = path
						});
					}

					Offers.Sort((a, b) => string.Compare(a.Family, b.Family, StringComparison.OrdinalIgnoreCase));
				}
			}
			catch {
			}

			string want = WeSave.Data.FontFile ?? "";
			if (string.IsNullOrEmpty(want)) {
				Drop();
				return;
			}

			WeFontOffer match = Offers.Find(item => string.Equals(item.FileName, want, StringComparison.OrdinalIgnoreCase));
			if (match == null) {
				WeSave.Data.FontFile = "";
				Drop();
				return;
			}

			if (!string.Equals(_loadedPath, match.Path, StringComparison.OrdinalIgnoreCase))
				Load(match.Path, match.Family);
			if (!Active)
				_deadFile = want;
		}

		internal static bool TryImport()
		{
			if (!WeFiles.TryPickFont(out string path))
				return false;

			try {
				WeSave.EnsureFolders();
				string dest = WeFiles.UniquePath(WeSave.FontFolder, Path.GetFileName(path));
				File.Copy(path, dest, overwrite: false);
				WeSave.Data.FontFile = Path.GetFileName(dest);
				WeSave.Save();
				Scan();
				if (!Active) {
					WeSave.Data.FontFile = "";
					WeSave.Save();
					WeToast.Show("ToastFontFail");
					return false;
				}

				WeToast.Show("ToastFont");
				return true;
			}
			catch {
				WeToast.Show("ToastFontFail");
				return false;
			}
		}

		internal static void Select(string fileName)
		{
			WeSave.Data.FontFile = fileName ?? "";
			WeSave.Save();
			Scan();
		}

		internal static void Clear()
		{
			WeSave.Data.FontFile = "";
			WeSave.Save();
			Drop();
			Scan();
		}

		internal static void Delete(WeFontOffer offer)
		{
			if (offer == null)
				return;
			try {
				bool was = string.Equals(offer.FileName, WeSave.Data.FontFile, StringComparison.OrdinalIgnoreCase);
				if (File.Exists(offer.Path))
					File.Delete(offer.Path);
				if (was)
					WeSave.Data.FontFile = "";
				WeSave.Save();
			}
			catch {
			}

			DropPreview(offer.FileName);
			Scan();
			WeToast.Show("ToastFontGone");
		}

		internal static Vector2 Measure(string text)
		{
			if (string.IsNullOrEmpty(text) || !Active)
				return Vector2.Zero;
			EnsureString(text);
			float w = 0f;
			foreach (char c in text) {
				if (c == '\n')
					continue;
				if (Glyphs.TryGetValue(c, out Glyph g))
					w += g.Advance;
			}

			return new Vector2(w, _line);
		}

		internal static void Draw(
			SpriteBatch spriteBatch,
			string text,
			Vector2 position,
			Color color,
			float rotation,
			Vector2 origin,
			Vector2 scale,
			SpriteEffects effects,
			float layerDepth)
		{
			if (!Active || string.IsNullOrEmpty(text) || color.A < 8)
				return;
			EnsureString(text);
			if (_atlas == null || _atlas.IsDisposed)
				return;

			Vector2 pen = Vector2.Zero;
			foreach (char c in text) {
				if (!Glyphs.TryGetValue(c, out Glyph g))
					continue;
				if (!g.Empty && g.Source.Width > 0 && g.Source.Height > 0) {
					Vector2 local = pen + g.Offset - origin;
					Vector2 scaled = new(local.X * scale.X, local.Y * scale.Y);
					Vector2 draw = position + Rotate(scaled, rotation);
					spriteBatch.Draw(
						_atlas, draw, g.Source, color, rotation, Vector2.Zero, scale,
						effects, layerDepth);
				}

				pen.X += g.Advance;
			}
		}

		internal static Texture2D PreviewOf(string fileName)
		{
			if (string.IsNullOrEmpty(fileName))
				return null;
			if (Previews.TryGetValue(fileName, out Texture2D cached) && cached != null && !cached.IsDisposed)
				return cached;

			WeFontOffer offer = Offers.Find(item => string.Equals(item.FileName, fileName, StringComparison.OrdinalIgnoreCase));
			if (offer == null)
				return null;
			Texture2D tex = BakePreview(offer.Path, offer.Family);
			if (tex != null)
				Previews[fileName] = tex;
			return tex;
		}

		internal static void Unload()
		{
			Drop();
			foreach (Texture2D tex in Previews.Values)
				DisposeTex(tex);
			Previews.Clear();
			Offers.Clear();
			_deadFile = "";
		}

		private static Vector2 Rotate(Vector2 v, float rotation)
		{
			if (Math.Abs(rotation) < 0.0001f)
				return v;
			float c = MathF.Cos(rotation);
			float s = MathF.Sin(rotation);
			return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
		}

		private static void Load(string path, string family)
		{
			Drop();
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
				return;

			try {
				_resourceAdded = AddFontResourceEx(path, FrPrivate, IntPtr.Zero) != 0;
				_loadedPath = path;
				_face = string.IsNullOrEmpty(family) ? Path.GetFileNameWithoutExtension(path) : family;
				if (!OpenDc()) {
					Drop();
					return;
				}

				TEXTMETRIC tm = default;
				GetTextMetrics(_hdc, out tm);
				_line = Math.Max(8f, tm.tmHeight);
				_ascent = Math.Max(4f, tm.tmAscent);
				MakeAtlas(512, 512);
				Prewarm();
			}
			catch {
				Drop();
			}
		}

		private static void Drop()
		{
			CloseDc();
			if (_resourceAdded && !string.IsNullOrEmpty(_loadedPath)) {
				try {
					RemoveFontResourceEx(_loadedPath, FrPrivate, IntPtr.Zero);
				}
				catch {
				}
			}

			_resourceAdded = false;
			_loadedPath = "";
			_face = "";
			Glyphs.Clear();
			DisposeTex(_atlas);
			_atlas = null;
			_pixels = null;
			_atlasW = _atlasH = _packX = _packY = _rowH = 0;
			_line = RasterPx;
			_ascent = RasterPx * 0.8f;
		}

		private static void DropPreview(string fileName)
		{
			if (string.IsNullOrEmpty(fileName))
				return;
			if (!Previews.TryGetValue(fileName, out Texture2D tex))
				return;
			Previews.Remove(fileName);
			DisposeTex(tex);
		}

		private static void Prewarm()
		{
			for (int i = 32; i <= 126; i++)
				EnsureChar((char)i);
			EnsureChar('Ё');
			EnsureChar('ё');
			for (int i = 0x0410; i <= 0x044F; i++)
				EnsureChar((char)i);
		}

		private static void EnsureString(string text)
		{
			foreach (char c in text)
				EnsureChar(c);
		}

		private static void EnsureChar(char c)
		{
			if (Glyphs.ContainsKey(c) || _hdc == IntPtr.Zero)
				return;

			if (c == '\r' || c == '\n') {
				Glyphs[c] = new Glyph { Empty = true };
				return;
			}

			try {
				if (!Raster(c, out Glyph glyph))
					glyph = new Glyph { Advance = _line * 0.35f, Empty = true };
				Glyphs[c] = glyph;
			}
			catch {
				Glyphs[c] = new Glyph { Advance = _line * 0.35f, Empty = true };
			}
		}

		private static bool Raster(char c, out Glyph glyph)
		{
			glyph = default;
			string sample = c.ToString();
			if (!GetTextExtentPoint32(_hdc, sample, 1, out SIZE size))
				return false;

			float advance = Math.Max(1, size.cx);
			if (char.IsWhiteSpace(c)) {
				glyph = new Glyph { Advance = advance, Empty = true };
				return true;
			}

			int pad = 3;
			int bw = Math.Max(1, size.cx + pad * 2);
			int bh = Math.Max(1, (int)MathF.Ceiling(_line) + pad * 2);
			if (!Blit(c, bw, bh, pad, out Color[] bits))
				return false;

			Trim(bits, bw, bh, out int x0, out int y0, out int x1, out int y1);
			if (x1 < x0 || y1 < y0) {
				glyph = new Glyph { Advance = advance, Empty = true };
				return true;
			}

			int gw = x1 - x0 + 1;
			int gh = y1 - y0 + 1;
			if (!Pack(gw, gh, out int px, out int py))
				return false;

			for (int y = 0; y < gh; y++) {
				for (int x = 0; x < gw; x++)
					_pixels[(py + y) * _atlasW + (px + x)] = bits[(y0 + y) * bw + (x0 + x)];
			}

			try {
				_atlas.SetData(_pixels);
			}
			catch {
				return false;
			}

			glyph = new Glyph {
				Source = new Rectangle(px, py, gw, gh),
				Offset = new Vector2(x0 - pad, y0 - pad),
				Advance = advance,
				Empty = false
			};
			return true;
		}

		private static bool Blit(char c, int width, int height, int pad, out Color[] bits)
		{
			bits = null;
			IntPtr dib = IntPtr.Zero;
			IntPtr oldBmp = IntPtr.Zero;
			IntPtr bitsPtr = IntPtr.Zero;
			try {
				var info = new BITMAPINFO();
				info.biSize = 40;
				info.biWidth = width;
				info.biHeight = -height;
				info.biPlanes = 1;
				info.biBitCount = 32;
				info.biCompression = 0;
				dib = CreateDIBSection(_hdc, ref info, 0, out bitsPtr, IntPtr.Zero, 0);
				if (dib == IntPtr.Zero || bitsPtr == IntPtr.Zero)
					return false;
				oldBmp = SelectObject(_hdc, dib);
				var bg = new RECT { Left = 0, Top = 0, Right = width, Bottom = height };
				IntPtr brush = CreateSolidBrush(0);
				FillRect(_hdc, ref bg, brush);
				DeleteObject(brush);
				SetBkMode(_hdc, 1);
				SetTextColor(_hdc, 0x00FFFFFF);
				TextOut(_hdc, pad, pad, c.ToString(), 1);

				int count = width * height;
				var raw = new int[count];
				Marshal.Copy(bitsPtr, raw, 0, count);
				bits = new Color[count];
				for (int i = 0; i < count; i++) {
					int v = raw[i];
					int r = v & 255;
					int g = (v >> 8) & 255;
					int b = (v >> 16) & 255;
					byte a = (byte)Math.Max(r, Math.Max(g, b));
					bits[i] = a < 2 ? Color.Transparent : new Color((byte)255, (byte)255, (byte)255, a);
				}

				return true;
			}
			finally {
				if (oldBmp != IntPtr.Zero)
					SelectObject(_hdc, oldBmp);
				if (dib != IntPtr.Zero)
					DeleteObject(dib);
			}
		}

		private static void Trim(Color[] bits, int w, int h, out int x0, out int y0, out int x1, out int y1)
		{
			x0 = w;
			y0 = h;
			x1 = -1;
			y1 = -1;
			for (int y = 0; y < h; y++) {
				for (int x = 0; x < w; x++) {
					if (bits[y * w + x].A < 8)
						continue;
					if (x < x0)
						x0 = x;
					if (y < y0)
						y0 = y;
					if (x > x1)
						x1 = x;
					if (y > y1)
						y1 = y;
				}
			}
		}

		private static bool Pack(int w, int h, out int x, out int y)
		{
			x = y = 0;
			if (_atlas == null)
				return false;
			if (_packX + w + 1 > _atlasW) {
				_packX = 1;
				_packY += _rowH + 1;
				_rowH = 0;
			}

			if (_packY + h + 1 > _atlasH) {
				if (!Grow())
					return false;
				return Pack(w, h, out x, out y);
			}

			x = _packX;
			y = _packY;
			_packX += w + 1;
			_rowH = Math.Max(_rowH, h);
			return true;
		}

		private static bool Grow()
		{
			int nw = Math.Min(2048, _atlasW * 2);
			int nh = Math.Min(2048, _atlasH * 2);
			if (nw == _atlasW && nh == _atlasH)
				return false;
			var next = new Color[nw * nh];
			for (int y = 0; y < _atlasH; y++)
				Array.Copy(_pixels, y * _atlasW, next, y * nw, _atlasW);
			Texture2D old = _atlas;
			try {
				_atlas = new Texture2D(Main.graphics.GraphicsDevice, nw, nh);
				_atlas.SetData(next);
			}
			catch {
				_atlas = old;
				return false;
			}

			DisposeTex(old);
			_pixels = next;
			_atlasW = nw;
			_atlasH = nh;
			return true;
		}

		private static void MakeAtlas(int w, int h)
		{
			DisposeTex(_atlas);
			_atlasW = w;
			_atlasH = h;
			_pixels = new Color[w * h];
			_packX = 1;
			_packY = 1;
			_rowH = 0;
			try {
				_atlas = new Texture2D(Main.graphics.GraphicsDevice, w, h);
				_atlas.SetData(_pixels);
			}
			catch {
				_atlas = null;
			}
		}

		private static bool OpenDc()
		{
			CloseDc();
			IntPtr screen = GetDC(IntPtr.Zero);
			_hdc = CreateCompatibleDC(screen);
			ReleaseDC(IntPtr.Zero, screen);
			if (_hdc == IntPtr.Zero)
				return false;

			string fileFace = string.IsNullOrEmpty(_loadedPath) ? "" : Path.GetFileNameWithoutExtension(_loadedPath);
			foreach (string face in FaceCandidates(_face, fileFace)) {
				var lf = new LOGFONT {
					lfHeight = -RasterPx,
					lfWeight = FwNormal,
					lfCharSet = DefaultCharset,
					lfOutPrecision = OutTtOnly,
					lfQuality = ClearTypeQuality,
					lfFaceName = face
				};
				_hfont = CreateFontIndirect(ref lf);
				if (_hfont == IntPtr.Zero)
					continue;
				_oldFont = SelectObject(_hdc, _hfont);
				return true;
			}

			return false;
		}

		private static IEnumerable<string> FaceCandidates(string family, string fileFace)
		{
			string a = FaceName(family);
			string b = FaceName(fileFace);
			if (!string.IsNullOrEmpty(a))
				yield return a;
			if (!string.IsNullOrEmpty(b) && !string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
				yield return b;
		}

		private static string FaceName(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
				return "";
			name = name.Trim();
			return name.Length <= 31 ? name : name.Substring(0, 31);
		}

		private static void CloseDc()
		{
			if (_hdc != IntPtr.Zero && _oldFont != IntPtr.Zero)
				SelectObject(_hdc, _oldFont);
			if (_hfont != IntPtr.Zero)
				DeleteObject(_hfont);
			if (_hdc != IntPtr.Zero)
				DeleteDC(_hdc);
			_hdc = IntPtr.Zero;
			_hfont = IntPtr.Zero;
			_oldFont = IntPtr.Zero;
		}

		private static Texture2D BakePreview(string path, string family)
		{
			if (Main.graphics?.GraphicsDevice == null || string.IsNullOrEmpty(path) || !File.Exists(path))
				return null;

			bool added = false;
			IntPtr hdc = IntPtr.Zero;
			IntPtr font = IntPtr.Zero;
			IntPtr old = IntPtr.Zero;
			IntPtr dib = IntPtr.Zero;
			try {
				if (!string.Equals(path, _loadedPath, StringComparison.OrdinalIgnoreCase)) {
					if (AddFontResourceEx(path, FrPrivate, IntPtr.Zero) == 0)
						return null;
					added = true;
				}

				IntPtr screen = GetDC(IntPtr.Zero);
				hdc = CreateCompatibleDC(screen);
				ReleaseDC(IntPtr.Zero, screen);
				var lf = new LOGFONT {
					lfHeight = -36,
					lfWeight = FwNormal,
					lfCharSet = DefaultCharset,
					lfQuality = ClearTypeQuality,
					lfFaceName = FaceName(family)
				};
				font = CreateFontIndirect(ref lf);
				if (hdc == IntPtr.Zero || font == IntPtr.Zero)
					return null;
				old = SelectObject(hdc, font);
				const string sample = "Aa";
				GetTextExtentPoint32(hdc, sample, sample.Length, out SIZE sz);
				int w = Math.Clamp(sz.cx + 8, 8, 160);
				int h = Math.Clamp(sz.cy + 8, 8, 80);
				var info = new BITMAPINFO();
				info.biSize = 40;
				info.biWidth = w;
				info.biHeight = -h;
				info.biPlanes = 1;
				info.biBitCount = 32;
				dib = CreateDIBSection(hdc, ref info, 0, out IntPtr bitsPtr, IntPtr.Zero, 0);
				if (dib == IntPtr.Zero || bitsPtr == IntPtr.Zero)
					return null;
				IntPtr oldBmp = SelectObject(hdc, dib);
				var bg = new RECT { Right = w, Bottom = h };
				IntPtr brush = CreateSolidBrush(0);
				FillRect(hdc, ref bg, brush);
				DeleteObject(brush);
				SetBkMode(hdc, 1);
				SetTextColor(hdc, 0x00FFFFFF);
				TextOut(hdc, 4, 4, sample, sample.Length);
				var raw = new int[w * h];
				Marshal.Copy(bitsPtr, raw, 0, raw.Length);
				var colors = new Color[w * h];
				for (int i = 0; i < raw.Length; i++) {
					int v = raw[i];
					byte a = (byte)Math.Max(v & 255, Math.Max((v >> 8) & 255, (v >> 16) & 255));
					colors[i] = a < 2 ? Color.Transparent : new Color((byte)255, (byte)255, (byte)255, a);
				}

				SelectObject(hdc, oldBmp);
				DeleteObject(dib);
				dib = IntPtr.Zero;
				var tex = new Texture2D(Main.graphics.GraphicsDevice, w, h);
				tex.SetData(colors);
				return tex;
			}
			catch {
				return null;
			}
			finally {
				if (old != IntPtr.Zero && hdc != IntPtr.Zero)
					SelectObject(hdc, old);
				if (font != IntPtr.Zero)
					DeleteObject(font);
				if (dib != IntPtr.Zero)
					DeleteObject(dib);
				if (hdc != IntPtr.Zero)
					DeleteDC(hdc);
				if (added)
					RemoveFontResourceEx(path, FrPrivate, IntPtr.Zero);
			}
		}

		private static void DisposeTex(Texture2D tex)
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

		private static string ReadFamily(string path)
		{
			try {
				using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
				using var reader = new BinaryReader(stream);
				byte[] tag = reader.ReadBytes(4);
				if (tag.Length < 4)
					return null;
				if (tag[0] == (byte)'t' && tag[1] == (byte)'t' && tag[2] == (byte)'c' && tag[3] == (byte)'f')
					return null;
				stream.Position = 0;
				ReadU32(reader);
				int tables = ReadU16(reader);
				stream.Position = 12;
				int nameOff = -1;
				int nameLen = 0;
				for (int i = 0; i < tables; i++) {
					string name = new string(reader.ReadChars(4));
					reader.ReadUInt32();
					int off = (int)ReadU32(reader);
					int len = (int)ReadU32(reader);
					if (name == "name") {
						nameOff = off;
						nameLen = len;
					}
				}

				if (nameOff < 0 || nameLen < 6)
					return null;
				stream.Position = nameOff;
				ReadU16(reader);
				int count = ReadU16(reader);
				int storage = ReadU16(reader);
				string family = null;
				string full = null;
				for (int i = 0; i < count; i++) {
					int plat = ReadU16(reader);
					int enc = ReadU16(reader);
					ReadU16(reader);
					int id = ReadU16(reader);
					int len = ReadU16(reader);
					int off = ReadU16(reader);
					if (id != 1 && id != 4)
						continue;
					if (!(plat == 3 && enc == 1) && plat != 0)
						continue;
					long here = stream.Position;
					stream.Position = nameOff + storage + off;
					byte[] raw = reader.ReadBytes(len);
					stream.Position = here;
					string text = DecodeName(raw, plat, enc);
					if (string.IsNullOrWhiteSpace(text))
						continue;
					if (id == 1)
						family = text;
					else
						full = text;
				}

				return family ?? full;
			}
			catch {
				return null;
			}
		}

		private static string DecodeName(byte[] raw, int plat, int enc)
		{
			try {
				if (plat == 3 || plat == 0) {
					if (raw.Length % 2 != 0)
						return null;
					var chars = new char[raw.Length / 2];
					for (int i = 0; i < chars.Length; i++)
						chars[i] = (char)((raw[i * 2] << 8) | raw[i * 2 + 1]);
					return new string(chars).Trim('\0', ' ');
				}

				return System.Text.Encoding.ASCII.GetString(raw).Trim('\0', ' ');
			}
			catch {
				return null;
			}
		}

		private static uint ReadU32(BinaryReader reader)
		{
			byte[] b = reader.ReadBytes(4);
			return (uint)((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]);
		}

		private static ushort ReadU16(BinaryReader reader)
		{
			byte[] b = reader.ReadBytes(2);
			return (ushort)((b[0] << 8) | b[1]);
		}

		[DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
		private static extern int AddFontResourceEx(string name, uint fl, IntPtr res);

		[DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
		private static extern bool RemoveFontResourceEx(string name, uint fl, IntPtr res);

		[DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
		private static extern IntPtr CreateFontIndirect(ref LOGFONT lf);

		[DllImport("gdi32.dll")]
		private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

		[DllImport("gdi32.dll")]
		private static extern bool DeleteDC(IntPtr hdc);

		[DllImport("gdi32.dll")]
		private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

		[DllImport("gdi32.dll")]
		private static extern bool DeleteObject(IntPtr obj);

		[DllImport("gdi32.dll")]
		private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO info, uint usage, out IntPtr bits, IntPtr section, uint offset);

		[DllImport("gdi32.dll")]
		private static extern IntPtr CreateSolidBrush(int color);

		[DllImport("user32.dll")]
		private static extern int FillRect(IntPtr hdc, ref RECT rect, IntPtr brush);

		[DllImport("gdi32.dll")]
		private static extern int SetBkMode(IntPtr hdc, int mode);

		[DllImport("gdi32.dll")]
		private static extern int SetTextColor(IntPtr hdc, int color);

		[DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "TextOutW")]
		private static extern bool TextOut(IntPtr hdc, int x, int y, string text, int count);

		[DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetTextExtentPoint32W")]
		private static extern bool GetTextExtentPoint32(IntPtr hdc, string text, int count, out SIZE size);

		[DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
		private static extern bool GetTextMetrics(IntPtr hdc, out TEXTMETRIC metrics);

		[DllImport("user32.dll")]
		private static extern IntPtr GetDC(IntPtr hwnd);

		[DllImport("user32.dll")]
		private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		private struct LOGFONT
		{
			public int lfHeight;
			public int lfWidth;
			public int lfEscapement;
			public int lfOrientation;
			public int lfWeight;
			public byte lfItalic;
			public byte lfUnderline;
			public byte lfStrikeOut;
			public byte lfCharSet;
			public byte lfOutPrecision;
			public byte lfClipPrecision;
			public byte lfQuality;
			public byte lfPitchAndFamily;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
			public string lfFaceName;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct BITMAPINFO
		{
			public int biSize;
			public int biWidth;
			public int biHeight;
			public short biPlanes;
			public short biBitCount;
			public int biCompression;
			public int biSizeImage;
			public int biXPelsPerMeter;
			public int biYPelsPerMeter;
			public int biClrUsed;
			public int biClrImportant;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct RECT
		{
			public int Left;
			public int Top;
			public int Right;
			public int Bottom;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct SIZE
		{
			public int cx;
			public int cy;
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		private struct TEXTMETRIC
		{
			public int tmHeight;
			public int tmAscent;
			public int tmDescent;
			public int tmInternalLeading;
			public int tmExternalLeading;
			public int tmAveCharWidth;
			public int tmMaxCharWidth;
			public int tmWeight;
			public int tmOverhang;
			public int tmDigitizedAspectX;
			public int tmDigitizedAspectY;
			public char tmFirstChar;
			public char tmLastChar;
			public char tmDefaultChar;
			public char tmBreakChar;
			public byte tmItalic;
			public byte tmUnderlined;
			public byte tmStruckOut;
			public byte tmPitchAndFamily;
			public byte tmCharSet;
		}
	}
}
