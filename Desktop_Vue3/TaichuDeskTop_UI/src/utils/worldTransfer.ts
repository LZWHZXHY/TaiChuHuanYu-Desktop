import type { CardDetail, WorldProject, CreateCardPayload } from '@/api/types/world';
import { blockTitle, parseLayout, remapLayoutCards, serializeLayout } from './worldLayout';
import { getCardTypeLabel, parseCoverImage } from './worldPresentation';
import { cardSchemas } from '@/views/世界观管理/card-schemas';
import type { CardType } from '@/views/世界观管理/card_type';
import {
  KIND_META, STATUS_META, eventStart, formatEventDate, normalizeCalendar, orderLanes,
  type ChronicleEvent, type ChronicleExport, type ChronicleTimeline, type EventKind, type TimelineStatus,
} from './chronicle';

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
    embeddedCards: card.embeddedCards, timelineEvents: card.timelineEvents, layout: card.layout ?? null,
    relations: card.outRelations.map(r => ({ targetCardId: r.targetCardId, relationType: r.relationType })),
  };
}

/** 区块里的引用（文本块的 {CARD:id}）和时间线事件的相关条目，换成新世界里的 ID */
function remapLayout(layout: string | null | undefined, target: (id: string) => string) {
  if (!layout) return layout ?? null;
  const text = layout.replace(/\{CARD:([^}]+)\}/g, (_, id: string) => `{CARD:${target(id)}}`);
  // 地图图钉、下一层地图、画布条目卡片、关系树节点里的条目 ID 也要换（fix121）
  const parsed = parseLayout(text);
  return parsed ? serializeLayout(remapLayoutCards(parsed, target)) : text;
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
    timelineEvents: array(v.timelineEvents, raw => {
      const e = object(raw);
      return { id: e.id == null ? undefined : string(e.id), date: string(e.date), era: optional(e.era), title: string(e.title), description: optional(e.description),
        image: optional(e.image), tags: e.tags == null ? [] : array(e.tags, string), cardIds: e.cardIds == null ? [] : array(e.cardIds, string), hidden: e.hidden === true,
        color: optional(e.color) };
    }),
    layout: optional(v.layout),
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
    content: remapContentReferences(item.payload, () => ''), contentBlocks: [], embeddedCards: [], relations: [],
    layout: remapLayout(item.payload.layout?.replace(/\{CARD:[^}]+\}/g, '（引用待恢复）'), () => ''),
    timelineEvents: (item.payload.timelineEvents || []).map(e => ({ ...e, cardIds: [] })) };
}

/** 读出文件末尾的恢复数据块（并核对可读部分没被改过）；没有恢复数据返回 null */
function readRestoreBlock(content: string) {
  if (!hasWorldRestoreData(content)) return null;
  const start = content.lastIndexOf(exportMarker);
  if (start < 0 || !content.endsWith('-->')) throw new Error('导出文件的恢复数据版本或结尾无效。');
  const data = JSON.parse(content.slice(start + exportMarker.length, -3));
  const readable = content.slice(0, start).trimEnd();
  if (data.version !== 1 || data.textChecksum !== textChecksum(readable))
    throw new Error('导出文件的可读内容已修改。为避免忽略修改，已停止完整恢复；可选择按普通 Markdown 导入。');
  return data;
}

function readWorldRestoreData(content: string): WorldTextItem[] | null {
  const data = readRestoreBlock(content);
  if (!data) return null;
  if (!Array.isArray(data.cards)) throw new Error('条目列表格式无效。');
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
const imageUrl = (url: string) => /^(https?:\/\/|\/[^/]|\.\/images\/)/i.test(url.trim()) ? url.trim().replace(/[<>\r\n]/g, '') : '';

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
    layout: remapLayout(item.payload.layout, restoredTarget),
    timelineEvents: (item.payload.timelineEvents || []).map(e => ({ ...e, cardIds: (e.cardIds || []).map(restoredTarget) })),
  } : { title: item.title, type: '', description: descriptions[index], galleryImages: item.images || [] });
  return { descriptions, payloads, unresolved };
}
// ================= 编年史（时间线、事件、历法） =================

const hasOwn = (o: object, key: unknown) => typeof key === 'string' && Object.prototype.hasOwnProperty.call(o, key);

