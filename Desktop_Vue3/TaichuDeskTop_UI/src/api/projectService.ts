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
  // 🌟 注入新意图（创建新任务）
  createTask: (projectId: string, data: CreateTaskDto) =>
    request.post(`Project/${projectId}/kanban/tasks`, data), // 👈 补上 /kanban 路径段（注意保持和你其他接口一样的 Project/ 前缀风格）


  updateTaskStatus: (projectId: string, taskId: string, data: MoveTaskDto) =>
    request.put(`/project/${projectId}/tasks/${taskId}/move`, data),

  getKanbanBoard: (projectId: string) =>
    request.get<{ board: any[], unclassified: any[] }>(`/project/${projectId}/kanban/board`),

  // 🌟 添加新的画布分栏
  createKanbanCategory: (projectId: string, data: { name: string, colorCode?: string }) =>
    request.post(`/project/${projectId}/kanban/categories`, data),

  // 🌟 修改分栏
  updateKanbanCategory: (projectId: string, categoryId: string, data: { name?: string, colorCode?: string }) =>
    request.put(`Project/${projectId}/kanban/categories/${categoryId}`, data),

  // 🌟 删除分栏
  deleteKanbanCategory: (projectId: string, categoryId: string) =>
    request.delete(`/project/${projectId}/kanban/categories/${categoryId}`),

  // 🌟 核心：拖拽任务（跨栏 + 精准插队排序）
  moveKanbanTask: (projectId: string, taskId: string, data: { targetCategoryId: string | null, prevSortOrder: number | null, nextSortOrder: number | null }) =>
    request.put(`/project/${projectId}/kanban/tasks/${taskId}/move`, data),
  // 🌟 全量/局部更新任务详情
  updateTaskDetails: (projectId: string, taskId: string, data: any) =>
    request.put(`Project/${projectId}/kanban/tasks/${taskId}`, data),

  // 🌟 获取项目成员 (用于指派下拉菜单)
  getProjectMembers: (projectId: string) =>
    request.get(`Project/${projectId}/members`),

  
};

export default projectService;