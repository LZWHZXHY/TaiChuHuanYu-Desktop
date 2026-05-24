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
}

export const wikiApi = {
  getCategories: () => request.get<IWikiCategory[]>('/wiki/categories'),
  
  // 确保接口定义包含所有必需字段
  applyCategory: (data: { name: string; reason: string; parentId: number | null; sortOrder: number }) => 
    request.post('/wiki/apply-category', data),

  publishFromNote: (data: any) => request.post('/wiki/publish', data),

  // 🌟 补全这个缺失的接口定义
  getAllArticles: () => request.get<any[]>('/wiki/articles'),

  getArticlesByCategory: (categoryId: number) => 
    request.get<any[]>(`/wiki/articles/by-category/${categoryId}`),

  getArticleDetail: (id: string) => 
    request.get<any>(`/wiki/article/${id}`),
};