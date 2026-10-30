// 论坛版块配置：版块和小类都以标签形式保存在帖子上（例如 ["我的世界", "作品分享"]）。
// 新增版块只需在 FORUM_BOARDS 里加一项；标签名一旦有帖子使用就不要再改，否则旧帖会从版块里消失。

export interface ForumSub {
  tag: string;
  desc: string;
  /** 仅管理员可发，并在版块顶部置顶显示 */
  adminOnly?: boolean;
}

export interface ForumBoard {
  tag: string;
  icon: string;
  desc: string;
  subs: ForumSub[];
  /** 版块页顶部的附加入口 */
  link?: { label: string; to: string };
  /** 版块里带「世界任务」页签（我的世界服务器全服任务） */
  quests?: boolean;
}

export const FORUM_BOARDS: ForumBoard[] = [
  {
    tag: '我的世界',
    icon: '⛏️',
    desc: '太初寰宇社区服务器的讨论、作品与求助',
    link: { label: '服务器地址与下载 →', to: '/games?game=MinecraftServer' },
    quests: true,
    subs: [
      { tag: '讨论交流', desc: '玩法、版本、模组相关的话题' },
      { tag: '作品分享', desc: '建筑、截图、红石、存档' },
      { tag: '求助问答', desc: '进服、安装、报错问题' },
      { tag: '任务报告', desc: '世界任务的完成报告和进度截图，管理员看帖确认' },
      { tag: '服务器公告', desc: '服务器维护与活动通知', adminOnly: true },
    ],
  },
];

export const FORUM_NAME = '论坛';

/** 世界任务放在「我的世界」版块里的「世界任务」页签（旧地址 /quests 会跳到这里） */
export const QUEST_BOARD = '我的世界';
/** 世界任务的报告帖发在这个小类里 */
export const QUEST_REPORT_SUB = '任务报告';
/** 发帖报告某个世界任务（发帖页会带上任务信息，发布后自动提交给管理员审核） */
export const questReportRoute = (questId: number) => ({ path: '/write/post', query: { tag: QUEST_BOARD, sub: QUEST_REPORT_SUB, quest: String(questId) } });
/** 论坛里所有任务报告帖 */
export const questReportsRoute = () => ({ path: '/stream', query: { tag: QUEST_BOARD, sub: QUEST_REPORT_SUB } });
export const questRoute = (tab?: 'manage' | 'record' | 'review') => ({ path: '/stream', query: { tag: QUEST_BOARD, view: 'quests', ...(tab ? { tab } : {}) } });

export function findBoard(tag?: string | null): ForumBoard | undefined {
  return tag ? FORUM_BOARDS.find(board => board.tag === tag) : undefined;
}

/** 从帖子标签里识别它所属的版块和小类，用于卡片上的标记。 */
export function boardOf(tags: unknown): { board?: ForumBoard; sub?: ForumSub } {
  let list: string[] = [];
  if (Array.isArray(tags)) list = tags.filter((t): t is string => typeof t === 'string');
  else if (typeof tags === 'string' && tags.trim()) {
    try { const parsed = JSON.parse(tags); if (Array.isArray(parsed)) list = parsed.filter((t): t is string => typeof t === 'string'); }
    catch { list = tags.split(/[,，]/).map(t => t.trim()); }
  }
  const board = FORUM_BOARDS.find(b => list.includes(b.tag));
  const sub = board?.subs.find(s => list.includes(s.tag));
  return { board, sub };
}

/** 把版块和小类写入标签：先移除所有版块相关标签，再加上新的选择。 */
export function applyBoardTags(tags: string[], boardTag: string, subTag: string): string[] {
  const reserved = new Set(FORUM_BOARDS.flatMap(b => [b.tag, ...b.subs.map(s => s.tag)]));
  const rest = tags.filter(tag => !reserved.has(tag));
  return [...rest, ...(boardTag ? [boardTag] : []), ...(boardTag && subTag ? [subTag] : [])];
}
