import fs from 'node:fs';
import path from 'node:path';
const base='../../TaiChuBackend_V2';
const project='TaiChuWeb_V2/TaiChuWeb_V2/';
function stage(p,fn) {
 const s=fs.readFileSync(path.join(base,p),'utf8').replace(/\r\n/g,'\n');
 const dest='.tmp/club-joint-battle/backend/'+p;
 fs.mkdirSync(path.dirname(dest),{recursive:true});
 fs.writeFileSync(dest+'.before',s); fs.writeFileSync(dest,fn(s));
}
stage(project+'Dtos/Chai/Joint/JointDtos.cs',s=>{
 const pos=s.indexOf('public class UpdateJointRequest');
 return s.slice(0,pos)+s.slice(pos).replace('public DateTime? EndDate { get; set; }',`private DateTime? _endDate;
        public DateTime? EndDate
        {
            get => _endDate;
            set { _endDate = value; EndDateSpecified = true; }
        }
        // 区分未提交字段与显式清空，保持局部更新的兼容性。
        [System.Text.Json.Serialization.JsonIgnore]
        public bool EndDateSpecified { get; private set; }`);
});
stage(project+'Controllers/ChaiCommunity/JointController.cs',s=>{
 const start=s.indexOf('            // ===== 新增：更新日期 =====',s.indexOf('public async Task<ActionResult<JointActivityDto>> Update'));
 const end=s.indexOf('            activity.UpdatedAt',start);
 s=s.slice(0,start)+`            activity.StartDate = startDate;
            activity.EndDate = endDate;

`+s.slice(end);
 const before=s.indexOf('            if (!string.IsNullOrEmpty(request.Title))',s.indexOf('public async Task<ActionResult<JointActivityDto>> Update'));
 return s.slice(0,before)+`            var startDate = request.StartDate ?? activity.StartDate;
            var endDate = request.EndDateSpecified ? request.EndDate : activity.EndDate;
            if (endDate.HasValue && endDate.Value <= startDate)
                return BadRequest(new { message = "结束时间必须晚于开始时间" });

`+s.slice(before);
});
stage(project+'Controllers/ChaiCommunity/BattleController.cs',s=>{
 s=s.replace('            try\n            {\n                var opponentDict', '            if (battle.Participants.Any(p => p.UserId == userId))\n                return BadRequest(new { message = "已报名，请使用取消报名" });\n\n            try\n            {\n                var opponentDict');
 s=s.replace('JsonSerializer.Deserialize<Dictionary<string, List<Guid>>>(battle.OpponentOcIds)', 'JsonSerializer.Deserialize<Dictionary<Guid, List<Guid>>>(battle.OpponentOcIds)').replace('!opponentDict.ContainsKey(userId.ToString())','!opponentDict.ContainsKey(userId)');
 s=s.replace('return BadRequest(new { message = "你并非此约战的指定对手" });','return BadRequest(new { message = "你并非此约战的指定对手" });\n                opponentDict.Remove(userId);\n                battle.IsPublic = opponentDict.Count == 0;\n                battle.OpponentOcIds = battle.IsPublic ? null : JsonSerializer.Serialize(opponentDict);');
 s=s.replace('            // ⭐ 清除指定对手信息 → 变为公开约战\n            battle.OpponentOcIds = null;\n            battle.IsPublic = true;\n\n','');
 s=s.replace('            List<Guid>? opponentOcIds = null;\n            Guid? opponentUserId = null;\n            if (request.OpponentOcIds != null && request.OpponentOcIds.Any())','            foreach (var invitation in request.OpponentOcIds ?? new())');
 s=s.replace('                var first = request.OpponentOcIds.First();\n                opponentUserId = first.Key;\n                opponentOcIds = first.Value;',`                var opponentUserId = invitation.Key;
                var opponentOcIds = invitation.Value?.Distinct().ToList();
                if (opponentUserId == userId)
                    return BadRequest(new { message = "不能邀请自己，请在发起者栏选择自己的OC" });`);
 const start=s.indexOf('            // ===== 3. 创建约战 =====');
 s=s.slice(0,start)+`            Dictionary<Guid, int> teams;
            try
            {
                using var config = JsonDocument.Parse(request.BattleConfigJson ?? "{}");
                teams = config.RootElement.TryGetProperty("teams", out var value)
                    ? value.Deserialize<Dictionary<Guid, int>>() ?? new() : new();
                if (teams.Any(t => t.Value is not (1 or 2) ||
                    (t.Key != userId && !(request.OpponentOcIds?.ContainsKey(t.Key) ?? false))))
                    return BadRequest(new { message = "队伍配置无效" });
                teams[userId] = 1;
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                return BadRequest(new { message = "队伍配置无效" });
            }

`+s.slice(start);
 s=s.replace('BattleConfigJson = "{}",','BattleConfigJson = JsonSerializer.Serialize(new { teams }),');
 s=s.replace('                OcIdsJson = SafeSerialize(ocIds),','                TeamNumber = 1,\n                OcIdsJson = SafeSerialize(ocIds),');
 s=s.replace('if (!battle.IsPublic && !string.IsNullOrEmpty(battle.OpponentOcIds))','if (!battle.IsPublic)').replace('JsonSerializer.Deserialize<Dictionary<string, List<Guid>>>(battle.OpponentOcIds);','JsonSerializer.Deserialize<Dictionary<Guid, List<Guid>>>(battle.OpponentOcIds ?? "null");').replace('JsonSerializer.Deserialize<Dictionary<Guid, List<Guid>>>(battle.OpponentOcIds);\n                    if (opponentOcIdsDict != null && !opponentOcIdsDict.ContainsKey(userId.ToString()))','JsonSerializer.Deserialize<Dictionary<Guid, List<Guid>>>(battle.OpponentOcIds ?? "null");\n                    if (opponentOcIdsDict == null || !opponentOcIdsDict.ContainsKey(userId))');
 const reg=s.indexOf('public async Task<ActionResult<BattleDto>> Register');
 const part=s.indexOf('            var participant = new BattleParticipant',reg);
 s=s.slice(0,part)+`            var activeBattles = await _context.Battles
                .Where(b => b.Id != id && b.Status != "finished" && b.Status != "cancelled")
                .Include(b => b.Participants).ToListAsync();
            if (ocIds.Any(oc => activeBattles.Any(b => b.Participants.Any(p =>
                p.OcIdsJson.Contains($"\\"{oc}\\"", StringComparison.OrdinalIgnoreCase)))))
                return BadRequest(new { message = "所选OC已在其他进行中的约战中" });

            var teamNumber = 2;
            try
            {
                using var config = JsonDocument.Parse(battle.BattleConfigJson ?? "{}");
                if (config.RootElement.TryGetProperty("teams", out var value))
                {
                    var teams = value.Deserialize<Dictionary<Guid, int>>();
                    if (teams?.TryGetValue(userId, out var team) == true && team is 1 or 2) teamNumber = team;
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            { return BadRequest(new { message = "队伍配置异常，请联系发起者" }); }

`+s.slice(part);
 const part2=s.indexOf('            var participant = new BattleParticipant',reg);
 s=s.slice(0,part2)+s.slice(part2).replace('                UserId = userId,','                UserId = userId,\n                TeamNumber = teamNumber,');
 const unreg=s.indexOf('public async Task<ActionResult<BattleDto>> Unregister');
 const remove=s.indexOf('            battle.Participants.Remove(participant);',unreg);
 s=s.slice(0,remove)+`            if (battle.Participants.OrderBy(p => p.JoinedAt).FirstOrDefault()?.UserId == userId)
                return BadRequest(new { message = "发起者不能退出报名，请使用取消约战" });

`+s.slice(remove);
 return s;
});
stage('tests/LingMai.Regression/Program.cs',s=>s.replace('checks += await CommunityChecks.Run();','checks += await CommunityChecks.Run();\nchecks += await JointBattleChecks.Run();'));
