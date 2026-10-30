namespace TaiChuWeb_V2.Services.Games.Doudizhu
{
    /// <summary>场次：底分、最低土豆、台费（每局每位真人玩家都扣，扣了就没有，是土豆的主要消耗）</summary>
    public sealed record DdzTier(string Key, string Name, int Base, long MinBalance, int Fee);

    public sealed class DdzSeat
    {
        public int Index { get; init; }
        public Guid? UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsBot { get; set; }
        public bool Ready { get; set; }
        public bool Online { get; set; } = true;
        /// <summary>掉线时间：等人的时候掉线先保留座位，超过宽限时间才让出来</summary>
        public DateTime? OfflineSince { get; set; }
        /// <summary>托管：由电脑代打（连续超时两次或掉线时自动开启）</summary>
        public bool Trustee { get; set; }
        public int Timeouts { get; set; }
        public List<int> Hand { get; set; } = new();
        public int Plays { get; set; }
        public bool Empty => UserId == null && !IsBot;
        public bool Human => UserId != null;
    }

    public sealed class DdzAction
    {
        public int Seat { get; init; }
        /// <summary>空 = 不出 / 不叫</summary>
        public List<int> Cards { get; init; } = new();
        public string? Type { get; init; }
        public int? Bid { get; init; }
    }

    public sealed class DdzResult
    {
        public bool LandlordWin { get; init; }
        public bool Spring { get; init; }
        public int Multiplier { get; init; }
        public Dictionary<int, long> Changes { get; init; } = new();
        public Dictionary<int, List<int>> Hands { get; init; } = new();
        public string Summary { get; init; } = string.Empty;
    }

    /// <summary>
    /// 一张斗地主牌桌的状态与规则流程（不涉及网络和数据库；调用方负责加锁）。
    /// 阶段：waiting 等人 → bidding 叫分 → playing 出牌 → ended 结算展示 → 回到 waiting。
    /// </summary>
    public sealed class DdzRoom
    {
        public const int BidSeconds = 15;
        public const int PlaySeconds = 25;
        public const int ResultSeconds = 8;

        public string Id { get; init; } = string.Empty;
        public DdzTier Tier { get; init; } = null!;
        public Guid OwnerId { get; set; }
        public DdzSeat[] Seats { get; } = new DdzSeat[] { new() { Index = 0 }, new() { Index = 1 }, new() { Index = 2 } };
        public string Phase { get; private set; } = "waiting";
        public int Round { get; private set; }
        public List<int> Bottom { get; private set; } = new();
        public int Landlord { get; private set; } = -1;
        public int CurrentBid { get; private set; }
        private int _bidder = -1;
        private int _bidStart;
        private int _bidsTaken;
        private int _redeals;
        public int Turn { get; private set; } = -1;
        public DateTime? Deadline { get; private set; }
        /// <summary>当前要压的牌（null 表示轮到的人自由出牌）</summary>
        public DdzAction? LastPlay { get; private set; }
        /// <summary>每个座位本轮最近一次动作（用于桌面显示）</summary>
        public DdzAction?[] Recent { get; } = new DdzAction?[3];
        public int Bombs { get; private set; }
        public DdzResult? Result { get; private set; }
        public DateTime StartedAt { get; private set; }
        public List<DdzAction> History { get; } = new();
        public DateTime LastActivity { get; private set; } = DateTime.UtcNow;
        /// <summary>电脑或托管下一次可以行动的时间（避免瞬间出牌）</summary>
        public DateTime? BotActAt { get; set; }

        public bool HasBots => Seats.Any(s => s.IsBot);
        public int Multiplier => Math.Max(1, CurrentBid) * (1 << Bombs);
        public IEnumerable<DdzSeat> Humans => Seats.Where(s => s.Human);
        public bool Full => Seats.All(s => !s.Empty);

        private static readonly Random Rng = Random.Shared;

        private void Touch() => LastActivity = DateTime.UtcNow;

        public DdzSeat? SeatOf(Guid userId) => Seats.FirstOrDefault(s => s.UserId == userId);

        // ---------------- 入座 ----------------

        public DdzSeat? Sit(Guid userId, string name)
        {
            if (Phase != "waiting" && Phase != "ended") return null;
            var seat = Seats.FirstOrDefault(s => s.Empty);
            if (seat == null) return null;
            seat.UserId = userId; seat.Name = name; seat.IsBot = false; seat.Ready = false; seat.Online = true; seat.OfflineSince = null; seat.Trustee = false; seat.Timeouts = 0;
            Touch();
            return seat;
        }

        public bool AddBot()
        {
            if (Phase != "waiting") return false;
            var seat = Seats.FirstOrDefault(s => s.Empty);
            if (seat == null) return false;
            var used = Seats.Where(s => s.IsBot).Select(s => s.Name).ToHashSet();
            seat.IsBot = true; seat.UserId = null; seat.Ready = true; seat.Online = true;
            seat.Name = new[] { "电脑·阿尔法", "电脑·贝塔", "电脑·伽马" }.First(n => !used.Contains(n));
            Touch();
            return true;
        }

        public void Vacate(DdzSeat seat)
        {
            seat.UserId = null; seat.IsBot = false; seat.Name = string.Empty; seat.Ready = false; seat.Online = true; seat.OfflineSince = null; seat.Trustee = false; seat.Timeouts = 0; seat.Hand.Clear();
            Touch();
        }

        public bool CanStart => Phase == "waiting" && Full && Seats.All(s => s.Ready) && Humans.Any();

        // ---------------- 发牌、叫分 ----------------

        public void Start()
        {
            Round++;
            StartedAt = DateTime.UtcNow;
            Result = null;
            History.Clear();
            Deal();
            _redeals = 0;
            _bidStart = Rng.Next(3);
            BeginBidding();
        }

        private void Deal()
        {
            var deck = Enumerable.Range(0, 54).OrderBy(_ => Rng.Next()).ToList();
            for (var i = 0; i < 3; i++)
            {
                Seats[i].Hand = DdzRules.Sort(deck.Skip(i * 17).Take(17));
                Seats[i].Plays = 0;
                Seats[i].Timeouts = 0;
                Seats[i].Trustee = Seats[i].Human && !Seats[i].Online;
            }
            Bottom = DdzRules.Sort(deck.Skip(51));
            Landlord = -1; CurrentBid = 0; Bombs = 0; LastPlay = null;
            Array.Clear(Recent);
        }

        private void BeginBidding()
        {
            Phase = "bidding";
            _bidder = -1; _bidsTaken = 0; CurrentBid = 0;
            Turn = _bidStart;
            SetDeadline(BidSeconds);
        }

        private void SetDeadline(int seconds)
        {
            Deadline = DateTime.UtcNow.AddSeconds(seconds);
            var seat = Seats[Turn];
            BotActAt = seat.IsBot || seat.Trustee ? DateTime.UtcNow.AddMilliseconds(900 + Rng.Next(900)) : null;
            Touch();
        }

        /// <summary>叫分：0 不叫，1–3 必须比当前高。返回错误信息或 null</summary>
        public string? Bid(int seat, int score)
        {
            if (Phase != "bidding") return "现在不是叫分阶段";
            if (seat != Turn) return "还没轮到你";
            if (score != 0 && (score <= CurrentBid || score > 3)) return $"要叫比 {CurrentBid} 分更高的分（最多 3 分）";
            Array.Clear(Recent);
            Recent[seat] = new DdzAction { Seat = seat, Bid = score };
            History.Add(new DdzAction { Seat = seat, Bid = score });
            if (score > 0) { CurrentBid = score; _bidder = seat; }
            _bidsTaken++;
            if (score == 3 || _bidsTaken >= 3)
            {
                if (_bidder < 0)
                {
                    // 都不叫：重新发牌；连续 3 次都没人叫，就让起叫的人当地主（1 分）
                    if (++_redeals >= 3) { CurrentBid = 1; _bidder = _bidStart; }
                    else
                    {
                        Deal();
                        _bidStart = (_bidStart + 1) % 3;
                        BeginBidding();
                        return null;
                    }
                }
                BecomeLandlord(_bidder);
                return null;
            }
            Turn = (Turn + 1) % 3;
            SetDeadline(BidSeconds);
            return null;
        }

        private void BecomeLandlord(int seat)
        {
            Landlord = seat;
            Seats[seat].Hand = DdzRules.Sort(Seats[seat].Hand.Concat(Bottom));
            Phase = "playing";
            Turn = seat;
            LastPlay = null;
            Array.Clear(Recent);
            SetDeadline(PlaySeconds + 5);
        }

        // ---------------- 出牌 ----------------

        public string? Play(int seat, IReadOnlyCollection<int> cards)
        {
            if (Phase != "playing") return "现在不能出牌";
            if (seat != Turn) return "还没轮到你";
            var s = Seats[seat];
            if (cards.Count == 0) return "请选择要出的牌";
            if (cards.Distinct().Count() != cards.Count || !cards.All(s.Hand.Contains)) return "出的牌不在你手里";
            var pattern = DdzRules.Analyze(cards);
            if (pattern == null) return "这几张牌不能这样出";
            var mustBeat = LastPlay != null && LastPlay.Seat != seat;
            if (mustBeat && !DdzRules.Beats(pattern, DdzRules.Analyze(LastPlay!.Cards)!)) return "要不起：出的牌没有大过上家";

            foreach (var c in cards) s.Hand.Remove(c);
            s.Plays++;
            if (pattern.Type is "bomb" or "rocket") Bombs++;
            var action = new DdzAction { Seat = seat, Cards = DdzRules.Sort(cards), Type = pattern.Type };
            if (LastPlay == null || LastPlay.Seat == seat) Array.Clear(Recent);
            LastPlay = action;
            Recent[seat] = action;
            History.Add(action);
            if (s.Hand.Count == 0) { Finish(seat == Landlord); return null; }
            NextTurn();
            return null;
        }

        public string? Pass(int seat)
        {
            if (Phase != "playing") return "现在不能操作";
            if (seat != Turn) return "还没轮到你";
            if (LastPlay == null || LastPlay.Seat == seat) return "你是这一轮第一个出牌的，必须出牌";
            var action = new DdzAction { Seat = seat };
            Recent[seat] = action;
            History.Add(action);
            NextTurn();
            return null;
        }

        private void NextTurn()
        {
            Turn = (Turn + 1) % 3;
            // 一圈没人要：轮回到出牌人，清空桌面，自由出牌
            if (LastPlay != null && LastPlay.Seat == Turn) { LastPlay = null; Array.Clear(Recent); }
            SetDeadline(PlaySeconds);
        }

        // ---------------- 电脑 / 托管 / 超时 ----------------

        /// <summary>电脑或托管替当前座位行动</summary>
        public void AutoAct()
        {
            var seat = Seats[Turn];
            if (Phase == "bidding")
            {
                Bid(Turn, seat.IsBot || seat.Trustee ? DdzRules.ChooseBid(seat.Hand, CurrentBid) : 0);
                return;
            }
            if (Phase != "playing") return;
            if (LastPlay == null || LastPlay.Seat == Turn)
            {
                Play(Turn, DdzRules.ChooseLead(seat.Hand));
                return;
            }
            var teammateLed = Turn != Landlord && LastPlay.Seat != Landlord;
            var opponentLeft = Turn == Landlord
                ? Seats.Where(x => x.Index != Landlord).Min(x => x.Hand.Count)
                : Seats[Landlord].Hand.Count;
            var follow = DdzRules.ChooseFollow(seat.Hand, DdzRules.Analyze(LastPlay.Cards)!, teammateLed, opponentLeft);
            if (follow != null) Play(Turn, follow); else Pass(Turn);
        }

        /// <summary>真人超时：叫分阶段不叫；出牌阶段能不出就不出、必须出就出最小的；连续两次超时开启托管</summary>
        public void Timeout()
        {
            var seat = Seats[Turn];
            if (seat.Human && ++seat.Timeouts >= 2) seat.Trustee = true;
            if (Phase == "bidding") { Bid(Turn, 0); return; }
            if (Phase != "playing") return;
            if (LastPlay == null || LastPlay.Seat == Turn) Play(Turn, DdzRules.ChooseLead(seat.Hand));
            else Pass(Turn);
        }

        // ---------------- 结算 ----------------

        private void Finish(bool landlordWin)
        {
            var farmers = Seats.Where(s => s.Index != Landlord).ToList();
            var spring = landlordWin ? farmers.All(f => f.Plays == 0) : Seats[Landlord].Plays <= 1;
            var multiplier = Multiplier * (spring ? 2 : 1);
            var unit = (long)Tier.Base * multiplier;
            var changes = new Dictionary<int, long>();
            foreach (var s in Seats)
            {
                var isLandlord = s.Index == Landlord;
                var win = isLandlord == landlordWin;
                var amount = isLandlord ? unit * 2 : unit;
                changes[s.Index] = win ? amount : -amount;
            }
            var parts = new List<string> { landlordWin ? "地主胜" : "农民胜", $"{CurrentBid} 分", $"×{multiplier}" };
            if (Bombs > 0) parts.Add($"{Bombs} 个炸弹");
            if (spring) parts.Add(landlordWin ? "春天" : "反春");
            Result = new DdzResult
            {
                LandlordWin = landlordWin,
                Spring = spring,
                Multiplier = multiplier,
                Changes = changes,
                Hands = Seats.ToDictionary(s => s.Index, s => s.Hand.ToList()),
                Summary = string.Join(" · ", parts),
            };
            Phase = "ended";
            Turn = -1;
            Deadline = DateTime.UtcNow.AddSeconds(ResultSeconds);
            BotActAt = null;
            Touch();
        }

        /// <summary>结算展示结束，回到等人阶段（电脑自动准备，真人需要重新准备）</summary>
        public void BackToWaiting()
        {
            Phase = "waiting";
            Turn = -1; Deadline = null; LastPlay = null; Landlord = -1; CurrentBid = 0; Bombs = 0;
            Array.Clear(Recent);
            Bottom = new List<int>();
            foreach (var s in Seats)
            {
                s.Hand.Clear(); s.Ready = s.IsBot; s.Trustee = false; s.Timeouts = 0;
            }
            Touch();
        }

        /// <summary>用实际结算后的数额替换（余额不足、电脑局减半等）</summary>
        public void ApplyActualChanges(Dictionary<int, long> actual)
        {
            if (Result == null) return;
            Result = new DdzResult { LandlordWin = Result.LandlordWin, Spring = Result.Spring, Multiplier = Result.Multiplier, Changes = actual, Hands = Result.Hands, Summary = Result.Summary };
        }
    }
}
