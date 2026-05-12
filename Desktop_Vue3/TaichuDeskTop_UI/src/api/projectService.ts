import request from '../utils/request';

/**
 * --- 🚀 项目元数据类型定义 ---
 */

// 项目列表项与设置详情
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

/**
 * --- 🛠️ 项目管理 API 服务 ---
 * 专注于项目本身的管理：公开性、描述、成员统计等
 */
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
    request.delete(`Project/${projectId}`)
};

export default projectService;