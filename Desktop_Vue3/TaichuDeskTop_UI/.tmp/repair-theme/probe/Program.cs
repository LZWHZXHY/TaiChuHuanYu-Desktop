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
var context = new ControllerContext { HttpContext = new DefaultHttpContext() };
var artwork = new ArtworkController(db, new ArtworkEngagementService(db)) { ControllerContext=context };
var stream = new LingMaiPublishController(db, []) { ControllerContext=context };
var catalog = new CatalogController(db) { ControllerContext=context };
var checks = new Dictionary<string, Func<Task<IActionResult>>> { ["gallery"] = () => artwork.GetGallery(0,20), ["stream"] = () => stream.GetPublicStream(null,1,20) };
foreach(var type in new[]{"all","blog","post","wiki","artwork","world","character","document"}) checks["catalog/"+type]=()=>catalog.Get(type,null,1,24,null);
foreach(var (name, check) in checks) {
 try { var result=await check(); Console.WriteLine(name+": "+(result as ObjectResult)?.StatusCode); }
 catch(Exception ex) { Console.WriteLine(name+": FAIL "+ex.Message); }
}