/** 外部文件里的编年史只取业务字段；Id 必须是文字（用来对上事件在哪条线上、从哪分叉），其他字段格式不对就当没写 */
function readChronicle(value: unknown): ChronicleExport | null {
  if (value == null) return null;
  const object = (v: unknown): Record<string, unknown> => {
    if (!v || typeof v !== 'object' || Array.isArray(v)) throw new Error('编年史数据格式无效。');
    return v as Record<string, unknown>;
  };
  const id = (v: unknown) => { if (typeof v !== 'string' || !v) throw new Error('编年史数据里的时间线或事件标识缺失。'); return v; };
  const optional = (v: unknown) => typeof v === 'string' ? v : null;
  const number = (v: unknown) => typeof v === 'number' && Number.isFinite(v) ? v : null;
  const strings = (v: unknown) => Array.isArray(v) ? v.filter((x): x is string => typeof x === 'string') : [];
  const v = object(value);
  if (!Array.isArray(v.timelines) || !Array.isArray(v.events)) throw new Error('编年史数据格式无效。');
  const timelines: ChronicleTimeline[] = v.timelines.map(raw => {
    const t = object(raw);
    return {
      id: id(t.id), name: optional(t.name) ?? '', description: optional(t.description), color: optional(t.color),
      status: hasOwn(STATUS_META, t.status) ? t.status as TimelineStatus : 'canon',
      parentId: optional(t.parentId), forkEventId: optional(t.forkEventId), mergeEventId: optional(t.mergeEventId), cardId: optional(t.cardId),
      sortOrder: number(t.sortOrder) ?? 0, hidden: t.hidden === true,
    };
  });
  const events: ChronicleEvent[] = v.events.map(raw => {
    const e = object(raw);
    return {
      id: id(e.id), timelineId: id(e.timelineId), kind: hasOwn(KIND_META, e.kind) ? e.kind as EventKind : 'event',
      title: optional(e.title) ?? '', description: optional(e.description),
      eraId: optional(e.eraId), year: number(e.year), month: number(e.month), day: number(e.day),
      endEraId: optional(e.endEraId), endYear: number(e.endYear), endMonth: number(e.endMonth), endDay: number(e.endDay),
      dateText: optional(e.dateText), color: optional(e.color), images: strings(e.images), tags: strings(e.tags), cardIds: strings(e.cardIds),
      locationCardId: optional(e.locationCardId), hidden: e.hidden === true, sortOrder: number(e.sortOrder) ?? 0,
    };
  });
  return { calendar: v.calendar && typeof v.calendar === 'object' && !Array.isArray(v.calendar) ? v.calendar : null, timelines, events };
}

/** 导出文件 / 备份里的编年史；老文件没有编年史、普通 Markdown 都返回 null */
export function parseWorldChronicle(text: string): ChronicleExport | null {
  const content = text.replace(/^\uFEFF/, '').replace(/\r\n?/g, '\n').trim();
  return readChronicle(readRestoreBlock(content)?.chronicle);
}

/**
 * 恢复 / 复制世界时，把编年史里引用的条目换成这次新建的条目 Id（ids 和 items 一一对应，同 resolveWorldImport）；
 * 文件里没有的条目（原来就已经删掉的）去掉引用。图片换成已上传的地址。时间线、事件自己的 Id 由服务器换新。
 * 普通 Markdown 导入（条目没有完整数据）不恢复编年史，返回 null。
 */
export function resolveWorldChronicle(chronicle: ChronicleExport | null | undefined, items: WorldTextItem[], ids: string[],
  images?: Map<string, string>): ChronicleExport | null {
  if (!chronicle || !items.every(item => item.payload)) return null;
  const source = images ? replaceImageUrls(chronicle, images) : chronicle;
  const map = new Map(items.flatMap((item, index) => item.sourceId && ids[index] ? [[item.sourceId, ids[index]!] as const] : []));
  const card = (id: string | null | undefined) => (id ? map.get(id) ?? null : null);
  const refs = (text: string | null | undefined) => text == null ? null
    : text.replace(/(!?)\{CARD:([^}]+)\}/g, (_, bang: string, id: string) => map.has(id) ? `${bang}{CARD:${map.get(id)}}` : '（已删除的条目）');
  return {
    calendar: source.calendar ?? null,
    timelines: source.timelines.map(line => ({ ...line, cardId: card(line.cardId) })),
    events: source.events.map(e => ({
      ...e, title: refs(e.title) ?? '', description: refs(e.description),
      cardIds: e.cardIds.map(card).filter((id): id is string => Boolean(id)), locationCardId: card(e.locationCardId),
    })),
  };
}

