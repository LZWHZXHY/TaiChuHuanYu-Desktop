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
            /// <summary>register 注册 / reset 找回密码；旧前端不传就不做这两项检查</summary>
            public string? Purpose { get; set; }
        }

        // ---------------- 验证码防刷 ----------------
        // 同一个邮箱：60 秒内只能发一次、一小时最多 6 次（防止有人拿这个接口给别人狂发邮件，发多了邮箱会被当成垃圾邮件，正常注册也收不到）。
        // 验证码输错 5 次就作废，要重新获取（6 位数字 5 分钟内不限次数地试，能被程序猜出来，找回密码时等于能盗号）。
        // 只记在内存里，重启清零。
        private const int CodeMinutes = 5, ResendSeconds = 60, MaxSendPerHour = 6, MaxWrongTries = 5;
        private static readonly global::System.Collections.Concurrent.ConcurrentDictionary<string, List<DateTime>> _sendLog = new();
        private static readonly global::System.Collections.Concurrent.ConcurrentDictionary<string, int> _wrongTries = new();
        private static string Key(string email) => email.Trim().ToLowerInvariant();

        private static bool IsEmail(string? email)
            => !string.IsNullOrWhiteSpace(email) && email.Length <= 100 && !email.Contains(' ')
               && global::System.Net.Mail.MailAddress.TryCreate(email, out var a) && a.Address == email;

        /// <summary>校验验证码：对了返回记录；错了累计次数，错满 5 次作废</summary>
        private async Task<(EmailVerification? Row, string? Error)> CheckCodeAsync(string email, string? code)
        {
            var v = await _context.EmailVerifications.FindAsync(email);
            if (v == null || v.ExpiresAt < DateTime.UtcNow) return (null, "验证码已失效，请重新获取");
            if (string.IsNullOrWhiteSpace(code) || v.Code != code.Trim())
            {
                var n = _wrongTries.AddOrUpdate(Key(email), 1, (_, c) => c + 1);
                if (n >= MaxWrongTries)
                {
                    _context.EmailVerifications.Remove(v);
                    await _context.SaveChangesAsync();
                    _wrongTries.TryRemove(Key(email), out _);
                    return (null, $"验证码错了 {MaxWrongTries} 次，已经作废，请重新获取");
                }
                return (null, $"验证码不对（还能再试 {MaxWrongTries - n} 次）");
            }
            _wrongTries.TryRemove(Key(email), out _);
            return (v, null);
        }

        private static string? CheckPassword(string? pwd)
            => string.IsNullOrEmpty(pwd) || pwd.Length < 6 ? "密码至少 6 位" : pwd.Length > 64 ? "密码最多 64 位" : null;

        // --- 新增：发送验证码接口 ---
        [HttpPost("send-code")]
        public async Task<IActionResult> SendCode([FromBody] SendCodeRequest request)
        {
            var email = (request.Email ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(email)) return BadRequest(new { message = "邮件地址不能为空" });
            if (!IsEmail(email)) return BadRequest(new { message = "邮箱格式不对，请检查一下" });

            // 注册时邮箱已经有账号：直接告诉对方，不用等填完一整张表才报错
            var registered = await _context.Users.AnyAsync(u => u.Email == email);
            if (request.Purpose == "register" && registered)
                return BadRequest(new { message = "这个邮箱已经注册过了，可以直接登录；忘了密码就用「找回密码」" });
            if (request.Purpose == "reset" && !registered)
                return BadRequest(new { message = "这个邮箱还没有注册过账号" });

            // 发送频率限制
            var now = DateTime.UtcNow;
            var log = _sendLog.GetOrAdd(Key(email), _ => new List<DateTime>());
            lock (log)
            {
                log.RemoveAll(t => (now - t).TotalHours >= 1);
                if (log.Count > 0 && (now - log[^1]).TotalSeconds < ResendSeconds)
                    return BadRequest(new { message = $"发得太快了，{ResendSeconds - (int)(now - log[^1]).TotalSeconds} 秒后再试" });
                if (log.Count >= MaxSendPerHour)
                    return BadRequest(new { message = "这个邮箱一小时内收的验证码太多了，请过一会儿再试" });
                log.Add(now);
            }

            // 1. 生成 6 位随机验证码（用加密级随机数）
            var code = global::System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

            // 2. 存入验证表（有旧码就直接覆盖）
            var verification = await _context.EmailVerifications.FindAsync(email);
            if (verification != null)
            {
                verification.Code = code;
                verification.ExpiresAt = now.AddMinutes(CodeMinutes);
            }
            else _context.EmailVerifications.Add(new EmailVerification { Email = email, Code = code, ExpiresAt = now.AddMinutes(CodeMinutes) });
            _wrongTries.TryRemove(Key(email), out _);

            try
            {
                // 3. 调用 MailKit 服务发送
                await _emailService.SendVerificationCodeAsync(email, code);
                await _context.SaveChangesAsync();
                return Ok(new { message = "验证码已发往邮件地址" });
            }
            catch (Exception ex)
            {
                // 发送失败不算次数
                lock (log) log.Remove(now);
                Console.WriteLine($"[{DateTime.UtcNow:O}] 验证码邮件发送失败 {email}: {ex.Message}");
                return StatusCode(500, new { message = "验证码邮件发送失败，请稍后再试；一直失败请联系管理员" });
            }
        }

        // --- 修改：带验证码校验的注册 ---
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            // 0. 基本检查：名号格式和改名的规则一样（2–20 个字，只能中文、字母、数字和 _ - ·），
            //    以前什么都不查：空名字、带空格、带 @ 的名字都能注册（名字和别人的邮箱一样时，用邮箱登录会登进错的账号）
            dto.Username = (dto.Username ?? string.Empty).Trim();
            dto.Email = (dto.Email ?? string.Empty).Trim();
            if (TaiChuWeb_V2.Controllers.Users.RenameController.ValidateName(dto.Username) is string nameError)
                return BadRequest(new { message = nameError });
            if (!IsEmail(dto.Email)) return BadRequest(new { message = "邮箱格式不对，请检查一下" });
            if (CheckPassword(dto.Password) is string pwdError) return BadRequest(new { message = pwdError });
            // 邮箱已有账号（数据库邮箱唯一，以前这里不查，直接报 500「服务暂时不可用」）
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
                return BadRequest(new { message = "这个邮箱已经注册过了，可以直接登录；忘了密码就用「找回密码」" });

            // 1. 校验验证码（从数据库查，错 5 次作废）
            var (v, codeError) = await CheckCodeAsync(dto.Email, dto.VerificationCode);
            if (v == null) return BadRequest(new { message = codeError });

            // 2. 检查重名
            if (await _context.Users.AnyAsync(u => u.Username == dto.Username))
                return BadRequest(new { message = "名号已存在，请重新输入" });
            // 别人用改名卡改掉的旧名字有 30 天保留期，防止冒充
            if (await TaiChuWeb_V2.Controllers.Users.RenameController.IsReservedAsync(_context, dto.Username, null))
                return BadRequest(new { message = "这个名号刚被原主人改掉，保留期内暂不可用，请换一个" });

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
                    MaxSpaces = 1,
                    MaxNotes = 100,
                    UsedSpaces = 1,
                    UsedNotes = 0,
                    MaxWorldCount = 3,          // ✅ 显式赋值
                    MaxCardsPerWorld = 100,     // ✅ 显式赋值
                                                // UsedWorldCount 默认为 0，无需显式设置
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
            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateException)
            {
                // 两个人同时用同一个名号 / 邮箱注册：后到的撞上唯一索引
                return BadRequest(new { message = "这个名号或邮箱刚刚被别人注册了，请换一个再试" });
            }

            return Ok(new { message = "注册成功！欢迎来到太初寰宇。" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            // 1. 基础校验：现在使用的是 Identifier 而非 Username
            if (dto == null || string.IsNullOrEmpty(dto.Identifier) || string.IsNullOrEmpty(dto.Password))
            {
                return BadRequest(new { message = "请填写名号（或邮箱）和密码" });
            }

            // 复制粘贴时常带进空格（包括中文输入法的全角空格），去掉再查
            dto.Identifier = dto.Identifier.Replace('\u3000', ' ').Trim();
            var id = dto.Identifier;
            var isEmail = id.Contains('@');

            // 2. 用名号或邮箱找账号。先精确匹配；找不到再忽略名号前后的空格找一次
            //    （fix126 之前注册不去空格，有人的名号前后带了空格，用名号就登不上、用邮箱能登上）
            var user = await _context.Users
                .Include(u => u.Profile)
                .Include(u => u.Stats)
                .FirstOrDefaultAsync(u => u.Username == id || u.Email == id);
            if (user == null && !isEmail)
                user = await _context.Users.Include(u => u.Profile).Include(u => u.Stats)
                    .FirstOrDefaultAsync(u => u.Username.Trim() == id);

            // 3. 没找到：如果是别人改名前的旧名号，告诉他现在叫什么
            if (user == null)
            {
                if (!isEmail && await RenamedToAsync(id) is string newName)
                    return Unauthorized(new { message = $"「{id}」这个名号已经改成「{newName}」了，请用新名号或邮箱登录", code = "renamed" });
                return Unauthorized(new
                {
                    message = isEmail
                        ? $"没有找到用邮箱「{id}」注册的账号。请检查有没有打错；还没有账号可以先注册"
                        : $"没有找到名号为「{id}」的账号。请检查有没有打错（注意全角 / 半角、大小写）；也可以改用注册时的邮箱登录",
                    code = "not_found",
                });
            }

            // 4. 校验密码。老账号的密码可能是别的格式，BCrypt 认不出会抛异常（以前直接显示「后端异常」）
            bool isPasswordValid;
            try { isPasswordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash); }
            catch (Exception)
            {
                return Unauthorized(new { message = "这个账号的密码格式比较旧，没法直接登录。请点「找回密码」用邮箱重新设置一次", code = "legacy_password" });
            }
            if (!isPasswordValid)
            {
                return Unauthorized(new { message = "密码不对。请注意大小写和输入法；忘了密码可以点「找回密码」用邮箱重设", code = "wrong_password" });
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


        /// <summary>这个名号是不是改名前的旧名号：是就返回账号现在的名号（改名记录表不存在或查询出错时返回 null）</summary>
        private async Task<string?> RenamedToAsync(string oldName)
        {
            try
            {
                var userText = await _context.Database
                    .SqlQuery<string>($"SELECT UserId AS Value FROM username_history WHERE OldName = {oldName} ORDER BY Id DESC LIMIT 1")
                    .FirstOrDefaultAsync();
                if (!Guid.TryParse(userText, out var uid)) return null;
                return await _context.Users.AsNoTracking().Where(u => u.Id == uid).Select(u => u.Username).FirstOrDefaultAsync();
            }
            catch (Exception) { return null; }
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

            dto.Email = dto.Email.Trim();
            if (CheckPassword(dto.NewPassword) is string pwdError) return BadRequest(new { message = pwdError });

            // 2. 校验验证码（逻辑同注册，错 5 次作废）
            var (v, codeError) = await CheckCodeAsync(dto.Email, dto.VerificationCode);
            if (v == null) return BadRequest(new { message = codeError });

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
                Console.WriteLine($"[{DateTime.UtcNow:O}] 重置密码失败 {dto.Email}: {ex.Message}");
                return StatusCode(500, new { message = "密码重置失败，请联系管理员" });
            }
        }




    }
}