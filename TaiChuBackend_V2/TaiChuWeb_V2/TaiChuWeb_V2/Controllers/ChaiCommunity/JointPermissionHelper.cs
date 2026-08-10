using TaiChuWeb_V2.Models.ChaiCommunity.Joint;
using TaiChuWeb_V2.Models.User;

namespace TaiChuWeb_V2.Controllers.ChaiCommunity
{
    /// <summary>
    /// 联合活动权限检查助手
    /// </summary>
    public static class JointPermissionHelper
    {
        /// <summary>
        /// 是否可以编辑联合活动
        /// </summary>
        /// <param name="activity">联合活动对象</param>
        /// <param name="userId">当前用户ID</param>
        /// <param name="permissions">当前用户权限列表</param>
        /// <returns>true=可以编辑，false=不可以编辑</returns>
        public static bool CanEdit(JointActivity activity, Guid userId, List<AdminPermission> permissions)
        {
            // ✅ 管理员（SuperAdmin / JointManager）可以编辑任何联合（包括官方和用户自建）
            if (permissions.Contains(AdminPermission.SuperAdmin) ||
                permissions.Contains(AdminPermission.JointManager))
                return true;

            // 官方联合：非管理员无权编辑
            if (activity.OrganizerType == "official")
                return false;

            // 用户自建：只有作者本人可编辑（非管理员）
            return activity.OrganizerId == userId;
        }

        /// <summary>
        /// 是否可以删除联合活动（只有 SuperAdmin）
        /// </summary>
        public static bool CanDelete(List<AdminPermission> permissions)
        {
            return permissions.Contains(AdminPermission.SuperAdmin);
        }

        /// <summary>
        /// 是否可以封禁联合活动
        /// </summary>
        /// <param name="activity">联合活动对象</param>
        /// <param name="userId">当前用户ID</param>
        /// <param name="permissions">当前用户权限列表</param>
        /// <returns>true=可以封禁，false=不可以封禁</returns>
        public static bool CanBan(JointActivity activity, Guid userId, List<AdminPermission> permissions)
        {
            // 不能封禁自己的活动
            if (activity.OrganizerId == userId)
                return false;

            // JointManager 或 SuperAdmin 可以封禁
            return permissions.Contains(AdminPermission.SuperAdmin) ||
                   permissions.Contains(AdminPermission.JointManager);
        }

        /// <summary>
        /// 是否可以审核参与者
        /// </summary>
        /// <param name="activity">联合活动对象</param>
        /// <param name="userId">当前用户ID</param>
        /// <param name="permissions">当前用户权限列表</param>
        /// <returns>true=可以审核，false=不可以审核</returns>
        public static bool CanAuditParticipants(JointActivity activity, Guid userId, List<AdminPermission> permissions)
        {
            // 用户自建：作者本人审核
            if (activity.OrganizerType == "user")
                return activity.OrganizerId == userId;

            // 官方联合：JointManager 或 SuperAdmin 审核
            return permissions.Contains(AdminPermission.SuperAdmin) ||
                   permissions.Contains(AdminPermission.JointManager);
        }

        /// <summary>
        /// 是否可以创建官方联合
        /// </summary>
        public static bool CanCreateOfficial(List<AdminPermission> permissions)
        {
            return permissions.Contains(AdminPermission.SuperAdmin) ||
                   permissions.Contains(AdminPermission.JointManager);
        }

        /// <summary>
        /// 是否可以审核用户自建联合（审批发布）
        /// </summary>
        public static bool CanApproveJoint(List<AdminPermission> permissions)
        {
            return permissions.Contains(AdminPermission.SuperAdmin) ||
                   permissions.Contains(AdminPermission.JointManager);
        }

        /// <summary>
        /// 是否可以查看联合活动的管理操作（封禁、审核等）
        /// </summary>
        public static bool CanManageJoint(List<AdminPermission> permissions)
        {
            return permissions.Contains(AdminPermission.SuperAdmin) ||
                   permissions.Contains(AdminPermission.JointManager);
        }

        /// <summary>
        /// 判断用户是否是活动的作者
        /// </summary>
        public static bool IsAuthor(JointActivity activity, Guid userId)
        {
            return activity.OrganizerId == userId;
        }

        /// <summary>
        /// 判断是否是官方联合
        /// </summary>
        public static bool IsOfficial(JointActivity activity)
        {
            return activity.OrganizerType == "official";
        }

        /// <summary>
        /// 判断是否需要审核（用户自建且状态为 pending）
        /// </summary>
        public static bool NeedsApproval(JointActivity activity)
        {
            return activity.OrganizerType == "user" &&
                   activity.ApprovalStatus == "pending";
        }

        /// <summary>
        /// 判断是否已审核通过
        /// </summary>
        public static bool IsApproved(JointActivity activity)
        {
            return activity.ApprovalStatus == "approved";
        }
    }
}