/** 图片地址替换（和 worldBackup 的 replaceWorldImages 一样的规则：长的先换，避免短地址是长地址的一部分） */
function replaceImageUrls(chronicle: ChronicleExport, urls: Map<string, string>): ChronicleExport {
  const entries = [...urls].sort((a, b) => b[0].length - a[0].length);
  const swap = (v: string) => entries.reduce((s, [from, to]) => s.split(from).join(to), v);
  const opt = (v: string | null | undefined) => v == null ? v ?? null : swap(v);
  return {
    ...chronicle,
    events: chronicle.events.map(e => ({ ...e, images: e.images.map(swap), description: opt(e.description) })),
  };
}

/** 可读稿里的编年史：每条时间线一节，事件按时间排（时间未定的放最后） */
function chronicleMarkdown(chronicle: ChronicleExport, reference: (id: string) => string): string[] {
  if (!chronicle.timelines.length) return [];
  const cal = normalizeCalendar(chronicle.calendar);
  const refs = (text: string) => text.replace(/!?\{CARD:([^}]+)\}/g, (_, id: string) => reference(id));
  const prose = (text: string) => mapWorldProse(text, line => line.replace(/^\s*!(?=\{CARD:)/, '').replace(/^#{1,5}\s+/, '###### ')
    .replace(/\{CARD:([^}]+)\}/g, (_, id: string) => reference(id)));
  const out = ['## 编年史', `历法：${cal.eras.map(e => escapeTitle(e.name)).join(' / ')}`];
  for (const { line } of orderLanes(chronicle.timelines)) {
    const parent = line.parentId ? chronicle.timelines.find(l => l.id === line.parentId) : undefined;
    out.push(`### ${escapeTitle(line.name)}（${STATUS_META[line.status]?.label ?? '正史'}${parent ? `，从「${escapeTitle(parent.name)}」分出` : ''}）${line.hidden ? '（仅成员可见）' : ''}`);
    if (line.description) out.push(prose(line.description));
    const events = chronicle.events.filter(e => e.timelineId === line.id)
      .map(e => ({ e, t: eventStart(cal, e) }))
      .sort((a, b) => (a.t ?? Infinity) - (b.t ?? Infinity) || a.e.sortOrder - b.e.sortOrder);
    for (const { e } of events) {
      const kind = e.kind !== 'event' ? `［${KIND_META[e.kind]?.label ?? ''}］` : '';
      out.push(`#### ${escapeTitle(formatEventDate(cal, e))}：${kind}${refs(escapeTitle(e.title))}${e.hidden ? '（仅成员可见）' : ''}`);
      if (e.description) out.push(prose(e.description));
      if (e.tags.length) out.push(`标签：${e.tags.map(escapeTitle).join('、')}`);
    }
  }
  return out;
}

