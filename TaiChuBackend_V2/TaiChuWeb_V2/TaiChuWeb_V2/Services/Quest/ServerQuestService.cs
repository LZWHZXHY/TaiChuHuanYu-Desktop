using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Quest;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Services.Games;
using TaiChuWeb_V2.Services.Notify;

namespace TaiChuWeb_V2.Services.Quest;

/// <summary>
/// 世界任务的结算：进度够了就完成 → 给所有贡献者发经验和土豆、发通知 → 自动开启排在最前面的下一个任务。
/// </summary>
public class ServerQuestService(AppDbContext db, PotatoService potatoes, ILogger<ServerQuestService> logger)
{
    /// <summary>进行中的任务进度够了就结算。返回是否刚刚完成。</summary>
    public async Task<bool> CheckCompletionAsync(ServerQuest quest, Guid? actorId)
    {
        if (quest.Status != ServerQuest.Active || quest.Target <= 0 || quest.Progress < quest.Target) return false;
        await CompleteAsync(quest, actorId);
        return true;
    }

    /// <summary>完成任务（进度够了自动调用，管理员也可以手动结束）。返回因此开启的后续任务</summary>
    public async Task<List<ServerQuest>> CompleteAsync(ServerQuest quest, Guid? actorId)
    {
        quest.Status = ServerQuest.Completed;
        quest.CompletedAt = DateTime.UtcNow;

        // 所有贡献被通过的网站账号，每人发一份奖励
        var contributors = await db.ServerQuestContributions.AsNoTracking()
            .Where(c => c.QuestId == quest.Id && c.Status == ServerQuestContribution.Approved && c.UserId != null)
            .Select(c => c.UserId!.Value).Distinct().ToListAsync();
        await GrantExpAsync(contributors, quest);
        var notify = new NotificationService(db);
        foreach (var userId in contributors)
            notify.Add(userId, null, "quest", "quest", quest.Id.ToString(),
                $"世界任务《{Clip(quest.Title, 40)}》完成了！{RewardSummary(quest)}{UnlockLine(quest)}", "/quests");

        var started = await StartFollowUpsAsync(quest);
        await db.SaveChangesAsync();
        await GrantPotatoAsync(contributors, quest);
        return started;
    }

    /// <summary>
    /// 任务完成后开启后续任务：
    /// 1. 前置任务是它的（「完成后解锁」的主线 / 支线），全部开启；
    /// 2. 它是主线、而且现在没有进行中的主线：按排队顺序开启下一个没有前置任务的主线。
    /// 返回开启了哪些任务（调用方负责 SaveChanges）。
    /// </summary>
    public async Task<List<ServerQuest>> StartFollowUpsAsync(ServerQuest quest)
    {
        var now = DateTime.UtcNow;
        var drafts = await db.ServerQuests.Where(q => q.Status == ServerQuest.Draft).OrderBy(q => q.SortOrder).ThenBy(q => q.Id).ToListAsync();
        var started = drafts.Where(q => q.PrereqId == quest.Id).ToList();
        if (!quest.IsSide && !started.Any(q => !q.IsSide)
            && !await db.ServerQuests.AnyAsync(q => q.Status == ServerQuest.Active && q.Line == ServerQuest.LineMain && q.Id != quest.Id))
        {
            var next = drafts.FirstOrDefault(q => !q.IsSide && q.PrereqId == null);
            if (next != null) started.Add(next);
        }
        foreach (var q in started) { q.Status = ServerQuest.Active; q.StartedAt = now; }
        return started;
    }

    /// <summary>任务完成以后才通过的贡献：这个人之前没拿过这个任务的奖励就补发</summary>
    public async Task RewardLateAsync(ServerQuest quest, ServerQuestContribution contribution)
    {
        if (quest.Status != ServerQuest.Completed || contribution.UserId is not Guid userId) return;
        var already = await db.ServerQuestContributions.AsNoTracking().AnyAsync(c => c.QuestId == quest.Id && c.UserId == userId
            && c.Status == ServerQuestContribution.Approved && c.Id != contribution.Id);
        if (already) return;
        await GrantExpAsync(new List<Guid> { userId }, quest);
        new NotificationService(db).Add(userId, null, "quest", "quest", quest.Id.ToString(),
            $"你在已完成的世界任务《{Clip(quest.Title, 40)}》里的贡献通过了，{RewardSummary(quest)}", "/quests");
        await db.SaveChangesAsync();
        await GrantPotatoAsync(new List<Guid> { userId }, quest);
    }

    private async Task GrantExpAsync(List<Guid> users, ServerQuest quest)
    {
        if (quest.RewardExp <= 0 || users.Count == 0) return;
        var stats = await db.UserStats.Where(s => users.Contains(s.UserId)).ToListAsync();
        foreach (var s in stats)
        {
            s.Experience += quest.RewardExp;
            db.UserExpLogs.Add(new UserExpLog { UserId = s.UserId, Change = quest.RewardExp, Reason = $"世界任务《{Clip(quest.Title, 30)}》完成奖励" });
        }
    }

    private async Task GrantPotatoAsync(List<Guid> users, ServerQuest quest)
    {
        if (quest.RewardPotato <= 0 || users.Count == 0) return;
        try
        {
            await potatoes.ApplyAsync(users.Select(u => new PotatoService.Change(u, quest.RewardPotato,
                $"世界任务《{Clip(quest.Title, 30)}》完成奖励", "quest", quest.Id.ToString())).ToList());
        }
        catch (Exception ex) { logger.LogError(ex, "世界任务土豆奖励发放失败 quest={Quest}", quest.Id); }
    }

    private static string RewardSummary(ServerQuest q)
    {
        var parts = new List<string>();
        if (q.RewardExp > 0) parts.Add($"{q.RewardExp} 经验");
        if (q.RewardPotato > 0) parts.Add($"{q.RewardPotato} 土豆");
        return parts.Count > 0 ? $"感谢你的贡献，获得 {string.Join("、", parts)}。" : "感谢你的贡献！";
    }

    private static string UnlockLine(ServerQuest q)
    {
        var first = (q.RewardText ?? "").Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
        return first == null ? "" : $"解锁：{Clip(first, 60)}";
    }

    public static string Clip(string? text, int max) => string.IsNullOrEmpty(text) ? "" : text.Length > max ? text[..max] + "…" : text;
}
