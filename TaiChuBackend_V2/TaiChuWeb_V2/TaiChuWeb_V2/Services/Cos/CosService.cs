using COSXML;
using COSXML.Auth;
using COSXML.Model.Object;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

namespace TaiChuWeb_V2.Services.Cos
{
    public class CosService
    {
        private readonly IConfiguration _configuration;
        private readonly CosXml _cosXml;
        private readonly string _bucket;
        private readonly string _region;

        public CosService(IConfiguration configuration)
        {
            _configuration = configuration;
            var secretId = _configuration["TencentCloud:SecretId"];
            var secretKey = _configuration["TencentCloud:SecretKey"];
            _bucket = _configuration["TencentCloud:COS:Bucket"];
            _region = _configuration["TencentCloud:COS:Region"];
            var appId = _configuration["TencentCloud:COS:AppId"];

            var cosConfig = new CosXmlConfig.Builder()
                .IsHttps(true)
                .SetRegion(_region)
                .SetAppid(appId)
                .Build();

            var credentialProvider = new DefaultQCloudCredentialProvider(secretId, secretKey, 60 * 60);
            _cosXml = new CosXmlServer(cosConfig, credentialProvider);
        }

        public string ObjectUrl(string key) =>
            $"{(_configuration["TencentCloud:COS:BaseUrl"] ?? $"https://{_bucket}.cos.{_region}.myqcloud.com").TrimEnd('/')}/{key}";

        public virtual Task<string> UploadAsync(byte[] data, string fileName, string folder)
        {
            var key = string.IsNullOrEmpty(folder) ? fileName : $"{folder.TrimEnd('/')}/{fileName}";
            return RetryAsync(async () =>
            {
                using var stream = new MemoryStream(data);
                var request = new PutObjectRequest(_bucket, key, stream);
                request.SetRequestHeader("x-cos-acl", "public-read");
                var result = await Task.Run(() => _cosXml.PutObject(request));
                if (result.httpCode != 200) throw new IOException($"COS 上传失败: {result.httpCode}");
                return ObjectUrl(key);
            });
        }

        // 只接收调用方持有的对象 key，不从任意用户 URL 推导删除目标。
        public virtual Task DeleteAsync(string key) => RetryAsync(async () =>
        {
            var result = await Task.Run(() => _cosXml.DeleteObject(new DeleteObjectRequest(_bucket, key)));
            if (result.httpCode is not (200 or 204 or 404)) throw new IOException($"COS 删除失败: {result.httpCode}");
            return true;
        });

        private static async Task<T> RetryAsync<T>(Func<Task<T>> action)
        {
            for (var attempt = 1; ; attempt++)
            {
                try { return await action(); }
                catch (Exception ex) when (attempt < 3 && (ex is COSXML.CosException.CosClientException or IOException || ex.InnerException is IOException))
                { await Task.Delay(1000 * attempt); }
            }
        }
    }
}
