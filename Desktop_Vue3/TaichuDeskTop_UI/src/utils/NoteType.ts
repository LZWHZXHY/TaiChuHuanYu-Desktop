// src/utils/NoteType.ts

// 1. 严格限制类型的值，包含百科系统所需的新形态
export type NoteType = 
  | 'note'      // 长文随笔
  | 'thought'   // 短篇简语
  | 'folder'    // 文件夹
  | 'wiki'      // 世界观词条 (新增)
  | 'char'      // 角色档案 (新增)
  | 'art'       // 艺术作品
  | 'video'     // 灵脉影像
  | 'audio';    // 太初之音

// 2. 统一管理每种类型的显示文本和元数据
export const NOTE_TYPE_CONFIG: Record<NoteType, { label: string; desc: string; icon: string }> = {
  note: {
    label: '长文随笔',
    desc: '深度思考，长篇沉浸式 Markdown 文章',
    icon: '📝'
  },
  thought: {
    label: '短篇简语',
    desc: '随时捕捉瞬息思绪与生活碎念',
    icon: '💬'
  },
  wiki: {
    label: '世界观词条',
    desc: '设定、地理、法宝或历史，构建 IP 的底层逻辑',
    icon: '🪐'
  },
  char: {
    label: '角色档案',
    desc: '核心人物、OC 设定，支持属性数值化展现',
    icon: '👤'
  },
  folder: {
    label: '灵脉文件夹',
    desc: '用于组织和分类随笔碎片',
    icon: '📁'
  },
  art: {
    label: '艺术作品',
    desc: '视觉作品、画廊图片展厅',
    icon: '🎨'
  },
  video: {
    label: '灵脉影像',
    desc: '视频短片与功能演示',
    icon: '🎬'
  },
  audio: {
    label: '太初之音',
    desc: '音乐与音频流转',
    icon: '🎵'
  }
};