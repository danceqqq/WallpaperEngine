using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace WallpaperEngine.Core
{
	internal static class WeGif
	{
		internal static bool LooksLike(byte[] data) =>
			data != null && data.Length >= 6 &&
			data[0] == (byte)'G' && data[1] == (byte)'I' && data[2] == (byte)'F';

		internal static WeClip Decode(byte[] data)
		{
			if (!LooksLike(data))
				return null;

			var r = new Cursor(data);
			r.Skip(6);
			int width = r.U16();
			int height = r.U16();
			byte packed = r.U8();
			r.U8();
			r.U8();
			if (!WeAnim.Fits(width, height, 1))
				return null;

			Color[] gct = null;
			if ((packed & 0x80) != 0)
				gct = ReadTable(r, 1 << ((packed & 7) + 1));

			byte gcePacked = 0;
			int delay = 10;
			int trans = -1;
			var frames = new List<WeAnimFrame>();

			while (r.Ok && frames.Count < 256) {
				byte tag = r.U8();
				if (tag == 0x3B)
					break;
				if (tag == 0)
					continue;
				if (tag == 0x21) {
					byte label = r.U8();
					if (label == 0xF9) {
						int n = r.U8();
						if (n >= 4) {
							gcePacked = r.U8();
							delay = r.U16();
							trans = r.U8();
							n -= 4;
						}

						r.Skip(n);
						if (r.U8() != 0)
							r.SkipBlocks();
					}
					else
						r.SkipBlocks();
					continue;
				}

				if (tag != 0x2C)
					break;

				int left = r.U16();
				int top = r.U16();
				int fw = r.U16();
				int fh = r.U16();
				byte ip = r.U8();
				Color[] table = gct;
				if ((ip & 0x80) != 0)
					table = ReadTable(r, 1 << ((ip & 7) + 1));
				if (table == null || fw < 1 || fh < 1 || !r.Ok)
					return frames.Count > 0 ? Finish(width, height, frames) : null;

				byte minCode = r.U8();
				byte[] compressed = r.ReadBlocks();
				byte[] indices = new byte[fw * fh];
				if (minCode < 2 || minCode > 8 || !Lzw(minCode, compressed, indices))
					return frames.Count > 0 ? Finish(width, height, frames) : null;

				if ((ip & 0x40) != 0)
					indices = Deinterlace(indices, fw, fh);

				bool hasTrans = (gcePacked & 1) != 0 && trans >= 0;
				var pixels = new Color[fw * fh];
				for (int i = 0; i < indices.Length; i++) {
					int idx = indices[i];
					if (hasTrans && idx == trans)
						continue;
					if ((uint)idx < (uint)table.Length)
						pixels[i] = table[idx];
				}

				int delayMs = delay <= 1 ? 100 : delay * 10;
				byte disposal = (byte)((gcePacked >> 2) & 7);
				if (disposal > 3)
					disposal = 1;

				frames.Add(new WeAnimFrame {
					DelayMs = delayMs,
					Disposal = disposal,
					X = left,
					Y = top,
					W = fw,
					H = fh,
					Pixels = pixels
				});

				gcePacked = 0;
				delay = 10;
				trans = -1;
			}

			return Finish(width, height, frames);
		}

		private static WeClip Finish(int width, int height, List<WeAnimFrame> frames)
		{
			if (frames == null || frames.Count == 0 || !WeAnim.Fits(width, height, frames.Count))
				return null;
			return new WeClip {
				Width = width,
				Height = height,
				Frames = frames.ToArray()
			};
		}

		private static Color[] ReadTable(Cursor r, int count)
		{
			var table = new Color[Math.Max(0, count)];
			for (int i = 0; i < table.Length; i++) {
				byte red = r.U8();
				byte green = r.U8();
				byte blue = r.U8();
				table[i] = new Color(red, green, blue, (byte)255);
			}

			return table;
		}

		private static byte[] Deinterlace(byte[] src, int w, int h)
		{
			var dest = new byte[w * h];
			int[] start = { 0, 4, 2, 1 };
			int[] step = { 8, 8, 4, 2 };
			int row = 0;
			for (int pass = 0; pass < 4; pass++) {
				for (int y = start[pass]; y < h; y += step[pass]) {
					if (row >= h)
						break;
					Array.Copy(src, row * w, dest, y * w, w);
					row++;
				}
			}

			return dest;
		}

		private static bool Lzw(byte minSize, byte[] src, byte[] dst)
		{
			if (src == null || dst == null || dst.Length == 0)
				return false;

			int clear = 1 << minSize;
			int eoi = clear + 1;
			int codeSize = minSize + 1;
			int avail = eoi + 1;
			int[] prefix = new int[4096];
			byte[] suffix = new byte[4096];
			byte[] stack = new byte[4096];
			for (int i = 0; i < clear; i++) {
				prefix[i] = -1;
				suffix[i] = (byte)i;
			}

			int buf = 0;
			int nbits = 0;
			int si = 0;
			int old = -1;
			int pos = 0;

			int Read()
			{
				while (nbits < codeSize) {
					if (si >= src.Length)
						return -1;
					buf |= src[si++] << nbits;
					nbits += 8;
				}

				int v = buf & ((1 << codeSize) - 1);
				buf >>= codeSize;
				nbits -= codeSize;
				return v;
			}

			while (pos < dst.Length) {
				int code = Read();
				if (code < 0 || code == eoi)
					break;
				if (code == clear) {
					codeSize = minSize + 1;
					avail = eoi + 1;
					old = -1;
					continue;
				}

				if (code > avail)
					break;

				int walk = code == avail && old >= 0 ? old : code;
				int sp = 0;
				while (walk >= clear && sp < 4095) {
					stack[sp++] = suffix[walk];
					walk = prefix[walk];
				}

				if (walk < 0)
					break;
				stack[sp++] = (byte)walk;
				byte first = stack[sp - 1];
				while (sp > 0 && pos < dst.Length)
					dst[pos++] = stack[--sp];
				if (code == avail && pos < dst.Length)
					dst[pos++] = first;

				if (old >= 0 && avail < 4096) {
					prefix[avail] = old;
					suffix[avail] = first;
					avail++;
					if (avail == 1 << codeSize && codeSize < 12)
						codeSize++;
				}

				old = code;
			}

			return pos > 0;
		}

		private class Cursor
		{
			private readonly byte[] _d;
			private int _p;

			internal Cursor(byte[] data)
			{
				_d = data ?? Array.Empty<byte>();
				_p = 0;
			}

			internal bool Ok => _p < _d.Length;

			internal byte U8() => _p < _d.Length ? _d[_p++] : (byte)0;

			internal ushort U16()
			{
				int lo = U8();
				return (ushort)(lo | (U8() << 8));
			}

			internal void Skip(int n)
			{
				_p = Math.Clamp(_p + n, 0, _d.Length);
			}

			internal void SkipBlocks()
			{
				while (Ok) {
					int n = U8();
					if (n == 0)
						return;
					Skip(n);
				}
			}

			internal byte[] ReadBlocks()
			{
				var list = new List<byte>(256);
				while (Ok) {
					int n = U8();
					if (n == 0)
						break;
					int take = Math.Min(n, _d.Length - _p);
					for (int i = 0; i < take; i++)
						list.Add(_d[_p++]);
					if (take < n)
						break;
				}

				return list.ToArray();
			}
		}
	}
}
