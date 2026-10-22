using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Controllers;
using TaiChuWeb_V2.Controllers.knowledgeBase;
using TaiChuWeb_V2.Controllers.LingMai;
using TaiChuWeb_V2.Services.Artworks;
var root = "E:/TaiChuHuanYu_DeskTop/TaiChuHuanYu-Desktop/TaiChuBackend_V2/TaiChuWeb_V2/TaiChuWeb_V2";
var config = new ConfigurationBuilder().AddJsonFile(root+"/appsettings.json").AddJsonFile(root+"/appsettings.Development.json",true).Build();
using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseMySql(config.GetConnectionString("DefaultConnection"), new MySqlServerVersion(new Version(8,0,31))).Options);

await db.Database.OpenConnectionAsync();
using (var cmd = db.Database.GetDbConnection().CreateCommand()) {
 cmd.CommandText = "SELECT TABLE_NAME,COLUMN_NAME,COLLATION_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND LOWER(TABLE_NAME) IN ('comments','users') AND COLUMN_NAME IN ('Id','UserId','ParentId','ArtworkId','Content')";
 using var reader=await cmd.ExecuteReaderAsync();
 while(await reader.ReadAsync()) Console.WriteLine($"schema {reader[0]}.{reader[1]}: {reader[2]}");
}
var service = new ArtworkEngagementService(db);
var ids = await db.Artworks.Where(a=>a.IsApproved && a.Status=="published").OrderByDescending(a=>a.CommentsCount).Take(5).Select(a=>a.Id).ToListAsync();
var count=0;
foreach(var id in ids) {
 try { await service.GetCommentsAsync(id,null,0,20); count++; }
 catch(Exception ex) { Console.WriteLine("comments FAIL: "+ex.Message); Environment.ExitCode=1; }
}
Console.WriteLine($"comments real DB: {count}/{ids.Count} passed");
var anonymous=new UserController(db) { ControllerContext=new ControllerContext { HttpContext=new DefaultHttpContext() }};
Console.WriteLine("private content anonymous: "+(await anonymous.GetContent() is UnauthorizedResult));
// Read-only queries under a selected existing owner, without issuing credentials or exposing records.
var owner=await db.Artworks.Select(a=>a.UploaderId).FirstAsync();
var identity=new System.Security.Claims.ClaimsIdentity(new[]{new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier,owner.ToString())},"readonly-probe");
var mine=new UserController(db) { ControllerContext=new ControllerContext { HttpContext=new DefaultHttpContext { User=new System.Security.Claims.ClaimsPrincipal(identity) } }};
foreach(var type in new[]{"artwork","blog","post","character","world"}) {
 try { var result=await mine.GetContent(type,1); Console.WriteLine("private content "+type+": "+(result as ObjectResult)?.StatusCode); }
 catch(Exception ex) { Console.WriteLine("private content "+type+" FAIL: "+ex.Message); Environment.ExitCode=1; }
}
Console.WriteLine("unknown type: "+(await mine.GetContent("invalid") is BadRequestObjectResult));
