using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
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
		internal string ChannelId = "";
		internal Texture2D Avatar;
	}

	internal sealed class DiscordSnap
	{
		internal string Name = "";
		internal string Invite = "";
		internal string InviteCode = "";
		internal string Voice = "";
		internal string VoiceChannelId = "";
		internal int Presence;
		internal Texture2D Icon;
		internal DiscordMember[] Members = Array.Empty<DiscordMember>();
		internal DiscordChan[] Channels = Array.Empty<DiscordChan>();
	}

	internal sealed class DiscordChan
	{
		internal string Id = "";
		internal string Name = "";
	}

	internal static class DiscordFeed
	{
		private const int PollOkSeconds = 10;
		private const int PollRetrySeconds = 8;
		private const int FlightGuardSeconds = 20;
		private static readonly HttpClient Http;
		private static readonly Regex SnowflakeRun = new(@"\d{17,20}", RegexOptions.Compiled);

		private static DateTime _nextFetch;
		private static DateTime _flightStarted;
		private static bool _inFlight;
		private static bool _migrated;
		private static string _fetchId = "";
		private static string _guildId = "";
		private static DiscordSnap _snap = new();
		private static DiscordFeedStatus _status = DiscordFeedStatus.Empty;
		private static readonly Dictionary<string, Texture2D> Avatars = new(StringComparer.Ordinal);

		static DiscordFeed()
		{
			var handler = new SocketsHttpHandler {
				PooledConnectionLifetime = TimeSpan.FromMinutes(1),
				AutomaticDecompression = DecompressionMethods.All
			};
			Http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
			Http.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };
			Http.DefaultRequestHeaders.Pragma.ParseAdd("no-cache");
			Http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "WallpaperEngine-tModLoader/0.8.5");
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

			if (_inFlight) {
				if ((DateTime.UtcNow - _flightStarted).TotalSeconds < FlightGuardSeconds)
					return;
				_inFlight = false;
			}

			if (DateTime.UtcNow < _nextFetch)
				return;

			StartFetch(_guildId);
		}

		internal static void RefreshNow()
		{
			_nextFetch = DateTime.MinValue;
			Tick();
		}

		internal static void OpenJoin(string channelId = null)
		{
			string guild = _guildId;
			string voice = channelId;
			if (string.IsNullOrEmpty(voice))
				voice = _snap?.VoiceChannelId ?? "";
			string code = _snap?.InviteCode ?? "";
			if (!string.IsNullOrEmpty(guild) && !string.IsNullOrEmpty(voice) && TryProtocol("discord://-/channels/" + guild + "/" + voice))
				return;
			if (!string.IsNullOrEmpty(code) && TryProtocol("discord://-/invite/" + code))
				return;
			if (!string.IsNullOrEmpty(guild) && TryProtocol("discord://-/channels/" + guild))
				return;
			if (!string.IsNullOrEmpty(_snap?.Invite)) {
				try {
					Utils.OpenToURL(_snap.Invite);
				}
				catch {
				}
			}
		}

		private static bool TryProtocol(string url)
		{
			try {
				Process.Start(new ProcessStartInfo {
					FileName = url,
					UseShellExecute = true
				});
				return true;
			}
			catch {
				return false;
			}
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
			_flightStarted = DateTime.UtcNow;
			_fetchId = id;
			if (_status != DiscordFeedStatus.Ok)
				_status = DiscordFeedStatus.Loading;

			Task.Run(async () => {
				int delay = PollOkSeconds;
				try {
					string url = "https://discord.com/api/guilds/" + id + "/widget.json?t=" + DateTime.UtcNow.Ticks;
					using HttpResponseMessage response = await GetNoCache(url).ConfigureAwait(false);
					if (_fetchId != id)
						return;

					if (response.StatusCode == System.Net.HttpStatusCode.Forbidden) {
						delay = PollRetrySeconds;
						ApplyStatus(id, DiscordFeedStatus.NeedWidget, null);
						return;
					}

					if (response.StatusCode == System.Net.HttpStatusCode.NotFound) {
						delay = PollRetrySeconds;
						ApplyStatus(id, DiscordFeedStatus.BadId, null);
						return;
					}

					if (!response.IsSuccessStatusCode) {
						delay = PollRetrySeconds;
						ApplyStatus(id, DiscordFeedStatus.NetError, null);
						return;
					}

					string json = await response.Content.ReadAsStringAsync();
					if (!TryParse(json, out DiscordSnap snap, out List<(DiscordMember member, string url)> pending, out string iconHint)) {
						delay = PollRetrySeconds;
						ApplyStatus(id, DiscordFeedStatus.NetError, null);
						return;
					}

					ApplyCachedIcon(snap, iconHint);
					ApplyStatus(id, DiscordFeedStatus.Ok, snap);
					_ = FillAvatars(id, pending);
					_ = FillInviteAndIcon(id, snap, iconHint);
				}
				catch {
					delay = PollRetrySeconds;
					ApplyStatus(id, DiscordFeedStatus.NetError, null);
				}
				finally {
					if (_fetchId == id)
						_inFlight = false;
					if (_guildId == id)
						_nextFetch = DateTime.UtcNow.AddSeconds(delay);
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

		private static bool TryParse(string json, out DiscordSnap snap, out List<(DiscordMember member, string url)> pending, out string iconHint)
		{
			snap = new DiscordSnap();
			pending = new List<(DiscordMember, string)>();
			iconHint = "";
			try {
				using JsonDocument doc = JsonDocument.Parse(json);
				JsonElement root = doc.RootElement;
				snap.Name = Str(root, "name");
				snap.Invite = Str(root, "instant_invite");
				snap.InviteCode = InviteCodeFrom(snap.Invite);
				if (root.TryGetProperty("presence_count", out JsonElement count) && count.TryGetInt32(out int n))
					snap.Presence = n;

				var channels = new Dictionary<string, string>(StringComparer.Ordinal);
				if (root.TryGetProperty("channels", out JsonElement chans) && chans.ValueKind == JsonValueKind.Array) {
					foreach (JsonElement ch in chans.EnumerateArray()) {
						string cid = IdOf(ch, "id");
						string cname = Str(ch, "name");
						if (cid.Length == 0)
							continue;
						if (cname.Length > 0)
							channels[cid] = cname;
					}
				}

				var members = new List<DiscordMember>();
				if (root.TryGetProperty("members", out JsonElement list) && list.ValueKind == JsonValueKind.Array) {
					foreach (JsonElement item in list.EnumerateArray()) {
						if (members.Count >= 48)
							break;
						var member = new DiscordMember {
							Name = Str(item, "username"),
							Status = Str(item, "status")
						};
						if (member.Name.Length == 0)
							continue;

						string channelId = IdOf(item, "channel_id");
						member.ChannelId = channelId;
						if (channelId.Length > 0) {
							member.Voice = channels.TryGetValue(channelId, out string voice) && voice.Length > 0
								? voice
								: "Voice";
						}

						string url = Str(item, "avatar_url");
						if (url.Length > 0)
							pending.Add((member, url));
						members.Add(member);
					}
				}

				members.Sort(CompareMembers);
				snap.Members = members.ToArray();
				var chanList = new List<DiscordChan>();
				foreach (KeyValuePair<string, string> pair in channels)
					chanList.Add(new DiscordChan { Id = pair.Key, Name = pair.Value });
				for (int i = 0; i < members.Count; i++) {
					string cid = members[i].ChannelId;
					if (string.IsNullOrEmpty(cid) || channels.ContainsKey(cid))
						continue;
					chanList.Add(new DiscordChan { Id = cid, Name = members[i].Voice });
					channels[cid] = members[i].Voice;
				}

				snap.Channels = chanList.ToArray();
				PickOccupiedVoice(snap);
				if (string.IsNullOrEmpty(snap.Name))
					snap.Name = "Discord";
				return true;
			}
			catch {
				return false;
			}
		}

		private static void PickOccupiedVoice(DiscordSnap snap)
		{
			snap.Voice = "";
			snap.VoiceChannelId = "";
			if (snap.Members == null)
				return;

			var counts = new Dictionary<string, int>(StringComparer.Ordinal);
			string bestId = "";
			string bestName = "";
			int bestCount = 0;
			for (int i = 0; i < snap.Members.Length; i++) {
				DiscordMember member = snap.Members[i];
				if (member == null || string.IsNullOrEmpty(member.ChannelId))
					continue;
				counts.TryGetValue(member.ChannelId, out int n);
				n++;
				counts[member.ChannelId] = n;
				if (n < bestCount)
					continue;
				bestCount = n;
				bestId = member.ChannelId;
				bestName = member.Voice;
			}

			snap.VoiceChannelId = bestId;
			snap.Voice = bestName;
		}

		private static async Task<HttpResponseMessage> GetNoCache(string url)
		{
			using var req = new HttpRequestMessage(HttpMethod.Get, url);
			req.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };
			req.Headers.Pragma.ParseAdd("no-cache");
			return await Http.SendAsync(req).ConfigureAwait(false);
		}

		private static int CompareMembers(DiscordMember a, DiscordMember b)
		{
			int voice = (string.IsNullOrEmpty(a?.Voice) ? 1 : 0).CompareTo(string.IsNullOrEmpty(b?.Voice) ? 1 : 0);
			if (voice != 0)
				return voice;
			return string.Compare(a?.Name, b?.Name, StringComparison.OrdinalIgnoreCase);
		}

		private static async Task FillAvatars(string id, List<(DiscordMember member, string url)> pending)
		{
			if (pending == null || pending.Count == 0)
				return;

			using var gate = new SemaphoreSlim(4);
			var tasks = new List<Task>(pending.Count);
			foreach (var (member, url) in pending) {
				DiscordMember who = member;
				string src = url;
				tasks.Add(Task.Run(async () => {
					await gate.WaitAsync().ConfigureAwait(false);
					try {
						if (_fetchId != id)
							return;
						Texture2D tex = await LoadAvatar(id, src).ConfigureAwait(false);
						if (tex == null || _fetchId != id)
							return;
						who.Avatar = tex;
					}
					finally {
						gate.Release();
					}
				}));
			}

			try {
				await Task.WhenAll(tasks).ConfigureAwait(false);
			}
			catch {
			}
		}

		private static async Task FillInviteAndIcon(string id, DiscordSnap snap, string iconHint)
		{
			if (snap == null || _fetchId != id)
				return;

			string iconUrl = iconHint ?? "";
			try {
				string code = snap.InviteCode;
				if (string.IsNullOrEmpty(code))
					code = InviteCodeFrom(snap.Invite);
				if (!string.IsNullOrEmpty(code)) {
					using HttpResponseMessage response = await GetNoCache("https://discord.com/api/invites/" + Uri.EscapeDataString(code) + "?t=" + DateTime.UtcNow.Ticks).ConfigureAwait(false);
					if (_fetchId != id)
						return;
					if (response.IsSuccessStatusCode) {
						string json = await response.Content.ReadAsStringAsync();
						ReadInvite(json, snap, id, out string fromInvite);
						if (fromInvite.Length > 0)
							iconUrl = fromInvite;
					}
				}
			}
			catch {
			}

			if (string.IsNullOrEmpty(iconUrl) || _fetchId != id)
				return;

			Texture2D icon = await LoadAvatar(id, iconUrl).ConfigureAwait(false);
			if (icon == null || _fetchId != id)
				return;

			Main.QueueMainThreadAction(() => {
				if (_guildId != id || _snap != snap)
					return;
				snap.Icon = icon;
			});
		}

		private static void ReadInvite(string json, DiscordSnap snap, string guildId, out string iconUrl)
		{
			iconUrl = "";
			try {
				using JsonDocument doc = JsonDocument.Parse(json);
				JsonElement root = doc.RootElement;
				if (root.TryGetProperty("guild", out JsonElement guild)) {
					string icon = Str(guild, "icon");
					string gid = Str(guild, "id");
					if (string.IsNullOrEmpty(gid))
						gid = guildId;
					if (icon.Length > 0 && gid.Length > 0)
						iconUrl = "https://cdn.discordapp.com/icons/" + gid + "/" + icon + ".png?size=128";
					if (string.IsNullOrEmpty(snap.Name))
						snap.Name = Str(guild, "name");
				}

				if (!root.TryGetProperty("channel", out JsonElement channel))
					return;

				string cid = IdOf(channel, "id");
				string cname = Str(channel, "name");
				int type = IntOf(channel, "type");
				if (cid.Length == 0)
					return;
				if (string.IsNullOrEmpty(snap.VoiceChannelId))
					snap.VoiceChannelId = cid;
				if ((type == 2 || type == 13) && string.IsNullOrEmpty(snap.Voice) && cname.Length > 0)
					snap.Voice = cname;
			}
			catch {
			}
		}

		private static void ApplyCachedIcon(DiscordSnap snap, string iconUrl)
		{
			if (snap == null)
				return;
			if (!string.IsNullOrEmpty(iconUrl)) {
				lock (Avatars) {
					if (Avatars.TryGetValue(iconUrl, out Texture2D cached) && cached != null && !cached.IsDisposed)
						snap.Icon = cached;
				}
			}

			if (snap.Icon == null && _snap?.Icon != null && !_snap.Icon.IsDisposed)
				snap.Icon = _snap.Icon;
		}

		private static string InviteCodeFrom(string invite)
		{
			if (string.IsNullOrWhiteSpace(invite))
				return "";

			string s = invite.Trim();
			int q = s.IndexOf('?');
			if (q >= 0)
				s = s[..q];
			s = s.TrimEnd('/');
			int slash = s.LastIndexOf('/');
			if (slash >= 0)
				s = s[(slash + 1)..];
			if (s.Length < 2 || s.Length > 64)
				return "";
			for (int i = 0; i < s.Length; i++) {
				char c = s[i];
				if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
					return "";
			}

			return s;
		}

		private static async Task<Texture2D> LoadAvatar(string fetchId, string url)
		{
			if (string.IsNullOrEmpty(url) || _fetchId != fetchId)
				return null;

			lock (Avatars) {
				if (Avatars.TryGetValue(url, out Texture2D cached) && cached != null && !cached.IsDisposed)
					return cached;
			}

			try {
				byte[] bytes = await Http.GetByteArrayAsync(url).ConfigureAwait(false);
				if (_fetchId != fetchId)
					return null;

				var done = new TaskCompletionSource<Texture2D>();
				Main.QueueMainThreadAction(() => {
					Texture2D ready = null;
					try {
						if (_fetchId == fetchId)
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
				return await done.Task.ConfigureAwait(false);
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

		private static string IdOf(JsonElement el, string name)
		{
			if (!el.TryGetProperty(name, out JsonElement value))
				return "";
			if (value.ValueKind == JsonValueKind.String)
				return value.GetString() ?? "";
			if (value.ValueKind == JsonValueKind.Number)
				return value.TryGetInt64(out long n) ? n.ToString() : value.GetRawText();
			return "";
		}

		private static int IntOf(JsonElement el, string name)
		{
			if (!el.TryGetProperty(name, out JsonElement value) || !value.TryGetInt32(out int n))
				return 0;
			return n;
		}
	}
}
