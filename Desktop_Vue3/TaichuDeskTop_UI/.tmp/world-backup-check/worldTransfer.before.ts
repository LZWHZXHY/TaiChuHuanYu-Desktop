import type { CardDetail, WorldProject, CreateCardPayload } from '@/api/types/world';
import { getCardTypeLabel, parseCoverImage } from './worldPresentation';
import { cardSchemas } from '@/views/世界观管理/card-schemas';
import type { CardType } from '@/views/世界观管理/card_type';

export interface WorldTextItem {
  title: string;
  description: string;
  anchors?: string[];
  images?: string[];
  sourceId?: string;
  payload?: CreateCardPayload;
}

const exportMarker = '\n<!-- taichu-world:v1\n';
// 仅检测可读稿是否被编辑，不作为文件来源或权限凭证。
function textChecksum(text: string) {
  text = text.replace(/\r\n?/g, '\n').trimEnd();
  let hash = 2166136261;
  for (let i = 0; i < text.length; i++) hash = Math.imul(hash ^ text.charCodeAt(i), 16777619);
  return (hash >>> 0).toString(16);
}

function snapshot(card: CardDetail): CreateCardPayload {
  return {
    title: card.title, type: card.type, subType: card.subType, description: card.description,
    content: card.content, coverImage: card.coverImage, galleryImages: card.galleryImages,
    aliases: card.aliases, tags: card.tags, attributes: card.attributes, contentBlocks: card.contentBlocks,
    embeddedCards: card.embeddedCards, timelineEvents: card.timelineEvents,
    relations: card.outRelations.map(r => ({ targetCardId: r.targetCardId, relationType: r.relationType })),
  };
}

/** 外部文件只取业务字段，绝不恢复账号、世界 ID、公开状态、版本或权限。 */
function readSnapshot(value: unknown): CreateCardPayload {
  const object = (v: unknown): Record<string, unknown> => {
    if (!v || typeof v !== 'object' || Array.isArray(v)) throw new Error('恢复数据格式无效。');
    return v as Record<string, unknown>;
  };
  const string = (v: unknown): string => { if (typeof v !== 'string') throw new Error('恢复字段必须是文本。'); return v; };
  const optional = (v: unknown) => v == null ? null : string(v);
  const array = <T>(v: unknown, read: (v: unknown) => T): T[] => {
    if (!Array.isArray(v)) throw new Error('恢复列表格式无效。');
    return v.map(read);
  };
  const v = object(value);
  const result: CreateCardPayload = {
    title: string(v.title), type: string(v.type), subType: optional(v.subType),
    description: optional(v.description), content: optional(v.content), coverImage: optional(v.coverImage),
    aliases: array(v.aliases, string), tags: array(v.tags, string), galleryImages: array(v.galleryImages, string),
    embeddedCards: array(v.embeddedCards, string),
    attributes: array(v.attributes, raw => { const a = object(raw); return { key: string(a.key), value: string(a.value), type: string(a.type) }; }),
    contentBlocks: array(v.contentBlocks, raw => {
      const b = object(raw);
      if (!Number.isInteger(b.order)) throw new Error('引用顺序无效。');
      return { id: string(b.id), cardId: string(b.cardId), cardType: string(b.cardType), order: b.order as number, contextLabel: optional(b.contextLabel) };
    }),
    timelineEvents: array(v.timelineEvents, raw => { const e = object(raw); return { date: string(e.date), era: optional(e.era), title: string(e.title), description: optional(e.description) }; }),
    relations: array(v.relations, raw => { const r = object(raw); return { targetCardId: string(r.targetCardId), relationType: string(r.relationType) }; }),
  };
  if (!result.title.trim() || result.title.length > 200 || !result.type.trim() || result.type.length > 50 || (result.subType?.length || 0) > 50)
    throw new Error('条目名称或类型超出允许范围。');
  return result;
}

export function hasWorldRestoreData(text: string) { return text.includes('\n<!-- taichu-world:'); }

function remapContentReferences(payload: CreateCardPayload, target: (id: string) => string) {
  let content: Record<string, unknown>;
  try { content = JSON.parse(payload.content || '{}'); } catch { return payload.content; }
  if (!content || typeof content !== 'object' || Array.isArray(content)) return payload.content;
  let changed = false;
  for (const field of cardSchemas[payload.type as CardType] || []) {
    const id = content[field.key];
    if (field.type !== 'card-ref' || typeof id !== 'string' || !id) continue;
    const next = target(id);
    if (next !== id) { content[field.key] = next; changed = true; }
  }
  return changed ? JSON.stringify(content) : payload.content;
}

export function createWorldImportPayload(item: WorldTextItem, fallbackType: string): CreateCardPayload {
  if (!item.payload) return { title: item.title, description: item.description, type: fallbackType, galleryImages: item.images || [] };
  return { ...item.payload, description: item.payload.description?.replace(/\{CARD:[^}]+\}/g, '（引用待恢复）') ?? null,
    content: remapContentReferences(item.payload, () => ''), contentBlocks: [], embeddedCards: [], relations: [] };
}

