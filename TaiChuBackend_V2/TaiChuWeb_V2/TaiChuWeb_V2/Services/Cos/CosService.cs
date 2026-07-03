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

        /// <summary>
        /// 上传文件到 COS，并设置为公有读（解决 AccessDenied）
        /// </summary>
        public async Task<string> UploadAsync(byte[] data, string fileName, string folder)
        {
            string cosPath = string.IsNullOrEmpty(folder)
                ? fileName
                : $"{folder.TrimEnd('/')}/{fileName}";

            var request = new PutObjectRequest(_bucket, cosPath, new MemoryStream(data));

            // ✅ 关键：设置文件为公有读，无需修改存储桶权限
            request.SetRequestHeader("x-cos-acl", "public-read");

            var result = await Task.Run(() => _cosXml.PutObject(request));

            if (result.httpCode == 200)
            {
                var baseUrl = _configuration["TencentCloud:COS:BaseUrl"]
                    ?? $"https://{_bucket}.cos.{_region}.myqcloud.com";
                return $"{baseUrl}/{cosPath}";
            }

            throw new Exception($"COS 上传失败: {result.httpCode}");
        }
    }
}