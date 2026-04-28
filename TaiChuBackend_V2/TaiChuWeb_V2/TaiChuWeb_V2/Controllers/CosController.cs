using Microsoft.AspNetCore.Mvc;
using TencentCloud.Common;
using TencentCloud.Sts.V20180813;
using TencentCloud.Sts.V20180813.Models;

namespace TaiChuWeb_V2.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CosController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        // 通过构造函数注入配置
        public CosController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("get-credential")]
        public async Task<IActionResult> GetCredential()
        {
            try
            {
                // 从 appsettings.json 中读取配置
                var secretId = _configuration["TencentCloud:SecretId"];
                var secretKey = _configuration["TencentCloud:SecretKey"];
                var bucket = _configuration["TencentCloud:COS:Bucket"];
                var region = _configuration["TencentCloud:COS:Region"];
                var appId = _configuration["TencentCloud:COS:AppId"];

                Credential cred = new Credential
                {
                    SecretId = secretId,
                    SecretKey = secretKey
                };

                StsClient client = new StsClient(cred, region);
                GetFederationTokenRequest req = new GetFederationTokenRequest();
                req.Name = "taichu-upload-sts";

                // 动态拼接 Policy 资源路径
                string resource = $"qcs::cos:{region}:uid/{appId}:{bucket}/*";
                req.Policy = "{\"version\":\"2.0\",\"statement\":[{\"action\":[\"name/cos:PutObject\",\"name/cos:PostObject\",\"name/cos:InitiateMultipartUpload\",\"name/cos:ListMultipartUploads\",\"name/cos:ListParts\",\"name/cos:UploadPart\",\"name/cos:CompleteMultipartUpload\"],\"resource\":[\"" + resource + "\"],\"effect\":\"allow\"}]}";

                GetFederationTokenResponse resp = await client.GetFederationToken(req);

                return Ok(new
                {
                    credentials = new
                    {
                        tmpSecretId = resp.Credentials.TmpSecretId,
                        tmpSecretKey = resp.Credentials.TmpSecretKey,
                        sessionToken = resp.Credentials.Token
                    },
                    startTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    expiredTime = resp.ExpiredTime,
                    bucket = bucket,
                    region = region
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}