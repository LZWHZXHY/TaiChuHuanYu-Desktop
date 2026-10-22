import { nanoid } from 'nanoid';

// src/utils/editorHelpers.ts
export const checkHasImage = (node: any): boolean => {
  if (!node) return false;
  if (node.type === 'image') return true;
  if (node.content && Array.isArray(node.content)) return node.content.some(checkHasImage);
  return false;
};

export const getTextLength = (node: any): number => {
  if (!node) return 0;
  let len = 0;
  if (node.text) len += node.text.length;
  if (node.content && Array.isArray(node.content)) {
    node.content.forEach((child: any) => len += getTextLength(child));
  }
  return len;
};
/** 保留各入口的正文形式：rawContent 由 API 拍平，blocks 则直接提交。 */
export function createNoteSyncPayload(noteId: string, note: { title?: string; extraData?: string; tags?: string[] } | null | undefined,
  content: { rawContent?: any; blocks?: any[] }) {
  return { noteId, title: note?.title || '', extraData: note?.extraData || '[]', tags: note?.tags || [], ...content };
}

export function createEditorBlocks(json: any, noteId: string, noteType = 'note') {
  return (json?.content || []).map((block: any, index: number) => ({
    id: block.attrs?.id || nanoid(21), ownerId: noteId, ownerType: noteType,
    type: block.type, sortOrder: index, data: JSON.stringify(block),
  }));
}
