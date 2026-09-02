using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Microsoft.Xna.Framework;

namespace WallpaperEngine.Core
{
	internal static class WeApng
	{
		private static readonly byte[] Sig = { 137, 80, 78, 71, 13, 10, 26, 10 };

		internal static bool LooksLike(byte[] data)
		{
			if (data == null || data.Length < 8)
				return false;
			for (int i = 0; i < 8; i++) {
				if (data[i] != Sig[i])
					return false;
			}

			return HasActl(data);
		}

		internal static WeClip Decode(byte[] data)
		{
			if (data == null || data.Length < 33)
				return null;

			int pos = 8;
			int width = 0;
			int height = 0;
			byte depth = 8;
			byte colorType = 6;
			byte[] plte = null;
			byte[] trns = null;
			var idat = new List<byte>();
			WeAnimFrame pending = null;
			var frames = new List<WeAnimFrame>();
			bool sawActl = false;
			bool sawFctl = false;

			while (pos + 12 <= data.Length) {
				int len = ReadU32(data, pos);
				if (len < 0 || pos + 12 + len > data.Length)
					break;
				string type = TypeOf(data, pos + 4);
				int body = pos + 8;
				pos += 12 + len;

				if (type == "IHDR" && len >= 13) {
					width = ReadU32(data, body);
					height = ReadU32(data, body + 4);
					depth = data[body + 8];
					colorType = data[body + 9];
					if (data[body + 12] != 0 || !WeAnim.Fits(width, height, 1))
						return null;
					continue;
				}

				if (type == "PLTE")
					plte = Slice(data, body, len);
				else if (type == "tRNS")
					trns = Slice(data, body, len);
				else if (type == "acTL")
					sawActl = true;
				else if (type == "fcTL" && len >= 26) {
					if (pending != null) {
						if (!Flush(pending, idat, width, height, depth, colorType, plte, trns, frames))
							return frames.Count > 0 ? Finish(width, height, frames) : null;
						pending = null;
					}

					idat.Clear();
					sawFctl = true;
					int fw = ReadU32(data, body + 4);
					int fh = ReadU32(data, body + 8);
					int ox = ReadU32(data, body + 12);
					int oy = ReadU32(data, body + 16);
					int num = data[body + 20] << 8 | data[body + 21];
					int den = data[body + 22] << 8 | data[body + 23];
					if (den == 0)
						den = 100;
					int delayMs = num == 0 ? 100 : (int)Math.Max(20, num * 1000L / den);
					pending = new WeAnimFrame {
						DelayMs = delayMs,
						Disposal = data[body + 24],
						BlendOver = data[body + 25] == 1,
						X = ox,
						Y = oy,
						W = fw,
						H = fh
					};
				}
				else if (type == "IDAT" && len > 0) {
					if (!sawFctl)
						continue;
					Append(idat, data, body, len);
				}
				else if (type == "fdAT" && len > 4) {
					Append(idat, data, body + 4, len - 4);
				}
				else if (type == "IEND")
					break;
			}

			if (pending != null && !Flush(pending, idat, width, height, depth, colorType, plte, trns, frames))
				return frames.Count > 0 ? Finish(width, height, frames) : null;

			if (!sawActl || frames.Count < 2)
				return null;
			return Finish(width, height, frames);
		}

		private static WeClip Finish(int width, int height, List<WeAnimFrame> frames)
		{
			if (frames == null || frames.Count < 2 || !WeAnim.Fits(width, height, frames.Count))
				return null;
			return new WeClip {
				Width = width,
				Height = height,
				Frames = frames.ToArray()
			};
		}

		private static bool Flush(
			WeAnimFrame frame,
			List<byte> zlib,
			int canvasW,
			int canvasH,
			byte depth,
			byte colorType,
			byte[] plte,
			byte[] trns,
			List<WeAnimFrame> frames)
		{
			if (frame == null || zlib.Count < 2 || frame.W < 1 || frame.H < 1)
				return false;
			if (frame.W > canvasW || frame.H > canvasH)
				return false;

			byte[] raw = Inflate(zlib.ToArray());
			if (raw == null)
				return false;
			Color[] pixels = Unfilter(raw, frame.W, frame.H, depth, colorType, plte, trns);
			if (pixels == null)
				return false;
			if (frame.Disposal > 2)
				frame.Disposal = 0;
			frame.Pixels = pixels;
			frames.Add(frame);
			return frames.Count <= 256;
		}

		private static byte[] Inflate(byte[] zlib)
		{
			try {
				using var input = new MemoryStream(zlib, writable: false);
				using var zip = new ZLibStream(input, CompressionMode.Decompress);
				using var output = new MemoryStream();
				zip.CopyTo(output);
				return output.ToArray();
			}
			catch {
				return null;
			}
		}

