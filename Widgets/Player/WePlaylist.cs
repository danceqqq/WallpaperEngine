using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using WallpaperEngine.Content;
using WallpaperEngine.Core;
using WallpaperEngine.UI;

namespace WallpaperEngine.Audio
{
	internal sealed class MenuTrack
	{
		public string Id;
		public string FileName;
		public string Title;
		public string Artist;
		public bool Enabled = true;

		internal string AudioPath => WeLibrary.FullPath(FileName);
	}

	internal static class WePlaylist
	{
		private static readonly List<MenuTrack> Active = new();
		private static bool _paused;
		private static bool _loop;
		private static bool _shuffle;
		private static int _index;
		private static string _loopedId = "";
		private static float _mix;
		private static uint _lifeFrame = uint.MaxValue;
		private static readonly List<int> ShuffleHistory = new();
		private static bool _menuAudioStarted;
		private static bool _pendingStart;
		private static bool _returnFromWorld;
		private static int _customRetryDelay;
		private static int _customPlayFails;
		private static string _customFailId = "";

		internal static IReadOnlyList<MenuTrack> Tracks => Active;
		internal static MenuTrack Current => Active.Count == 0 ? Empty : Active[Math.Clamp(_index, 0, Active.Count - 1)];
		internal static bool IsPaused => _paused;
		internal static bool LoopEnabled => _loop;
		internal static bool ShuffleEnabled => _shuffle;
		internal static float OutputMix => MathHelper.Clamp(_mix, 0f, 1f);

		private static readonly MenuTrack Empty = new() { Title = "—", Artist = "", FileName = "", Id = "" };

		internal static int MenuMusicId
		{
			get
			{
				if (WePersist.MenuStillLoading)
					return Main.curMusic > 0 ? Main.curMusic : 50;
				return 0;
			}
		}

		internal static void Load(Terraria.ModLoader.Mod _)
		{
			WeSave.EnsureLoaded();
			RecoverCrashedCustom();
			Rebuild(play: false);
		}

		internal static void Unload()
		{
			WeCustomAudio.Stop();
		}

		internal static void Rebuild(string playId = null, bool play = true)
		{
			string keepId = playId ?? Current?.Id;
			WeLibrary.ScanIntoSave();
			WeSaveData data = WeSave.Data;
			Active.Clear();
			foreach (WeTrackRecord record in data.Tracks) {
				var track = new MenuTrack {
					Id = record.Id,
					FileName = record.FileName,
					Title = string.IsNullOrWhiteSpace(record.Title) ? record.FileName : record.Title,
					Artist = string.IsNullOrWhiteSpace(record.Artist) ? "Custom" : record.Artist,
					Enabled = !data.DisabledTrackIds.Contains(record.Id)
				};
				if (track.Enabled && File.Exists(track.AudioPath))
					Active.Add(track);
			}

			_loop = data.LoopEnabled && Active.Any(track => track.Id == data.LoopedTrackId);
			_loopedId = _loop ? data.LoopedTrackId : "";
			_shuffle = data.ShuffleEnabled;
			int next = Math.Max(0, Active.FindIndex(track => track.Id == keepId));
			if (_loop && !string.IsNullOrEmpty(_loopedId)) {
				int looped = Active.FindIndex(track => track.Id == _loopedId);
				if (looped >= 0)
					next = looped;
			}

			if (!play) {
				_index = next;
				return;
			}

			if (Active.Count > 0)
				PlayIndex(next);
			else
				_index = 0;
		}

		internal static void OnThemeSelected()
		{
			Rebuild(play: false);
			if (WeSave.Data.Music != MusicKind.Custom) {
				Silence();
				return;
			}

			if (WePersist.MenuStillLoading) {
				_pendingStart = true;
				return;
			}

			if (_menuAudioStarted)
				return;

			StartPlayback();
			_returnFromWorld = false;
			_pendingStart = false;
			_menuAudioStarted = true;
		}

