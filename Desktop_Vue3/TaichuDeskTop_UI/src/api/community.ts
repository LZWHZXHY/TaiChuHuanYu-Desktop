// src/api/community.ts
import request from '../utils/request';

// 首页“社区动态”信息流条目
export interface CommunityFeedItem {
  id: string;
  kind: 'blog' | 'post' | 'artwork' | 'wiki-new' | 'wiki-update' | 'user-joined' | 'signin' | 'comment-blog' | 'comment-post' | 'comment-artwork' | 'joint' | 'survey' | 'dev-project';
  actorName: string;
  targetTitle: string;
  targetId: string;
  createdAt: string;
}

export interface CommunityFeedPage {
  items: CommunityFeedItem[];
  hasMore: boolean;
}

export const communityApi = {
  getFeed: (offset = 0, limit = 6) =>
    request.get<CommunityFeedPage>('/Community/feed', { params: { offset, limit }, silentError: true })
};
