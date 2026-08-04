// 文件：src/views/活动中心/活动中心组件/storage.ts

// ------ 类型定义（与后端 DTO 保持一致） ------
export interface Activity {
  id: number;
  title: string;
  owner: string;
  cycle: string;
  participants: number;
  type: string;
  status: '进行中' | '招募中' | '已结束';
  desc: string;
  cover: string;
  completedRate: number;
  days: number;
  createdAt: number;
}

export interface RecordItem {
  day: number;
  isCompleted: boolean;
  isLate: boolean;
  text: string;
  image: string;
}

export interface Member {
  id: number;
  name: string;
  active: boolean;
  records: RecordItem[];
}

export interface Post {
  id: number;
  author: string;
  content: string;
  createdAt: number;
  replies: Reply[];
}

export interface Reply {
  id: number;
  author: string;
  content: string;
  createdAt: number;
}

// ------ API 基础配置 ------
const API_BASE = import.meta.env.VITE_API_BASE || 'https://bianyuzhou.com/api';
const TOKEN_KEY = 'token';

function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

function setToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token);
}

function removeToken(): void {
  localStorage.removeItem(TOKEN_KEY);
}

// 通用请求函数（带 token）
async function apiRequest<T>(
  endpoint: string,
  options: RequestInit = {}
): Promise<T> {
  const token = getToken();
  const headers: HeadersInit = {
    'Content-Type': 'application/json',
    ...(token ? { 'Authorization': `Bearer ${token}` } : {}),
    ...options.headers,
  };

  const response = await fetch(`${API_BASE}${endpoint}`, {
    ...options,
    headers,
  });

  if (!response.ok) {
    const errorData = await response.json().catch(() => ({}));
    throw new Error(errorData.message || `请求失败: ${response.status}`);
  }

  // 如果响应是 204 No Content
  if (response.status === 204) {
    return {} as T;
  }

  return await response.json();
}

// ------ 登录/注册（新增） ------
export async function login(username: string, password: string): Promise<{ token: string; username: string; id: string }> {
  const result = await apiRequest<{ token: string; username: string; id: string }>('/auth/login', {
    method: 'POST',
    body: JSON.stringify({ username, password }),
  });
  setToken(result.token);
  return result;
}

export async function register(username: string, password: string): Promise<{ token: string; username: string; id: string }> {
  const result = await apiRequest<{ token: string; username: string; id: string }>('/auth/register', {
    method: 'POST',
    body: JSON.stringify({ username, password }),
  });
  setToken(result.token);
  return result;
}

export function logout(): void {
  removeToken();
}

// 检查是否已登录
export function isAuthenticated(): boolean {
  return !!getToken();
}

// ------ 活动相关 API ------

// 获取活动列表（支持筛选）
export async function getActivities(params?: { status?: string; keyword?: string; type?: string }): Promise<Activity[]> {
  const query = new URLSearchParams(params as any).toString();
  const endpoint = `/activities${query ? '?' + query : ''}`;
  // 后端返回的是 ActivityResponseDto 数组，需要映射成 Activity（字段名略有差异）
  const data = await apiRequest<any[]>(endpoint);
  return data.map(item => ({
    id: item.id,
    title: item.title,
    owner: item.owner,
    cycle: `${item.days}天`,
    participants: item.participants,
    type: item.type,
    status: item.status,
    desc: item.description || '',
    cover: item.cover || '',
    completedRate: item.completedRate || 0,
    days: item.days,
    createdAt: new Date(item.createdAt).getTime(),
  }));
}

// 获取单个活动详情（含成员、记录等）
export async function getActivityDetail(id: number): Promise<{
  activity: Activity;
  members: Member[];
}> {
  // 并行请求活动详情和成员列表
  const [activityData, membersData] = await Promise.all([
    apiRequest<any>(`/activities/${id}`),
    apiRequest<any[]>(`/activities/${id}/members`),
  ]);

  const activity: Activity = {
    id: activityData.id,
    title: activityData.title,
    owner: activityData.owner,
    cycle: `${activityData.days}天`,
    participants: activityData.participants,
    type: activityData.type,
    status: activityData.status,
    desc: activityData.description || '',
    cover: activityData.cover || '',
    completedRate: activityData.completedRate || 0,
    days: activityData.days,
    createdAt: new Date(activityData.createdAt).getTime(),
  };

  const members: Member[] = membersData.map((m: any) => ({
    id: m.id,
    name: m.name,
    active: false, // 由前端控制
    records: m.records.map((r: any) => ({
      day: r.day,
      isCompleted: r.isCompleted,
      isLate: r.isLate,
      text: r.text || '',
      image: r.image || '',
    })),
  }));

  return { activity, members };
}