function readWorldRestoreData(content: string): WorldTextItem[] | null {
  if (!hasWorldRestoreData(content)) return null;
  const start = content.lastIndexOf(exportMarker);
  if (start < 0 || !content.endsWith('-->')) throw new Error('导出文件的恢复数据版本或结尾无效。');
  const data = JSON.parse(content.slice(start + exportMarker.length, -3));
  const readable = content.slice(0, start).trimEnd();
  if (data.version !== 1 || data.textChecksum !== textChecksum(readable))
    throw new Error('导出文件的可读内容已修改。为避免忽略修改，已停止完整恢复；可选择按普通 Markdown 导入。');
  if (!Array.isArray(data.cards) || !data.cards.length || data.cards.length > 50)
    throw new Error('一次可恢复 1 至 50 个条目。');
  const seen = new Set<string>();
  const items: WorldTextItem[] = data.cards.map((entry: { sourceId?: unknown; payload?: unknown }) => {
    if (!entry || typeof entry.sourceId !== 'string' || !entry.sourceId || seen.has(entry.sourceId))
      throw new Error('恢复数据中的条目标识缺失或重复。');
    seen.add(entry.sourceId);
    const payload = readSnapshot(entry.payload);
    return { title: payload.title, description: payload.description || '', sourceId: entry.sourceId, payload,
      anchors: [`card-${entry.sourceId}`], images: [...(payload.galleryImages || [])] };
  });
  // 在发送任何写请求前检查引用集合，避免半途才发现文件被截断。
  resolveWorldImport(items, items.map(item => item.sourceId!));
  return items;
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

export function parseWorldText(text: string, filename: string, plain = false) {
  let content = text.replace(/^\uFEFF/, '').replace(/\r\n?/g, '\n').trim();
  if (!plain) {
    const restored = readWorldRestoreData(content);
    if (restored) return restored;
  } else if (hasWorldRestoreData(content)) content = content.slice(0, content.lastIndexOf('\n<!-- taichu-world:')).trimEnd();
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
  if (items.length !== ids.length || ids.some(id => !id) || new Set(ids).size !== ids.length)
    throw new Error('导入条目标识不完整，无法重建引用。');
  const restoredIds = new Map(items.flatMap((item, index) => item.sourceId ? [[item.sourceId, ids[index]!] as const] : []));
  const restoredTarget = (id: string) => {
    const target = restoredIds.get(id);
    if (!target) throw new Error('恢复数据引用了文件中未包含的条目，请导出完整世界后重试。');
    return target;
  };
  const targets = new Map<string, string | null>();
  items.forEach((item, index) => {
    for (const key of new Set([headingKey(item.title), ...(item.anchors || [])])) {
      if (key) targets.set(key, targets.has(key) ? null : ids[index] || null);
    }
  });
  let unresolved = 0;
  const descriptions = items.map(item => item.payload
    ? (item.payload.description || '').replace(/\{CARD:([^}]+)\}/g, (_, id: string) => `{CARD:${restoredTarget(id)}}`)
    : mapWorldProse(item.description, line => line
    .replace(/(?<!!)\[((?:\\.|[^\]\\])+)\]\(#([^\s)]+)\)/g, (match, label: string, anchor: string) => {
      let key = anchor;
      try { key = decodeURIComponent(key); } catch { /* 保留原文并计入未匹配。 */ }
      const id = targets.get(key);
      if (id) return `{CARD:${id}}`;
      unresolved++;
      return `${unescapeTitle(label)}（未匹配引用：#${anchor}）`;
    })));
  const payloads: CreateCardPayload[] = items.map((item, index) => item.payload ? {
    ...item.payload,
    description: item.payload.description == null ? null : descriptions[index],
    content: remapContentReferences(item.payload, restoredTarget),
    contentBlocks: item.payload.contentBlocks!.map(block => ({ ...block, cardId: restoredTarget(block.cardId) })),
    embeddedCards: item.payload.embeddedCards!.map(restoredTarget),
    relations: item.payload.relations!.map(relation => ({ ...relation, targetCardId: restoredTarget(relation.targetCardId) })),
  } : { title: item.title, type: '', description: descriptions[index], galleryImages: item.images || [] });
  return { descriptions, payloads, unresolved };
}
const textValue = (value: unknown): string => typeof value === 'string' ? value : value == null ? '' : typeof value === 'object' ? JSON.stringify(value) : String(value);
const escapeTitle = (value: unknown) => textValue(value).replace(/[\r\n]/g, ' ').replace(/([\\`*_[\]<>])/g, '\\$1');
export function worldMarkdown(project: WorldProject, cards: CardDetail[], restoreData = true) {
  const byId = new Map(cards.map(card => [card.id, card]));
  const reference = (id: string) => { const card = byId.get(id); return card ? `[${escapeTitle(card.title)}](#card-${card.id})` : '不可用的条目'; };
  const readable = [`# ${escapeTitle(project.name)}`, project.description || '', ...cards.flatMap(card => [
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
    ])].filter(Boolean).join('\n\n');
  if (!restoreData) return readable + '\n';
  const data = { version: 1, textChecksum: textChecksum(readable), cards: cards.map(card => ({ sourceId: card.id, payload: snapshot(card) })) };
  // 转义尖括号，正文中的注释结束符不能破坏恢复数据块。
  return readable + exportMarker + JSON.stringify(data).replace(/</g, '\\u003c').replace(/>/g, '\\u003e') + '\n-->\n';
}
