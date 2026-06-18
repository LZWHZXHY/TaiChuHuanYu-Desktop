// src/utils/NoteType.ts

// 1. 严格限制类型的值，包含百科系统和可视化构建所需的新形态
export type NoteType = 
  | 'note'      // 长文随笔
  | 'blog'      // 🌟 核心修复：允许全局流通 blog 多态形态
  | 'post'      // 短篇简语 (🌟 彻底将 'thought' 改为 'post')
  | 'folder'    // 文件夹
  | 'wiki'      // 世界观词条
  | 'char'      // 角色档案
  | 'art'       // 艺术作品
  | 'video'     // 灵脉影像
  | 'audio'     // 太初之音
  | 'canvas'    // 星图白板
  | 'map'       // 世界地图
  | 'excel';

// 2. 统一管理每种类型的显示文本和元数据
export const NOTE_TYPE_CONFIG: Record<NoteType, { label: string; desc: string; icon: string }> = {
  note: {
    label: '长文随笔',
    desc: '深度思考，长篇沉浸式 Markdown 文章',
    icon: '📝'
  },
  blog: { // 🌟 核心修复：补全 blog 节点的显示配置
    label: '深度博客',
    desc: '正式发布的专属长文专栏作品',
    icon: '✒️'
  },
  post: { // 🌟 彻底将 'thought' 键名变更为 'post'
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
  },
  canvas: {
    label: '星图白板',
    desc: '无限视界的无边际节点图谱',
    icon: '🕸️'
  },
  map: {
    label: '世界地图',
    desc: '俯视全局的纯平面坐标系地标',
    icon: '🗺️'
  },
  excel:{
    label: 'Excel表格',
    desc: '测试版excel表格',
    icon: '📊' // 顺手帮你补个小图标
  },
};