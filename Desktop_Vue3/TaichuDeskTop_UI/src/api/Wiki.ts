// src/api/wiki.ts
import request from '@/utils/request'; // 🌟 引入你封装好的 request 实例

/**
 * 🌟 灵脉百科专用 API
 * 专门处理已发布的词条、世界观流和详情感应
 */
export const wikiApi = {
  
  /**
   * 1. 获取全域或空间的公开内容流
   * @param type 类型，如 'wiki', 'thought', 'char'
   * @param spaceId 可选，过滤特定位面
   */
  async getPublicStream(type: string = 'wiki', spaceId?: string) {
    // 🌟 request 已经配置了 baseURL，且会自动剥离 response.data
    return request.get('/LingMaiPublish/stream', { 
      params: { type, spaceId } 
    });
  },

  /**
   * 2. 获取单个词条的完整编织详情
   * @param id 发布表中的 ID (publishedNoteId)
   */
  async getPublishedDetail(id: string) {
    // 🌟 直接传入 ID 即可感应后端详情接口
    return request.get(`/LingMaiPublish/published/${id}`);
  },

  /**
   * 3. 获取所有包含公开词条的位面信息
   */
  async getWikiSpaces() {
    return request.get('/LingMai/spaces');
  }
};