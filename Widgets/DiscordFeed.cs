using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using WallpaperEngine.Core;

namespace WallpaperEngine.Widgets
{
	internal enum DiscordFeedStatus
	{
		Empty,
		Typing,
		Loading,
		Ok,
		NeedWidget,
		BadId,
		NetError
	}

	internal sealed class DiscordMember
	{
		internal string Name = "";
		internal string Status = "online";
		internal string Voice = "";
		internal Texture2D Avatar;
	}

	internal sealed class DiscordSnap
	{
		internal string Name = "";
		internal string Invite = "";
		internal string Voice = "";
		internal int Presence;
		internal DiscordMember[] Members = Array.Empty<DiscordMember>();
	}

	internal static class DiscordFeed
	{
		private const int PollSeconds = 45;
		private static readonly HttpClient Http;
		private static readonly Regex SnowflakeRun = new(@"\d{17,20}", RegexOptions.Compiled);

		private static DateTime _nextFetch;
		private static bool _inFlight;
		private static bool _migrated;
		private static string _fetchId = "";
		private static string _guildId = "";
		private static DiscordSnap _snap = new();
		private static DiscordFeedStatus _status = DiscordFeedStatus.Empty;
		private static readonly Dictionary<string, Texture2D> Avatars = new(StringComparer.Ordinal);

		static DiscordFeed()
		{
			Http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
			Http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "WallpaperEngine-tModLoader/0.6");
			Http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
		}

		internal static string GuildId => _guildId;
		internal static bool HasGuildId => IsSnowflake(_guildId);
		internal static DiscordSnap Snap => _snap ?? new DiscordSnap();
		internal static DiscordFeedStatus Status => _status;

		internal static void Unload()
		{
			_inFlight = false;
			_fetchId = "";
			DisposeAvatars();
			_snap = new DiscordSnap();
			_status = DiscordFeedStatus.Empty;
		}

		internal static string ExtractId(string raw)
		{
			if (string.IsNullOrWhiteSpace(raw))
				return "";

			string digits = DigitsOnly(raw, 24);
			if (IsSnowflake(digits))
				return digits.Length > 20 ? digits[..20] : digits;

			Match match = SnowflakeRun.Match(raw);
			return match.Success && IsSnowflake(match.Value) ? match.Value : DigitsOnly(raw, 20);
		}

		internal static string DigitsOnly(string raw, int max)
		{
			if (string.IsNullOrEmpty(raw))
				return "";

			var buffer = new char[Math.Min(max, raw.Length)];
			int n = 0;
			for (int i = 0; i < raw.Length && n < max; i++) {
				if (raw[i] >= '0' && raw[i] <= '9')
					buffer[n++] = raw[i];
			}

			return n == 0 ? "" : new string(buffer, 0, n);
		}

		internal static bool IsSnowflake(string id)
		{
			if (string.IsNullOrEmpty(id) || id.Length < 17 || id.Length > 20)
				return false;
			for (int i = 0; i < id.Length; i++) {
				if (id[i] < '0' || id[i] > '9')
					return false;
			}

			return true;
		}

		internal static void SetGuildId(string id)
		{
			id = ExtractId(id ?? "");
			if (_guildId == id && (WeSave.Data.DiscordGuildId ?? "") == id)
				return;

			_guildId = id;
			if (WeSave.Data.DiscordGuildId != id) {
				WeSave.Data.DiscordGuildId = id;
				WeSave.Save();
			}

			_fetchId = id;
			_nextFetch = DateTime.MinValue;
			_snap = new DiscordSnap();
			DisposeAvatars();
			if (!IsSnowflake(id)) {
				_status = string.IsNullOrEmpty(id) ? DiscordFeedStatus.Empty : DiscordFeedStatus.Typing;
				return;
			}

			_status = DiscordFeedStatus.Loading;
			if (WeSave.Data.DiscordWidget)
				StartFetch(id);
		}

