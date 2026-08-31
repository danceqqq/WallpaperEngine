using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using WallpaperEngine.Core;

namespace WallpaperEngine.Chrome
{
	internal static class ClientChrome
	{
		private const int DwmwaUseImmersiveDarkMode = 20;
		private const int DwmwaBorderColor = 34;
		private const int DwmwaCaptionColor = 35;
		private const int DwmwaTextColor = 36;
		private const uint DwmwaColorDefault = 0xFFFFFFFF;
		private const int WmSetIcon = 0x0080;
		private const int WmGetIcon = 0x007F;
		private const int IconSmall = 0;
		private const int IconBig = 1;
		private const uint ImageIcon = 1;
		private const uint LrLoadFromFile = 0x00000010;

		private static IntPtr _originalSmall;
		private static IntPtr _originalBig;
		private static IntPtr _customIcon;
		private static bool _captured;

		internal static void Apply()
		{
			IntPtr hwnd = Hwnd();
			if (hwnd == IntPtr.Zero)
				return;

			Capture(hwnd);
			WeSaveData data = WeSave.Data;
			if (!data.ChromeCustom) {
				SetAttr(hwnd, DwmwaUseImmersiveDarkMode, 1);
				return;
			}

			int dark = data.DarkTitleBar ? 1 : 0;
			SetAttr(hwnd, DwmwaUseImmersiveDarkMode, dark);
			SetColor(hwnd, DwmwaCaptionColor, data.CaptionR, data.CaptionG, data.CaptionB);
			SetColor(hwnd, DwmwaBorderColor, data.BorderR, data.BorderG, data.BorderB);
			SetColor(hwnd, DwmwaTextColor, data.TitleTextR, data.TitleTextG, data.TitleTextB);
			ApplySavedIcon(hwnd);
		}

