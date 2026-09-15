// src/utils/NoteType.ts

// 1. 严格限制类型的值，区分个人动态与社区公共知识
export type NoteType = 
  | 'note'      // 长文随笔
  | 'blog'      // 深度博客
  | 'post'      // 短篇简语
  | 'folder'    // 文件夹
  | 'wiki'      // 社区百科、游戏攻略与规则教程
  | 'canvas'    // 星图白板
  | 'schedule'; // 日历看板

// 2. 统一管理每种类型的显示文本和元数据
export const NOTE_TYPE_CONFIG: Record<NoteType, { label: string; desc: string; icon: string }> = {
  note: { label: '长文随笔', desc: '深度思考，长篇沉浸式 Markdown 文章', icon: '📝' },
  blog: { label: '深度博客', desc: '正式发布的专属长文专栏作品', icon: '✒️' },
  post: { label: '短篇简语', desc: '随时捕捉瞬息思绪与生活碎念', icon: '💬' },
  folder: { label: '灵脉文件夹', desc: '用于组织和分类随笔碎片', icon: '📁' },
  wiki: { label: '百科与攻略', desc: '柴圈科普、历史背景、游戏攻略与社区规则', icon: '🪐' },
  canvas: { label: '星图白板', desc: '无边界思维导图与节点推演', icon: '🕸️' },
  schedule: { label: '日历看板', desc: '项目进度流转与时间轴', icon: '📅' }
};