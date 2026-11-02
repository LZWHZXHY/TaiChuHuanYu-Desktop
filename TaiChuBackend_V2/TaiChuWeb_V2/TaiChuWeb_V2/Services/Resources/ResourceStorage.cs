using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace TaiChuWeb_V2.Services.Resources;

/// <summary>
/// 社区资源的文件存储。可以放两个地方，上传的人自己选（每个资源记在 community_resources.Storage 列里，NULL = NAS）：
/// · nas：站长 NAS 上的 MinIO（和旧的「下载资源」同一个桶），配置 MinioSettings:Endpoint / AccessKey / SecretKey / BucketName；
/// · cos：腾讯云 COS（默认和网站图片同一个桶，放在 community-resources/ 下面，文件本身设成私有，只能用下载链接拿），
///   配置沿用 TencentCloud:SecretId / SecretKey / COS:Bucket / COS:Region；
///   可选 TencentCloud:COS:ResourceBucket（资源单独放一个桶）、TencentCloud:COS:ResourceAccelerate = true（走 COS 全球加速域名，
///   桶要先在腾讯云控制台开「全球加速」，海外上传会快很多，按流量另外收费）。
///
/// 为什么用 AWSSDK.S3：Minio 的 .NET SDK 没有公开「给每一片生成上传链接」的接口；COS 也兼容 S3 协议，同一套代码都能用。
/// 大文件由浏览器分片直接传到存储（不走网站服务器的带宽）。
/// </summary>
public sealed class ResourceStorage : IDisposable
{
    public const string Nas = "nas";
    public const string Cos = "cos";

    private readonly ResourceBucket _nas;
    private readonly ResourceBucket? _cos;

    /// <summary>NAS 的桶名（列官方资源用）</summary>
    public string Bucket => _nas.Bucket;
    /// <summary>腾讯云配好了没有（没配就只能存 NAS）</summary>
    public bool CosAvailable => _cos != null;
    /// <summary>腾讯云有没有开全球加速（上传弹窗里提示用）</summary>
    public bool CosAccelerated { get; }

    public ResourceStorage(IConfiguration config)
    {
        // ---------- NAS（MinIO） ----------
        var nasBucket = config["MinioSettings:BucketName"] ?? "tcpr";
        _nas = new ResourceBucket(Nas, nasBucket, () =>
        {
            // 和 Program.cs 里 Minio 客户端一样：去掉协议前缀，统一走 https（前面挂着 Cloudflare）
            var endpoint = (config["MinioSettings:Endpoint"] ?? "").Replace("https://", "").Replace("http://", "").TrimEnd('/');
            var s3Config = new AmazonS3Config
            {
                ServiceURL = "https://" + endpoint,
                // MinIO 用「域名/桶名/文件」这种路径写法，不用「桶名.域名」
                ForcePathStyle = true,
                // MinIO 默认地区就是 us-east-1，签名要用它
                AuthenticationRegion = "us-east-1",
                // 新版 AWS SDK 默认给请求加 CRC 校验头，部分 MinIO 版本不认；只在必须时才加
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
                // 合并大文件时 MinIO 可能要多想一会儿
                Timeout = TimeSpan.FromMinutes(5),
            };
            return new AmazonS3Client(new BasicAWSCredentials(config["MinioSettings:AccessKey"] ?? "", config["MinioSettings:SecretKey"] ?? ""), s3Config);
        }, privateObjects: false, headFirst: false);

        // ---------- 腾讯云 COS ----------
        var secretId = config["TencentCloud:SecretId"];
        var secretKey = config["TencentCloud:SecretKey"];
        var region = config["TencentCloud:COS:Region"];
        var cosBucket = config["TencentCloud:COS:ResourceBucket"];
        if (string.IsNullOrWhiteSpace(cosBucket)) cosBucket = config["TencentCloud:COS:Bucket"];
        CosAccelerated = string.Equals(config["TencentCloud:COS:ResourceAccelerate"], "true", StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(secretId) && !string.IsNullOrWhiteSpace(secretKey) && !string.IsNullOrWhiteSpace(region) && !string.IsNullOrWhiteSpace(cosBucket))
        {
            var accelerate = CosAccelerated;
            _cos = new ResourceBucket(Cos, cosBucket!, () => new AmazonS3Client(new BasicAWSCredentials(secretId, secretKey), new AmazonS3Config
            {
                // COS 的 S3 兼容地址：https://桶名.cos.地域.myqcloud.com（SDK 会自动把桶名放到域名前面）；全球加速是 桶名.cos.accelerate.myqcloud.com
                ServiceURL = accelerate ? "https://cos.accelerate.myqcloud.com" : $"https://cos.{region}.myqcloud.com",
                ForcePathStyle = false,
                // COS 用自己的地域名签名（比如 ap-beijing）
                AuthenticationRegion = region,
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
                Timeout = TimeSpan.FromMinutes(5),
            }), privateObjects: true, headFirst: true);
        }
    }

