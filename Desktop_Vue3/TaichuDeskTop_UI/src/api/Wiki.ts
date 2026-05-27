import request from '../utils/request';

// 与 C# 后端对应的模型
export interface IWikiCategory {
  id: number;
  name: string;
  parentId: number | null;
  sortOrder: number;
}

export interface ICategoryApplyDto {
  name: string;
  parentId: number | null;
  sortOrder: number;
  reason: string;
}

// 词条简略模型 (用于列表展示)
export interface IWikiArticle {
  id: string;
  title: string;
  excerpt: string;
  categoryId: number;
  spaceId?: string;
  spaceName?: string;
  tags: string[];
  publishedAt: string;
  authorId?: string;
}

export const wikiApi = {
  getCategories: () => request.get<IWikiCategory[]>('/wiki/categories'),
  
  applyCategory: (data: { name: string; reason: string; parentId: number | null; sortOrder: number }) => 
    request.post('/wiki/apply-category', data),

  publishFromNote: (data: any) => request.post('/wiki/publish', data),

  // 🌟 核心修改：这就是 index.vue 里调用的真正更新接口！
// 🌟 修改为直接接收 content 字符串
  updateFromNote: (data: { 
    articleId: string; 
    content: string; 
    summary?: string; 
    baseRevisionId: number; 
  }) => request.post('/wiki/update', data),

  getAllArticles: () => request.get<any[]>('/wiki/articles'),

  getArticlesByCategory: (categoryId: number) => 
    request.get<any[]>(`/wiki/articles/by-category/${categoryId}`),

  getArticleDetail: (id: string) => 
    request.get<any>(`/wiki/article/${id}`),
};