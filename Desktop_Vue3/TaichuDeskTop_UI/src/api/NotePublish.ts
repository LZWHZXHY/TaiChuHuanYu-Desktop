// src/api/NotePublish.ts
import request from '../utils/request'; // 🌟 接入你带 Bearer Token 的请求拦截器
// 🌟 1. 引入你刚刚修改好的前端底层统一强类型定义
import type { NoteType } from '../utils/NoteType'; 

// 严格与后端 PublishedNote 映射的数据接口定义
export interface PublishedNoteItem {
  id: string;
  title: string;
  // 🌟 2. 动态复用全局 NoteType（现在它已经是 note | post | wiki...），完美消灭 'thought' 遗毒
  type: NoteType; 
  spaceId: string;
  publishedAt?: string;
  createdAt?: string;
  resonance: number;
  excerpt?: string;
  authorName?: string; // 补齐广场所需的作者名字段
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
   * 🌟 广场公共信息流：获取已发布的公开帖子/简语
   * 使用 unknown as Promise<T> 抹平 Axios 拦截器剥离 response.data 后的类型差异
   */
  getPublicStream(params: { type?: string, page?: number, pageSize?: number } = {}) {
    const searchParams = new URLSearchParams();
    
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
   * 🌟 空间中创建草稿笔记：同步将创建形态从 'thought' 转换为 'post'
   */
  createDraft(data: { spaceId: string; type: 'note' | 'post'; title: string; folderId: string | null; sortOrder: string }) {
    return request.post('/LingMai', data) as unknown as Promise<{ id: string }>;
  },

  /**
   * 🌟 同步草稿的内容块 (Blocks)
   */
  syncDraftBlocks(data: { noteId: string; title: string; blocks: Array<{ id: string; type: string; data: string; sortOrder: string }> }) {
    return request.post('/LingMai/sync', data) as unknown as Promise<{ success: boolean }>;
  },

  /**
   * 🌟 一键物理隔离拷贝发布到广场：同步将发布形态从 'thought' 转换为 'post'
   */
  publishNote(id: string, type: 'note' | 'post' = 'note') {
    return request.post(`/LingMaiPublish/notes/${id}/publish?type=${type}`) as unknown as Promise<{ success: boolean; message: string; isPublic: boolean }>;
  }
};

export default notePublishApi;