// 旧版编辑器用 Markdown 保存正文，迁移到富文本后，`![描述](地址)` 只会显示成一行文字。
// 展示和打开编辑时，把“整段只有 Markdown 图片”的段落还原为图片节点；其余段落原样保留。

const MARKDOWN_IMAGE = /!\[([^\]]*)\]\(\s*<?(https?:\/\/[^)\s>]+)>?(?:\s+"[^"]*")?\s*\)/g;

function paragraphText(node: any): string | null {
  if (!Array.isArray(node?.content)) return null;
  let text = '';
  for (const child of node.content) {
    if (child?.type === 'text' && typeof child.text === 'string') text += child.text;
    else if (child?.type === 'hardBreak') text += '\n';
    else return null; // 段落里有图片、公式等其他节点时不改动。
  }
  return text;
}

function imagesFromParagraph(node: any): any[] | null {
  if (node?.type !== 'paragraph') return null;
  const text = paragraphText(node);
  if (!text || !text.includes('![')) return null;
  const matches = [...text.matchAll(MARKDOWN_IMAGE)];
  if (!matches.length || text.replace(MARKDOWN_IMAGE, '').trim()) return null;
  return matches.map((match, index) => ({
    type: 'image',
    attrs: {
      id: index === 0 ? node.attrs?.id : undefined,
      src: match[2],
      alt: match[1] && match[1] !== '图片描述' ? match[1] : null,
      title: null,
      align: 'center',
      width: '100%',
      caption: '',
      assetId: null,
    },
  }));
}

/** 返回新的文档对象；没有旧格式图片时返回原对象。 */
export function upgradeLegacyImages<T extends { content?: any[] }>(doc: T): T {
  if (!doc || !Array.isArray(doc.content)) return doc;
  let changed = false;
  const content = doc.content.flatMap((node: any) => {
    const images = imagesFromParagraph(node);
    if (!images) return [node];
    changed = true;
    return images;
  });
  return changed ? { ...doc, content } : doc;
}
