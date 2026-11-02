// OC 的能力面板接口（后端 Controllers/ChaiCommunity/StickmanPanelController.cs）。
// 每个 OC 两份，分开存、互不覆盖：
//   展示面板（show）：自由设计（自定义维度、刻度），OC 详情页显示；
//   约战面板（battle）：只能用官方约战模板（维度和等级由后端定），约战详情里双方对比用。
// 面板和 OC 的属性、图片是两套接口，保存 OC 不会动面板，保存面板也不会动 OC。
import request from '@/utils/request'
import { ALL_GRADES, type Grade, type StatPanel } from '@/utils/statPanel'

/** 约战模板的维度：key 是稳定标识（OC 的值按它存，改名不变），新维度 key 留空、由后端生成 */
export interface BattleTemplateDim { key: string; name: string; hint?: string | null }
/** 官方约战模板：超级管理员在「管理面板 → 约战面板模板」里改，存在后端 SystemConfigs 里 */
export interface BattlePanelTemplate {
  version: number
  title: string
  scale: 'grade'
  /** 能选的等级（默认 A～E 和 ?） */
  grades: string[]
  dims: BattleTemplateDim[]
  /** 删掉的维度：OC 填过的值还在，只是不显示；管理面板里可以加回来 */
  retired: BattleTemplateDim[]
  /** 当前用户能不能改模板（超级管理员） */
  canEdit?: boolean
}
/** 约战面板：按维度 key 存的等级（可能有模板里已经去掉的旧 key，显示时只看当前模板） */
export interface BattlePanelData { values: Record<string, string>; filled: boolean }
export interface OcPanels { show: StatPanel | null; battle: BattlePanelData | null; canEdit: boolean }

// 模板整个网站共用一份，读一次就记住（失败了下次再读；管理员改完会清掉重读）
let templatePromise: Promise<BattlePanelTemplate> | null = null

export const ocPanelApi = {
  template: () => {
    templatePromise ??= request.get<BattlePanelTemplate>('/StickMan/battle-panel-template', { silentError: true })
      .catch(err => { templatePromise = null; throw err })
    return templatePromise
  },
  /** 管理面板：读最新模板（不用缓存，带 canEdit） */
  templateFresh: () => request.get<BattlePanelTemplate>('/StickMan/battle-panel-template', { silentError: true }),
  /** 管理面板：保存模板（只有超级管理员）；保存后清掉缓存，别的页面下次读到新模板 */
  saveTemplate: (t: Pick<BattlePanelTemplate, 'title' | 'grades' | 'dims'>) =>
    request.put<BattlePanelTemplate>('/StickMan/battle-panel-template', t, { silentError: true })
      .then(res => { templatePromise = null; return res }),
  get: (id: string) => request.get<OcPanels>(`/StickMan/${id}/panel`, { silentError: true }),
  saveShow: (id: string, panel: StatPanel) => request.put<{ show: StatPanel }>(`/StickMan/${id}/panel/show`, panel, { silentError: true }),
  saveBattle: (id: string, values: Record<string, string>) =>
    request.put<{ battle: BattlePanelData }>(`/StickMan/${id}/panel/battle`, { values }, { silentError: true }),
  remove: (id: string, kind: 'show' | 'battle') => request.delete(`/StickMan/${id}/panel/${kind}`, { silentError: true }),
  /** 一次读好几个 OC 的约战面板（没填的、看不到的不在结果里） */
  battlePanels: (ids: string[]) => ids.length
    ? request.get<{ items: Record<string, BattlePanelData> }>('/StickMan/battle-panels', { params: { ids: ids.join(',') }, silentError: true })
    : Promise.resolve({ items: {} as Record<string, BattlePanelData> }),
}

/** 模板里这个维度的等级；没填或者不在模板允许的等级里，都当 ?（未知） */
export function battleGrade(template: BattlePanelTemplate, data: BattlePanelData | null | undefined, key: string): Grade {
  const g = data?.values?.[key]
  return g && template.grades.includes(g) && (ALL_GRADES as string[]).includes(g) ? g as Grade : '?'
}

/** 「填了约战面板」：当前模板里至少有一项不是 ?（和后端 BattlePanelTemplate.IsFilled 一样） */
export const battleFilled = (template: BattlePanelTemplate, data: BattlePanelData | null | undefined) =>
  template.dims.some(d => battleGrade(template, data, d.key) !== '?')

/** 把约战面板按当前模板换成雷达图能画的面板 */
export function battleToStatPanel(template: BattlePanelTemplate, data: BattlePanelData | null | undefined, title = template.title): StatPanel {
  return {
    v: 1, title, scale: 'grade', max: 100, overall: true,
    dims: template.dims.map(d => ({ name: d.name, value: battleGrade(template, data, d.key) })),
  }
}

/** 错误信息：后端给的中文 message 优先 */
export function panelError(err: unknown, fallback: string) {
  const message = (err as { response?: { data?: { message?: unknown } } })?.response?.data?.message
  return typeof message === 'string' && message ? message : fallback
}
