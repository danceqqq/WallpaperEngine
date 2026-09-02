using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Terraria;

namespace WallpaperEngine.Core
{
	internal static class WeFiles
	{
		internal static bool TryPickAudio(out string path) => TryPick(out path, ShowAudio);
		internal static bool TryPickImage(out string path) => TryPick(out path, ShowImage);
		internal static bool TryPickIcon(out string path) => TryPick(out path, ShowIcon);
		internal static bool TryPickFont(out string path) => TryPick(out path, ShowFont);

		internal static void OpenFile(string path)
		{
			try {
				if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
					return;
				Process.Start(new ProcessStartInfo {
					FileName = path,
					UseShellExecute = true
				});
			}
			catch {
			}
		}

		internal static void OpenFolder(string folder)
		{
			try {
				Directory.CreateDirectory(folder);
				Process.Start(new ProcessStartInfo {
					FileName = folder,
					UseShellExecute = true
				});
			}
			catch {
			}
		}

		private static bool TryPick(out string path, Func<string> picker)
		{
			path = null;
			string picked = null;
			var thread = new Thread(() => picked = picker());
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			thread.Join();
			if (string.IsNullOrEmpty(picked) || !File.Exists(picked))
				return false;

			path = picked;
			return true;
		}

		private static string ShowAudio() => ShowDialog(
			"Audio (*.ogg;*.mp3;*.wav)\0*.ogg;*.mp3;*.wav\0Ogg\0*.ogg\0MP3\0*.mp3\0WAV\0*.wav\0",
			"Upload a song");

		private static string ShowImage() => ShowDialog(
			"Images (*.png;*.jpg;*.jpeg)\0*.png;*.jpg;*.jpeg\0PNG\0*.png\0JPEG\0*.jpg;*.jpeg\0",
			"Upload an image");

		private static string ShowIcon() => ShowDialog(
			"Icons (*.ico;*.png;*.jpg;*.jpeg)\0*.ico;*.png;*.jpg;*.jpeg\0ICO\0*.ico\0PNG\0*.png\0",
			"Choose a window icon");

		private static string ShowFont() => ShowDialog(
			"Fonts (*.ttf;*.otf)\0*.ttf;*.otf\0TrueType\0*.ttf\0OpenType\0*.otf\0",
			"Choose a font");

		private static string ShowDialog(string filter, string title)
		{
			var ofn = new OpenFileName();
			ofn.lStructSize = Marshal.SizeOf<OpenFileName>();
			ofn.lpstrFilter = filter;
			ofn.lpstrFile = new string('\0', 1024);
			ofn.nMaxFile = ofn.lpstrFile.Length;
			ofn.lpstrTitle = title;
			ofn.Flags = 0x00080000 | 0x00001000 | 0x00000800;
			return GetOpenFileName(ref ofn) ? ofn.lpstrFile.Split('\0')[0] : null;
		}

		internal static string UniquePath(string folder, string fileName)
		{
			Directory.CreateDirectory(folder);
			string dest = Path.Combine(folder, fileName);
			if (!File.Exists(dest))
				return dest;

			string name = Path.GetFileNameWithoutExtension(fileName);
			string ext = Path.GetExtension(fileName);
			for (int i = 2; i < 100; i++) {
				dest = Path.Combine(folder, $"{name}_{i}{ext}");
				if (!File.Exists(dest))
					return dest;
			}

			return Path.Combine(folder, $"{name}_{Guid.NewGuid():N}{ext}");
		}

		[DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern bool GetOpenFileName(ref OpenFileName ofn);

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		private struct OpenFileName
		{
			public int lStructSize;
			public IntPtr hwndOwner;
			public IntPtr hInstance;
			public string lpstrFilter;
			public string lpstrCustomFilter;
			public int nMaxCustFilter;
			public int nFilterIndex;
			public string lpstrFile;
			public int nMaxFile;
			public string lpstrFileTitle;
			public int nMaxFileTitle;
			public string lpstrInitialDir;
			public string lpstrTitle;
			public int Flags;
			public short nFileOffset;
			public short nFileExtension;
			public string lpstrDefExt;
			public IntPtr lCustData;
			public IntPtr lpfnHook;
			public string lpTemplateName;
			public IntPtr pvReserved;
			public int dwReserved;
			public int FlagsEx;
		}
	}
}