		internal static void Tick()
		{
			MigrateLegacyFile();
			string saved = WeSave.Data.DiscordGuildId ?? "";
			if (_guildId != saved)
				_guildId = saved;

			if (!WeSave.Data.DiscordWidget)
				return;
			if (!HasGuildId) {
				_status = string.IsNullOrEmpty(_guildId) ? DiscordFeedStatus.Empty : DiscordFeedStatus.Typing;
				return;
			}

			if (_inFlight || DateTime.UtcNow < _nextFetch)
				return;

			StartFetch(_guildId);
		}

		internal static void RefreshNow()
		{
			_nextFetch = DateTime.MinValue;
			Tick();
		}

		private static void MigrateLegacyFile()
		{
			if (_migrated)
				return;
			_migrated = true;
			try {
				if (!File.Exists(WeSave.DiscordPath))
					return;

				string fromFile = "";
				foreach (string raw in File.ReadAllLines(WeSave.DiscordPath)) {
					string found = ExtractId(raw);
					if (IsSnowflake(found)) {
						fromFile = found;
						break;
					}
				}

				if (IsSnowflake(fromFile) && !IsSnowflake(WeSave.Data.DiscordGuildId)) {
					WeSave.Data.DiscordGuildId = fromFile;
					WeSave.Save();
					_guildId = fromFile;
				}

				File.Delete(WeSave.DiscordPath);
			}
			catch {
			}
		}

		private static void StartFetch(string id)
		{
			if (!IsSnowflake(id))
				return;

			_inFlight = true;
			_fetchId = id;
			if (_status != DiscordFeedStatus.Ok)
				_status = DiscordFeedStatus.Loading;

			Task.Run(async () => {
				try {
					using HttpResponseMessage response = await Http.GetAsync("https://discord.com/api/guilds/" + id + "/widget.json");
					if (_fetchId != id)
						return;

					if (response.StatusCode == System.Net.HttpStatusCode.Forbidden) {
						ApplyStatus(id, DiscordFeedStatus.NeedWidget, null);
						return;
					}

					if (response.StatusCode == System.Net.HttpStatusCode.NotFound) {
						ApplyStatus(id, DiscordFeedStatus.BadId, null);
						return;
					}

					if (!response.IsSuccessStatusCode) {
						ApplyStatus(id, DiscordFeedStatus.NetError, null);
						return;
					}

					string json = await response.Content.ReadAsStringAsync();
					if (!TryParse(json, out DiscordSnap snap, out List<(DiscordMember member, string url)> pending)) {
						ApplyStatus(id, DiscordFeedStatus.NetError, null);
						return;
					}

					foreach (var (member, url) in pending) {
						if (_fetchId != id)
							return;
						member.Avatar = await LoadAvatar(url);
					}

					ApplyStatus(id, DiscordFeedStatus.Ok, snap);
				}
				catch {
					ApplyStatus(id, DiscordFeedStatus.NetError, null);
				}
				finally {
					if (_fetchId == id)
						_inFlight = false;
					if (_guildId == id)
						_nextFetch = DateTime.UtcNow.AddSeconds(PollSeconds);
				}
			});
		}

		private static void ApplyStatus(string id, DiscordFeedStatus status, DiscordSnap snap)
		{
			void Apply()
			{
				if (_guildId != id)
					return;
				_status = status;
				if (snap != null)
					_snap = snap;
			}

			if (Main.dedServ) {
				Apply();
				return;
			}

			Main.QueueMainThreadAction(Apply);
		}