// 获取当前用户在该活动中的打卡状态（用于判断 isJoined 和 records）
export async function getMyStatus(activityId: number): Promise<{
  isJoined: boolean;
  totalDays: number;
  elapsedDays: number;
  completedDays: number;
  completionRate: number;
  consecutiveDays: number;
  records: RecordItem[];
}> {
  const data = await apiRequest<any>(`/activities/${activityId}/my-status`);
  return {
    isJoined: data.isJoined,
    totalDays: data.totalDays,
    elapsedDays: data.elapsedDays,
    completedDays: data.completedDays,
    completionRate: data.completionRate,
    consecutiveDays: data.consecutiveDays || 0,
    records: data.records?.map((r: any) => ({
      day: r.day,
      isCompleted: r.isCompleted,
      isLate: r.isLate,
      text: r.text || '',
      image: r.image || '',
    })) || [],
  };
}

// 创建活动
export async function createActivity(data: {
  title: string;
  description?: string;
  type: string;
  cover?: string;
  days: number;
}): Promise<{ id: number; title: string; message: string }> {
  return await apiRequest('/activities', {
    method: 'POST',
    body: JSON.stringify(data),
  });
}

// 更新活动
export async function updateActivity(id: number, data: Partial<{
  title: string;
  description: string;
  type: string;
  status: string;
  cover: string;
  days: number;
}>): Promise<{ message: string }> {
  return await apiRequest(`/activities/${id}`, {
    method: 'PUT',
    body: JSON.stringify(data),
  });
}

// 删除活动
export async function deleteActivity(id: number): Promise<{ message: string }> {
  return await apiRequest(`/activities/${id}`, {
    method: 'DELETE',
  });
}

// 加入活动
export async function joinActivity(id: number): Promise<{ isJoined: boolean; membersCount: number; message: string }> {
  return await apiRequest(`/activities/${id}/join`, {
    method: 'POST',
  });
}

// 退出活动
export async function leaveActivity(id: number): Promise<{ isJoined: boolean; membersCount: number; message: string }> {
  return await apiRequest(`/activities/${id}/leave`, {
    method: 'POST',
  });
}

// 打卡
export async function checkin(activityId: number, day: number, text?: string, image?: string): Promise<{
  id: number;
  day: number;
  isCompleted: boolean;
  isLate: boolean;
  text: string;
  image: string;
  createdAt: string;
}> {
  return await apiRequest(`/activities/${activityId}/checkin`, {
    method: 'POST',
    body: JSON.stringify({ day, text, image }),
  });
}

// 获取活动统计数据（打卡率、连续打卡等）
export async function getStats(activityId: number): Promise<{
  totalDays: number;
  elapsedDays: number;
  completionRate: number;
  consecutiveDays: number;
  rank: number;
}> {
  return await apiRequest(`/activities/${activityId}/stats`);
}

// ------ 讨论区 API ------

// 获取帖子列表
export async function getPosts(activityId: number): Promise<Post[]> {
  const data = await apiRequest<any[]>(`/activities/${activityId}/posts`);
  return data.map((p: any) => ({
    id: p.id,
    author: p.author,
    content: p.content,
    createdAt: new Date(p.createdAt).getTime(),
    replies: p.replies?.map((r: any) => ({
      id: r.id,
      author: r.author,
      content: r.content,
      createdAt: new Date(r.createdAt).getTime(),
    })) || [],
  }));
}

// 发布帖子
export async function createPost(activityId: number, content: string): Promise<Post> {
  const data = await apiRequest<any>(`/activities/${activityId}/posts`, {
    method: 'POST',
    body: JSON.stringify({ content }),
  });
  return {
    id: data.id,
    author: data.author,
    content: data.content,
    createdAt: new Date(data.createdAt).getTime(),
    replies: data.replies?.map((r: any) => ({
      id: r.id,
      author: r.author,
      content: r.content,
      createdAt: new Date(r.createdAt).getTime(),
    })) || [],
  };
}

// 回复帖子
export async function createReply(activityId: number, postId: number, content: string): Promise<Reply> {
  const data = await apiRequest<any>(`/activities/${activityId}/posts/${postId}/replies`, {
    method: 'POST',
    body: JSON.stringify({ content }),
  });
  return {
    id: data.id,
    author: data.author,
    content: data.content,
    createdAt: new Date(data.createdAt).getTime(),
  };
}

// 删除帖子
export async function deletePost(activityId: number, postId: number): Promise<{ message: string }> {
  return await apiRequest(`/activities/${activityId}/posts/${postId}`, {
    method: 'DELETE',
  });
}

// 删除回复
export async function deleteReply(activityId: number, replyId: number): Promise<{ message: string }> {
  return await apiRequest(`/activities/${activityId}/posts/replies/${replyId}`, {
    method: 'DELETE',
  });
}

// 导出 token 相关（供组件使用）
export { getToken, setToken, removeToken };

// 注意：以下旧函数（如 generateRecords、generateId 等）不再使用，可以移除或保留为兼容，但建议移除。