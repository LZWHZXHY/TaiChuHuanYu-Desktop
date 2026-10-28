using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaiChuWeb_V2.Controllers.ChaiCommunity;
using TaiChuWeb_V2.Dtos.Chai.Battle;
using TaiChuWeb_V2.Dtos.Chai.Joint;
using TaiChuWeb_V2.Models.ChaiCommunity;
using TaiChuWeb_V2.Models.ChaiCommunity.Battle;
using TaiChuWeb_V2.Models.ChaiCommunity.Joint;
using TaiChuWeb_V2.Controllers.Club;
using TaiChuWeb_V2.Models.Club;

static class JointBattleChecks
{
    public static async Task<int> Run()
    {
        int checks = 0;
        void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
        var owner = Guid.NewGuid(); var guest = Guid.NewGuid(); var teammate = Guid.NewGuid();
        ControllerContext Login(Guid user) => new() { HttpContext = new DefaultHttpContext {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.ToString()), new Claim(ClaimTypes.Name, "测试") }, "test")) } };
        var db = new TestContext();
        var joints = new TestSet<JointActivity>(); db.JointActivities = joints;
        var activity = new JointActivity { Id = Guid.NewGuid(), OrganizerId = owner, Title = "联创", Description = "说明", StartDate = DateTime.UtcNow.AddDays(1), EndDate = DateTime.UtcNow.AddDays(2), OrganizerType = "user", Status = "open" };
        joints.Rows.Add(activity);
        var joint = new JointController(db) { ControllerContext = Login(owner) };
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var omitted = JsonSerializer.Deserialize<UpdateJointRequest>("{\"contact\":\"\"}", jsonOptions)!;
        Check(!omitted.EndDateSpecified, "omitted end date remains unspecified");
        Check((await joint.Update(activity.Id, omitted)).Result is OkObjectResult && activity.EndDate != null, "partial edit preserves end date");
        var clear = JsonSerializer.Deserialize<UpdateJointRequest>("{\"endDate\":null}", jsonOptions)!;
        Check(clear.EndDateSpecified, "explicit null is tracked");
        Check((await joint.Update(activity.Id, clear)).Result is OkObjectResult && activity.EndDate == null, "edit clears optional end date");
        Check((await joint.Update(activity.Id, new() { Title = "不应保存", EndDate = activity.StartDate })).Result is BadRequestObjectResult && activity.Title == "联创", "invalid dates leave fields unchanged");
        joint.ControllerContext = Login(guest);
        Check((await joint.Update(activity.Id, new() { Title = "越权" })).Result is ForbidResult, "non-owner cannot edit");

        var battles = new TestSet<Battle>(); db.Battles = battles;
        var ocs = new TestSet<StickmanCharacter>(); db.StickmanCharacters = ocs;
        StickmanCharacter Oc(Guid author) { var oc = new StickmanCharacter { Id = Guid.NewGuid(), AuthorId = author, Title = "测试OC", Status = "published", IsBattleEnabled = true }; ocs.Rows.Add(oc); return oc; }
        var ownOc = Oc(owner); var guestOc = Oc(guest); var mateOc = Oc(teammate);
        var controller = new BattleController(db) { ControllerContext = Login(owner) };
        CreateBattleRequest Request() => new() { Title = "流程验收", BattleType = "2v1", Rules = "测试", JudgmentType = "internal", ChallengerOcIds = new() { ownOc.Id }, OpponentOcIds = new() { [guest] = new() { guestOc.Id }, [teammate] = new() { mateOc.Id } }, BattleConfigJson = JsonSerializer.Serialize(new { teams = new Dictionary<Guid, int> { [owner] = 1, [guest] = 2, [teammate] = 1 }, custom = "preserved" }) };
        mateOc.Status = "draft";
        Check((await controller.Create(Request())).Result is BadRequestObjectResult && battles.Rows.Count == 0, "validate second invited user's OC");
        mateOc.Status = "published";
        var created = await controller.Create(Request());
        Check(created.Result is OkObjectResult, "create multi-user invitation");
        var battle = battles.Rows.Single();
        Check(battle.Participants.Count == 1 && battle.Participants.Single().TeamNumber == 1, "invitation is not automatic participation");
        Check(battle.BattleConfigJson.Contains("preserved"), "custom configuration survives");
        Check((await controller.Unregister(battle.Id)).Result is BadRequestObjectResult && battle.Participants.Count == 1, "initiator cannot unregister and transfer ownership");
        controller.ControllerContext = Login(guest);
        Check((await controller.Reject(battle.Id)).Result is OkObjectResult, "guest rejects own invitation");
        Check(!battle.IsPublic && JsonSerializer.Deserialize<Dictionary<Guid, List<Guid>>>(battle.OpponentOcIds!)!.ContainsKey(teammate), "other invitation survives rejection");
        Check((await controller.Register(battle.Id, new() { OcIds = new() { guestOc.Id } })).Result is BadRequestObjectResult, "removed invitation cannot register while private");
        controller.ControllerContext = Login(teammate);
        var conflict = new Battle { Id = "other", Participants = new List<BattleParticipant> { new() { UserId = teammate, OcIdsJson = JsonSerializer.Serialize(new[] { mateOc.Id }) } } };
        battles.Rows.Add(conflict);
        Check((await controller.Register(battle.Id, new() { OcIds = new() { mateOc.Id } })).Result is BadRequestObjectResult, "registration rejects OC active elsewhere");
        battles.Rows.Remove(conflict);
        Check((await controller.Register(battle.Id, new() { OcIds = new() { mateOc.Id } })).Result is OkObjectResult, "invited teammate accepts");
        Check(battle.Participants.Single(p => p.UserId == teammate).TeamNumber == 1, "teammate remains on team A");
        Check((await controller.Reject(battle.Id)).Result is BadRequestObjectResult, "participant cannot reject accepted invitation");
        Check((await controller.CloseRegistration(battle.Id)).Result is ForbidResult, "participant cannot advance organizer workflow");
        controller.ControllerContext = Login(owner);
        Check((await controller.CloseRegistration(battle.Id)).Result is OkObjectResult && battle.Status == "ongoing", "owner starts creation after two participants");
        Check((await controller.SubmitWork(battle.Id, new() { Title = "作品", ContentUrl = "https://example.invalid/work" })).Result is OkObjectResult, "participant submits work");
        Check((await controller.SubmitWork(battle.Id, new() { Title = "重复", ContentUrl = "https://example.invalid/work" })).Result is BadRequestObjectResult, "duplicate submission rejected");
        Check((await controller.CloseCreation(battle.Id)).Result is OkObjectResult && battle.Status == "judging", "owner advances to judging");
        Check((await controller.SetInternalResult(battle.Id, new() { WinnerIds = new() { battle.Participants.First().Id } })).Result is OkObjectResult, "owner records internal result");
        Check((await controller.PublishResult(battle.Id)).Result is OkObjectResult && battle.Status == "finished", "owner publishes recorded result");
        // 只验证派单权限与前置状态；不进入计价、结算或退款分支。
        var club = new TestContext();
        var services = new TestSet<CustomerService>(); club.CustomerServices = services;
        var orders = new TestSet<ClubOrder>(); club.ClubOrders = orders;
        var cs = new CsController(club) { ControllerContext = Login(owner) };
        Check(await cs.AssignOperator("test", new()) is BadRequestObjectResult, "non-CS account cannot assign");
        Check(await cs.ReassignOperators("test", new()) is BadRequestObjectResult, "non-CS account cannot reassign");
        var service = new CustomerService { UserId = owner, AuditStatus = "approved" }; services.Rows.Add(service);
        var order = new ClubOrder { OrderNo = "test", CustomerServiceId = Guid.NewGuid(), Status = "pending" }; orders.Rows.Add(order);
        Check(await cs.AssignOperator("test", new()) is ObjectResult { StatusCode: 403 }, "CS cannot assign another service's order");
        order.CustomerServiceId = service.Id;
        Check(await cs.AssignOperator("test", new()) is BadRequestObjectResult, "unconfirmed payment blocks assignment");
        Check(await cs.ReassignOperators("test", new() { Reason = "测试" }) is BadRequestObjectResult, "pending order cannot reassign");
        Check(club.Saves == 0, "CS permission checks never write or settle orders");
        var projectDb = new TestContext();
        var documents = new TestSet<TaiChuWeb_V2.Models.Project.ProjectDocument>(); projectDb.ProjectDocuments = documents;
        var project = new TaiChuWeb_V2.Models.Project.Project { Id = "shared", OwnerId = owner, Name = "协作" };
        projectDb.ProjectRows.Rows.Add(project);
        projectDb.MemberRows.Rows.Add(new() { ProjectId = project.Id, UserId = owner.ToString() });
        projectDb.MemberRows.Rows.Add(new() { ProjectId = project.Id, UserId = guest.ToString() });
        var note = new TaiChuWeb_V2.Models.LingMai.Note { Id = Guid.NewGuid(), AuthorId = owner.ToString(), Type = "doc", Title = "内部文档" };
        projectDb.NoteRows.Rows.Add(note);
        projectDb.BlockRows.Rows.Add(new() { Id = "p", OwnerId = note.Id.ToString(), OwnerType = "doc", Type = "paragraph", Data = "{}" });
        var publisher = new TaiChuWeb_V2.Services.Publish.DocPublishHandler(projectDb);
        Check(await publisher.ExecutePublishAsync(note.Id, teammate.ToString(), null, project.Id) is ForbidResult && projectDb.Saves == 0, "non-member cannot share into project");
        Check(await publisher.ExecutePublishAsync(note.Id, owner.ToString(), null, project.Id) is OkObjectResult && !note.IsPublic, "project sharing keeps private note private");
        await publisher.ExecutePublishAsync(note.Id, owner.ToString(), null, project.Id);
        Check(documents.Rows.Count == 1, "sharing twice creates one link");
        note.IsPublic = true;
        await publisher.ExecutePublishAsync(note.Id, owner.ToString(), null, project.Id);
        Check(note.IsPublic, "sharing preserves an already-public note");
        var reader = new TaiChuWeb_V2.Controllers.Projects.ProjectController(projectDb) { ControllerContext = Login(guest) };
        var detail = await reader.GetProjectDocument(project.Id, note.Id);
        Check(detail is OkObjectResult && JsonSerializer.Serialize(((OkObjectResult)detail).Value).Contains("paragraph"), "project member reads another author's shared blocks");
        Check(await reader.GetProjectDocument(project.Id, Guid.NewGuid()) is NotFoundObjectResult, "project endpoint cannot read unshared notes");
        note.Status = 2;
        Check(await reader.GetProjectDocument(project.Id, note.Id) is NotFoundObjectResult, "removed note cannot be read");
        note.Status = 0;
        projectDb.MemberRows.Rows.RemoveAll(m => m.UserId == guest.ToString());
        Check(await reader.GetProjectDocument(project.Id, note.Id) is ForbidResult, "revoked membership loses shared reading access");
        projectDb.SpaceRows.Rows.Add(new() { Id = note.SpaceId, UserId = owner.ToString() });
        var publication = new TaiChuWeb_V2.Controllers.LingMai.LingMaiPublishController(projectDb, new[] { publisher }, new TaiChuWeb_V2.Services.LingMai.NoteCommentService(projectDb)) { ControllerContext = Login(guest) };
        Check(await publication.UnpublishNote(note.Id) is ForbidResult && documents.Rows.Count == 1, "reader cannot withdraw author's shared document");
        publication.ControllerContext = Login(owner);
        Check(await publication.UnpublishNote(note.Id) is OkObjectResult && documents.Rows.Count == 0 && note.IsPublic, "unshare removes project links without changing public status");
        reader.ControllerContext = Login(owner);
        Check(await reader.GetProjectDocument(project.Id, note.Id) is NotFoundObjectResult, "withdrawn document is no longer readable from project");
        return checks;
    }
}
