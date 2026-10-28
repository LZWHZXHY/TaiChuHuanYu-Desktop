import type { CardDetail, WorldProject } from '@/api/types/world';
import { getCardTypeLabel } from './worldPresentation';
export function parseWorldText(text: string, filename: string) {
  const content = text.replace(/^\uFEFF/, '').replace(/\r\n?/g, '\n').trim();
  if (!content)
    throw new Error('文件没有可导入的正文。');
  const title = (content.match(/^#\s+(.+)$/m)?.[1] || filename.replace(/\.(md|txt)$/i, '')).trim();
  const parts: {
    title: string;
    description: string;
  }[] = [];
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
  return parts;
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
      (card.description || '').replace(/\{CARD:([^}]+)\}/g, (_, id: string) => reference(id)),
      ...card.attributes.filter(a => textValue(a.value).trim()).map(a => `- ${escapeTitle(a.key)}：${escapeTitle(a.value)}`),
      ...card.contentBlocks.map(block => `- ${escapeTitle(block.contextLabel || '引用')}：${reference(block.cardId)}`),
      ...card.outRelations.map(r => `- ${escapeTitle(r.relationType)} → ${reference(r.targetCardId)}`),
      ...card.timelineEvents.map(e => `- ${escapeTitle(e.era || '')} ${escapeTitle(e.date)} ${escapeTitle(e.title)}：${e.description || ''}`),
      ...(card.coverImage ? [`封面：${card.coverImage}`] : []),
      ...card.embeddedCards.filter(id => !card.contentBlocks.some(b => b.cardId === id)).map(id => `- 引用：${reference(id)}`),
      ...card.galleryImages.map(url => `图片：<${url.replace(/[<>\r\n]/g, '')}>`),
      card.content && !['{}', 'null', ''].includes(card.content) ? `\`\`\`json\n${card.content.replace(/`/g, '\\u0060')}\n\`\`\`` : '',
    ])].filter(Boolean).join('\n\n') + '\n';
}
