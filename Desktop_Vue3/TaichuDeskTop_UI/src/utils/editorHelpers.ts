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