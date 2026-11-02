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
  authorId?: string | null; // 作者用户 ID，点击作者名进入个人主页
  commentsCount?: number; // 评论数
  tags?: string | null; // 发布时的标签快照（JSON 数组字符串），用于识别论坛版块
  extraData?: string;
}

export interface PublishedNoteDetail extends PublishedNoteItem {
  /** 我有没有点过「灵动」 */
  resonated?: boolean;
  canEdit?: boolean;
  originalNoteId?: string;
  tags?: string;
  blocks: {
    id: string;
    type: string;
    data: string;
    sortOrder: number;
  }[];
}

// 🌟 论坛的评论区（与画廊评论同构）
export interface NoteComment {
  id: string;
  userId: string;
  authorName: string;
  avatar?: string;
  content: string;
  parentId?: string;
  replyToAuthor?: string;
  createdAt: string;
  isDeleted: boolean;
  canDelete: boolean;
}

export interface NoteCommentPage {
  items: NoteComment[];
  total: number;
  hasMore: boolean;
}

export const notePublishApi = {
  getPublicBlog(id: string) {
    return request.get(`/LingMaiPublish/blog/${id}`) as unknown as Promise<PublishedNoteDetail>;
  },
  /** 灵动（点赞）：点一下 +1，再点取消 */
  toggleResonance: (id: string, active?: boolean) =>
    request.post<{ active: boolean; count: number }>(`/LingMaiPublish/blog/${id}/resonance`, null, { params: active === undefined ? undefined : { active }, silentError: true }),

  /**
   * 🌟 论坛评论区
   */
  getComments: (id: string, offset = 0) =>
    request.get<NoteCommentPage>(`/LingMaiPublish/blog/${id}/comments`, { params: { offset, limit: 20 }, silentError: true }),
  addComment: (id: string, content: string, parentId?: string) =>
    request.post<{ id: string; commentsCount: number }>(`/LingMaiPublish/blog/${id}/comments`, { content, parentId }, { silentError: true }),
  deleteComment: (id: string, commentId: string) =>
    request.delete<{ commentsCount: number }>(`/LingMaiPublish/blog/${id}/comments/${commentId}`, { silentError: true }),

  /**
   * 🌟 广场公共信息流：获取已发布的公开帖子/简语
   * 使用 unknown as Promise<T> 抹平 Axios 拦截器剥离 response.data 后的类型差异
   */
  getPublicStream(params: { type?: string, tag?: string, sub?: string, author?: string, page?: number, pageSize?: number } = {}) {
    const searchParams = new URLSearchParams();
    if (params.author) searchParams.set('author', params.author); // 个人主页：只看某位用户的内容
    if (params.tag) searchParams.set('tag', params.tag);
    if (params.sub) searchParams.set('sub', params.sub); // 版块内小类，需与 tag 同时满足
    
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