const textValue = (value: unknown): string => typeof value === 'string' ? value : value == null ? '' : typeof value === 'object' ? JSON.stringify(value) : String(value);
const escapeTitle = (value: unknown) => textValue(value).replace(/[\r\n]/g, ' ').replace(/([\\`*_[\]<>])/g, '\\$1');
export function worldMarkdown(project: WorldProject, cards: CardDetail[], restoreData = true, chronicle: ChronicleExport | null = null) {
  const byId = new Map(cards.map(card => [card.id, card]));
  const reference = (id: string) => { const card = byId.get(id); return card ? `[${escapeTitle(card.title)}](#card-${card.id})` : '不可用的条目'; };
  const readable = [`# ${escapeTitle(project.name)}`, project.description || '', ...cards.flatMap(card => [
      `## ${escapeTitle(card.title)}`, `<a id="card-${card.id}"></a>`, `类型：${escapeTitle(getCardTypeLabel(card.type))}`,
      card.aliases.length ? `别名：${card.aliases.map(escapeTitle).join('、')}` : '',
      card.tags.length ? `标签：${card.tags.map(escapeTitle).join('、')}` : '',
      // 「显示全部内容」的引用（单独一行的 !{CARD:id}）导出时当普通引用，避免被当成 Markdown 图片
      mapWorldProse(card.description || '', line => line.replace(/^\s*!(?=\{CARD:)/, '').replace(/^#{1,2}\s+/, '### ')
        .replace(/\{CARD:([^}]+)\}/g, (_, id: string) => reference(id))),
      // 自定义文本块（背景、性格……）
      ...(parseLayout(card.layout)?.rows.flatMap(r => r.blocks) ?? []).filter(b => b.kind === 'text' && b.text?.trim()).flatMap(b => [
        `### ${escapeTitle(blockTitle(b))}${b.hidden ? '（仅成员可见）' : ''}`,
        mapWorldProse(b.text || '', line => line.replace(/^\s*!(?=\{CARD:)/, '').replace(/^#{1,3}\s+/, '#### ').replace(/\{CARD:([^}]+)\}/g, (_, id: string) => reference(id))),
      ]),
      ...(parseLayout(card.layout)?.rows.flatMap(r => r.blocks) ?? []).filter(b => b.kind === 'media').flatMap(b => (b.images || []).map(imageUrl).filter(Boolean)
        .map((url, index) => `![${escapeTitle(card.title)} · ${escapeTitle(blockTitle(b))} ${index + 1}](<${url}>)`)),
      // 参考来源
      ...(parseLayout(card.layout)?.rows.flatMap(r => r.blocks) ?? []).filter(b => b.kind === 'sources' && b.sources?.some(x => x.title?.trim() || x.url?.trim())).flatMap(b => [
        `### ${escapeTitle(blockTitle(b))}${b.hidden ? '（仅成员可见）' : ''}`,
        ...(b.sources ?? []).filter(x => x.title?.trim() || x.url?.trim()).map(x => {
          const url = /^https?:\/\//i.test(x.url?.trim() ?? '') ? x.url!.trim() : ''
          const name = escapeTitle(x.title?.trim() || x.url?.trim() || '')
          return `- ${url ? `[${name}](<${url}>)` : name}${x.note ? `（${escapeTitle(x.note)}）` : ''}`
        }),
      ]),
      ...card.attributes.filter(a => textValue(a.value).trim()).map(a => `- ${escapeTitle(a.key)}：${escapeTitle(a.value)}`),
      ...card.contentBlocks.map(block => `- ${escapeTitle(block.contextLabel || '引用')}：${reference(block.cardId)}`),
      ...card.outRelations.map(r => `- ${escapeTitle(r.relationType)} → ${reference(r.targetCardId)}`),
      ...card.timelineEvents.map(e => `- ${escapeTitle(e.era || '')} ${escapeTitle(e.date)} ${escapeTitle(e.title)}：${e.description || ''}`),
      ...(imageUrl(parseCoverImage(card.coverImage)) ? [`![${escapeTitle(card.title)} · 封面](<${imageUrl(parseCoverImage(card.coverImage))}>)`] : []),
      ...card.embeddedCards.filter(id => !card.contentBlocks.some(b => b.cardId === id)).map(id => `- 引用：${reference(id)}`),
      ...card.galleryImages.map(imageUrl).filter(Boolean).map((url, index) => `![${escapeTitle(card.title)} · 图片 ${index + 1}](<${url}>)`),
      card.content && !['{}', 'null', ''].includes(card.content) ? `\`\`\`json\n${card.content.replace(/`/g, '\\u0060')}\n\`\`\`` : '',
    ]),
    // 编年史放在最后（PDF、Markdown 里也能读到）
    ...(chronicle ? chronicleMarkdown(chronicle, reference) : []),
  ].filter(Boolean).join('\n\n');
  if (!restoreData) return readable + '\n';
  // chronicle：编年史的完整数据（fix121 新增；老文件没有这一项，恢复时就跳过编年史）
  const data = { version: 1, textChecksum: textChecksum(readable), cards: cards.map(card => ({ sourceId: card.id, payload: snapshot(card) })),
    ...(chronicle ? { chronicle } : {}) };
  // 转义尖括号，正文中的注释结束符不能破坏恢复数据块。
  return readable + exportMarker + JSON.stringify(data).replace(/</g, '\\u003c').replace(/>/g, '\\u003e') + '\n-->\n';
}