		private static bool TryParse(string json, out DiscordSnap snap, out List<(DiscordMember member, string url)> pending)
		{
			snap = new DiscordSnap();
			pending = new List<(DiscordMember, string)>();
			try {
				using JsonDocument doc = JsonDocument.Parse(json);
				JsonElement root = doc.RootElement;
				snap.Name = Str(root, "name");
				snap.Invite = Str(root, "instant_invite");
				if (root.TryGetProperty("presence_count", out JsonElement count) && count.TryGetInt32(out int n))
					snap.Presence = n;

				var channels = new Dictionary<string, string>(StringComparer.Ordinal);
				if (root.TryGetProperty("channels", out JsonElement chans) && chans.ValueKind == JsonValueKind.Array) {
					foreach (JsonElement ch in chans.EnumerateArray()) {
						string cid = Str(ch, "id");
						string cname = Str(ch, "name");
						if (cid.Length > 0 && cname.Length > 0)
							channels[cid] = cname;
					}
				}

				var members = new List<DiscordMember>();
				if (root.TryGetProperty("members", out JsonElement list) && list.ValueKind == JsonValueKind.Array) {
					foreach (JsonElement item in list.EnumerateArray()) {
						if (members.Count >= 12)
							break;
						var member = new DiscordMember {
							Name = Str(item, "username"),
							Status = Str(item, "status")
						};
						if (member.Name.Length == 0)
							continue;

						string channelId = Str(item, "channel_id");
						if (channelId.Length > 0 && channels.TryGetValue(channelId, out string voice)) {
							member.Voice = voice;
							if (string.IsNullOrEmpty(snap.Voice))
								snap.Voice = voice;
						}

						string url = Str(item, "avatar_url");
						if (url.Length > 0)
							pending.Add((member, url));
						members.Add(member);
					}
				}

				snap.Members = members.ToArray();
				if (string.IsNullOrEmpty(snap.Name))
					snap.Name = "Discord";
				return true;
			}
			catch {
				return false;
			}
		}

		private static async Task<Texture2D> LoadAvatar(string url)
		{
			if (string.IsNullOrEmpty(url))
				return null;

			lock (Avatars) {
				if (Avatars.TryGetValue(url, out Texture2D cached) && cached != null && !cached.IsDisposed)
					return cached;
			}

			try {
				byte[] bytes = await Http.GetByteArrayAsync(url);
				var done = new TaskCompletionSource<Texture2D>();
				Main.QueueMainThreadAction(() => {
					Texture2D ready = null;
					try {
						ready = StampCircle(bytes);
						if (ready != null) {
							lock (Avatars) {
								Avatars[url] = ready;
							}
						}
					}
					catch {
						ready = null;
					}

					done.TrySetResult(ready);
				});
				return await done.Task;
			}
			catch {
				return null;
			}
		}

		private static Texture2D StampCircle(byte[] bytes)
		{
			if (bytes == null || bytes.Length < 32 || Main.graphics?.GraphicsDevice == null)
				return null;

			using var stream = new MemoryStream(bytes);
			Texture2D raw = Texture2D.FromStream(Main.graphics.GraphicsDevice, stream);
			if (raw == null || raw.IsDisposed)
				return null;

			const int size = 64;
			var src = new Color[raw.Width * raw.Height];
			raw.GetData(src);
			var dst = new Color[size * size];
			float cx = (size - 1) * 0.5f;
			float radius = size * 0.5f;
			int srcW = raw.Width;
			int srcH = raw.Height;
			for (int y = 0; y < size; y++) {
				for (int x = 0; x < size; x++) {
					float dx = x - cx;
					float dy = y - cx;
					float dist = MathF.Sqrt(dx * dx + dy * dy) / radius;
					if (dist > 1f)
						continue;

					int sx = (int)MathF.Round(x / (float)(size - 1) * (srcW - 1));
					int sy = (int)MathF.Round(y / (float)(size - 1) * (srcH - 1));
					sx = Math.Clamp(sx, 0, srcW - 1);
					sy = Math.Clamp(sy, 0, srcH - 1);
					Color p = src[sy * srcW + sx];
					float edge = MathHelper.Clamp((1f - dist) * 10f, 0f, 1f);
					dst[y * size + x] = new Color(p.R, p.G, p.B, (byte)(p.A * edge));
				}
			}

			var circle = new Texture2D(Main.graphics.GraphicsDevice, size, size);
			circle.SetData(dst);
			try {
				if (!raw.IsDisposed)
					raw.Dispose();
			}
			catch {
			}

			return circle;
		}

		private static void DisposeAvatars()
		{
			foreach (Texture2D tex in Avatars.Values) {
				try {
					if (tex != null && !tex.IsDisposed)
						tex.Dispose();
				}
				catch {
				}
			}

			Avatars.Clear();
		}

		private static string Str(JsonElement el, string name)
		{
			if (!el.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String)
				return "";
			return value.GetString() ?? "";
		}
	}
}
