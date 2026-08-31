using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NVorbis;
using WallpaperEngine.Core;

namespace WallpaperEngine.Audio
{
	internal static class WeLibrary
	{
		private static readonly string[] AudioExtensions = { ".ogg", ".mp3", ".wav" };

		internal static string FullPath(string fileName) => Path.Combine(WeSave.MusicFolder, fileName);

		internal static void ScanIntoSave()
		{
			WeSave.EnsureLoaded();
			WeSave.EnsureFolders();
			WeSaveData data = WeSave.Data;
			var files = Directory.Exists(WeSave.MusicFolder)
				? Directory.GetFiles(WeSave.MusicFolder)
					.Where(path => AudioExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
					.Select(Path.GetFileName)
					.Where(name => !string.IsNullOrEmpty(name))
					.ToHashSet(StringComparer.OrdinalIgnoreCase)
				: new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			data.Tracks.RemoveAll(track => !files.Contains(track.FileName));
			foreach (string fileName in files) {
				if (data.Tracks.Any(track => string.Equals(track.FileName, fileName, StringComparison.OrdinalIgnoreCase)))
					continue;

				data.Tracks.Add(new WeTrackRecord {
					Id = "custom:" + Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant(),
					FileName = fileName,
					Title = Prettify(Path.GetFileNameWithoutExtension(fileName)),
					Artist = "Custom"
				});
			}

			EnsureUniqueIds(data);
		}

		internal static void Import(string sourcePath)
		{
			try {
				WeSave.EnsureFolders();
				string ext = Path.GetExtension(sourcePath).ToLowerInvariant();
				if (!AudioExtensions.Contains(ext))
					return;

				string dest = WeFiles.UniquePath(WeSave.MusicFolder, Path.GetFileName(sourcePath));
				File.Copy(sourcePath, dest, overwrite: false);
				string fileName = Path.GetFileName(dest);
				var tags = ReadTags(dest);
				WeSave.Data.Tracks.Add(new WeTrackRecord {
					Id = "custom:" + Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant(),
					FileName = fileName,
					Title = string.IsNullOrWhiteSpace(tags.Title) ? Prettify(Path.GetFileNameWithoutExtension(sourcePath)) : tags.Title,
					Artist = string.IsNullOrWhiteSpace(tags.Artist) ? "Custom" : tags.Artist
				});
				EnsureUniqueIds(WeSave.Data);
				WeSettings.SetMusic(MusicKind.Custom);
				WeSave.Save();
				WePlaylist.Rebuild(play: true);
				WeToast.Show("ToastMusic");
			}
			catch {
			}
		}

		internal static void Delete(WeTrackRecord record)
		{
			if (record == null)
				return;
			string path = FullPath(record.FileName);
			try {
				WeCustomAudio.Stop();
				if (File.Exists(path)) {
					File.SetAttributes(path, FileAttributes.Normal);
					File.Delete(path);
				}
			}
			catch {
			}

			WeSave.Data.Tracks.RemoveAll(item => item.Id == record.Id || string.Equals(item.FileName, record.FileName, StringComparison.OrdinalIgnoreCase));
			WeSave.Data.DisabledTrackIds.Remove(record.Id);
			WeSave.Save();
		}

		internal static void Quarantine(MenuTrack track)
		{
			if (track == null)
				return;

			WeCustomAudio.Stop();
			string source = track.AudioPath;
			WeSave.EnsureFolders();
			try {
				if (File.Exists(source)) {
					string dest = WeFiles.UniquePath(WeSave.BrokenFolder, track.FileName);
					File.SetAttributes(source, FileAttributes.Normal);
					File.Move(source, dest);
				}
			}
			catch {
			}

			WeSave.Data.Tracks.RemoveAll(item => item.Id == track.Id || string.Equals(item.FileName, track.FileName, StringComparison.OrdinalIgnoreCase));
			WeSave.Save();
		}

		private static void EnsureUniqueIds(WeSaveData data)
		{
			var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (WeTrackRecord record in data.Tracks) {
				if (string.IsNullOrEmpty(record.Id) || !used.Add(record.Id)) {
					string baseId = "custom:" + Path.GetFileNameWithoutExtension(record.FileName).ToLowerInvariant();
					string id = baseId;
					int n = 2;
					while (!used.Add(id))
						id = baseId + "_" + n++;
					record.Id = id;
				}
			}
		}

		private static string Prettify(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
				return "Track";
			return name.Replace('_', ' ').Replace('-', ' ').Trim();
		}

		private static (string Title, string Artist) ReadTags(string path)
		{
			try {
				string ext = Path.GetExtension(path).ToLowerInvariant();
				if (ext == ".ogg") {
					using var vorbis = new VorbisReader(path);
					return (Clean(vorbis.Tags?.Title), Clean(vorbis.Tags?.Artist));
				}
			}
			catch {
			}

			return (null, null);
		}

		private static string Clean(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return null;
			value = value.Replace('\0', ' ').Replace('\n', ' ').Trim();
			return string.IsNullOrEmpty(value) ? null : value;
		}
	}
}
