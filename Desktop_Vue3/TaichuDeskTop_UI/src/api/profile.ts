// 公开个人主页：任何人都能查看的资料与公开作品（不含邮箱、手机号等私人信息）。
import request from '../utils/request';

export interface PublicProfile {
  id: string;
  username: string;
  joinedAt: string;
  avatar?: string | null;
  bio?: string | null;
  mood?: string | null;
  level: number;
  title?: string | null;
  maxSignStreak: number;
  likesReceived: number;
  /** chaiWorks / novels / history 是后来加的，旧后端没有 */
  counts: { posts: number; artworks: number; worlds: number; characters: number; chaiWorks?: number; novels?: number; history?: number };
  artworks: { id: number; title: string; coverImageUrl?: string | null }[];
  worlds: { id: string; name: string; description?: string | null; coverImage?: string | null }[];
  characters: { id: string; title: string; coverUrl?: string | null; description?: string | null }[];
  chaiWorks?: { id: string; title: string; coverUrl?: string | null; description?: string | null; mediaType?: string }[];
  novels?: { id: string; title: string; coverUrl?: string | null; description?: string | null; wordCount: number; status: string }[];
}

export const profileApi = {
  getPublicProfile: (id: string) => request.get<PublicProfile>(`/User/${id}/public`, { silentError: true }),
};

/** 个人主页路由；没有 ID（旧数据）时返回 undefined，调用处显示纯文本作者名。 */
export const userRoute = (id?: string | null) => id ? { name: 'UserCenter', params: { id } } : undefined;
