// 兼容现有单文档、节点数组及逐行 JSON；损坏的结构化内容不能静默覆盖。
export function parseWikiDocument(content: unknown): any {
    let value = content;
    if (typeof value === 'string') {
        const text = value.trim();
        if (!text)
            return { type: 'doc', content: [] };
        try {
            value = JSON.parse(text);
        }
        catch {
            if (/^[{\[]/.test(text))
                value = text.split('\n').filter(line => line.trim()).map(line => JSON.parse(line));
            else
                return { type: 'doc', content: [{ type: 'paragraph', content: [{ type: 'text', text }] }] };
        }
    }
    if (!value || typeof value !== 'object')
        throw new Error('词条内容格式异常，请联系管理员修复。');
    const doc: any = value;
    if (doc.type === 'doc' && Array.isArray(doc.content))
        return doc;
    const nodes = Array.isArray(doc) ? doc : [doc];
    if (nodes.some(node => !node || typeof node !== 'object' || Array.isArray(node)))
        throw new Error('词条内容格式异常。');
    return { type: 'doc', content: nodes.map(node => ({ ...node, type: node.type || 'paragraph' })) };
}
export function wikiText(value: unknown): string {
    if (typeof value === 'string') {
        try {
            return wikiText(JSON.parse(value));
        }
        catch {
            return value;
        }
    }
    if (Array.isArray(value))
        return value.map(wikiText).join(' ');
    if (value && typeof value === 'object') {
        const node = value as {
            text?: string;
            content?: unknown;
        };
        return node.text ?? wikiText(node.content);
    }
    return '';
}
export function wikiTags(tags: unknown): string[] {
    return (Array.isArray(tags) ? tags : typeof tags === 'string' ? tags.split(/[,，]/) : [])
        .filter((tag): tag is string => typeof tag === 'string').map(tag => tag.trim()).filter(Boolean);
}
