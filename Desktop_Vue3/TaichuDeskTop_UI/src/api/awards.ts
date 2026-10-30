// 年度最佳展览：整份数据一次读写，后台由超级管理员维护。
import request from '../utils/request';

export type AwardWorkType = 'artwork' | 'chaiWork' | 'post' | 'custom';

export interface AwardCategory {
  key: string;
  name: string;
  icon?: string | null;
  description?: string | null;
  /** 本板块作品默认来源 */
  source?: AwardWorkType | null;
}

export interface AwardEntry {
  id: string;
  category: string;
  awardName: string;
  rank: number;
  workType?: AwardWorkType | null;
  workId?: string | null;
  workTitle?: string | null;
  coverUrl?: string | null;
  workLink?: string | null;
  authorId?: string | null;
  authorName?: string | null;
  citation?: string | null;
  prize?: string | null;
  prizeImage?: string | null;
  prizeNote?: string | null;
}

export interface AwardYear {
  year: number;
  title?: string | null;
  intro?: string | null;
  ceremonyDate?: string | null;
  published: boolean;
  entries: AwardEntry[];
}

export interface AwardsHall {
  categories: AwardCategory[];
  years: AwardYear[];
  canEdit?: boolean;
}

export const awardsApi = {
  get: () => request.get<AwardsHall>('/Awards', { silentError: true }),
  save: (hall: Pick<AwardsHall, 'categories' | 'years'>) => request.put<{ message: string }>('/Awards', hall, { silentError: true }),
  lookup: (type: AwardWorkType, id: string) => request.get<AwardEntry>('/Awards/lookup', { params: { type, id }, silentError: true }),
};

export const WORK_TYPE_LABELS: Record<AwardWorkType, string> = { artwork: '画廊作品', chaiWork: '柴圈作品', post: '论坛帖子', custom: '手动填写' };

/** 从粘贴的链接里取出作品 ID（例如 /gallery?workId=12、/works/xxx、/stream?post=xxx），直接填 ID 也可以。 */
export function extractWorkId(input: string): string {
  const text = input.trim()
  const match = /[?&](?:workId|post)=([^&#]+)/.exec(text) || /\/works\/([^/?#]+)/.exec(text)
  return match ? decodeURIComponent(match[1]) : text
}