    /// <summary>这个资源存在哪就用哪个（NULL、空、认不出来的都当 NAS，老数据都在 NAS 上）</summary>
    public ResourceBucket For(string? storage) =>
        storage == Cos ? (_cos ?? throw new InvalidOperationException("网站没有配置腾讯云存储，读不到存在腾讯云上的资源")) : _nas;

    public static string Label(string? storage) => storage == Cos ? "腾讯云" : "NAS";

    public void Dispose()
    {
        _nas.Dispose();
        _cos?.Dispose();
    }
}

/// <summary>一个存储桶（NAS 的 MinIO 或者腾讯云 COS）上的分片上传、核对大小、下载链接</summary>
public sealed class ResourceBucket : IDisposable
{
    private readonly Lazy<AmazonS3Client> _client;
    /// <summary>新文件设成私有（COS 的桶是公共读的：资源文件要单独设私有，只能用带签名的下载链接拿）</summary>
    private readonly bool _privateObjects;
    /// <summary>核对大小时先用 HEAD（COS 没有 Cloudflare 的问题）；NAS 先查列表（经过 Cloudflare 时 HEAD 可能出错）</summary>
    private readonly bool _headFirst;

    public string Name { get; }
    public string Bucket { get; }

    internal ResourceBucket(string name, string bucket, Func<AmazonS3Client> factory, bool privateObjects, bool headFirst)
    {
        Name = name;
        Bucket = bucket;
        _client = new Lazy<AmazonS3Client>(factory);
        _privateObjects = privateObjects;
        _headFirst = headFirst;
    }

    private AmazonS3Client S3 => _client.Value;

    /// <summary>开始一次分片上传，返回存储给的上传编号（UploadId）</summary>
    public async Task<string> StartUploadAsync(string key, string contentType, CancellationToken ct)
    {
        var req = new InitiateMultipartUploadRequest
        {
            BucketName = Bucket,
            Key = key,
            ContentType = contentType,
        };
        if (_privateObjects) req.CannedACL = S3CannedACL.Private;
        var res = await S3.InitiateMultipartUploadAsync(req, ct);
        if (string.IsNullOrEmpty(res.UploadId)) throw new InvalidOperationException("存储没有返回上传编号");
        return res.UploadId;
    }

