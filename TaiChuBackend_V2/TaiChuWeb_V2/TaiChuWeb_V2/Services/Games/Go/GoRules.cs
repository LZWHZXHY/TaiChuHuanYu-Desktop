namespace TaiChuWeb_V2.Services.Games.Go
{
    /// <summary>
    /// 围棋规则（服务器判定用；前端 Go/index.vue 本地对弈有一份同样的逻辑）。
    /// 棋盘是 size*size 个字符：'.' 空、'b' 黑、'w' 白，下标 = 行 * size + 列。
    /// 提子、禁着点（不能自杀）、打劫（不能马上提回刚被单提的那一子）；中国规则数子，白贴 7.5。
    /// </summary>
    public static class GoRules
    {
        public const double Komi = 7.5;
        public static readonly int[] Sizes = { 9, 13, 19 };

        public static char Other(char c) => c == 'b' ? 'w' : 'b';

        public static IEnumerable<int> Neighbors(int size, int i)
        {
            int r = i / size, c = i % size;
            if (r > 0) yield return i - size;
            if (r < size - 1) yield return i + size;
            if (c > 0) yield return i - 1;
            if (c < size - 1) yield return i + 1;
        }

        /// <summary>一整块棋和它的气数</summary>
        public static (List<int> Stones, int Liberties) Group(char[] b, int size, int start)
        {
            var color = b[start];
            var stones = new List<int>();
            var seen = new HashSet<int> { start };
            var libs = new HashSet<int>();
            var stack = new Stack<int>();
            stack.Push(start);
            while (stack.Count > 0)
            {
                var i = stack.Pop();
                stones.Add(i);
                foreach (var n in Neighbors(size, i))
                {
                    if (b[n] == '.') libs.Add(n);
                    else if (b[n] == color && seen.Add(n)) stack.Push(n);
                }
            }
            return (stones, libs.Count);
        }

        /// <summary>
        /// 落子。成功时返回新棋盘、提子数和新的劫点（没有劫为 -1）；失败返回错误文字。
        /// </summary>
        public static (string? Error, string Board, int Captured, int Ko) Play(string board, int size, int idx, char color, int ko)
        {
            if (idx < 0 || idx >= size * size) return ("这里不在棋盘上", board, 0, ko);
            if (board[idx] != '.') return ("这里已经有子了", board, 0, ko);
            if (idx == ko) return ("打劫：不能马上提回，先在别处下一手", board, 0, ko);
            var b = board.ToCharArray();
            b[idx] = color;
            var captured = new List<int>();
            var enemy = Other(color);
            foreach (var n in Neighbors(size, idx))
            {
                if (b[n] != enemy) continue;
                var (stones, libs) = Group(b, size, n);
                if (libs == 0) { foreach (var s in stones) b[s] = '.'; captured.AddRange(stones); }
            }
            var own = Group(b, size, idx);
            if (own.Liberties == 0) return ("禁着点：这里落子没有气", board, 0, ko);
            var newKo = captured.Count == 1 && own.Stones.Count == 1 && own.Liberties == 1 ? captured[0] : -1;
            return (null, new string(b), captured.Count, newKo);
        }

        /// <summary>
        /// 中国规则数子：活子 + 只被一方围住的空点（死子当作对方的地），白方加贴目。
        /// owner 为每个点的归属：'b' / 'w' / '.'（公共或死子以外的空点）。
        /// </summary>
        public static (int Black, double White, string Owner) Score(string board, int size, ISet<int> dead)
        {
            var b = board.ToCharArray();
            foreach (var d in dead) if (d >= 0 && d < b.Length) b[d] = '.';
            var owner = new char[b.Length];
            Array.Fill(owner, '.');
            int black = 0, white = 0;
            var seen = new bool[b.Length];
            for (var i = 0; i < b.Length; i++)
            {
                if (b[i] == 'b') { black++; owner[i] = 'b'; continue; }
                if (b[i] == 'w') { white++; owner[i] = 'w'; continue; }
                if (seen[i]) continue;
                var region = new List<int>();
                bool touchB = false, touchW = false;
                var stack = new Stack<int>();
                stack.Push(i);
                seen[i] = true;
                while (stack.Count > 0)
                {
                    var p = stack.Pop();
                    region.Add(p);
                    foreach (var n in Neighbors(size, p))
                    {
                        if (b[n] == '.') { if (!seen[n]) { seen[n] = true; stack.Push(n); } }
                        else if (b[n] == 'b') touchB = true;
                        else touchW = true;
                    }
                }
                var who = touchB && !touchW ? 'b' : touchW && !touchB ? 'w' : '.';
                foreach (var p in region) owner[p] = who;
                if (who == 'b') black += region.Count;
                else if (who == 'w') white += region.Count;
            }
            return (black, white + Komi, new string(owner));
        }
    }
}