		internal static void Silence()
		{
			_menuAudioStarted = false;
			_mix = 0f;
			WeCustomAudio.Stop();
			Main.newMusic = 0;
			MuteVanilla();
		}

		internal static void HandleMenuLifecycle()
		{
			if (!Main.gameMenu) {
				if (_lifeFrame != Main.GameUpdateCount) {
					_lifeFrame = Main.GameUpdateCount;
					_pendingStart = false;
					_menuAudioStarted = false;
					TickMix(0f, 0.01f);
					WeCustomAudio.Update();
					if (_mix <= 0.001f)
						WeCustomAudio.Stop();
				}

				_returnFromWorld = true;
				return;
			}

			if (WePersist.MenuStillLoading) {
				_pendingStart = true;
				return;
			}

			if (!_menuAudioStarted && WeSave.Data.Music == MusicKind.Custom && (_returnFromWorld || _pendingStart)) {
				_returnFromWorld = false;
				_pendingStart = false;
				StartPlayback();
				_menuAudioStarted = true;
			}

			if (_lifeFrame == Main.GameUpdateCount)
				return;

			_lifeFrame = Main.GameUpdateCount;
			if (WeSave.Data.Music == MusicKind.Custom && !_paused)
				WeCustomAudio.Update();
		}

		internal static void Update()
		{
			if (WeSave.Data.Music != MusicKind.Custom || Active.Count == 0) {
				if (WeSave.Data.Music != MusicKind.Vanilla)
					MuteVanilla();
				return;
			}

			Main.newMusic = 0;
			MuteVanilla();
			TickMix(1f, 0.01f);
			WeCustomAudio.Update();
			if (_paused)
				return;

			if (!WeCustomAudio.IsPlaying && !WeCustomAudio.Finished) {
				if (_customRetryDelay > 0)
					_customRetryDelay--;
				else if (!TryPlay(Current))
					_customRetryDelay = 20;
			}

			if (WeCustomAudio.Finished) {
				if (_loop)
					PlayIndex(_index);
				else
					Next();
			}
		}

		internal static void TogglePause()
		{
			if (Active.Count == 0) {
				WePanels.Open(WePanelId.Music);
				return;
			}

			EnsureCustomMode();
			if (!WeCustomAudio.HasOutput) {
				_paused = false;
				PlayIndex(_index);
				return;
			}

			_paused = !_paused;
			WeCustomAudio.TogglePause();
			if (_paused)
				MuteVanilla();
		}

		internal static void Next()
		{
			if (Active.Count == 0)
				return;
			EnsureCustomMode();
			int next = _shuffle ? RandomIndex(_index) : (_index + 1) % Active.Count;
			if (_shuffle)
				ShuffleHistory.Add(_index);
			PlayIndex(next);
		}

		internal static void Previous()
		{
			if (Active.Count == 0)
				return;
			EnsureCustomMode();
			if (_shuffle && ShuffleHistory.Count > 0) {
				int last = ShuffleHistory[^1];
				ShuffleHistory.RemoveAt(ShuffleHistory.Count - 1);
				PlayIndex(Math.Clamp(last, 0, Active.Count - 1));
				return;
			}

			PlayIndex((_index - 1 + Active.Count) % Active.Count);
		}

		internal static void ToggleLoop()
		{
			_loop = !_loop;
			_loopedId = _loop ? Current.Id : "";
			WeSave.Data.LoopEnabled = _loop;
			WeSave.Data.LoopedTrackId = _loopedId;
			WeSave.Save();
		}

		internal static void ToggleShuffle()
		{
			_shuffle = !_shuffle;
			WeSave.Data.ShuffleEnabled = _shuffle;
			WeSave.Save();
		}

		internal static void Seek01(float t)
		{
			WeCustomAudio.Seek01(t);
		}

		internal static float GetDisplayTime() => WeCustomAudio.Time;
		internal static float GetDuration() => WeCustomAudio.Duration;

