using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace TaiChuWeb_V2.Services.Analytics
{
    /// <summary>
    /// 社区统计统一用中国时间（UTC+8，无夏令时）划分“天”，与签到逻辑一致。
    /// </summary>
    public static class ChinaClock
    {
        public static readonly TimeSpan Offset = TimeSpan.FromHours(8);
        public static DateTime Now => DateTime.UtcNow + Offset;
        public static DateTime Today => Now.Date;
        public static DateTime ToLocal(DateTime utc) => utc + Offset;
        /// <summary>中国时间某天 00:00 对应的 UTC 时刻</summary>
        public static DateTime DayStartUtc(DateTime localDay) => DateTime.SpecifyKind(localDay.Date - Offset, DateTimeKind.Utc);
    }

    /// <summary>
    /// 在线状态与活跃记录（内存）。
    /// · 登录用户：任何带有效登录凭证的请求都会记一次“出现”；
    /// · 游客：前端每分钟发一次心跳，带一个随机生成的匿名访客编号（不含任何个人信息）。
    /// 最近 5 分钟内出现过的算“在线”。每天的活跃记录先攒在内存里，由 AnalyticsWorker 每分钟批量写入数据库。
    /// </summary>
    public sealed partial class PresenceTracker
    {
        public static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(5);

        public sealed class Seen
        {
            public Guid? UserId;
            public DateTime FirstUtc;
            public DateTime LastUtc;
            public int Hits;
        }

        private readonly ConcurrentDictionary<string, Seen> _online = new();
        private ConcurrentDictionary<(DateTime Day, Guid UserId), Seen> _memberDays = new();
        private ConcurrentDictionary<(DateTime Day, string VisitorId), Seen> _visitorDays = new();

        [GeneratedRegex("^[A-Za-z0-9_-]{8,64}$")]
        private static partial Regex VisitorIdPattern();

        public static bool IsValidVisitorId(string? id) => !string.IsNullOrEmpty(id) && VisitorIdPattern().IsMatch(id);

        public void TouchMember(Guid userId, string? visitorId = null)
        {
            var now = DateTime.UtcNow;
            Upsert(_online, "u:" + userId, userId, now);
            // 同一个浏览器登录后，不再把它当游客重复计数
            if (IsValidVisitorId(visitorId)) _online.TryRemove("v:" + visitorId, out _);
            Upsert(_memberDays, (ChinaClock.ToLocal(now).Date, userId), userId, now);
        }

        public void TouchVisitor(string visitorId)
        {
            if (!IsValidVisitorId(visitorId)) return;
            var now = DateTime.UtcNow;
            Upsert(_online, "v:" + visitorId, null, now);
            Upsert(_visitorDays, (ChinaClock.ToLocal(now).Date, visitorId), null, now);
        }

        private static void Upsert<TKey>(ConcurrentDictionary<TKey, Seen> map, TKey key, Guid? userId, DateTime now) where TKey : notnull
        {
            map.AddOrUpdate(key,
                _ => new Seen { UserId = userId, FirstUtc = now, LastUtc = now, Hits = 1 },
                (_, s) => { lock (s) { s.LastUtc = now; s.Hits++; } return s; });
        }

        /// <summary>当前在线：登录用户列表（按最近出现时间倒序）与游客人数。顺便清理过期记录。</summary>
        public (List<(Guid UserId, DateTime LastUtc)> Members, int Guests) Snapshot()
        {
            var cutoff = DateTime.UtcNow - OnlineWindow;
            var members = new List<(Guid, DateTime)>();
            var guests = 0;
            foreach (var (key, seen) in _online)
            {
                if (seen.LastUtc < cutoff) { _online.TryRemove(key, out _); continue; }
                if (seen.UserId is Guid id) members.Add((id, seen.LastUtc));
                else guests++;
            }
            members.Sort((a, b) => b.Item2.CompareTo(a.Item2));
            return (members, guests);
        }

        // ---------- 隐身 / 正在看的板块 ----------
        private readonly ConcurrentDictionary<Guid, byte> _invisible = new();
        private readonly ConcurrentDictionary<Guid, (string Section, DateTime At)> _sections = new();

        [GeneratedRegex("^[a-z]{1,16}$")]
        private static partial Regex SectionPattern();

        /// <summary>隐身的人：不出现在在线列表里，也不显示「最近来过」（在线人数里还算，只是不知道是谁）</summary>
        public bool IsInvisible(Guid userId) => _invisible.ContainsKey(userId);
        public void SetInvisible(Guid userId, bool invisible)
        {
            if (invisible) _invisible[userId] = 1;
            else _invisible.TryRemove(userId, out _);
        }
        public void LoadInvisible(IEnumerable<Guid> userIds) { foreach (var id in userIds) _invisible[id] = 1; }

        /// <summary>前端心跳带来的「正在看哪个板块」（只是板块名，比如 chat、gallery，不记具体页面）</summary>
        public void SetSection(Guid userId, string? section)
        {
            if (string.IsNullOrEmpty(section) || !SectionPattern().IsMatch(section)) { _sections.TryRemove(userId, out _); return; }
            _sections[userId] = (section, DateTime.UtcNow);
        }
        public string? SectionOf(Guid userId)
        {
            if (!_sections.TryGetValue(userId, out var s)) return null;
            if (s.At < DateTime.UtcNow - OnlineWindow) { _sections.TryRemove(userId, out _); return null; }
            return s.Section;
        }

        /// <summary>取走尚未写入数据库的每日活跃记录。</summary>
        public (Dictionary<(DateTime Day, Guid UserId), Seen> Members, Dictionary<(DateTime Day, string VisitorId), Seen> Visitors) Drain()
        {
            var members = Interlocked.Exchange(ref _memberDays, new ConcurrentDictionary<(DateTime Day, Guid UserId), Seen>());
            var visitors = Interlocked.Exchange(ref _visitorDays, new ConcurrentDictionary<(DateTime Day, string VisitorId), Seen>());
            return (members.ToDictionary(kv => kv.Key, kv => kv.Value), visitors.ToDictionary(kv => kv.Key, kv => kv.Value));
        }

        /// <summary>写库失败时把记录放回去，下次再试。</summary>
        public void Restore(Dictionary<(DateTime Day, Guid UserId), Seen> members, Dictionary<(DateTime Day, string VisitorId), Seen> visitors)
        {
            foreach (var (k, v) in members) _memberDays.AddOrUpdate(k, v, (_, s) => Merge(s, v));
            foreach (var (k, v) in visitors) _visitorDays.AddOrUpdate(k, v, (_, s) => Merge(s, v));
        }

        private static Seen Merge(Seen into, Seen from)
        {
            lock (into)
            {
                into.Hits += from.Hits;
                if (from.FirstUtc < into.FirstUtc) into.FirstUtc = from.FirstUtc;
                if (from.LastUtc > into.LastUtc) into.LastUtc = from.LastUtc;
            }
            return into;
        }
    }
}
