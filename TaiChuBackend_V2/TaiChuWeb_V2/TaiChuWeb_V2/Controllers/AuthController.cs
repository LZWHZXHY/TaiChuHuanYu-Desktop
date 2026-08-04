using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Asn1.Ocsp;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.LoginRegister;
using TaiChuWeb_V2.Models.LingMai;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Services.Email; // 确保引用了接口命名空间
using TaiChuWeb_V2.Models.User;  // 用于 UserPermission 和 AdminPermission



namespace TaiChuWeb_V2.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly JwtService _jwtService;
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService; // 注入邮件服务

        public AuthController(AppDbContext context, IEmailService emailService, JwtService jwtService)
        {
            _context = context;
            _emailService = emailService;
            _jwtService = jwtService;
        }

        public class SendCodeRequest
        {
            public string Email { get; set; } = string.Empty;
        }

        // --- 新增：发送验证码接口 ---
        [HttpPost("send-code")]
        public async Task<IActionResult> SendCode([FromBody] SendCodeRequest request)
        {
            string email = request.Email;

            if (string.IsNullOrEmpty(email)) return BadRequest(new { message = "邮件地址不能为空" });

            // 1. 生成 6 位随机验证码
            var code = new Random().Next(100000, 999999).ToString();

            // 2. 存入验证表（覆盖旧码）
            var verification = await _context.EmailVerifications.FindAsync(email);
            if (verification != null) _context.EmailVerifications.Remove(verification);

            _context.EmailVerifications.Add(new EmailVerification
            {
                Email = email,
                Code = code,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5) // 5分钟有效
            });

            try
            {
                // 3. 调用 MailKit 服务发送
                await _emailService.SendVerificationCodeAsync(email, code);
                await _context.SaveChangesAsync();
                return Ok(new { message = "验证码已发往邮件地址" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "邮件发送失败，请检查 SMTP 配置", detail = ex.Message });
            }
        }

        // --- 修改：带验证码校验的注册 ---
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            // 1. 校验验证码（从数据库查）
            var v = await _context.EmailVerifications.FindAsync(dto.Email);
            if (v == null || v.Code != dto.VerificationCode || v.ExpiresAt < DateTime.UtcNow)
            {
                return BadRequest(new { message = "验证码错误或已失效" });
            }

            // 2. 检查重名
            if (await _context.Users.AnyAsync(u => u.Username == dto.Username))
                return BadRequest(new { message = "名号已存在，请重新输入" });

            // 🌟 提前生成新用户的 ID，因为我们要用它来绑定“默认空间”
            var newUserId = Guid.NewGuid();

            // 3. 执行“一键四连”：账号 + 档案 + 数值/配额 + 默认空间
            var newUser = new User
            {
                Id = newUserId, // 🌟 显式赋值 ID
                Username = dto.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Email = dto.Email,
                CreatedAt = DateTime.UtcNow,
                // 初始化档案
                Profile = new UserProfile
                {
                    Avatar = "https://api.dicebear.com/7.x/avataaars/svg?seed=" + dto.Username, // 使用动态默认头像
                    Gender = "未知",
                    Bio = "初入寰宇，请多指教。",
                    SocialLinks = "[]", // 初始化为空的 JSON 数组字符串
                    Mood = "暂无心情"
                },
                // 初始化数值与配额
                Stats = new UserStats
                {
                    Experience = 0,
                    CurrentSignStreak = 0,
                    MaxSignStreak = 0,
                    // 🌟 补齐配额系统所需的基础字段
                    MaxSpaces = 1,
                    MaxNotes = 100,
                    UsedSpaces = 1, // 算上下面即将分配的默认空间，已用 1 个
                    UsedNotes = 0
                }
            };

            _context.Users.Add(newUser);

            // ========================================================================
            // 🌟 4. 新手大礼包：发配初始默认空间
            // ========================================================================
            var defaultSpace = new Space
            {
                Id = Guid.NewGuid(),
                Name = "我的初识灵脉", // 充满寰宇气息的默认名字
                UserId = newUserId.ToString(), // 你的 Space.UserId 是 string 类型
                IsPublic = false, // 保护新手隐私，默认私有
                CreatedAt = DateTime.UtcNow
            };

            _context.Spaces.Add(defaultSpace);

            // 5. 注册成功后作废验证码
            _context.EmailVerifications.Remove(v);

            // 统一提交事务，如果这一步出错，User、Profile、Stats、Space 会一起回滚，保证数据绝对干净
            await _context.SaveChangesAsync();

            return Ok(new { message = "认证成功！欢迎来到太初寰宇。" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            // 1. 基础校验：现在使用的是 Identifier 而非 Username
            if (dto == null || string.IsNullOrEmpty(dto.Identifier) || string.IsNullOrEmpty(dto.Password))
            {
                return BadRequest(new { message = "请填写完整的昵称或者邮箱地址" });
            }

            // 2. 核心逻辑：用“标识符”去匹配数据库中的 名号(Username) 或 灵觉地址(Email)
            var user = await _context.Users
                .Include(u => u.Profile)
                .Include(u => u.Stats)
                .FirstOrDefaultAsync(u => u.Username == dto.Identifier || u.Email == dto.Identifier);

            // 3. 检查是否存在该用户
            if (user == null)
            {
                return Unauthorized(new { message = "寰宇中未发现此名号或地址" });
            }

            // 4. 校验密印（密码哈希验证）
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
            if (!isPasswordValid)
            {
                return Unauthorized(new { message = "昵称或邮箱地址不匹配，接入失败" });
            }

            // ✅ 5. 获取用户权限列表
            var permissions = await _context.UserPermissions
                .Where(p => p.UserId == user.Id)
                .Select(p => p.Permission.ToString())  // 枚举转字符串
                .ToListAsync();

            // 6. 生成 JWT Token
            var token = _jwtService.GenerateToken(user);

            // 7. 返回用户信息 + 权限列表
            return Ok(new
            {
                token = token,
                user = new
                {
                    id = user.Id,
                    username = user.Username,
                    email = user.Email,
                    avatar = user.Profile?.Avatar ?? "default_avatar.png",
                    level = user.Stats?.Level ?? 1,
                    // ✅ 核心：返回权限列表
                    permissions = permissions  // 如 ["Survey_Manage", "Wiki_Editor"]
                },
                message = "欢迎回归太初寰宇"
            });
        }


        // --- 新增：密钥找回（重置密码）接口 ---
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            // 1. 基础校验
            if (string.IsNullOrEmpty(dto.Email) || string.IsNullOrEmpty(dto.VerificationCode) || string.IsNullOrEmpty(dto.NewPassword))
            {
                return BadRequest(new { message = "请填写完整的重塑信息" });
            }

            // 2. 校验验证码（逻辑同注册）
            var v = await _context.EmailVerifications.FindAsync(dto.Email);
            if (v == null || v.Code != dto.VerificationCode || v.ExpiresAt < DateTime.UtcNow)
            {
                return BadRequest(new { message = "验证码错误或已失效" });
            }

            // 3. 查找用户
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null)
            {
                return NotFound(new { message = "寰宇中未发现此邮箱地址绑定的身份" });
            }

            try
            {
                // 4. 重塑密钥（使用 BCrypt 加密新密码）
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);

                // 5. 成功后作废验证码，防止二次使用
                _context.EmailVerifications.Remove(v);

                await _context.SaveChangesAsync();

                return Ok(new { message = "密钥已重塑，请重新认证接入" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "密钥重塑失败，请联系管理员", detail = ex.Message });
            }
        }




    }
}