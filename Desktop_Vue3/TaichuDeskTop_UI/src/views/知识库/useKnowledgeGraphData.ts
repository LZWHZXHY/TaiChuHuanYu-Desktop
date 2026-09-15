import request from '@/utils/request'
import type { GraphDataResponse, NodeTheme } from './types'

export function useKnowledgeGraphData() {
const NODE_THEMES: Record<string, NodeTheme> = {
    character: { color: '#c084fc', emissive: '#7e22ce', label: '#f5d0fe', typeLabel: 'CHARACTER' },
    location: { color: '#38bdf8', emissive: '#0369a1', label: '#bae6fd', typeLabel: 'LOCATION' },
    concept: { color: '#34d399', emissive: '#047857', label: '#a7f3d0', typeLabel: 'CONCEPT' },
    event: { color: '#fbbf24', emissive: '#b45309', label: '#fde68a', typeLabel: 'EVENT' },
    organization: { color: '#fb7185', emissive: '#be123c', label: '#fecdd3', typeLabel: 'ORGANIZATION' },
    blog: { color: '#f472b6', emissive: '#db2777', label: '#fbcfe8', typeLabel: 'BLOG' },
    post: { color: '#818cf8', emissive: '#4f46e5', label: '#c7d2fe', typeLabel: 'POST' },
    wiki: { color: '#60a5fa', emissive: '#2563eb', label: '#bfdbfe', typeLabel: 'WIKI' },
    setting: { color: '#facc15', emissive: '#ca8a04', label: '#fef08a', typeLabel: 'SETTING' },
    // 👇 新增：专门给 NAS 文件用的数据晶体主题
    // 在 useKnowledgeGraphData.ts 的 NODE_THEMES 中追加或修改：
    folder: { color: '#f59e0b', emissive: '#d97706', label: '#fef3c7', typeLabel: 'DIRECTORY' },
    file: { color: '#0ea5e9', emissive: '#0284c7', label: '#e0f2fe', typeLabel: 'DATA_FILE' },
    default: { color: '#94a3b8', emissive: '#334155', label: '#e2e8f0', typeLabel: 'NODE' }
  }

  const getTheme = (type: string): NodeTheme => {
    return NODE_THEMES[type] || NODE_THEMES.default
  }

  const getNodeSize = (size: number): number => {
    const value = Number(size)
    if (!Number.isFinite(value) || value <= 0) return 6
    return Math.max(4, Math.min(18, Math.sqrt(value) * 1.8))
  }

  const escapeHtml = (value: unknown): string => {
    return String(value ?? '')
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;')
  }

  const hexToRgba = (hex: string, opacity: number): string => {
    const clean = hex.replace('#', '')
    const bigint = parseInt(clean, 16)
    const r = (bigint >> 16) & 255
    const g = (bigint >> 8) & 255
    const b = bigint & 255
    return `rgba(${r}, ${g}, ${b}, ${opacity})`
  }

  const fetchData = async (): Promise<GraphDataResponse> => {
    try {
      const res = await request.get<GraphDataResponse>('/knowledge-base/Graph/space/00000000-0000-0000-0000-000000000000')
      return res
    } catch (error) {
      console.error('3D 星图数据拉取失败：', error)
      return { nodes: [], edges: [] }
    }
  }

  return {
    fetchData,
    getTheme,
    getNodeSize,
    escapeHtml,
    hexToRgba
  }
}