namespace TaiChuWeb_V2.Services.Games.Xiangqi
{
    /// <summary>
    /// 中国象棋规则（与前端 xqRules.ts 一致，服务器以这里的判定为准）。
    /// 棋盘是 90 个字符：下标 = 行 * 9 + 列，第 0 行是黑方底线，第 9 行是红方底线。
    /// 大写红方、小写黑方：K 帅 A 仕 B 相 N 马 R 车 C 炮 P 兵，'.' 为空。
    /// </summary>
    public static class XqRules
    {
        public const int Rows = 10;
        public const int Cols = 9;
        public const string Initial =
            "rnbakabnr" +
            "........." +
            ".c.....c." +
            "p.p.p.p.p" +
            "........." +
            "........." +
            "P.P.P.P.P" +
            ".C.....C." +
            "........." +
            "RNBAKABNR";

        /// <summary>'r' 红 / 'b' 黑 / null 空</summary>
        public static char? SideOf(char ch) => ch == '.' ? null : char.IsUpper(ch) ? 'r' : 'b';
        public static char Other(char side) => side == 'r' ? 'b' : 'r';

        private static bool Inside(int r, int c) => r >= 0 && r < Rows && c >= 0 && c < Cols;
        private static bool InPalace(char side, int r, int c) => c >= 3 && c <= 5 && (side == 'r' ? r >= 7 : r <= 2);
        private static bool OwnHalf(char side, int r) => side == 'r' ? r >= 5 : r <= 4;

        private static readonly (int, int)[] Orth = { (1, 0), (-1, 0), (0, 1), (0, -1) };
        private static readonly (int, int)[] Diag = { (1, 1), (1, -1), (-1, 1), (-1, -1) };
        private static readonly (int, int)[] Elephant = { (2, 2), (2, -2), (-2, 2), (-2, -2) };
        private static readonly (int, int)[] Horse = { (2, 1), (2, -1), (-2, 1), (-2, -1), (1, 2), (1, -2), (-1, 2), (-1, -2) };

        /// <summary>不考虑送将的走法</summary>
        public static List<int> PseudoMoves(string b, int from)
        {
            var list = new List<int>();
            var p = b[from];
            if (SideOf(p) is not char side) return list;
            int r = from / Cols, c = from % Cols;
            void Add(int rr, int cc)
            {
                if (!Inside(rr, cc)) return;
                var t = b[rr * Cols + cc];
                if (SideOf(t) != side) list.Add(rr * Cols + cc);
            }
            char At(int rr, int cc) => b[rr * Cols + cc];

            switch (char.ToUpperInvariant(p))
            {
                case 'K':
                    foreach (var (dr, dc) in Orth) if (InPalace(side, r + dr, c + dc)) Add(r + dr, c + dc);
                    break;
                case 'A':
                    foreach (var (dr, dc) in Diag) if (InPalace(side, r + dr, c + dc)) Add(r + dr, c + dc);
                    break;
                case 'B':
                    foreach (var (dr, dc) in Elephant)
                    {
                        int rr = r + dr, cc = c + dc;
                        if (Inside(rr, cc) && OwnHalf(side, rr) && At(r + dr / 2, c + dc / 2) == '.') Add(rr, cc);
                    }
                    break;
                case 'N':
                    foreach (var (dr, dc) in Horse)
                    {
                        int rr = r + dr, cc = c + dc;
                        if (!Inside(rr, cc)) continue;
                        int legR = Math.Abs(dr) == 2 ? r + dr / 2 : r;
                        int legC = Math.Abs(dc) == 2 ? c + dc / 2 : c;
                        if (At(legR, legC) == '.') Add(rr, cc);
                    }
                    break;
                case 'R':
                case 'C':
                    {
                        var cannon = char.ToUpperInvariant(p) == 'C';
                        foreach (var (dr, dc) in Orth)
                        {
                            int rr = r + dr, cc = c + dc;
                            var jumped = false;
                            while (Inside(rr, cc))
                            {
                                var t = At(rr, cc);
                                if (!jumped)
                                {
                                    if (t == '.') list.Add(rr * Cols + cc);
                                    else if (cannon) jumped = true;
                                    else { if (SideOf(t) != side) list.Add(rr * Cols + cc); break; }
                                }
                                else if (t != '.')
                                {
                                    if (SideOf(t) != side) list.Add(rr * Cols + cc);
                                    break;
                                }
                                rr += dr; cc += dc;
                            }
                        }
                        break;
                    }
                case 'P':
                    {
                        var fwd = side == 'r' ? -1 : 1;
                        Add(r + fwd, c);
                        if (!OwnHalf(side, r)) { Add(r, c - 1); Add(r, c + 1); }
                        break;
                    }
            }
            return list;
        }

        public static string Apply(string b, int from, int to)
        {
            var arr = b.ToCharArray();
            arr[to] = arr[from];
            arr[from] = '.';
            return new string(arr);
        }

        public static bool KingsFacing(string b)
        {
            int k = b.IndexOf('k'), K = b.IndexOf('K');
            if (k < 0 || K < 0 || k % Cols != K % Cols) return false;
            for (var i = k + Cols; i < K; i += Cols) if (b[i] != '.') return false;
            return true;
        }

        public static bool InCheck(string b, char side)
        {
            var king = b.IndexOf(side == 'r' ? 'K' : 'k');
            if (king < 0) return true;
            if (KingsFacing(b)) return true;
            var enemy = Other(side);
            for (var i = 0; i < b.Length; i++)
                if (SideOf(b[i]) == enemy && PseudoMoves(b, i).Contains(king)) return true;
            return false;
        }

        public static List<int> LegalMoves(string b, int from)
        {
            if (SideOf(b[from]) is not char side) return new List<int>();
            return PseudoMoves(b, from).Where(to => !InCheck(Apply(b, from, to), side)).ToList();
        }

        public static bool IsLegal(string b, char side, int from, int to) =>
            from is >= 0 and < 90 && to is >= 0 and < 90 && SideOf(b[from]) == side && LegalMoves(b, from).Contains(to);

        public static bool HasAnyLegal(string b, char side)
        {
            for (var i = 0; i < b.Length; i++)
                if (SideOf(b[i]) == side && LegalMoves(b, i).Count > 0) return true;
            return false;
        }
    }
}
