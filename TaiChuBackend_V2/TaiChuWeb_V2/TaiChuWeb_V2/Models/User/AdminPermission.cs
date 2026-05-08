namespace TaiChuWeb_V2.Models.User
{
    public enum AdminPermission
    {
        SuperAdmin,    // 核心中枢：拥有所有权限
        Trade_Manage,  // 交易行：上架、补货、调价
        User_Audit,    // 用户：EXP发放、存储配额审计
        Wiki_Editor,   // 知识库：审核、修订
        System_Monitor // 系统：查看负载、日志
    }
}
