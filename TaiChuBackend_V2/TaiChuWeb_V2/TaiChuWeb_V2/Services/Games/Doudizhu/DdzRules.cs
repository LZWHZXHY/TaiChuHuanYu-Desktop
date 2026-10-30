namespace TaiChuWeb_V2.Services.Games.Doudizhu
{
    /// <summary>
    /// 斗地主规则（与前端 views/太虚游/games/Doudizhu/ddzRules.ts 是同一套逻辑，改动要两边一致）。
    /// 牌的编号 0–53：0–51 普通牌，点数 = id/4 + 3，花色 = id%4；52 小王，53 大王。
    /// 点数：3…10, J=11, Q=12, K=13, A=14, 2=15, 小王=16, 大王=17。
    /// </summary>
    public sealed record DdzPattern(string Type, int Rank, int Len);

    public static class DdzRules
    {
        public static int RankOf(int card) => card < 52 ? card / 4 + 3 : card == 52 ? 16 : 17;

        public static List<int> Sort(IEnumerable<int> cards) => cards.OrderBy(c => RankOf(c)).ThenBy(c => c).ToList();

        private static Dictionary<int, int> CountRanks(IEnumerable<int> cards)
        {
            var m = new Dictionary<int, int>();
            foreach (var c in cards) m[RankOf(c)] = m.GetValueOrDefault(RankOf(c)) + 1;
            return m;
        }

        private static bool Consecutive(List<int> ranks)
        {
            for (var i = 1; i < ranks.Count; i++) if (ranks[i] != ranks[i - 1] + 1) return false;
            return ranks[^1] <= 14;
        }

        private static int? HighestRun(Dictionary<int, int> cnt, int k, int min, bool exact = false)
        {
            for (var top = 14; top - k + 1 >= 3; top--)
            {
                var ok = true;
                for (var r = top - k + 1; r <= top; r++)
                {
                    var n = cnt.GetValueOrDefault(r);
                    if (exact ? n != min : n < min) { ok = false; break; }
                }
                if (ok) return top;
            }
            return null;
        }

        /// <summary>识别牌型；不合法返回 null</summary>
        public static DdzPattern? Analyze(IReadOnlyCollection<int> cards)
        {
            var n = cards.Count;
            if (n == 0) return null;
            var cnt = CountRanks(cards);
            var ranks = cnt.Keys.OrderBy(r => r).ToList();
            bool All(int k) => ranks.All(r => cnt[r] == k);
            List<int> RankWith(int k) => ranks.Where(r => cnt[r] == k).ToList();

            if (n == 1) return new DdzPattern("single", ranks[0], 1);
            if (n == 2)
            {
                if (ranks.Count == 2 && ranks[0] == 16 && ranks[1] == 17) return new DdzPattern("rocket", 17, 1);
                return All(2) ? new DdzPattern("pair", ranks[0], 1) : null;
            }
            if (n == 3) return All(3) ? new DdzPattern("triple", ranks[0], 1) : null;
            if (n == 4)
            {
                if (All(4)) return new DdzPattern("bomb", ranks[0], 1);
                var t = RankWith(3);
                return t.Count == 1 ? new DdzPattern("triple1", t[0], 1) : null;
            }
            if (n == 5 && ranks.Count == 2 && RankWith(3).Count == 1 && RankWith(2).Count == 1)
                return new DdzPattern("triple2", RankWith(3)[0], 1);

            if (n >= 5 && All(1) && Consecutive(ranks)) return new DdzPattern("straight", ranks[^1], n);
            if (n >= 6 && n % 2 == 0 && All(2) && Consecutive(ranks)) return new DdzPattern("pairs", ranks[^1], n / 2);
            if (n >= 6 && n % 3 == 0 && All(3) && Consecutive(ranks)) return new DdzPattern("plane", ranks[^1], n / 3);

            var fours = RankWith(4);
            if (n == 6 && fours.Count == 1) return new DdzPattern("four2", fours[0], 1);
            if (n == 8 && fours.Count >= 1)
            {
                var main = fours[^1];
                var pairsLeft = ranks.Where(r => r != main).Sum(r => cnt[r] % 2 == 0 ? cnt[r] / 2 : -99);
                if (pairsLeft == 2) return new DdzPattern("four22", main, 1);
            }
            if (n >= 8 && n % 4 == 0)
            {
                var k = n / 4;
                if (HighestRun(cnt, k, 3) is int top) return new DdzPattern("plane1", top, k);
            }
            if (n >= 10 && n % 5 == 0)
            {
                var k = n / 5;
                if (HighestRun(cnt, k, 3, exact: true) is int top)
                {
                    var rest = ranks.Where(r => r < top - k + 1 || r > top).ToList();
                    if (rest.All(r => cnt[r] % 2 == 0) && rest.Sum(r => cnt[r] / 2) == k) return new DdzPattern("plane2", top, k);
                }
            }
            return null;
        }

        /// <summary>a 能不能压过 b</summary>
        public static bool Beats(DdzPattern a, DdzPattern b)
        {
            if (a.Type == "rocket") return true;
            if (b.Type == "rocket") return false;
            if (a.Type == "bomb" && b.Type != "bomb") return true;
            if (b.Type == "bomb" && a.Type != "bomb") return false;
            return a.Type == b.Type && a.Len == b.Len && a.Rank > b.Rank;
        }

        // ---------------- 找牌（电脑 / 托管） ----------------

        private static SortedDictionary<int, List<int>> Group(IEnumerable<int> hand)
        {
            var g = new SortedDictionary<int, List<int>>();
            foreach (var c in Sort(hand))
            {
                var r = RankOf(c);
                if (!g.TryGetValue(r, out var list)) g[r] = list = new List<int>();
                list.Add(c);
            }
            return g;
        }

        private static List<int>? PickWings(SortedDictionary<int, List<int>> g, HashSet<int> exclude, int count, int size)
        {
            var hasRocket = g.ContainsKey(16) && g.ContainsKey(17);
            var options = g.Where(kv => !exclude.Contains(kv.Key) && kv.Value.Count >= size && !(size == 1 && kv.Key >= 16 && hasRocket))
                .OrderBy(kv => kv.Value.Count == size ? 0 : 1).ThenBy(kv => kv.Key);
            var output = new List<int>();
            foreach (var kv in options)
            {
                if (output.Count / size >= count) break;
                output.AddRange(kv.Value.Take(size));
            }
            return output.Count / size >= count ? output.Take(count * size).ToList() : null;
        }

        /// <summary>所有能压过 target 的出法，代价小的在前；炸弹、王炸排最后</summary>
        public static List<List<int>> FindBeats(IEnumerable<int> hand, DdzPattern t)
        {
            var g = Group(hand);
            bool Has(int r, int k) => g.TryGetValue(r, out var l) && l.Count >= k;
            List<int> Take(int r, int k) => g[r].Take(k).ToList();
            var output = new List<List<int>>();

            if (t.Type is "single" or "pair" or "triple")
            {
                var k = t.Type == "single" ? 1 : t.Type == "pair" ? 2 : 3;
                var ranks = g.Keys.Where(r => r > t.Rank && Has(r, k)).OrderBy(r => g[r].Count == k ? 0 : 1).ThenBy(r => r).ToList();
                foreach (var r in ranks)
                {
                    if (k == 1 && r >= 16 && Has(16, 1) && Has(17, 1) && g[r].Count == 1 && ranks.Any(x => x < 16)) continue;
                    output.Add(Take(r, k));
                }
            }
            else if (t.Type is "triple1" or "triple2")
            {
                foreach (var r in g.Keys)
                {
                    if (r <= t.Rank || !Has(r, 3)) continue;
                    var wings = PickWings(g, new HashSet<int> { r }, 1, t.Type == "triple1" ? 1 : 2);
                    if (wings != null) output.Add(Take(r, 3).Concat(wings).ToList());
                }
            }
            else if (t.Type is "straight" or "pairs" or "plane" or "plane1" or "plane2")
            {
                var k = t.Type == "straight" ? 1 : t.Type == "pairs" ? 2 : 3;
                var len = t.Len;
                for (var top = t.Rank + 1; top <= 14; top++)
                {
                    var start = top - len + 1;
                    if (start < 3) continue;
                    var ok = true;
                    for (var r = start; r <= top; r++) if (!Has(r, k)) { ok = false; break; }
                    if (!ok) continue;
                    var body = new List<int>();
                    var used = new HashSet<int>();
                    for (var r = start; r <= top; r++) { body.AddRange(Take(r, k)); used.Add(r); }
                    if (t.Type is "plane1" or "plane2")
                    {
                        var wings = PickWings(g, used, len, t.Type == "plane1" ? 1 : 2);
                        if (wings == null) continue;
                        body.AddRange(wings);
                    }
                    output.Add(body);
                }
            }
            else if (t.Type is "four2" or "four22")
            {
                foreach (var r in g.Keys)
                {
                    if (r <= t.Rank || !Has(r, 4)) continue;
                    var wings = PickWings(g, new HashSet<int> { r }, 2, t.Type == "four2" ? 1 : 2);
                    if (wings != null) output.Add(Take(r, 4).Concat(wings).ToList());
                }
            }

            foreach (var r in g.Keys)
            {
                if (!Has(r, 4) || t.Type == "rocket") continue;
                if (t.Type == "bomb" && r <= t.Rank) continue;
                output.Add(Take(r, 4));
            }
            if (t.Type != "rocket" && Has(16, 1) && Has(17, 1)) output.Add(Take(16, 1).Concat(Take(17, 1)).ToList());
            return output;
        }

        /// <summary>自己先出时挑一手牌：先出小牌，尽量成组</summary>
        public static List<int> ChooseLead(IReadOnlyCollection<int> hand)
        {
            var whole = Analyze(hand);
            if (whole != null && whole.Type is not ("four2" or "four22")) return Sort(hand);
            var g = Group(hand);
            var hasRocket = g.ContainsKey(16) && g.ContainsKey(17);
            int? low = null;
            foreach (var r in g.Keys) if (g[r].Count < 4 && !(r >= 16 && hasRocket)) { low = r; break; }
            if (low is not int lo)
            {
                foreach (var r in g.Keys) if (g[r].Count == 4) return g[r].ToList();
                return g[16].Concat(g[17]).ToList();
            }
            var c = g[lo].Count;
            bool Has(int r, int k) => g.TryGetValue(r, out var l) && l.Count >= k && l.Count < 4;
            int Count(int r) => g.TryGetValue(r, out var l) ? l.Count : 0;

            if (lo <= 10)
            {
                var end = lo;
                while (end + 1 <= 14 && Has(end + 1, 1)) end++;
                if (end - lo + 1 >= 5)
                {
                    var cards = new List<int>();
                    for (var r = lo; r <= end; r++) cards.Add(g[r][0]);
                    return cards;
                }
            }
            if (c == 2)
            {
                var end = lo;
                while (end + 1 <= 14 && Count(end + 1) == 2) end++;
                if (end - lo + 1 >= 3)
                {
                    var cards = new List<int>();
                    for (var r = lo; r <= end; r++) cards.AddRange(g[r]);
                    return cards;
                }
                return g[lo].ToList();
            }
            if (c == 3)
            {
                var end = lo;
                while (end + 1 <= 14 && Count(end + 1) == 3) end++;
                var k = end - lo + 1;
                var used = new HashSet<int>();
                var body = new List<int>();
                for (var r = lo; r <= end; r++) { used.Add(r); body.AddRange(g[r]); }
                var singles = PickWings(g, used, k, 1);
                if (singles != null && singles.All(card => RankOf(card) < 15)) return body.Concat(singles).ToList();
                var pairs = PickWings(g, used, k, 2);
                if (pairs != null && pairs.All(card => RankOf(card) < 15)) return body.Concat(pairs).ToList();
                return body;
            }
            return new List<int> { g[lo][0] };
        }

        /// <summary>电脑 / 托管跟牌：找最小能压过的；队友出的不压；炸弹省着用</summary>
        public static List<int>? ChooseFollow(IReadOnlyCollection<int> hand, DdzPattern target, bool teammateLed, int opponentLeft)
        {
            if (teammateLed) return null;
            var options = FindBeats(hand, target);
            if (options.Count == 0) return null;
            var normal = options.FirstOrDefault(o => Analyze(o) is { } p && p.Type != "bomb" && p.Type != "rocket");
            if (normal != null)
            {
                var top = normal.Max(c => RankOf(c));
                if (top >= 15 && target.Rank < 11 && opponentLeft > 5 && hand.Count > 6) return null;
                return normal;
            }
            return opponentLeft <= 5 || hand.Count <= 6 ? options[0] : null;
        }

        /// <summary>电脑叫分：0 不叫，或 1–3（必须比当前高）</summary>
        public static int ChooseBid(IEnumerable<int> hand, int currentBid)
        {
            var g = Group(hand);
            var score = 0;
            if (g.ContainsKey(17)) score += 4;
            if (g.ContainsKey(16)) score += 3;
            score += (g.TryGetValue(15, out var twos) ? twos.Count : 0) * 2;
            score += g.TryGetValue(14, out var aces) ? aces.Count : 0;
            score += g.Values.Count(l => l.Count == 4) * 4;
            var want = score >= 11 ? 3 : score >= 8 ? 2 : score >= 6 ? 1 : 0;
            return want > currentBid ? want : 0;
        }

        public static string TypeLabel(string type) => type switch
        {
            "single" => "单张", "pair" => "对子", "triple" => "三张", "triple1" => "三带一", "triple2" => "三带一对",
            "straight" => "顺子", "pairs" => "连对", "plane" => "飞机", "plane1" => "飞机带单", "plane2" => "飞机带对",
            "four2" => "四带二", "four22" => "四带两对", "bomb" => "炸弹", "rocket" => "王炸", _ => type,
        };
    }
}
