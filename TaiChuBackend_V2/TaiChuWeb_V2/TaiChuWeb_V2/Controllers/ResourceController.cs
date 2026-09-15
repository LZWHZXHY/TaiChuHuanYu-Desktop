using Microsoft.AspNetCore.Mvc;
using Minio;
using Minio.ApiEndpoints;
using Minio.DataModel.Args;
using System.Reactive.Linq; // 必须引入这个来处理 MinIO 的异步流

namespace TaichuHuanyu.Controllers; // .NET 现代写法：文件级命名空间

[ApiController]
[Route("api/[controller]")]
public class ResourceController : ControllerBase
{
    private readonly IMinioClient _minioClient;
    private readonly string _bucketName;

    public ResourceController(IMinioClient minioClient, IConfiguration config)
    {
        _minioClient = minioClient;
        // 读取配置中的 BucketName，如果没配默认叫 "tcpr"
        _bucketName = config["MinioSettings:BucketName"] ?? "tcpr";
    }

    // 接口 1：让前端获取太初寰宇社区资源列表
    // GET: api/resource/list
    [HttpGet("list")]
    public async Task<IActionResult> GetFileList()
    {
        var files = new List<object>();

        try
        {
            var listArgs = new ListObjectsArgs()
                .WithBucket(_bucketName)
                .WithPrefix("")       // 如果你想只拉取某个子文件夹，可以填比如 "音效包/"
                .WithRecursive(true); // true 代表连子文件夹里的文件也一并翻出来

            // MinIO SDK 返回的是 IObservable 流，需要用 Rx 扩展来遍历
            var observable = _minioClient.ListObjectsAsync(listArgs);

            await observable.ForEachAsync(item =>
            {
                if (!item.IsDir) // 过滤掉虚拟的文件夹占位符，只保留真实文件
                {
                    files.Add(new
                    {
                        name = item.Key,           // 文件名及路径，例如 "音效包/BGM.mp3"
                        size = item.Size,          // 文件大小 (Bytes)
                        lastModified = item.LastModifiedDateTime // 文件的最后修改时间
                    });
                }
            });

            return Ok(new { success = true, data = files });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    // 接口 2：Vue 3 拿着文件名来请求下载通行证（预签名 URL）
    // GET: api/resource/download?fileName=音效包/BGM.mp3
    [HttpGet("download")]
    public async Task<IActionResult> GetDownloadUrl([FromQuery] string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return BadRequest(new { success = false, message = "文件名不能为空" });
        }

        try
        {
            var presignedArgs = new PresignedGetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(fileName)
                .WithExpiry(60 * 15); // 生成一个 15 分钟后自动过期的临时下载链接

            // 向 NAS 申请下载直链
            string url = await _minioClient.PresignedGetObjectAsync(presignedArgs);

            return Ok(new { success = true, downloadUrl = url });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
}