    /// <summary>
    /// 某一片的上传链接（PUT）。链接里签了桶、对象名、上传编号和片号，
    /// 拿到它的人只能往这个资源自己的位置传这一片，改不了别的文件。
    /// </summary>
    public string PartUrl(string key, string uploadId, int partNumber, DateTime expiresUtc) =>
        S3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = Bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            UploadId = uploadId,
            PartNumber = partNumber,
            Expires = expiresUtc,
            Protocol = Amazon.S3.Protocol.HTTPS,
        });

    /// <summary>已经传完的片（以存储为准）。上传编号已经失效时返回 null。</summary>
    public async Task<List<PartDetail>?> ListPartsAsync(string key, string uploadId, CancellationToken ct)
    {
        var all = new List<PartDetail>();
        string? marker = null;
        try
        {
            for (var guard = 0; guard < 100; guard++)
            {
                var req = new ListPartsRequest { BucketName = Bucket, Key = key, UploadId = uploadId, MaxParts = 1000 };
                if (marker != null) req.PartNumberMarker = marker;
                var res = await S3.ListPartsAsync(req, ct);
                // 新版 SDK：一片都没有时 Parts 是 null
                if (res.Parts != null) all.AddRange(res.Parts);
                if (res.IsTruncated != true || res.NextPartNumberMarker is not int next) break;
                marker = next.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchUpload")
        {
            return null;
        }
        return all;
    }

    /// <summary>合并所有分片。ETag 用服务器自己 ListParts 拿到的（不依赖浏览器读响应头）。</summary>
    public async Task CompleteAsync(string key, string uploadId, IEnumerable<PartDetail> parts, CancellationToken ct)
    {
        await S3.CompleteMultipartUploadAsync(new CompleteMultipartUploadRequest
        {
            BucketName = Bucket,
            Key = key,
            UploadId = uploadId,
            PartETags = parts.OrderBy(p => p.PartNumber ?? 0).Select(p => new PartETag(p.PartNumber ?? 0, p.ETag)).ToList(),
        }, ct);
    }

    /// <summary>放弃一次分片上传（存储会删掉已经传上去的碎片）。已经不存在的就当成功。</summary>
    public async Task AbortAsync(string key, string uploadId, CancellationToken ct)
    {
        try
        {
            await S3.AbortMultipartUploadAsync(new AbortMultipartUploadRequest { BucketName = Bucket, Key = key, UploadId = uploadId }, ct);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.ErrorCode == "NoSuchUpload") { }
    }

    /// <summary>
    /// 文件实际大小；文件不存在返回 null。
    /// NAS：先查文件列表（普通 GET 请求，MinIO 直接给出大小，最可靠），列表查不了再用 HEAD 兜底（实测经过 Cloudflare 时 HEAD 可能出错）；
    /// COS：先 HEAD（最直接），出错再查列表。
    /// </summary>
    public async Task<long?> SizeAsync(string key, CancellationToken ct)
    {
        if (_headFirst)
        {
            try { return await HeadSizeAsync(key, ct); }
            catch (Exception headError) when (headError is not OperationCanceledException) { return await ListSizeAsync(key, ct); }
        }
        try { return await ListSizeAsync(key, ct); }
        catch (Exception listError) when (listError is not OperationCanceledException) { return await HeadSizeAsync(key, ct); }
    }

    private async Task<long?> ListSizeAsync(string key, CancellationToken ct)
    {
        var list = await S3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = Bucket, Prefix = key, MaxKeys = 20 }, ct);
        var hit = list.S3Objects?.FirstOrDefault(o => o.Key == key);
        return hit == null ? null : (long?)(hit.Size ?? 0);
    }

    private async Task<long?> HeadSizeAsync(string key, CancellationToken ct)
    {
        try
        {
            var res = await S3.GetObjectMetadataAsync(new GetObjectMetadataRequest { BucketName = Bucket, Key = key }, ct);
            return res.Headers.ContentLength;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound) { return null; }
    }

    /// <summary>删除文件（只用在「合并后大小不对、这次上传作废」的情况；正常删除资源时文件保留）</summary>
    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        try { await S3.DeleteObjectAsync(Bucket, key, ct); }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound) { }
    }

    /// <summary>
    /// 下载链接（15 分钟过期）。
    /// · Content-Disposition 让浏览器保存成原文件名；
    /// · Cache-Control: private, no-store 让 Cloudflare / CDN 不缓存（否则链接过期后缓存里的文件还能被拿到，付费资源就漏了）；
    /// · inline=true 只给管理员预览图片 / 音频 / 视频 / PDF / 纯文本用，并强制指定类型，避免网页文件在浏览器里执行。
    /// </summary>
    public string DownloadUrl(string key, string fileName, bool inline = false, string? previewContentType = null)
    {
        var req = new GetPreSignedUrlRequest
        {
            BucketName = Bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(ResourceRules.DownloadUrlLife),
            Protocol = Amazon.S3.Protocol.HTTPS,
        };
        req.ResponseHeaderOverrides.ContentDisposition = ResourceRules.ContentDisposition(fileName, inline);
        req.ResponseHeaderOverrides.CacheControl = "private, no-store";
        if (inline && !string.IsNullOrEmpty(previewContentType)) req.ResponseHeaderOverrides.ContentType = previewContentType;
        return S3.GetPreSignedURL(req);
    }

    public void Dispose()
    {
        if (_client.IsValueCreated) _client.Value.Dispose();
    }
}
