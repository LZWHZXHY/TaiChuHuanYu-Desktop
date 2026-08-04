using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.Survey;
using TaiChuWeb_V2.Models.Survey;
using TaiChuWeb_V2.Utils;
using TaiChuWeb_V2.Models.User;   // 添加：引用 AdminPermission

namespace TaiChuWeb_V2.Controllers.Survey
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SurveyController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SurveyController> _logger;

        public SurveyController(AppDbContext context, ILogger<SurveyController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ============================================================
        // 1. 创建问卷（含题目和选项）
        // ============================================================
        [HttpPost("create")]
        public async Task<IActionResult> CreateSurvey([FromBody] CreateSurveyRequest request)
        {
            try
            {
                var userId = User.GetUserId();

                // ✅ 权限校验：必须拥有 Survey_Manage 或 SuperAdmin
                if (!await PermissionHelper.HasPermission(_context, userId, AdminPermission.Survey_Manage))
                    return Forbid("您没有管理问卷的权限");

                // 基本校验
                if (string.IsNullOrWhiteSpace(request.Title))
                    return BadRequest(new { message = "问卷标题不能为空" });

                if (request.StartTime >= request.EndTime)
                    return BadRequest(new { message = "结束时间必须大于开始时间" });

                if (request.Questions == null || request.Questions.Count == 0)
                    return BadRequest(new { message = "问卷至少需要一个问题" });

                // 校验每个题目
                foreach (var q in request.Questions)
                {
                    if (string.IsNullOrWhiteSpace(q.Title))
                        return BadRequest(new { message = "题目标题不能为空" });

                    if ((q.QuestionType == 1 || q.QuestionType == 2) && (q.Options == null || q.Options.Count < 2))
                        return BadRequest(new { message = $"题目「{q.Title}」至少需要2个选项" });

                    if (q.Options != null)
                    {
                        foreach (var opt in q.Options)
                        {
                            if (string.IsNullOrWhiteSpace(opt.OptionText))
                                return BadRequest(new { message = "选项内容不能为空" });
                        }
                    }
                }

                var survey = new Models.Survey.Survey
                {
                    Title = request.Title.Trim(),
                    Description = request.Description?.Trim(),
                    CoverImage = request.CoverImage,
                    StartTime = request.StartTime.ToUniversalTime(),
                    EndTime = request.EndTime.ToUniversalTime(),
                    IsPublic = request.IsPublic,
                    AllowAnonymous = request.AllowAnonymous,
                    MaxSubmissions = request.MaxSubmissions,
                    Status = 0,
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow,
                };

                _context.Surveys.Add(survey);
                await _context.SaveChangesAsync();

                int sortOrder = 0;
                foreach (var qDto in request.Questions)
                {
                    var question = new Question
                    {
                        SurveyId = survey.Id,
                        QuestionType = qDto.QuestionType,
                        Title = qDto.Title.Trim(),
                        Description = qDto.Description?.Trim(),
                        IsRequired = qDto.IsRequired,
                        SortOrder = sortOrder++,
                        Config = qDto.Config,
                        CreatedAt = DateTime.UtcNow,
                    };

                    _context.Questions.Add(question);
                    await _context.SaveChangesAsync();

                    if (qDto.Options != null && qDto.Options.Count > 0)
                    {
                        int optSort = 0;
                        foreach (var oDto in qDto.Options)
                        {
                            var option = new QuestionOption
                            {
                                QuestionId = question.Id,
                                OptionText = oDto.OptionText.Trim(),
                                OptionValue = oDto.OptionValue,
                                SortOrder = optSort++,
                                CreatedAt = DateTime.UtcNow,
                            };
                            _context.QuestionOptions.Add(option);
                        }
                        await _context.SaveChangesAsync();
                    }
                }

                return Ok(new
                {
                    success = true,
                    message = "问卷创建成功",
                    surveyId = survey.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建问卷失败");
                return StatusCode(500, new { message = "创建问卷失败，请稍后重试" });
            }
        }

        // ============================================================
        // 2. 获取问卷列表（无需权限）
        // ============================================================
        [HttpGet("list")]
        public async Task<IActionResult> GetSurveyList([FromQuery] string? status = null)
        {
            try
            {
                var query = _context.Surveys
                    .Include(s => s.Creator)
                    .Include(s => s.Questions)
                    .Include(s => s.Submissions)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(status) && int.TryParse(status, out int statusInt))
                {
                    query = query.Where(s => s.Status == statusInt);
                }

                var surveys = await query
                    .OrderByDescending(s => s.CreatedAt)
                    .Select(s => new SurveyListItemDto
                    {
                        Id = s.Id,
                        Title = s.Title,
                        Description = s.Description,
                        CoverImage = s.CoverImage,
                        Status = s.Status,
                        StartTime = s.StartTime,
                        EndTime = s.EndTime,
                        TotalSubmissions = s.Submissions.Count,
                        QuestionCount = s.Questions.Count,
                        CreatedAt = s.CreatedAt,
                        CreatorName = s.Creator != null ? s.Creator.Username : "未知用户"
                    })
                    .ToListAsync();

                return Ok(surveys);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取问卷列表失败");
                return StatusCode(500, new { message = "获取列表失败，请稍后重试" });
            }
        }

        // ============================================================
        // 3. 获取问卷详情（无需权限）
        // ============================================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetSurveyDetail(int id)
        {
            try
            {
                var survey = await _context.Surveys
                    .Include(s => s.Creator)
                    .Include(s => s.Questions)
                        .ThenInclude(q => q.Options)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (survey == null)
                    return NotFound(new { message = "问卷不存在" });

                var dto = new SurveyDetailDto
                {
                    Id = survey.Id,
                    Title = survey.Title,
                    Description = survey.Description,
                    CoverImage = survey.CoverImage,
                    Status = survey.Status,
                    StartTime = survey.StartTime,
                    EndTime = survey.EndTime,
                    IsPublic = survey.IsPublic,
                    AllowAnonymous = survey.AllowAnonymous,
                    MaxSubmissions = survey.MaxSubmissions,
                    TotalSubmissions = survey.TotalSubmissions,
                    CreatedAt = survey.CreatedAt,
                    CreatorName = survey.Creator?.Username ?? "未知用户",
                    Questions = survey.Questions
                        .OrderBy(q => q.SortOrder)
                        .Select(q => new QuestionDetailDto
                        {
                            Id = q.Id,
                            QuestionType = q.QuestionType,
                            Title = q.Title,
                            Description = q.Description,
                            IsRequired = q.IsRequired,
                            SortOrder = q.SortOrder,
                            Config = q.Config,
                            Options = q.Options
                                .OrderBy(o => o.SortOrder)
                                .Select(o => new OptionDto
                                {
                                    Id = o.Id,
                                    OptionText = o.OptionText,
                                    OptionValue = o.OptionValue,
                                    SortOrder = o.SortOrder
                                }).ToList()
                        }).ToList()
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取问卷详情失败，ID: {id}");
                return StatusCode(500, new { message = "获取详情失败，请稍后重试" });
            }
        }

        // ============================================================
        // 4. 获取问卷填写内容（无需权限）
        // ============================================================
        [HttpGet("{id}/fill")]
        public async Task<IActionResult> GetSurveyFill(int id)
        {
            try
            {
                var userId = User.GetUserId();

                var survey = await _context.Surveys
                    .Include(s => s.Questions)
                        .ThenInclude(q => q.Options)
                    .FirstOrDefaultAsync(s => s.Id == id && s.Status == 1);

                if (survey == null)
                    return NotFound(new { message = "问卷不存在或未发布" });

                var hasSubmitted = await _context.SurveySubmissions
                    .AnyAsync(s => s.SurveyId == id && s.UserId == userId && s.IsValid);

                var dto = new SurveyFillDto
                {
                    Id = survey.Id,
                    Title = survey.Title,
                    Description = survey.Description,
                    CoverImage = survey.CoverImage,
                    Status = survey.Status,
                    IsPublic = survey.IsPublic,
                    HasSubmitted = hasSubmitted,
                    Questions = survey.Questions
                        .OrderBy(q => q.SortOrder)
                        .Select(q => new QuestionFillDto
                        {
                            Id = q.Id,
                            QuestionType = q.QuestionType,
                            Title = q.Title,
                            Description = q.Description,
                            IsRequired = q.IsRequired,
                            Config = q.Config,
                            Options = q.Options
                                .OrderBy(o => o.SortOrder)
                                .Select(o => new OptionDto
                                {
                                    Id = o.Id,
                                    OptionText = o.OptionText,
                                    OptionValue = o.OptionValue,
                                    SortOrder = o.SortOrder
                                }).ToList()
                        }).ToList()
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取问卷填写内容失败，ID: {id}");
                return StatusCode(500, new { message = "获取问卷失败，请稍后重试" });
            }
        }

        // ============================================================
        // 5. 提交问卷（无需权限）
        // ============================================================
        [HttpPost("{id}/submit")]
        public async Task<IActionResult> SubmitSurvey(int id, [FromBody] SubmitSurveyRequest request)
        {
            try
            {
                var userId = User.GetUserId();

                var survey = await _context.Surveys
                    .Include(s => s.Questions)
                    .ThenInclude(q => q.Options)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (survey == null)
                    return NotFound(new { message = "问卷不存在" });

                if (survey.Status != 1)
                    return BadRequest(new { message = "问卷未发布或已结束" });

                if (DateTime.UtcNow < survey.StartTime || DateTime.UtcNow > survey.EndTime)
                    return BadRequest(new { message = "问卷不在有效时间内" });

                var existingSubmission = await _context.SurveySubmissions
                    .FirstOrDefaultAsync(s => s.SurveyId == id && s.UserId == userId && s.IsValid);

                if (existingSubmission != null)
                    return BadRequest(new { message = "您已提交过此问卷" });

                var questionIds = survey.Questions.Select(q => q.Id).ToList();
                var answeredIds = request.Answers.Select(a => a.QuestionId).ToList();

                foreach (var q in survey.Questions)
                {
                    if (q.IsRequired && !answeredIds.Contains(q.Id))
                        return BadRequest(new { message = $"请回答必答题：{q.Title}" });
                }

                var submission = new SurveySubmission
                {
                    SurveyId = id,
                    UserId = userId,
                    SubmittedAt = DateTime.UtcNow,
                    CompletedTime = request.CompletedTime,
                    IsValid = true,
                };
                _context.SurveySubmissions.Add(submission);
                await _context.SaveChangesAsync();

                foreach (var answerDto in request.Answers)
                {
                    var question = survey.Questions.FirstOrDefault(q => q.Id == answerDto.QuestionId);
                    if (question == null) continue;

                    var answer = new Answer
                    {
                        SubmissionId = submission.Id,
                        QuestionId = answerDto.QuestionId,
                        CreatedAt = DateTime.UtcNow,
                    };

                    switch (question.QuestionType)
                    {
                        case 1:
                        case 2:
                            if (answerDto.SelectedOptionIds != null && answerDto.SelectedOptionIds.Any())
                            {
                                answer.SelectedOptionIds = JsonSerializer.Serialize(answerDto.SelectedOptionIds);
                            }
                            break;

                        case 3:
                            answer.AnswerText = answerDto.AnswerText;
                            break;

                        case 4:
                            answer.AnswerText = answerDto.AnswerText;
                            break;

                        case 5:
                            if (answerDto.SortResult != null && answerDto.SortResult.Any())
                            {
                                answer.SortResult = JsonSerializer.Serialize(answerDto.SortResult);
                            }
                            break;

                        case 6:
                            if (answerDto.MatrixResult != null && answerDto.MatrixResult.Any())
                            {
                                answer.MatrixResult = JsonSerializer.Serialize(answerDto.MatrixResult);
                            }
                            break;
                    }

                    _context.Answers.Add(answer);
                }

                survey.TotalSubmissions = await _context.SurveySubmissions
                    .CountAsync(s => s.SurveyId == id && s.IsValid);

                await _context.SaveChangesAsync();

                return Ok(new SubmitSurveyResponse
                {
                    Success = true,
                    Message = "问卷提交成功",
                    SubmissionId = submission.Id,
                    CanViewResult = survey.IsPublic
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"提交问卷失败，ID: {id}");
                return StatusCode(500, new { message = "提交失败，请稍后重试" });
            }
        }

        // ============================================================
        // 6. 更新问卷（需要权限）
        // ============================================================
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSurvey(int id, [FromBody] UpdateSurveyRequest request)
        {
            try
            {
                var userId = User.GetUserId();

                // ✅ 权限校验
                if (!await PermissionHelper.HasPermission(_context, userId, AdminPermission.Survey_Manage))
                    return Forbid("您没有管理问卷的权限");

                var survey = await _context.Surveys
                    .Include(s => s.Questions)
                        .ThenInclude(q => q.Options)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (survey == null)
                    return NotFound(new { message = "问卷不存在" });

                if (survey.Status != 0)
                    return BadRequest(new { message = "只有草稿状态的问卷可以修改" });

                // 基本信息
                if (!string.IsNullOrWhiteSpace(request.Title))
                    survey.Title = request.Title.Trim();

                if (request.Description != null)
                    survey.Description = request.Description.Trim();

                if (request.CoverImage != null)
                    survey.CoverImage = request.CoverImage;

                if (request.StartTime.HasValue)
                    survey.StartTime = request.StartTime.Value.ToUniversalTime();

                if (request.EndTime.HasValue)
                    survey.EndTime = request.EndTime.Value.ToUniversalTime();

                if (request.Status.HasValue)
                    survey.Status = request.Status.Value;

                if (request.IsPublic.HasValue)
                    survey.IsPublic = request.IsPublic.Value;

                if (request.AllowAnonymous.HasValue)
                    survey.AllowAnonymous = request.AllowAnonymous.Value;

                if (request.MaxSubmissions.HasValue)
                    survey.MaxSubmissions = request.MaxSubmissions.Value;

                survey.UpdatedAt = DateTime.UtcNow;

                // 题目和选项
                if (request.Questions != null)
                {
                    var existingQuestionIds = survey.Questions.Select(q => q.Id).ToList();
                    var updatedQuestionIds = request.Questions.Where(q => q.Id > 0).Select(q => q.Id).ToList();

                    var idsToDelete = existingQuestionIds.Except(updatedQuestionIds).ToList();
                    if (idsToDelete.Any())
                    {
                        var questionsToDelete = _context.Questions.Where(q => idsToDelete.Contains(q.Id));
                        _context.Questions.RemoveRange(questionsToDelete);
                        await _context.SaveChangesAsync();
                    }

                    foreach (var qDto in request.Questions.OrderBy(q => q.SortOrder))
                    {
                        Question question;

                        if (qDto.Id > 0)
                        {
                            question = await _context.Questions
                                .Include(q => q.Options)
                                .FirstOrDefaultAsync(q => q.Id == qDto.Id && q.SurveyId == id);

                            if (question == null)
                                continue;

                            question.QuestionType = qDto.QuestionType;
                            question.Title = qDto.Title.Trim();
                            question.Description = qDto.Description?.Trim();
                            question.IsRequired = qDto.IsRequired;
                            question.SortOrder = qDto.SortOrder;
                            question.Config = qDto.Config;

                            await UpdateQuestionOptions(question, qDto.Options);
                        }
                        else
                        {
                            question = new Question
                            {
                                SurveyId = id,
                                QuestionType = qDto.QuestionType,
                                Title = qDto.Title.Trim(),
                                Description = qDto.Description?.Trim(),
                                IsRequired = qDto.IsRequired,
                                SortOrder = qDto.SortOrder,
                                Config = qDto.Config,
                                CreatedAt = DateTime.UtcNow,
                            };
                            _context.Questions.Add(question);
                            await _context.SaveChangesAsync();

                            if (qDto.Options != null && qDto.Options.Any())
                            {
                                int optSort = 0;
                                foreach (var oDto in qDto.Options)
                                {
                                    var option = new QuestionOption
                                    {
                                        QuestionId = question.Id,
                                        OptionText = oDto.OptionText.Trim(),
                                        OptionValue = oDto.OptionValue,
                                        SortOrder = optSort++,
                                        CreatedAt = DateTime.UtcNow,
                                    };
                                    _context.QuestionOptions.Add(option);
                                }
                                await _context.SaveChangesAsync();
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "问卷更新成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"更新问卷失败，ID: {id}");
                return StatusCode(500, new { message = "更新失败，请稍后重试" });
            }
        }

        /// <summary>
        /// 更新题目的选项（处理新增、更新、删除）
        /// </summary>
        private async Task UpdateQuestionOptions(Question question, List<UpdateOptionDto>? optionDtos)
        {
            if (optionDtos == null) return;

            var existingOptionIds = question.Options.Select(o => o.Id).ToList();
            var updatedOptionIds = optionDtos.Where(o => o.Id > 0).Select(o => o.Id).ToList();

            var idsToDelete = existingOptionIds.Except(updatedOptionIds).ToList();
            if (idsToDelete.Any())
            {
                var optionsToDelete = _context.QuestionOptions.Where(o => idsToDelete.Contains(o.Id));
                _context.QuestionOptions.RemoveRange(optionsToDelete);
            }

            int sortOrder = 0;
            foreach (var oDto in optionDtos.OrderBy(o => o.SortOrder))
            {
                if (oDto.Id > 0)
                {
                    var option = question.Options.FirstOrDefault(o => o.Id == oDto.Id);
                    if (option != null)
                    {
                        option.OptionText = oDto.OptionText.Trim();
                        option.OptionValue = oDto.OptionValue;
                        option.SortOrder = sortOrder++;
                    }
                }
                else
                {
                    var option = new QuestionOption
                    {
                        QuestionId = question.Id,
                        OptionText = oDto.OptionText.Trim(),
                        OptionValue = oDto.OptionValue,
                        SortOrder = sortOrder++,
                        CreatedAt = DateTime.UtcNow,
                    };
                    _context.QuestionOptions.Add(option);
                }
            }
        }

        // ============================================================
        // 7. 发布问卷（需要权限）
        // ============================================================
        [HttpPost("{id}/publish")]
        public async Task<IActionResult> PublishSurvey(int id)
        {
            try
            {
                var userId = User.GetUserId();

                // ✅ 权限校验
                if (!await PermissionHelper.HasPermission(_context, userId, AdminPermission.Survey_Manage))
                    return Forbid("您没有管理问卷的权限");

                var survey = await _context.Surveys
                    .Include(s => s.Questions)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (survey == null)
                    return NotFound(new { message = "问卷不存在" });

                if (survey.Status != 0)
                    return BadRequest(new { message = "只有草稿状态的问卷可以发布" });

                if (survey.Questions.Count == 0)
                    return BadRequest(new { message = "问卷至少需要一个问题" });

                if (DateTime.UtcNow > survey.EndTime)
                    return BadRequest(new { message = "问卷已过结束时间，请修改时间后发布" });

                survey.Status = 1;
                survey.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "问卷发布成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"发布问卷失败，ID: {id}");
                return StatusCode(500, new { message = "发布失败，请稍后重试" });
            }
        }

        // ============================================================
        // 8. 结束问卷（需要权限）
        // ============================================================
        [HttpPost("{id}/close")]
        public async Task<IActionResult> CloseSurvey(int id)
        {
            try
            {
                var userId = User.GetUserId();

                // ✅ 权限校验
                if (!await PermissionHelper.HasPermission(_context, userId, AdminPermission.Survey_Manage))
                    return Forbid("您没有管理问卷的权限");

                var survey = await _context.Surveys.FindAsync(id);
                if (survey == null)
                    return NotFound(new { message = "问卷不存在" });

                if (survey.Status != 1)
                    return BadRequest(new { message = "只有发布中的问卷可以结束" });

                survey.Status = 2;
                survey.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "问卷已结束" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"结束问卷失败，ID: {id}");
                return StatusCode(500, new { message = "操作失败，请稍后重试" });
            }
        }

        // ============================================================
        // 9. 删除问卷（需要权限）
        // ============================================================
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSurvey(int id)
        {
            try
            {
                var userId = User.GetUserId();

                // ✅ 权限校验
                if (!await PermissionHelper.HasPermission(_context, userId, AdminPermission.Survey_Manage))
                    return Forbid("您没有管理问卷的权限");

                var survey = await _context.Surveys.FindAsync(id);
                if (survey == null)
                    return NotFound(new { message = "问卷不存在" });

                // ✅ 修改：草稿和已结束的问卷可以删除（发布中的不能删）
                if (survey.Status == 1)
                    return BadRequest(new { message = "发布中的问卷不能删除，请先结束" });

                _context.Surveys.Remove(survey);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "问卷删除成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"删除问卷失败，ID: {id}");
                return StatusCode(500, new { message = "删除失败，请稍后重试" });
            }
        }

        // ============================================================
        // 10. 获取问卷统计结果（无需权限）
        // ============================================================
        [HttpGet("{id}/stats")]
        public async Task<IActionResult> GetSurveyStats(int id)
        {
            try
            {
                var survey = await _context.Surveys
                    .Include(s => s.Questions)
                        .ThenInclude(q => q.Options)
                    .Include(s => s.Submissions)
                        .ThenInclude(s => s.Answers)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (survey == null)
                    return NotFound(new { message = "问卷不存在" });

                var validSubmissions = survey.Submissions.Where(s => s.IsValid).ToList();
                var totalSubmissions = validSubmissions.Count;

                var stats = new SurveyStatsOverviewDto
                {
                    TotalSubmissions = totalSubmissions,
                    TotalQuestions = survey.Questions.Count,
                    AvgCompletionTime = validSubmissions
                        .Where(s => s.CompletedTime.HasValue)
                        .Select(s => s.CompletedTime.Value)
                        .DefaultIfEmpty(0)
                        .Average(),
                    QuestionStats = new List<QuestionStatsDto>()
                };

                foreach (var question in survey.Questions.OrderBy(q => q.SortOrder))
                {
                    var qStats = new QuestionStatsDto
                    {
                        QuestionId = question.Id,
                        Title = question.Title,
                        QuestionType = question.QuestionType,
                        TotalAnswers = validSubmissions.Count(s => s.Answers.Any(a => a.QuestionId == question.Id)),
                        SkipCount = validSubmissions.Count(s => !s.Answers.Any(a => a.QuestionId == question.Id))
                    };

                    switch (question.QuestionType)
                    {
                        case 1:
                        case 2:
                            var optionStats = new List<OptionStatsDto>();
                            foreach (var opt in question.Options)
                            {
                                var count = validSubmissions
                                    .SelectMany(s => s.Answers)
                                    .Where(a => a.QuestionId == question.Id && !string.IsNullOrEmpty(a.SelectedOptionIds))
                                    .SelectMany(a => JsonSerializer.Deserialize<List<int>>(a.SelectedOptionIds) ?? new List<int>())
                                    .Count(id => id == opt.Id);

                                optionStats.Add(new OptionStatsDto
                                {
                                    OptionId = opt.Id,
                                    OptionText = opt.OptionText,
                                    Count = count,
                                    Percentage = totalSubmissions > 0
                                        ? Math.Round((double)count / totalSubmissions * 100, 2)
                                        : 0
                                });
                            }
                            qStats.OptionStats = optionStats;
                            break;

                        case 3:
                            qStats.TextAnswers = validSubmissions
                                .SelectMany(s => s.Answers)
                                .Where(a => a.QuestionId == question.Id && !string.IsNullOrEmpty(a.AnswerText))
                                .Select(a => a.AnswerText!)
                                .ToList();
                            break;

                        case 4:
                            var scores = validSubmissions
                                .SelectMany(s => s.Answers)
                                .Where(a => a.QuestionId == question.Id && !string.IsNullOrEmpty(a.AnswerText))
                                .Select(a => int.TryParse(a.AnswerText, out int val) ? val : 0)
                                .ToList();

                            if (scores.Any())
                            {
                                qStats.AverageScore = Math.Round(scores.Average(), 2);
                                qStats.MinScore = scores.Min();
                                qStats.MaxScore = scores.Max();
                                qStats.ScoreDistribution = scores
                                    .GroupBy(s => s)
                                    .ToDictionary(g => g.Key, g => g.Count());
                            }
                            break;

                        case 5:
                            var rankDict = new Dictionary<int, List<int>>();
                            foreach (var opt in question.Options)
                            {
                                rankDict[opt.Id] = new List<int>();
                            }

                            foreach (var submission in validSubmissions)
                            {
                                var answer = submission.Answers.FirstOrDefault(a => a.QuestionId == question.Id);
                                if (answer != null && !string.IsNullOrEmpty(answer.SortResult))
                                {
                                    var sortedIds = JsonSerializer.Deserialize<List<int>>(answer.SortResult) ?? new List<int>();
                                    for (int i = 0; i < sortedIds.Count; i++)
                                    {
                                        if (rankDict.ContainsKey(sortedIds[i]))
                                        {
                                            rankDict[sortedIds[i]].Add(i + 1);
                                        }
                                    }
                                }
                            }

                            qStats.AvgRank = rankDict
                                .Where(kv => kv.Value.Any())
                                .ToDictionary(
                                    kv => kv.Key,
                                    kv => Math.Round(kv.Value.Average(), 2)
                                );
                            break;

                        case 6:
                            var matrixAggregation = new Dictionary<string, List<double>>();
                            foreach (var submission in validSubmissions)
                            {
                                var answer = submission.Answers.FirstOrDefault(a => a.QuestionId == question.Id);
                                if (answer != null && !string.IsNullOrEmpty(answer.MatrixResult))
                                {
                                    var result = JsonSerializer.Deserialize<Dictionary<string, int>>(answer.MatrixResult) ?? new Dictionary<string, int>();
                                    foreach (var kv in result)
                                    {
                                        if (!matrixAggregation.ContainsKey(kv.Key))
                                            matrixAggregation[kv.Key] = new List<double>();
                                        matrixAggregation[kv.Key].Add(kv.Value);
                                    }
                                }
                            }

                            qStats.MatrixAverages = matrixAggregation
                                .ToDictionary(
                                    kv => kv.Key,
                                    kv => Math.Round(kv.Value.Average(), 2)
                                );
                            break;
                    }

                    stats.QuestionStats.Add(qStats);
                }

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取问卷统计失败，ID: {id}");
                return StatusCode(500, new { message = "获取统计失败，请稍后重试" });
            }
        }
    }
}