// src/utils/NoteType.ts
// 灵脉里每一篇都是「笔记」；类型决定它用什么编辑器、能否发布、发布到哪里。
// 侧边栏、新建菜单、设置面板、发布弹窗统一从这里取名称和说明，不要在组件里另写一套叫法。

export type NoteType =
  | 'note'      // 笔记：私人记录，不发布
  | 'doc'       // 项目文档：分享给协作项目
  | 'blog'      // 博客：发布到「论坛」
  | 'post'      // 帖子：发布到「论坛」
  | 'folder'    // 文件夹
  | 'wiki'      // 百科词条：发布到「灵脉百科」
  | 'canvas'    // 白板
  | 'schedule'; // 日程

export interface NoteKind {
  value: string;
  /** 完整名称，用于菜单和设置 */
  label: string;
  /** 侧边栏列表右侧的短标签 */
  short: string;
  icon: string;
  /** 一句话说明这是什么 */
  desc: string;
  /** 发布后出现在哪个板块；null 表示不对外发布 */
  destination: string | null;
  /** 新建菜单里直接显示；其余收在「更多类型」 */
  common?: boolean;
}

export const NOTE_KINDS: NoteKind[] = [
  { value: 'note', label: '笔记', short: '笔记', icon: '📝', desc: '只有自己能看到的记录', destination: null, common: true },
  { value: 'post', label: '帖子', short: '帖子', icon: '💬', desc: '500 字以内的短内容，可以配图', destination: '论坛', common: true },
  { value: 'blog', label: '博客', short: '博客', icon: '✒️', desc: '带标题、摘要和封面的长文', destination: '论坛', common: true },
  { value: 'wiki', label: '百科词条', short: '词条', icon: '📚', desc: '社区百科和攻略，发布时选择分类', destination: '灵脉百科' },
  { value: 'doc', label: '项目文档', short: '文档', icon: '📄', desc: '分享给协作项目的成员阅读', destination: '协作项目' },
  { value: 'schedule', label: '日程', short: '日程', icon: '📅', desc: '日历和任务看板', destination: null },
  { value: 'canvas', label: '白板', short: '白板', icon: '🧩', desc: '自由摆放卡片和连线', destination: null },
  { value: 'art', label: '画作', short: '画作', icon: '🖼️', desc: '以图片为主的作品', destination: '画廊' },
  { value: 'map', label: '地图', short: '地图', icon: '🗺️', desc: '世界观地图', destination: null },
  { value: 'excel', label: '表格', short: '表格', icon: '📊', desc: '电子表格', destination: null },
];

export const FOLDER_KIND: NoteKind = { value: 'folder', label: '文件夹', short: '文件夹', icon: '📁', desc: '用来整理笔记', destination: null };

// 由其他板块生成、不在设置里切换的类型。
const SPECIAL_KINDS: NoteKind[] = [
  FOLDER_KIND,
  { value: 'char', label: '角色档案', short: '角色', icon: '🧍', desc: '柴圈角色设定', destination: '柴圈板块' },
];

export function noteKind(type?: string | null): NoteKind {
  if (!type) return NOTE_KINDS[0];
  return NOTE_KINDS.find(kind => kind.value === type) || SPECIAL_KINDS.find(kind => kind.value === type)
    || { value: type, label: type, short: type, icon: '📄', desc: '', destination: null };
}

/** 新建笔记时使用的默认标题；发布帖子时后端会用正文开头代替它。 */
export const DEFAULT_NOTE_TITLE = '未命名笔记';

// 兼容旧引用。
export const NOTE_TYPE_CONFIG = Object.fromEntries(
  [...NOTE_KINDS, FOLDER_KIND].map(kind => [kind.value, { label: kind.label, desc: kind.desc, icon: kind.icon }])
) as Record<NoteType, { label: string; desc: string; icon: string }>;
