import { parseWikiDocument, wikiText } from './wikiContent';
// 对齐顶层内容块；优先沿用稳定块 ID，没有 ID 的旧词条按相同内容对齐。
export function wikiDiff(before: unknown, after: unknown) {
    const left = parseWikiDocument(before).content as any[], right = parseWikiDocument(after).content as any[];
    const sameBlock = (a: any, b: any) => a.attrs?.id && b.attrs?.id
        ? a.attrs.id === b.attrs.id
        : a.type === b.type && (wikiText(a) ? wikiText(a) === wikiText(b) : a.attrs?.src === b.attrs?.src && a.attrs?.url === b.attrs?.url);
    if (left.length * right.length > 250000)
        throw new Error('词条过长，请查看完整历史版本进行比对。');
    const rows = Array.from({ length: left.length + 1 }, () => new Uint32Array(right.length + 1));
    for (let i = left.length - 1; i >= 0; i--)
        for (let j = right.length - 1; j >= 0; j--)
            rows[i]![j] = sameBlock(left[i], right[j]) ? rows[i + 1]![j + 1]! + 1 : Math.max(rows[i + 1]![j]!, rows[i]![j + 1]!);
    const changes: {
        before: string;
        after: string;
        oldParagraph?: number;
        newParagraph?: number;
        structural: boolean;
    }[] = [];
    const add = (a: any, b: any, i?: number, j?: number) => {
        if (JSON.stringify(a) === JSON.stringify(b))
            return;
        const oldText = wikiText(a), newText = wikiText(b);
        changes.push({ before: oldText, after: newText, oldParagraph: i === undefined ? undefined : i + 1, newParagraph: j === undefined ? undefined : j + 1, structural: oldText === newText });
    };
    let i = 0, j = 0;
    while (i < left.length || j < right.length) {
        if (i < left.length && j < right.length && sameBlock(left[i], right[j])) {
            add(left[i], right[j], i++, j++);
        }
        else if (i < left.length && (j === right.length || rows[i + 1]![j]! >= rows[i]![j + 1]!)) {
            add(left[i], undefined, i++);
        }
        else {
            add(undefined, right[j], undefined, j++);
        }
    }
    // 旧词条首次编辑尚无稳定 ID，相邻的删除/新增可作为一段前后内容展示。
    return changes.reduce<typeof changes>((result, change) => {
        const previous = result[result.length - 1];
        if (previous?.oldParagraph && !previous.newParagraph && !change.oldParagraph && change.newParagraph) {
            previous.after = change.after;
            previous.newParagraph = change.newParagraph;
            previous.structural = previous.before === previous.after;
        }
        else
            result.push(change);
        return result;
    }, []);
}
export function changedText(before: string, after: string) {
    let start = 0, end = 0;
    while (start < Math.min(before.length, after.length) && before[start] === after[start])
        start++;
    while (end < Math.min(before.length, after.length) - start && before[before.length - 1 - end] === after[after.length - 1 - end])
        end++;
    return { prefix: before.slice(0, start), removed: before.slice(start, before.length - end), added: after.slice(start, after.length - end), suffix: end ? before.slice(-end) : '' };
}
