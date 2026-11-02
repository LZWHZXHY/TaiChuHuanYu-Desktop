// src/api/worldLegend.ts：时间线颜色图例（每个世界自己规定每种颜色代表什么，比如 红 = 战争）
import request from '@/utils/request'

export interface WorldLegendData {
  /** 色系 → 含义，比如 { red: '战争', gray: '灾难' }；没定义的颜色不在里面 */
  items: Record<string, string>
  /** 当前用户能不能改（只有创建者能改） */
  canEdit: boolean
}

const base = (pid: string) => `/world/projects/${pid}/legend`

export const worldLegendApi = {
  // 读失败（比如没权限）就当作没有图例，不弹全局错误提示
  get: (pid: string) => request.get<WorldLegendData>(base(pid), { silentError: true }),
  save: (pid: string, items: Record<string, string>) => request.put<WorldLegendData>(base(pid), { items }),
}
