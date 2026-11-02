import request from '@/utils/request';
export const knowledgeTypes = { all: '全部资料', blog: '博客与文章', post: '帖子', artwork: '画廊作品', work: '柴圈作品', resource: '下载资源', brush: '茶绘笔刷', world: '公开世界观', setting: '世界观设定', novel: '世界观小说', character: '柴圈人设', joint: '联动活动', devlog: '开发进度', musteat: '必吃榜', news: '公告', release: '版本更新', manual: '使用手册', document: '图谱资料', wiki: '公共知识 · Wiki' } as const;
/** 在知识库里直接阅读全文的类型（没有单独的详情页） */
export const TEXT_TYPES = ['news', 'release', 'manual', 'musteat'] as const;
export interface KnowledgeText {
    type: string;
    id: string;
    title: string;
    /** markdown（使用手册）/ text（原样换行） */
    format: 'markdown' | 'text';
    content: string;
    meta?: string | null;
    coverUrl?: string | null;
    updatedAt: string;
    link?: string | null;
    linkText?: string | null;
    externalUrl?: string | null;
}
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
    if (item.type === 'novel') return { path: '/novels/' + encodeURIComponent(item.id) };
    if (item.type === 'work') return { path: '/works/' + encodeURIComponent(item.id) };
    // 世界观设定条目的 id 是「世界ID:条目ID」
    if (item.type === 'setting') {
        const [projectId, cardId] = item.id.split(':');
        return { path: `/world/project/${encodeURIComponent(projectId ?? '')}/card/${encodeURIComponent(cardId ?? '')}` };
    }
    if (item.type === 'brush') return { name: 'KnowledgeHome', query: { type: 'brush', q: item.id } };
    if (item.type === 'joint') return { path: '/joint/' + encodeURIComponent(item.id) };
    if (item.type === 'devlog') return { name: 'DevReportDetail', params: { id: item.id } };
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
        /** 开发进度只给管理员和赞助会员；其他人选这一类时为 true、列表为空 */
        locked?: boolean;
    }>('/knowledge-base/catalog', { params, silentError: true }),
    resources: () => request.get<{
        success: boolean;
        data: PublicResource[];
    }>('/Resource/list', { silentError: true }),
    download: (fileName: string) => request.get<{
        success: boolean;
        downloadUrl: string;
    }>('/Resource/download', { params: { fileName }, silentError: true }),
    /** 公告 / 版本更新 / 使用手册 / 必吃榜条目的全文 */
    text: (type: string, id: string) => request.get<KnowledgeText>(`/knowledge-base/catalog/text/${encodeURIComponent(type)}/${encodeURIComponent(id)}`, { silentError: true }),
    document: (id: string) => request.get<{
        title: string;
        description: string;
    }>(`/knowledge-base/Node/${encodeURIComponent(id)}`, { silentError: true })
};
