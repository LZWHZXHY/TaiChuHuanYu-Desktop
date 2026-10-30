// 论坛帖子卡片的公共处理：取摘要文字和封面图，首页和论坛列表共用。
import type { PublishedNoteItem } from '@/api/NotePublish';

// 后端在没有正文时写入的占位摘要，卡片上不显示。
const PLACEHOLDER_EXCERPTS = new Set(['灵感碎片已捕获...', '深度博客，静候回响...']);

export interface PostCard extends PublishedNoteItem {
  /** 可显示的摘要文字（已去掉占位语和 Markdown 标记） */
  content: string;
  /** 帖子配图或博客封面，没有时为空字符串 */
  cardCover: string;
}

export function formatPostCard(item: PublishedNoteItem): PostCard {
  let content = item.excerpt || '', cardCover = '';
  try {
    if (item.type === 'post') cardCover = JSON.parse(item.extraData || '{}').cardCover || '';
    else if (content.trim().startsWith('{')) {
      const excerpt = JSON.parse(content);
      if ('text' in excerpt || 'coverUrl' in excerpt) {
        content = excerpt.text || '';
        cardCover = excerpt.coverUrl || '';
      }
    }
  } catch { /* 旧的纯文本摘要保持可读。 */ }
  content = content.replace(/^#+\s*/gm, '').replace(/!\[[^\]]*]\([^)]*\)/g, '').trim();
  if (PLACEHOLDER_EXCERPTS.has(content)) content = '';
  return { ...item, content, cardCover };
}

/** 「3 分钟前」「2 天前」这类相对时间，超过一周显示日期。 */
export function relativeTime(value?: string) {
  if (!value) return '';
  const date = new Date(value);
  const min = Math.floor((Date.now() - date.getTime()) / 60000);
  if (min < 1) return '刚刚';
  if (min < 60) return `${min} 分钟前`;
  const hour = Math.floor(min / 60);
  if (hour < 24) return `${hour} 小时前`;
  const day = Math.floor(hour / 24);
  if (day < 7) return `${day} 天前`;
  return date.toLocaleDateString('zh-CN', { month: 'short', day: 'numeric' });
}
