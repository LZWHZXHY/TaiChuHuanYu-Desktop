import type { CardDetail, WorldProject } from '@/api/types/world';
import { getCardTypeLabel, parseCoverImage } from './worldPresentation';

export interface WorldTextItem {
  title: string;
  description: string;
  anchors?: string[];
  images?: string[];
}

// Markdown 示例代码不应被当成正文引用、标题或图片。
export function mapWorldProse(text: string, transform: (text: string) => string): string {
  let fence = '';
  return text.split('\n').map(line => {
    const marker = line.match(/^\s*(`{3,}|~{3,})/)?.[1];
    if (marker) {
      if (!fence) fence = marker;
      else if (marker[0] === fence[0] && marker.length >= fence.length) fence = '';
      return line;
    }
    if (fence) return line;
    return line.split(/(`+[^`]*`+)/g).map((part, index) => index % 2 ? part : transform(part)).join('');
  }).join('\n');
}

const headingKey = (title: string) => title.toLowerCase().replace(/[^\p{L}\p{N}\s_-]/gu, '').trim().replace(/\s+/g, '-');
const unescapeTitle = (text: string) => text.replace(/\\([\\`*_[\]<>])/g, '$1');
const imageUrl = (url: string) => /^(https?:\/\/|\/[^/])/i.test(url.trim()) ? url.trim().replace(/[<>\r\n]/g, '') : '';

export function parseWorldText(text: string, filename: string) {
  const content = text.replace(/^\uFEFF/, '').replace(/\r\n?/g, '\n').trim();
  if (!content)
    throw new Error('文件没有可导入的正文。');
  const title = (content.match(/^#\s+(.+)$/m)?.[1] || filename.replace(/\.(md|txt)$/i, '')).trim();
  const parts: WorldTextItem[] = [];
  let fence = '', heading = '', introduction = '', body: string[] = [];
  for (const line of content.split('\n')) {
    const marker = line.match(/^\s*(`{3,}|~{3,})/)?.[1];
    if (marker) {
      if (!fence)
        fence = marker;
      else if (marker[0] === fence[0] && marker.length >= fence.length)
        fence = '';
    }
    const section = !fence && line.match(/^##\s+(.+)$/);
    if (section) {
      if (!heading)
        introduction = body.join('\n');
      if (heading)
        parts.push({ title: heading, description: body.join('\n').trim() });
      heading = section[1]!.trim();
      body = [];
    }
    else
      body.push(line);
  }
  if (heading)
    parts.push({ title: heading, description: body.join('\n').trim() });
  if (!parts.length)
    parts.push({ title, description: content });
  else {
    const preamble = introduction.replace(/^#\s+.*\n?/, '').trim();
    if (preamble && !content.startsWith('## '))
      parts.unshift({ title: `${title} · 简介`, description: preamble });
  }
  if (parts.length > 50)
    throw new Error('一次最多导入 50 个条目，请拆分文件。');
  if (parts.some(part => !part.title || part.title.length > 200))
    throw new Error('条目标题不能为空，且不能超过 200 字。');
  for (const part of parts) {
    part.title = unescapeTitle(part.title);
    const anchors: string[] = [], images: string[] = [];
    part.description = mapWorldProse(part.description, line => line
      .replace(/<a\s+id=["']([^"']+)["']\s*>\s*<\/a>/g, (_, id: string) => { anchors.push(id); return ''; })
      .replace(/!\[((?:\\.|[^\]\\])*)\]\(<?([^\s<>]+)>?\)/g, (match, label: string, url: string) => {
        const safe = imageUrl(url);
        if (!safe) return match;
        images.push(safe);
        return label ? `图片：${unescapeTitle(label)}` : '';
      })).trim();
    if (anchors.length) part.anchors = anchors;
    if (images.length) part.images = [...new Set(images)];
  }
  return parts;
}

/** 只在本次文件内匹配引用；重名/重复锚点不能自动指向任意一条。 */
export function resolveWorldImport(items: WorldTextItem[], ids: string[]) {
  const targets = new Map<string, string | null>();
  items.forEach((item, index) => {
    for (const key of new Set([headingKey(item.title), ...(item.anchors || [])])) {
      if (key) targets.set(key, targets.has(key) ? null : ids[index] || null);
    }
  });
  let unresolved = 0;
  const descriptions = items.map(item => mapWorldProse(item.description, line => line
    .replace(/(?<!!)\[((?:\\.|[^\]\\])+)\]\(#([^\s)]+)\)/g, (match, label: string, anchor: string) => {
      let key = anchor;
      try { key = decodeURIComponent(key); } catch { /* 保留原文并计入未匹配。 */ }
      const id = targets.get(key);
      if (id) return `{CARD:${id}}`;
      unresolved++;
      return `${unescapeTitle(label)}（未匹配引用：#${anchor}）`;
    })));
  return { descriptions, unresolved };
}
const textValue = (value: unknown): string => typeof value === 'string' ? value : value == null ? '' : typeof value === 'object' ? JSON.stringify(value) : String(value);
const escapeTitle = (value: unknown) => textValue(value).replace(/[\r\n]/g, ' ').replace(/([\\`*_[\]<>])/g, '\\$1');
export function worldMarkdown(project: WorldProject, cards: CardDetail[]) {
  const byId = new Map(cards.map(card => [card.id, card]));
  const reference = (id: string) => { const card = byId.get(id); return card ? `[${escapeTitle(card.title)}](#card-${card.id})` : '不可用的条目'; };
  return [`# ${escapeTitle(project.name)}`, project.description || '', ...cards.flatMap(card => [
      `## ${escapeTitle(card.title)}`, `<a id="card-${card.id}"></a>`, `类型：${escapeTitle(getCardTypeLabel(card.type))}`,
      card.aliases.length ? `别名：${card.aliases.map(escapeTitle).join('、')}` : '',
      card.tags.length ? `标签：${card.tags.map(escapeTitle).join('、')}` : '',
      mapWorldProse(card.description || '', line => line.replace(/^#{1,2}\s+/, '### ')
        .replace(/\{CARD:([^}]+)\}/g, (_, id: string) => reference(id))),
      ...card.attributes.filter(a => textValue(a.value).trim()).map(a => `- ${escapeTitle(a.key)}：${escapeTitle(a.value)}`),
      ...card.contentBlocks.map(block => `- ${escapeTitle(block.contextLabel || '引用')}：${reference(block.cardId)}`),
      ...card.outRelations.map(r => `- ${escapeTitle(r.relationType)} → ${reference(r.targetCardId)}`),
      ...card.timelineEvents.map(e => `- ${escapeTitle(e.era || '')} ${escapeTitle(e.date)} ${escapeTitle(e.title)}：${e.description || ''}`),
      ...(imageUrl(parseCoverImage(card.coverImage)) ? [`![${escapeTitle(card.title)} · 封面](<${imageUrl(parseCoverImage(card.coverImage))}>)`] : []),
      ...card.embeddedCards.filter(id => !card.contentBlocks.some(b => b.cardId === id)).map(id => `- 引用：${reference(id)}`),
      ...card.galleryImages.map(imageUrl).filter(Boolean).map((url, index) => `![${escapeTitle(card.title)} · 图片 ${index + 1}](<${url}>)`),
      card.content && !['{}', 'null', ''].includes(card.content) ? `\`\`\`json\n${card.content.replace(/`/g, '\\u0060')}\n\`\`\`` : '',
    ])].filter(Boolean).join('\n\n') + '\n';
}
