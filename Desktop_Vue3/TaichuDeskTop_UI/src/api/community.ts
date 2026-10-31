// src/api/community.ts
import request from '../utils/request';

// 首页“社区动态”信息流条目
export interface CommunityFeedItem {
  id: string;
  kind: 'blog' | 'post' | 'artwork' | 'wiki-new' | 'wiki-update' | 'user-joined' | 'signin' | 'comment-blog' | 'comment-post' | 'comment-artwork' | 'joint' | 'survey' | 'dev-project' | 'world-new' | 'world-edit' | 'quest-start' | 'quest-complete' | 'history' | 'comment-history' | 'comment-work' | 'comment-character'
    | 'comment-activity' | 'chronicle' | 'novel-new' | 'novel-chapter' | 'quest-report' | 'chai-work' | 'oc' | 'battle' | 'activity' | 'checkin'
    | 'news' | 'release' | 'trade-item' | 'must-eat' | 'sponsor';
  actorName: string;
  actorId?: string | null; // 点击进入个人主页；官方动态为空
  targetTitle: string;
  targetId: string;
  createdAt: string;
  /** 补充说明（世界观动态：新建 / 更新了哪些条目） */
  detail?: string | null;
}

export interface CommunityFeedPage {
  items: CommunityFeedItem[];
  hasMore: boolean;
}

export const communityApi = {
  getFeed: (offset = 0, limit = 6) =>
    request.get<CommunityFeedPage>('/Community/feed', { params: { offset, limit }, silentError: true })
};