		internal static void Reset()
		{
			WeSave.Data.ChromeCustom = false;
			WeSave.Data.WindowIconFile = "";
			WeSave.Save();
			IntPtr hwnd = Hwnd();
			if (hwnd == IntPtr.Zero)
				return;

			uint def = DwmwaColorDefault;
			DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref def, sizeof(uint));
			DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref def, sizeof(uint));
			DwmSetWindowAttribute(hwnd, DwmwaTextColor, ref def, sizeof(uint));
			int dark = 1;
			DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
			RestoreIcon(hwnd);
		}

		internal static void SetIcon(string sourcePath)
		{
			try {
				WeSave.EnsureFolders();
				string dest = WeFiles.UniquePath(WeSave.IconFolder, Path.GetFileName(sourcePath));
				File.Copy(sourcePath, dest, overwrite: false);
				WeSave.Data.WindowIconFile = Path.GetFileName(dest);
				WeSave.Data.ChromeCustom = true;
				WeSave.Save();
				Apply();
				WeToast.Show("ToastIcon");
			}
			catch {
			}
		}

		internal static void Unload()
		{
			IntPtr hwnd = Hwnd();
			if (hwnd != IntPtr.Zero)
				RestoreIcon(hwnd);
			DestroyCustom();
		}

		private static void ApplySavedIcon(IntPtr hwnd)
		{
			if (string.IsNullOrEmpty(WeSave.Data.WindowIconFile))
				return;

			string path = Path.Combine(WeSave.IconFolder, WeSave.Data.WindowIconFile);
			if (!File.Exists(path))
				return;

			Capture(hwnd);
			IntPtr icon = LoadIconFile(path);
			if (icon == IntPtr.Zero)
				icon = CreateIconFromImage(path);
			if (icon == IntPtr.Zero)
				return;

			DestroyCustom();
			_customIcon = icon;
			SendMessage(hwnd, WmSetIcon, (IntPtr)IconSmall, icon);
			SendMessage(hwnd, WmSetIcon, (IntPtr)IconBig, icon);
		}

		private static void Capture(IntPtr hwnd)
		{
			if (_captured)
				return;
			_originalSmall = SendMessage(hwnd, WmGetIcon, (IntPtr)IconSmall, IntPtr.Zero);
			_originalBig = SendMessage(hwnd, WmGetIcon, (IntPtr)IconBig, IntPtr.Zero);
			_captured = true;
		}

		private static void RestoreIcon(IntPtr hwnd)
		{
			if (!_captured)
				return;
			SendMessage(hwnd, WmSetIcon, (IntPtr)IconSmall, _originalSmall);
			SendMessage(hwnd, WmSetIcon, (IntPtr)IconBig, _originalBig);
			DestroyCustom();
		}

		private static void DestroyCustom()
		{
			if (_customIcon == IntPtr.Zero)
				return;
			DestroyIcon(_customIcon);
			_customIcon = IntPtr.Zero;
		}

		private static void SetAttr(IntPtr hwnd, int attr, int value) =>
			DwmSetWindowAttribute(hwnd, attr, ref value, sizeof(int));

		private static void SetColor(IntPtr hwnd, int attr, int r, int g, int b)
		{
			uint color = (uint)(Math.Clamp(r, 0, 255) | (Math.Clamp(g, 0, 255) << 8) | (Math.Clamp(b, 0, 255) << 16));
			DwmSetWindowAttribute(hwnd, attr, ref color, sizeof(uint));
		}

		private static IntPtr Hwnd()
		{
			try {
				IntPtr h = Process.GetCurrentProcess().MainWindowHandle;
				if (h != IntPtr.Zero)
					return h;
			}
			catch {
			}

			try {
				return Main.instance?.Window?.Handle ?? IntPtr.Zero;
			}
			catch {
				return IntPtr.Zero;
			}
		}

		private static IntPtr LoadIconFile(string path)
		{
			if (!path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
				return IntPtr.Zero;
			return LoadImage(IntPtr.Zero, path, ImageIcon, 32, 32, LrLoadFromFile);
		}

		private static IntPtr CreateIconFromImage(string path)
		{
			try {
				using FileStream stream = File.OpenRead(path);
				Texture2D tex = Texture2D.FromStream(Main.instance.GraphicsDevice, stream);
				int size = 32;
				var src = new Color[tex.Width * tex.Height];
				tex.GetData(src);
				var bits = new int[size * size];
				for (int y = 0; y < size; y++) {
					for (int x = 0; x < size; x++) {
						int sx = x * tex.Width / size;
						int sy = y * tex.Height / size;
						Color c = src[sy * tex.Width + sx];
						bits[y * size + x] = (c.A << 24) | (c.R << 16) | (c.G << 8) | c.B;
					}
				}

				GCHandle handle = GCHandle.Alloc(bits, GCHandleType.Pinned);
				try {
					IntPtr hbmColor = CreateBitmap(size, size, 1, 32, handle.AddrOfPinnedObject());
					IntPtr hbmMask = CreateBitmap(size, size, 1, 1, IntPtr.Zero);
					var info = new IconInfo {
						fIcon = true,
						xHotspot = 0,
						yHotspot = 0,
						hbmMask = hbmMask,
						hbmColor = hbmColor
					};
					IntPtr icon = CreateIconIndirect(ref info);
					DeleteObject(hbmColor);
					DeleteObject(hbmMask);
					tex.Dispose();
					return icon;
				}
				finally {
					handle.Free();
				}
			}
			catch {
				return IntPtr.Zero;
			}
		}

		[DllImport("dwmapi.dll")]
		private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int size);

		[DllImport("dwmapi.dll")]
		private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref uint attrValue, int size);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);

		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool DestroyIcon(IntPtr hIcon);

		[DllImport("user32.dll")]
		private static extern IntPtr CreateIconIndirect(ref IconInfo piconinfo);

		[DllImport("gdi32.dll")]
		private static extern IntPtr CreateBitmap(int nWidth, int nHeight, uint cPlanes, uint cBitsPerPel, IntPtr lpvBits);

		[DllImport("gdi32.dll")]
		private static extern bool DeleteObject(IntPtr hObject);

		[StructLayout(LayoutKind.Sequential)]
		private struct IconInfo
		{
			public bool fIcon;
			public int xHotspot;
			public int yHotspot;
			public IntPtr hbmMask;
			public IntPtr hbmColor;
		}
	}
}
