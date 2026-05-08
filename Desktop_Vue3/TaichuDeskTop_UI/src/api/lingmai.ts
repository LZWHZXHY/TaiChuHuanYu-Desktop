// src/api/lingmai.ts
import request from '../utils/request'; 
import { nanoid } from 'nanoid';

// --- 1. 类型定义 ---
export interface FlatBlock {
  id: string;
  type: string;
  data: string;
  sortOrder?: string;
}

// --- 2. 核心转换逻辑 ---

/**
 * 🌟 拍平：将 Tiptap Tree 转换为后端扁平化 Blocks
 */
const flattenTiptapJson = (doc: any): FlatBlock[] => {
  if (!doc || !doc.content) return [];

  return doc.content.map((node: any) => {
    const attrs = { ...node.attrs };
    const blockId = attrs.id || nanoid(21);
    attrs.id = blockId; 

    return {
      id: blockId,
      type: node.type,
      data: JSON.stringify({
        attrs: attrs,
        content: node.content
      })
    };
  });
};

/**
 * 🌟 重组：将后端 Blocks 还原为 Tiptap Tree
 */
const rebuildTiptapJson = (blocks: any[]) => {
  return {
    type: 'doc',
    content: (blocks || []).map(b => {
      try {
        const parsedData = JSON.parse(b.data);
        return {
          type: b.type,
          attrs: { ...parsedData.attrs, id: b.id },
          content: parsedData.content
        };
      } catch (e) {
        console.error("块解析失败:", b);
        return { type: 'paragraph', content: [] };
      }
    })
  };
};

// --- 3. API 请求封装 ---
export const lingmaiApi = {

  /**
   * 🌟 获取当前空间下完整的网状图谱数据 (支持全局/跨空间全量)
   */
  getGraphData(spaceId: string, scope: string = 'current') {
    const url = scope === 'all' 
      ? `/LingMaiNodeGraph/spaces/${spaceId}/graph?scope=all` 
      : `/LingMaiNodeGraph/spaces/${spaceId}/graph`;
    return request.get(url);
  },

  /**
   * 🌟 反向链接：获取引用了当前笔记的所有笔记
   */
  getBacklinks(noteId: string) {
    return request.get(`/LingMaiNodeGraph/notes/${noteId}/backlinks`);
  },

  /**
   * 🌟 正向链接：获取当前笔记引用的所有笔记
   */
  getOutlinks(noteId: string) {
    return request.get(`/LingMaiNodeGraph/notes/${noteId}/outlinks`);
  },

  /**
   * 🌟 获取笔记并自动重组
   */
  async getNote(noteId: string) {
    const res: any = await request.get(`/LingMai/${noteId}`);
    return {
      ...res,
      tiptapContent: rebuildTiptapJson(res.blocks)
    };
  },

  /**
   * 🌟 获取列表
   */
  getNoteList(spaceId?: string) {
    const url = spaceId ? `/LingMai/all?spaceId=${spaceId}` : '/LingMai/all';
    return request.get(url);
  },

  /**
   * 🌟 同步块数据（自动拍平）
   */
  async syncBlocks(noteId: string, tiptapDoc: any) {
    const blocks = flattenTiptapJson(tiptapDoc);
    return request.post('/LingMai/sync', {
      noteId,
      blocks
    });
  },

  /**
   * 🌟 物理切断空间及所有碎片
   */
  deleteSpace(id: string) {
    return request.delete(`/LingMai/spaces/${id}`);
  },

  /**
   * 🌟 创建笔记
   */
  createNote(dto: { 
    title: string; 
    spaceId: string; 
    folderId: string | null; 
    type: string 
  }) {
    return request.post('/LingMai/notes', dto);
  },

  /**
   * 🌟 修改标题
   */
  updateNoteInfo(noteId: string, title: string) {
    return request.patch(`/LingMai/notes/${noteId}`, `"${title}"`, {
      headers: { 'Content-Type': 'application/json' }
    });
  },

  /**
   * 🌟 移动笔记
   */
  moveNote(noteId: string, folderId: string | null) {
    return request.patch(`/LingMai/notes/${noteId}/move`, { folderId });
  },

  /**
   * 🌟 删除笔记
   */
  deleteNote(noteId: string) {
    return request.delete(`/LingMai/notes/${noteId}`);
  },

  /**
   * 🌟 获取 20 份历史列表
   */
  getHistoryList(noteId: string) {
    return request.get(`/LingMai/notes/${noteId}/history`);
  },

  /**
   * 🌟 重命名空间
   */
  updateSpaceName(id: string, name: string) {
    return request.patch(`/LingMai/spaces/${id}`, JSON.stringify(name), {
      headers: { 'Content-Type': 'application/json' }
    });
  },

  /**
   * 🌟 创建快照
   */
  async createSnapshot(noteId: string, tiptapDoc: any, remark: string = "手动保存") {
    const contentJson = JSON.stringify(tiptapDoc);
    return request.post(`/LingMai/notes/${noteId}/snapshot`, {
      contentJson,
      remark
    });
  },

  /**
   * 🌟 穿梭回滚
   */
  rollbackTo(noteId: string, historyId: string) {
    return request.post(`/LingMai/history/${historyId}/rollback`);
  },

  /**
   * 🌟 获取空间列表
   */
  getSpaces() {
    return request.get('/LingMai/spaces');
  },

  /**
   * 🌟 创建空间
   */
  createSpace(name: string) {
    return request.post('/LingMai/spaces', { name });
  },

  // ==========================================================================
  // 🌟 发布与广场接口 (与后端的 LingMaiPublishController 保持完全对齐)
  // ==========================================================================
  
  /**
   * 🌟 改变已有碎片的公开状态（发布至广场 / 设为私密）
   * @param noteId 笔记的 GUID
   * @param isPublic 是否公开到广场
   */
  updateNotePublishStatus(noteId: string, isPublic: boolean) {
    return request.patch(`/LingMaiPublish/notes/${noteId}/status`, { isPublic });
  },

  /**
   * 🌟 使用 UnifiedPublishDto 一键联合发布随笔（博客）或简语（帖子）
   */
  publishUnified(dto: {
    spaceId: string;
    folderId: string | null;
    type: string;
    title: string;
    isPublic: boolean;
    blocks: any[];
  }) {
    return request.post('/LingMaiPublish/unified', dto);
  },

  /**
   * 🌟 拉取广场公开流
   * @param type 'note' (随笔) | 'thought' (简语)
   */
  getPublicStream(type?: string) {
    const url = type ? `/LingMaiPublish/public-stream?type=${type}` : '/LingMaiPublish/public-stream';
    return request.get(url);
  },

  /**
   * 🌟 游客或外部用户通过该方法读取公开博客内容
   */
  getPublicBlog(noteId: string) {
    return request.get(`/LingMaiPublish/blog/${noteId}`);
  },
  // ==========================================================================
  // 🌟 双表物理隔离发布接口
  // ==========================================================================
  
  /**
   * 🌟 一键发布或更新发布（内容深拷贝至发布表）
   * @param noteId 笔记的 GUID
   * @param type 发布形态 'note' (随笔) | 'thought' (简语)
   */
  publishNote(noteId: string, type: string = 'note') {
    return request.post(`/LingMaiPublish/notes/${noteId}/publish?type=${type}`);
  },

  /**
   * 🌟 取消发布（从广场物理下线，清除发布表数据）
   * @param noteId 笔记的 GUID
   */
  unpublishNote(noteId: string) {
    return request.delete(`/LingMaiPublish/notes/${noteId}/unpublish`);
  },

  getQuota() {
    return request.get('/LingMai/quota');
  }




};