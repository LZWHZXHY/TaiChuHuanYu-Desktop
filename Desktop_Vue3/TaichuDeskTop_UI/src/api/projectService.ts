import request from '../utils/request';

export interface ProjectMetadata {
  id: string;
  name: string;
  description: string;
  isPublic: boolean;
  joinPolicy: number; // 0=仅限邀请, 1=允许申请, 2=自由加入
  myRole: number;     // 0=Owner, 1=Dev, 2=Viewer
  memberCount: number;
  taskCount?: number;
  createdAt: string;
  startTime?: string;
  endTime?: string;
  status: number;
}

// 修改项目时使用的传输对象
export interface UpdateProjectDto {
  name?: string;
  description?: string;
  isPublic?: boolean;
  joinPolicy?: number;
}

export interface ProjectTaskMetadata {
  id: string;
  title: string;
  status: number; // 0=Todo, 1=Doing, 2=Done
  categoryId?: string | null;
  categoryName?: string | null;
  categoryColor?: string | null;
  assigneeId?: string | null;
  cost: number;
  updatedAt: string;
}

export interface CreateTaskDto {
  title: string;
  status: number;
  categoryId?: string | null;
  assigneeId?: string | null;
  cost?: number;
}

export interface MoveTaskDto {
  targetStatus: number;
  prevSortOrder?: number | null;
  nextSortOrder?: number | null;
}

// 🌟 新增：成员管理相关的 Dto 声明
// 在 projectService.ts 的类型声明区更新 Dto
export interface InviteMemberDto {
  usernameOrId: string; // 🌟 接收用户名或唯一 ID 字符串
}



export interface UpdateMemberRoleDto {
  roleValue: number; // 0=owner, 1=admin, 2=editor, 3=executor, 4=viewer
}


const projectService = {

  getPublicProjects: () => request.get<any[]>('Project/public'),

  // 1. 获取“我的项目”列表 (用于 index.vue)
  getMyProjects: () => 
    request.get<ProjectMetadata[]>('Project/my'),

  // 2. 开启新项目
  createProject: (data: { name: string; description: string; isPublic: boolean }) => 
    request.post<ProjectMetadata>('Project/create', data),

  // 3. 获取项目的详细设置与统计 (用于设置页面)
  getProjectSettings: (projectId: string) =>
    request.get<ProjectMetadata>(`Project/${projectId}/settings`),

  // 4. 修改项目元数据 (更名、描述、公开性、准入策略)
  updateProject: (projectId: string, data: UpdateProjectDto) =>
    request.patch<ProjectMetadata>(`Project/${projectId}`, data),

  // 5. 彻底解散/抹除项目
  deleteProject: (projectId: string) =>
    request.delete(`Project/${projectId}`),

  getProjectTasks: (projectId: string) =>
    request.get<ProjectTaskMetadata[]>(`/project/${projectId}/tasks`),

  // 7. 注入新意图（创建新任务）
  createTask: (projectId: string, data: CreateTaskDto) =>
    request.post(`Project/${projectId}/kanban/tasks`, data), 


  updateTaskStatus: (projectId: string, taskId: string, data: MoveTaskDto) =>
    request.put(`/project/${projectId}/tasks/${taskId}/move`, data),

  getKanbanBoard: (projectId: string) =>
    request.get<{ board: any[], unclassified: any[] }>(`/project/${projectId}/kanban/board`),

  // 添加新的画布分栏
  createKanbanCategory: (projectId: string, data: { name: string, colorCode?: string }) =>
    request.post(`/project/${projectId}/kanban/categories`, data),

  // 修改分栏
  updateKanbanCategory: (projectId: string, categoryId: string, data: { name?: string, colorCode?: string }) =>
    request.put(`Project/${projectId}/kanban/categories/${categoryId}`, data),

  // 删除分栏
  deleteKanbanCategory: (projectId: string, categoryId: string) =>
    request.delete(`/project/${projectId}/kanban/categories/${categoryId}`),

  // 核心：拖拽任务（跨栏 + 精准插队排序）
  moveKanbanTask: (projectId: string, taskId: string, data: { targetCategoryId: string | null, prevSortOrder: number | null, nextSortOrder: number | null }) =>
    request.put(`/project/${projectId}/kanban/tasks/${taskId}/move`, data),
  
  // 全量/局部更新任务详情
  updateTaskDetails: (projectId: string, taskId: string, data: any) =>
    request.put(`Project/${projectId}/kanban/tasks/${taskId}`, data),

  /* ========================================================
     🌟 新增/完善：共建者团队管理模块 (无缝对接后端 Controller 路由)
     ======================================================== */

  // 获取项目共建者列表
  getProjectMembers: (projectId: string) =>
    request.get<any[]>(`Project/${projectId}/members`),

  // 邀请新成员加入灵脉
  inviteMember: (projectId: string, data: InviteMemberDto) =>
  request.post(`Project/${projectId}/members/invite`, data),

  // 变更共建者角色等级
  updateMemberRole: (projectId: string, memberId: string, data: UpdateMemberRoleDto) =>
    request.put(`Project/${projectId}/members/${memberId}/role`, data),

  // 将共建者移出项目
  removeMember: (projectId: string, memberId: string) =>
    request.delete(`Project/${projectId}/members/${memberId}`),
  
};

export default projectService;