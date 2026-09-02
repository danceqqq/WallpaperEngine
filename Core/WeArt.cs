using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace WallpaperEngine.Core
{
	internal static class WeArt
	{
		private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".apng" };
		private static readonly Dictionary<string, Texture2D> Cache = new(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, DateTime> Times = new(StringComparer.OrdinalIgnoreCase);

		internal static void Scan()
		{
			WeSave.EnsureLoaded();
			WeSave.EnsureFolders();
			SyncFolder(WeSave.LogoFolder, WeSave.Data.Logos, "logo:");
			SyncFolder(WeSave.WallpaperFolder, WeSave.Data.Wallpapers, "wall:");
			if (WeSave.Data.Logo == LogoKind.Custom &&
			    !string.IsNullOrEmpty(WeSave.Data.LogoId) &&
			    WeSave.Data.Logos.All(item => item.Id != WeSave.Data.LogoId))
				WeSave.Data.LogoId = "";
			if (WeSave.Data.Wallpaper != WallpaperKind.Borrowed &&
			    !string.IsNullOrEmpty(WeSave.Data.WallpaperId) &&
			    WeSave.Data.Wallpapers.All(item => item.Id != WeSave.Data.WallpaperId))
				WeSave.Data.WallpaperId = "";
		}

		internal static bool TryGetWallpaper(out Texture2D texture) =>
			TryGetWallpaper(WeSave.Data.WallpaperId, out texture);

		internal static bool TryGetWallpaper(string id, out Texture2D texture)
		{
			texture = TextureOf(WeSave.WallpaperFolder, id, WeSave.Data.Wallpapers, true);
			return texture != null;
		}

		internal static bool TryGetLogo(out Texture2D texture)
		{
			texture = TextureOf(WeSave.LogoFolder, WeSave.Data.LogoId, WeSave.Data.Logos, true);
			return texture != null;
		}

		internal static Texture2D Preview(WeArtRecord record, bool logo)
		{
			if (record == null)
				return null;
			return TextureOf(
				logo ? WeSave.LogoFolder : WeSave.WallpaperFolder,
				record.Id,
				logo ? WeSave.Data.Logos : WeSave.Data.Wallpapers,
				false);
		}

		internal static bool TryImportWallpaper()
		{
			if (!WeFiles.TryPickImage(out string path))
				return false;

			WeArtRecord record = ImportFile(path, WeSave.WallpaperFolder, WeSave.Data.Wallpapers, "wall:");
			if (record == null)
				return false;

			WeSettings.SetWallpaperImage(record.Id);
			WeToast.Show("ToastWallpaper");
			return true;
		}

		internal static bool TryImportLogo()
		{
			if (!WeFiles.TryPickImage(out string path))
				return false;

			WeArtRecord record = ImportFile(path, WeSave.LogoFolder, WeSave.Data.Logos, "logo:");
			if (record == null)
				return false;

			WeSettings.SetLogo(LogoKind.Custom, record.Id);
			WeToast.Show("ToastLogo");
			return true;
		}

		internal static void Delete(WeArtRecord record, bool logo)
		{
			if (record == null)
				return;

			string folder = logo ? WeSave.LogoFolder : WeSave.WallpaperFolder;
			List<WeArtRecord> records = logo ? WeSave.Data.Logos : WeSave.Data.Wallpapers;
			string path = Path.Combine(folder, record.FileName);
			try {
				if (File.Exists(path)) {
					File.SetAttributes(path, FileAttributes.Normal);
					File.Delete(path);
				}
			}
			catch {
			}

			if (Cache.Remove(path, out Texture2D tex)) {
				Main.QueueMainThreadAction(() => {
					try {
						if (tex != null && !tex.IsDisposed)
							tex.Dispose();
					}
					catch {
					}
				});
			}

			Times.Remove(path);
			WeAnim.Drop(path);
			records.RemoveAll(item => item.Id == record.Id || string.Equals(item.FileName, record.FileName, StringComparison.OrdinalIgnoreCase));
			if (logo && WeSave.Data.LogoId == record.Id)
				WeSave.Data.LogoId = "";
			if (!logo && WeSave.Data.WallpaperId == record.Id) {
				WeSave.Data.WallpaperId = "";
				WeSave.Data.Wallpaper = WallpaperKind.Vanilla;
			}

			if (!logo) {
				foreach (WeLayerRecord layer in WeSave.Data.Layers) {
					if (layer.ArtId == record.Id)
						layer.ArtId = "";
				}
			}

			WeSave.Save();
		}

		internal static void Unload()
		{
			foreach (Texture2D tex in Cache.Values) {
				Texture2D local = tex;
				Main.QueueMainThreadAction(() => {
					try {
						if (local != null && !local.IsDisposed)
							local.Dispose();
					}
					catch {
					}
				});
			}

			Cache.Clear();
			Times.Clear();
			WeAnim.Unload();
		}

		private static void SyncFolder(string folder, List<WeArtRecord> records, string prefix)
		{
			var files = Directory.Exists(folder)
				? Directory.GetFiles(folder)
					.Where(path => ImageExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
					.Select(Path.GetFileName)
					.Where(name => !string.IsNullOrEmpty(name))
					.ToHashSet(StringComparer.OrdinalIgnoreCase)
				: new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			records.RemoveAll(item => !files.Contains(item.FileName));
			foreach (string fileName in files) {
				if (records.Any(item => string.Equals(item.FileName, fileName, StringComparison.OrdinalIgnoreCase)))
					continue;

				records.Add(new WeArtRecord {
					Id = prefix + Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant(),
					FileName = fileName
				});
			}

			EnsureUniqueIds(records, prefix);
		}

		private static void EnsureUniqueIds(List<WeArtRecord> records, string prefix)
		{
			var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (WeArtRecord record in records) {
				if (string.IsNullOrEmpty(record.Id) || !used.Add(record.Id)) {
					string baseId = prefix + Path.GetFileNameWithoutExtension(record.FileName).ToLowerInvariant();
					string id = baseId;
					int n = 2;
					while (!used.Add(id))
						id = baseId + "_" + n++;
					record.Id = id;
				}
			}
		}

		private static WeArtRecord ImportFile(string source, string folder, List<WeArtRecord> records, string prefix)
		{
			try {
				WeSave.EnsureFolders();
				string ext = Path.GetExtension(source).ToLowerInvariant();
				if (!ImageExtensions.Contains(ext))
					return null;

				string dest = WeFiles.UniquePath(folder, Path.GetFileName(source));
				File.Copy(source, dest, overwrite: false);
				var record = new WeArtRecord {
					Id = prefix + Path.GetFileNameWithoutExtension(dest).ToLowerInvariant(),
					FileName = Path.GetFileName(dest),
					PanX = 0.5f,
					PanY = 0.5f
				};
				records.RemoveAll(item => string.Equals(item.FileName, record.FileName, StringComparison.OrdinalIgnoreCase));
				records.Add(record);
				EnsureUniqueIds(records, prefix);
				WeSave.Save();
				return record;
			}
			catch {
				return null;
			}
		}

		private static Texture2D TextureOf(string folder, string id, List<WeArtRecord> records, bool motion)
		{
			if (string.IsNullOrEmpty(id))
				return null;

			WeArtRecord record = records.FirstOrDefault(item => item.Id == id);
			if (record == null)
				return null;

			string path = Path.Combine(folder, record.FileName);
			if (!File.Exists(path))
				return null;

			DateTime write = File.GetLastWriteTimeUtc(path);
			if (motion) {
				Texture2D live = WeAnim.Play(path, write);
				if (live != null)
					return live;
				if (WeAnim.Loaded(path))
					return null;
			}

			if (Cache.TryGetValue(path, out Texture2D cached) && cached != null && !cached.IsDisposed &&
			    Times.TryGetValue(path, out DateTime known) && known == write)
				return cached;

			try {
				using FileStream stream = File.OpenRead(path);
				Texture2D tex = Texture2D.FromStream(Main.instance.GraphicsDevice, stream);
				if (cached != null && !cached.IsDisposed)
					cached.Dispose();
				Cache[path] = tex;
				Times[path] = write;
				return tex;
			}
			catch {
				return motion ? null : WeAnim.Play(path, write);
			}
		}
	}
}
