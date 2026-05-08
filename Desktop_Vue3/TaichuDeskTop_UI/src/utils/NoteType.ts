// src/utils/NoteType.ts

// 1. 严格限制类型的值，包含 folder 形态
export type NoteType = 'note' | 'thought' | 'folder' | 'art' | 'video' | 'audio';

// 2. 统一管理每种类型的显示文本和元数据
export const NOTE_TYPE_CONFIG: Record<NoteType, { label: string; desc: string }> = {
  note: {
    label: '长文随笔',
    desc: '深度思考，长篇沉浸式 Markdown 文章'
  },
  thought: {
    label: '短篇简语',
    desc: '随时捕捉瞬息思绪与生活碎念'
  },
  folder: {
    label: '灵脉文件夹',
    desc: '用于组织和分类随笔碎片'
  },
  art: {
    label: '艺术作品',
    desc: '视觉作品、画廊图片展厅'
  },
  video: {
    label: '灵脉影像',
    desc: '视频短片与功能演示'
  },
  audio: {
    label: '太初之音',
    desc: '音乐与音频流转'
  }
};