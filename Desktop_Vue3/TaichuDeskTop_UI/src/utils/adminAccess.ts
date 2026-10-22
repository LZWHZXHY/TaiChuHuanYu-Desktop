// 与后台各管理接口的权限范围保持一致；最终授权由服务端完成。
export const adminPermissions = ['SuperAdmin', 'Trade_Manage', 'User_Audit', 'Wiki_Editor', 'System_Monitor']
export const canOpenAdmin = (permissions: string[]) => adminPermissions.some(permission => permissions.includes(permission))
