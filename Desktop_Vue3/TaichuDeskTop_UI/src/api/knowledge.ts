import request from '@/utils/request';
export const knowledgeTypes = { all: '全部资料', blog: '博客与文章', post: '帖子', artwork: '画廊作品', resource: '下载资源', world: '公开世界观', character: '柴圈人设', document: '图谱资料', wiki: '公共知识 · Wiki' } as const;
export type KnowledgeType = keyof typeof knowledgeTypes;
export interface KnowledgeItem {
    id: string;
    type: Exclude<KnowledgeType, 'all'>;
    title: string;
    summary: string;
    author: string;
    tags: string;
    coverUrl?: string;
    updatedAt: string;
}
export interface PublicResource {
    name: string;
    size: number;
    lastModified?: string;
}
// 引用、复制和浏览共用稳定的站内入口，下载资源不保存会过期的签名地址。
export function knowledgeRoute(item: Pick<KnowledgeItem, 'type' | 'id'>) {
    if (item.type === 'wiki') return { name: 'WikiDetail', params: { id: item.id } };
    if (item.type === 'world') return { name: 'ProjectDetail', params: { id: item.id } };
    if (item.type === 'character') return { path: '/ocs/' + encodeURIComponent(item.id) };
    if (item.type === 'resource') return { name: 'KnowledgeHome', query: { type: 'resource', q: item.id } };
    return { name: 'KnowledgeHome', query: { detail: item.id, detailType: item.type } };
}
export const knowledgeApi = {
    search: (params: {
        type: string;
        search: string;
        page: number;
        categoryId?: number;
    }) => request.get<{
        items: KnowledgeItem[];
        total: number;
        page: number;
        pageSize: number;
    }>('/knowledge-base/catalog', { params, silentError: true }),
    resources: () => request.get<{
        success: boolean;
        data: PublicResource[];
    }>('/Resource/list', { silentError: true }),
    download: (fileName: string) => request.get<{
        success: boolean;
        downloadUrl: string;
    }>('/Resource/download', { params: { fileName }, silentError: true }),
    document: (id: string) => request.get<{
        title: string;
        description: string;
    }>(`/knowledge-base/Node/${encodeURIComponent(id)}`, { silentError: true })
};
