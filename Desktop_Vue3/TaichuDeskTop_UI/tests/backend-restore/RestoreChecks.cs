using System.Collections.Concurrent;
using System.Data.Common;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.World;
using TaiChuWeb_V2.Services.Cos;
using TaiChuWeb_V2.Services.World;

internal static class RestoreChecks
{
 public static async Task Run(IConfiguration config, Guid projectId)
 {
  var connection = config.GetConnectionString("DefaultConnection")!;
  using var cache = new MemoryCache(new MemoryCacheOptions());
  var cos = new TestCos(config); var interceptor = new FailRelations();
  AppDbContext Open(bool fault=false) { var options=new DbContextOptionsBuilder<AppDbContext>().UseMySql(connection,new MySqlServerVersion(new Version(8,0,31)),o=>o.EnableRetryOnFailure(3)); if(fault)options.AddInterceptors(interceptor); return new(options.Options); }
  WorldRestoreService Service(AppDbContext db) => new(db,cos,new WorldQuotaService(db,NullLogger<WorldQuotaService>.Instance),cache,NullLogger<WorldRestoreService>.Instance);
  await using var initial=Open(); var project=await initial.WorldProjects.AsNoTracking().SingleAsync(p=>p.Id==projectId); var owner=project.OwnerId;
  var original=await initial.WorldCards.Where(c=>c.ProjectId==projectId).Select(c=>c.Id).ToListAsync();
  int count=0; void Check(bool ok,string name){ if(!ok)throw new Exception(name); Console.WriteLine("PASS "+name);count++; }
  var png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a51cAAAAASUVORK5CYII=");
  var image=Convert.ToHexStringLower(SHA256.HashData(png))+".png";
  StartWorldRestoreDto Input(bool images=true) => new(){SourceProjectId=projectId,Fingerprint=Convert.ToHexStringLower(SHA256.HashData(Guid.NewGuid().ToByteArray())),FileName="恢复回归验收.zip",Name="恢复回归验收",CardCount=2,Images=images?[new(){Name=image,Size=png.Length}]:[]};
  async Task<WorldRestoreStatus> Start(StartWorldRestoreDto source){await using var db=Open();return await Service(db).StartAsync(owner,source);}
  async Task<WorldRestoreStatus> Upload(Guid id){await using var db=Open();using var stream=new MemoryStream(png);return await Service(db).UploadAsync(owner,id,image,new FormFile(stream,0,png.Length,"file",image));}
  CommitWorldRestoreDto Payload(WorldRestoreStatus job) => new(){Cards=[new(){Title="回归验收人物",Type="character",Description="人物正文",EmbeddedCards=[job.Plan.CardIds[1]],Relations=[new(){TargetCardId=job.Plan.CardIds[1],RelationType="位于"}]},new(){Title="回归验收地点",Type="location"}]};
  async Task<WorldRestoreStatus> Commit(WorldRestoreStatus job,bool fault=false){await using var db=Open(fault);return await Service(db).CommitAsync(owner,job.Id,Payload(job));}
  async Task<WorldRestoreStatus> Status(Guid id){await using var db=Open();return await Service(db).StatusAsync(owner,id);}
  async Task<int> Cards(WorldRestoreStatus job){await using var db=Open();var ids=job.Plan.CardIds.ToList();return await db.WorldCards.CountAsync(c=>ids.Contains(c.Id));}
  var committedIds=new List<Guid>();
  try
  {
   var input=Input();var jobs=await Task.WhenAll(Start(input),Start(input));var job=jobs[0];
   Check(jobs[1].Id==job.Id&&jobs[1].Plan.CardIds.SequenceEqual(job.Plan.CardIds),"两个标签页开始同一文件只有一个恢复记录和一组条目 ID");
   cos.LoseUploadReply=true;try{await Upload(job.Id);throw new Exception("Expected loss");}catch(IOException){}
   Check((await Status(job.Id)).Images.Count==0&&cos.Objects.Count==1,"上传成功但响应丢失：数据库进度未确认，计划仍可追踪原图");
   var resumed=await Upload(job.Id);Check(resumed.Images.Count==1&&cos.Objects.Count==1,"重开服务实例续传复用同一图片 key");
   await Upload(job.Id);Check(cos.UploadCalls==2,"已确认上传不再重复写 COS");
   await using(var other=Open())try{await Service(other).StatusAsync(Guid.NewGuid(),job.Id);throw new Exception("permission bypass");}catch(WorldOperationException e){Check(e.Kind==WorldErrorKind.NotFound,"其他账号无法读取恢复记录");}
   var results=await Task.WhenAll(Commit(resumed),Commit(resumed));committedIds.AddRange(job.Plan.CardIds);
   Check(results.All(r=>r.State=="committed")&&await Cards(job)==2,"并发提交只生成一份完整条目");
   await Commit(resumed);Check(await Cards(job)==2,"提交响应丢失后重试不会重复创建");
   await using(var db=Open()){var result=await Service(db).CancelAsync(owner,job.Id);Check(result.State=="committed"&&await Cards(job)==2,"迟到的取消不删除已提交条目和图片");}
   Check((await Start(input)).State=="committed","关闭窗口再选择相同备份会识别已完成记录");

   var failed=await Start(Input());await Upload(failed.Id);interceptor.Fail=true;
   try{await Commit(failed,true);throw new Exception("Expected failure");}catch(DbUpdateException){}
   Check(interceptor.Triggered&&await Cards(failed)==0,"插入卡片后关系写入失败：真实 MySQL 事务回滚全部卡片");
   Check((await Status(failed.Id)).State=="cancelled"&&!cos.Objects.Keys.Any(k=>k.Contains(failed.Id.ToString("N"))),"提交失败自动清理本次全部图片");

   var cancelled=await Start(Input());cos.LoseUploadReply=true;try{await Upload(cancelled.Id);}catch(IOException){}
   cos.FailDelete=true;
   await using(var db=Open()){Check((await Service(db).CancelAsync(owner,cancelled.Id)).State=="cleaning","COS 删除失败时保留待清理记录");}
   Check(await Cards(cancelled)==0,"图片清理失败也不会留下半恢复条目");
   cos.FailDelete=false;
   await using(var db=Open()){await Service(db).CleanExpiredAsync();}
   Check((await Status(cancelled.Id)).State=="cancelled"&&!cos.Objects.Keys.Any(k=>k.Contains(cancelled.Id.ToString("N"))),"服务重开后补偿清理包含未确认成功的上传");
   try{await Commit(cancelled);throw new Exception("cancel bypass");}catch(WorldOperationException){}
   Check(await Cards(cancelled)==0,"已取消的恢复不能被旧标签页迟到提交");

   var invalid=await Start(Input(false));var payload=Payload(invalid);payload.Cards[0].Relations![0].TargetCardId=Guid.NewGuid();
   await using(var db=Open())try{await Service(db).CommitAsync(owner,invalid.Id,payload);throw new Exception("foreign relation");}catch(WorldOperationException){}
   Check(await Cards(invalid)==0&&(await Status(invalid.Id)).State=="cancelled","越界引用拒绝且整批无写入");
   foreach(var self in new[]{true,false})
   {
    var bad=await Start(Input(false));var badPayload=Payload(bad);
    if(self)badPayload.Cards[0].Relations![0].TargetCardId=bad.Plan.CardIds[0];else badPayload.Cards[0].Relations!.Add(new(){TargetCardId=bad.Plan.CardIds[1],RelationType="重复"});
    await using(var db=Open())try{await Service(db).CommitAsync(owner,bad.Id,badPayload);throw new Exception("relation rule bypass");}catch(WorldOperationException){}
    Check(await Cards(bad)==0&&(await Status(bad.Id)).State=="cancelled",self?"恢复沿用禁止自关联校验":"恢复沿用禁止重复关联同一条目的校验");
   }
   var expiry=await Start(Input());await Upload(expiry.Id);
   await using(var db=Open()){await db.WorldRestores.Where(j=>j.Id==expiry.Id).ExecuteUpdateAsync(s=>s.SetProperty(j=>j.UpdatedAt,DateTime.UtcNow.AddDays(-8)));await Service(db).CleanExpiredAsync();}
   Check((await Status(expiry.Id)).State=="cancelled","闲置七天恢复自动清理");
   await using(var db=Open()){Check(await db.WorldCards.CountAsync(c=>original.Contains(c.Id))==original.Count,"原有条目保持完整");}
  }
  finally
  {
   await using var db=Open();
   await db.WorldRelations.Where(r=>committedIds.Contains(r.SourceCardId)||committedIds.Contains(r.TargetCardId)).ExecuteDeleteAsync();
   await db.WorldCards.Where(c=>committedIds.Contains(c.Id)&&c.ProjectId==projectId).ExecuteDeleteAsync();
   Check(await db.WorldCards.CountAsync(c=>c.ProjectId==projectId)==original.Count,"清理本轮验收新增条目，原始数量不变");
  }
  Console.WriteLine($"{count} restore integration checks passed (real MySQL transactions; injected COS failures).");
 }
}
internal sealed class TestCos(IConfiguration config):CosService(config)
{
 public ConcurrentDictionary<string,byte[]> Objects=new(); public int UploadCalls; public bool LoseUploadReply,FailDelete;
 public override Task<string> UploadAsync(byte[] bytes,string name,string folder){UploadCalls++;var key=folder+"/"+name;Objects[key]=bytes;if(LoseUploadReply){LoseUploadReply=false;throw new IOException("injected lost upload reply");}return Task.FromResult(ObjectUrl(key));}
 public override Task DeleteAsync(string key){if(FailDelete)throw new IOException("injected cleanup outage");Objects.TryRemove(key,out _);return Task.CompletedTask;}
}
internal sealed class FailRelations:DbCommandInterceptor
{
 public bool Fail,Triggered;
 public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData data,InterceptionResult<DbDataReader> result,CancellationToken ct=default)
 {
  if(Fail&&command.CommandText.Contains("INSERT INTO `WorldRelations`")){Fail=false;Triggered=true;throw new InvalidOperationException("injected relation insert failure after card writes");}
  return ValueTask.FromResult(result);
 }
}