		internal static string FormatTime(float seconds)
		{
			seconds = Math.Max(0f, seconds);
			int whole = (int)seconds;
			return $"{whole / 60}:{whole % 60:00}";
		}

		internal static void PlayTrack(WeTrackRecord record)
		{
			if (record == null)
				return;
			WeSave.Data.DisabledTrackIds.Remove(record.Id);
			WeSave.Data.Music = MusicKind.Custom;
			WeSave.Save();
			Rebuild(playId: record.Id, play: true);
		}

		internal static void SetEnabled(WeTrackRecord record, bool enabled)
		{
			if (enabled)
				WeSave.Data.DisabledTrackIds.Remove(record.Id);
			else if (!WeSave.Data.DisabledTrackIds.Contains(record.Id))
				WeSave.Data.DisabledTrackIds.Add(record.Id);
			WeSave.Save();
			Rebuild();
		}

		internal static void DeleteCustom(WeTrackRecord record)
		{
			WeLibrary.Delete(record);
			Rebuild();
		}

		private static void StartPlayback()
		{
			if (Active.Count == 0)
				return;
			int index = _index;
			if (_loop) {
				int looped = Active.FindIndex(track => track.Id == _loopedId);
				if (looped >= 0)
					index = looped;
			}
			else if (_shuffle && !_loop)
				index = RandomIndex(_index);

			PlayIndex(index);
		}

		private static void EnsureCustomMode()
		{
			if (WeSave.Data.Music == MusicKind.Custom)
				return;
			WeSave.Data.Music = MusicKind.Custom;
			WeSave.Save();
		}

		private static void PlayIndex(int index)
		{
			WeCustomAudio.Stop();
			if (Active.Count == 0)
				return;

			EnsureCustomMode();
			_index = Math.Clamp(index, 0, Active.Count - 1);
			_paused = false;
			_customRetryDelay = 0;
			_mix = 1f;
			Main.newMusic = 0;
			MuteVanilla();
			WeSave.WritePlayGuard(Current.FileName);
			TryPlay(Current);
			WeSave.ClearPlayGuard();
		}

		private static bool TryPlay(MenuTrack track)
		{
			if (track == null || string.IsNullOrEmpty(track.FileName))
				return false;

			bool played = WeCustomAudio.Play(track.AudioPath);
			if (played) {
				_customPlayFails = 0;
				_customFailId = "";
				return true;
			}

			if (_customFailId != track.Id) {
				_customFailId = track.Id;
				_customPlayFails = 0;
			}

			_customPlayFails++;
			if (_customPlayFails >= 12)
				WeLibrary.Quarantine(track);
			return false;
		}

		private static void MuteVanilla()
		{
			Main.newMusic = 0;
			int max = Main.musicFade == null ? 0 : Math.Min(Main.maxMusic, Main.musicFade.Length);
			for (int i = 0; i < max; i++)
				Main.musicFade[i] = 0f;
		}

		private static void TickMix(float target, float speed)
		{
			if (_mix < target)
				_mix = Math.Min(target, _mix + speed);
			else if (_mix > target)
				_mix = Math.Max(target, _mix - speed);
		}

		private static int RandomIndex(int except)
		{
			int count = Math.Max(1, Active.Count);
			if (count <= 1)
				return 0;
			int next = except;
			int guard = 0;
			while (next == except && guard++ < 24)
				next = Main.rand != null ? Main.rand.Next(count) : Random.Shared.Next(count);
			if (next == except)
				next = (except + 1) % count;
			return next;
		}

		private static void RecoverCrashedCustom()
		{
			string fileName = WeSave.ReadPlayGuard();
			WeSave.ClearPlayGuard();
			if (string.IsNullOrEmpty(fileName))
				return;
			WeLibrary.Quarantine(new MenuTrack { FileName = fileName, Id = "custom:" + Path.GetFileNameWithoutExtension(fileName) });
		}
	}
}
