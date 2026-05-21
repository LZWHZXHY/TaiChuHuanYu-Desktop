// src/api/NotePublish.ts
import request from '../utils/request'; // 🌟 接入你带 Bearer Token 的请求拦截器

// 严格与后端 PublishedNote 映射的数据接口定义
export interface PublishedNoteItem {
  id: string;
  title: string;
  type: 'note' | 'thought';
  spaceId: string;
  publishedAt?: string;
  createdAt?: string;
  resonance: number;
  excerpt?: string;
}

export interface PublishedNoteDetail extends PublishedNoteItem {
  blocks: {
    id: string;
    type: string;
    data: string;
    sortOrder: number;
  }[];
}

export const notePublishApi = {
    getPublicBlog(id: string) {
    return request.get(`/LingMaiPublish/blog/${id}`) as unknown as Promise<PublishedNoteDetail>;
  },
  /**
   * 🌟 1. 广场公共信息流：获取已发布的公开帖子/简语
   * 使用 unknown as Promise<T> 抹平 Axios 拦截器剥离 response.data 后的类型差异
   */
  // 在参数最后加上 = {} 作为默认空对象
getPublicStream(params: { type?: string, page?: number, pageSize?: number } = {}) {
  const searchParams = new URLSearchParams();
  
  // 加上 params 存在性校验，绝对安全
  if (params && params.type) {
    searchParams.append('type', params.type);
  }
  
  const page = params?.page || 1;
  const pageSize = params?.pageSize || 20;
  
  searchParams.append('page', page.toString());
  searchParams.append('pageSize', pageSize.toString());
  
  return request.get(`/LingMaiPublish/public-stream?${searchParams.toString()}`) as unknown as Promise<PublishedNoteItem[]>;
},

  /**
   * 🌟 2. 在空间中创建草稿笔记
   */
  createDraft(data: { spaceId: string; type: 'note' | 'thought'; title: string; folderId: string | null; sortOrder: string }) {
    return request.post('/LingMai', data) as unknown as Promise<{ id: string }>;
  },

  /**
   * 🌟 3. 同步草稿的内容块 (Blocks)
   */
  syncDraftBlocks(data: { noteId: string; title: string; blocks: Array<{ id: string; type: string; data: string; sortOrder: string }> }) {
    return request.post('/LingMai/sync', data) as unknown as Promise<{ success: boolean }>;
  },

  /**
   * 🌟 4. 将刚才创建的草稿一键物理隔离拷贝发布到广场
   */
  publishNote(id: string, type: 'note' | 'thought' = 'note') {
    return request.post(`/LingMaiPublish/notes/${id}/publish?type=${type}`) as unknown as Promise<{ success: boolean; message: string; isPublic: boolean }>;
  }
};

export default notePublishApi;