		private static Color[] Unfilter(byte[] data, int w, int h, byte depth, byte colorType, byte[] plte, byte[] trns)
		{
			int bpp = BytesPerPixel(depth, colorType);
			if (bpp < 1)
				return null;
			int stride = w * bpp;
			int expect = h * (stride + 1);
			if (data.Length < expect)
				return null;

			var recon = new byte[h * stride];
			int src = 0;
			for (int y = 0; y < h; y++) {
				byte filter = data[src++];
				int row = y * stride;
				for (int x = 0; x < stride; x++) {
					byte cur = data[src++];
					byte a = x >= bpp ? recon[row + x - bpp] : (byte)0;
					byte b = y > 0 ? recon[row - stride + x] : (byte)0;
					byte c = y > 0 && x >= bpp ? recon[row - stride + x - bpp] : (byte)0;
					recon[row + x] = filter switch {
						1 => (byte)(cur + a),
						2 => (byte)(cur + b),
						3 => (byte)(cur + ((a + b) >> 1)),
						4 => (byte)(cur + Paeth(a, b, c)),
						_ => cur
					};
				}
			}

			return ToColors(recon, w, h, depth, colorType, plte, trns, bpp);
		}

		private static Color[] ToColors(byte[] recon, int w, int h, byte depth, byte colorType, byte[] plte, byte[] trns, int bpp)
		{
			var pixels = new Color[w * h];
			int stride = w * bpp;
			for (int y = 0; y < h; y++) {
				int row = y * stride;
				for (int x = 0; x < w; x++) {
					int i = row + x * bpp;
					pixels[y * w + x] = Sample(recon, i, depth, colorType, plte, trns);
				}
			}

			return pixels;
		}

		private static Color Sample(byte[] d, int i, byte depth, byte colorType, byte[] plte, byte[] trns)
		{
			if (colorType == 6) {
				if (depth == 16)
					return new Color(d[i], d[i + 2], d[i + 4], d[i + 6]);
				return new Color(d[i], d[i + 1], d[i + 2], d[i + 3]);
			}

			if (colorType == 2) {
				byte a = 255;
				if (depth == 16) {
					if (trns != null && trns.Length >= 6 && d[i] == trns[0] && d[i + 2] == trns[2] && d[i + 4] == trns[4])
						a = 0;
					return new Color(d[i], d[i + 2], d[i + 4], a);
				}

				if (trns != null && trns.Length >= 6 && d[i] == trns[1] && d[i + 1] == trns[3] && d[i + 2] == trns[5])
					a = 0;
				return new Color(d[i], d[i + 1], d[i + 2], a);
			}

			if (colorType == 3) {
				int idx = d[i];
				byte a = 255;
				if (trns != null && idx < trns.Length)
					a = trns[idx];
				if (plte == null || idx * 3 + 2 >= plte.Length)
					return new Color((byte)0, (byte)0, (byte)0, a);
				return new Color(plte[idx * 3], plte[idx * 3 + 1], plte[idx * 3 + 2], a);
			}

			if (colorType == 4) {
				byte g = d[i];
				byte a = depth == 16 ? d[i + 2] : d[i + 1];
				return new Color(g, g, g, a);
			}

			byte gray = d[i];
			byte alpha = 255;
			if (trns != null && trns.Length >= 2 && gray == (depth == 16 ? trns[0] : trns[1]))
				alpha = 0;
			return new Color(gray, gray, gray, alpha);
		}

		private static int BytesPerPixel(byte depth, byte colorType)
		{
			int ch = colorType switch {
				0 => 1,
				2 => 3,
				3 => 1,
				4 => 2,
				6 => 4,
				_ => 0
			};
			if (ch == 0)
				return 0;
			if (depth == 16)
				return ch * 2;
			if (depth == 8)
				return ch;
			return 0;
		}

		private static byte Paeth(byte a, byte b, byte c)
		{
			int p = a + b - c;
			int pa = Math.Abs(p - a);
			int pb = Math.Abs(p - b);
			int pc = Math.Abs(p - c);
			if (pa <= pb && pa <= pc)
				return a;
			return pb <= pc ? b : c;
		}

		private static bool HasActl(byte[] data)
		{
			int pos = 8;
			while (pos + 12 <= data.Length) {
				int len = ReadU32(data, pos);
				if (len < 0 || pos + 12 + len > data.Length)
					return false;
				string type = TypeOf(data, pos + 4);
				if (type == "acTL")
					return true;
				if (type is "IDAT" or "IEND")
					return false;
				pos += 12 + len;
			}

			return false;
		}

		private static void Append(List<byte> dest, byte[] src, int start, int len)
		{
			int end = Math.Min(src.Length, start + len);
			for (int i = start; i < end; i++)
				dest.Add(src[i]);
		}

		private static byte[] Slice(byte[] src, int start, int len)
		{
			int n = Math.Min(len, Math.Max(0, src.Length - start));
			var copy = new byte[n];
			Array.Copy(src, start, copy, 0, n);
			return copy;
		}

		private static int ReadU32(byte[] d, int i) =>
			(d[i] << 24) | (d[i + 1] << 16) | (d[i + 2] << 8) | d[i + 3];

		private static string TypeOf(byte[] d, int i) =>
			((char)d[i]).ToString() + (char)d[i + 1] + (char)d[i + 2] + (char)d[i + 3];
	}
}
