<!-- src/components/KnowledgeGraph/index.vue -->
<template>
  <div class="graph-wrapper" @click="handleBackgroundClick">
    <GraphControlPanel 
      v-model:searchQuery="searchQuery" 
      v-model:repulsionForce="repulsionForce"
      :showLabels="showLabels" 
      @toggle:labels="handleToggleLabels"
      @search="handleSearch"
    />

    <GraphFilterPanel
      :availableTypes="availableTypes"
      v-model:activeTypes="activeTypes"
    />

    <GraphDetailsPanel 
      :show="showDetails" 
      :node="selectedNode" 
      :neighbors="neighborNodes"
      @close="closeDetailsPanel"
      @navigate:node="navigateToNeighborNode"
    />

    <div ref="containerRef" class="graph-container"></div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount, computed, watch } from 'vue'
import type { GraphNode, GraphLink } from './types'
import { useKnowledgeGraphEngine } from './useKnowledgeGraphEngine'
import { useKnowledgeGraphData } from './useKnowledgeGraphData'
import GraphControlPanel from './GraphControlPanel.vue'
import GraphDetailsPanel from './GraphDetailsPanel.vue'
import GraphFilterPanel from './GraphFilterPanel.vue'
import request from '@/utils/request' 

const searchQuery = ref('')
const showLabels = ref(true)
const showDetails = ref(false)
const repulsionForce = ref(50)
const selectedNode = ref<GraphNode | null>(null)
const containerRef = ref<HTMLElement | null>(null)
let resizeObserver: ResizeObserver | null = null

const availableTypes = ref<string[]>([])
const activeTypes = ref<string[]>([])

const { fetchData } = useKnowledgeGraphData()

const escapeHtml = (text: string) => {
  return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
};

const extractTextFromTiptap = (node: any): string => {
  if (!node) return '';
  
  let prefix = '';
  let suffix = '';

  if (node.type === 'heading') {
    const level = node.attrs?.level || 1;
    prefix = '#'.repeat(level) + ' ';
    suffix = '\n\n';
  } else if (node.type === 'listItem') {
    prefix = '• ';
    suffix = '\n';
  } else if (node.type === 'spiritLink') {
    return `<span style="color: #38bdf8; font-weight: 600; background: rgba(56, 189, 248, 0.1); padding: 2px 6px; border-radius: 4px; margin: 0 2px;">【${escapeHtml(node.attrs?.title || '关联碎片')}】</span>`;
  } else if (node.type === 'image') {
    const src = node.attrs?.src || '';
    const alt = escapeHtml(node.attrs?.alt || '');
    return `\n\n<img src="${src}" alt="${alt}" />\n\n`;
  } else if (node.type === 'text') {
    return escapeHtml(node.text || '');
  }

  let childText = '';
  if (Array.isArray(node.content)) {
    childText = node.content.map((c: any) => extractTextFromTiptap(c)).join('');
  } else if (node.text) {
    childText = escapeHtml(node.text);
  }

  return prefix + childText + suffix;
};

const parseBlockDataToText = (rawText: string): string => {
  if (!rawText || rawText === '[]' || rawText === '{}' || rawText === 'null') return '';
  
  try {
    const parsed = JSON.parse(rawText);
    let blocks: any[] = [];

    if (Array.isArray(parsed) && typeof parsed[0] === 'string') {
      blocks = parsed.map(str => {
        try { return JSON.parse(str); } catch { return { type: 'text', text: str }; }
      });
    } else if (Array.isArray(parsed)) {
      blocks = parsed;
    } else if (parsed?.type === 'doc' && Array.isArray(parsed.content)) {
      blocks = parsed.content;
    } else {
      blocks = [parsed];
    }

    let htmlText = blocks
      .map(b => extractTextFromTiptap(b).trim())
      .filter(text => text.length > 0)
      .join('\n\n'); 
    
    htmlText = htmlText.replace(
      /!\[([^\]]*)\]\(([^)]+)\)/g, 
      '\n\n<img src="$2" alt="$1" />\n\n'
    );

    return htmlText;
  } catch (e) {
    return escapeHtml(rawText);
  }
};

const { 
  graph, 
  initEngine, 
  flyCameraToNode, 
  toggleLabelsVisibility, 
  destroyEngine, 
  handleResize,
  updateRepulsion,
  updateVisibility
} = useKnowledgeGraphEngine(containerRef, {
  onNodeClick: async (node) => {
    if (selectedNode.value?.id === node.id && showDetails.value) return
    
    selectedNode.value = node
    showDetails.value = true
    flyCameraToNode(node)

    if (node.type === 'file') {
     const filePath = node._nasPath || '';
      
      const customHtml = `
        <div class="nas-attachment-card" data-filename="${filePath}">
          <div class="nas-icon">📦</div>
          <div class="nas-info">
            <div class="nas-name">${node.name}</div>
            <div class="nas-size">太初资源中枢 (MinIO)</div>
          </div>
          <div class="nas-download-btn">点击下载资源</div>
        </div>
        <p style="color: #64748b; font-size: 13px; margin-top: 16px; line-height: 1.6;">
          这是通过太初寰宇空间网络直接挂载的物理资源。底层由绿联 NAS 对象存储引擎提供高并发读取支持。
        </p>
      `;

      selectedNode.value = { ...node, description: customHtml };
      return; 
    }

    try {
      const res: any = await request.get(`/knowledge-base/Node/${node.id}`)
      
      const data = res.data || res; 
      const rawText = data.description || data.Description || data.content || data.Content;

      const cleanText = parseBlockDataToText(rawText);

      if (selectedNode.value && selectedNode.value.id === node.id) {
        selectedNode.value = { 
          ...selectedNode.value, 
          description: cleanText 
        }
      }
    } catch (error) {
      console.error('寰宇纪要拉取失败:', error)
    }
  }
})

watch(activeTypes, (newTypes) => {
  updateVisibility(newTypes)
}, { deep: true })

watch(repulsionForce, (newForce) => {
  updateRepulsion(newForce)
})

const neighborNodes = computed<GraphNode[]>(() => {
  if (!selectedNode.value || !graph.value) return []
  const selectedId = selectedNode.value.id
  const { links, nodes } = graph.value.graphData()
  const neighborIds = new Set<string>()

  links.forEach((link: GraphLink) => {
    const sourceId = typeof link.source === 'object' ? link.source.id : link.source
    const targetId = typeof link.target === 'object' ? link.target.id : link.target
    if (sourceId === selectedId) neighborIds.add(targetId)
    else if (targetId === selectedId) neighborIds.add(sourceId)
  })

  return nodes.filter((n: GraphNode) => neighborIds.has(n.id) && n.id !== selectedId)
})

const handleToggleLabels = () => {
  showLabels.value = !showLabels.value
  toggleLabelsVisibility(showLabels.value)
}

const handleSearch = () => {
  if (!graph.value || !searchQuery.value.trim()) return
  const query = searchQuery.value.toLowerCase().trim()
  const { nodes } = graph.value.graphData()
  const targetNode = nodes.find((n: any) => n.name && n.name.toLowerCase().includes(query))
  if (targetNode) {
    flyCameraToNode(targetNode)
    searchQuery.value = ''
  }
}

const closeDetailsPanel = () => {
  showDetails.value = false
  selectedNode.value = null
}

const handleBackgroundClick = (event: MouseEvent) => {
  const target = event.target as HTMLElement
  if (showDetails.value && !target.closest('.details-panel') && !target.closest('.control-panel') && !target.closest('.filter-panel')) {
    closeDetailsPanel()
  }
}

const navigateToNeighborNode = (neighborNode: GraphNode) => {
  selectedNode.value = neighborNode
  flyCameraToNode(neighborNode, true, 1000)
}

onMounted(async () => {
  const res = await fetchData()
  const graphNodes = [...res.nodes]
  const graphEdges = [...res.edges]

  try {
    const nasRes: any = await request.get('/Resource/list') 
    
    if (nasRes.success && nasRes.data && nasRes.data.length > 0) {
      const rootHubId = 'virtual_nas_hub'
      
      // 1. 创建最顶层的“资源中枢”节点
      graphNodes.push({
        id: rootHubId,
        title: '太初资源中枢',
        type: 'organization', 
        size: 15,
        coverImage: null
      })

      // 用一个 Set 记录已经创建过的文件夹路径，避免重复生成相同的文件夹节点
      const createdFolders = new Set<string>()

      // 2. 遍历并解析所有文件的层级路径
      nasRes.data.forEach((file: any, index: number) => {
        // 将 "PS笔刷/分类A/笔刷1.abr" 拆分为 ["PS笔刷", "分类A", "笔刷1.abr"]
        const pathParts = file.name.split('/')
        
        // 游标，记录当前层级的父节点 ID，初始为顶层中枢
        let currentParentId = rootHubId 

        // 遍历前面的部分，生成文件夹节点 (跳过最后一个真正的文件名)
        for (let i = 0; i < pathParts.length - 1; i++) {
          const folderName = pathParts[i]
          // 用完整的层级路径作为唯一 ID，例如 "folder_PS笔刷/分类A"
          const folderFullPath = pathParts.slice(0, i + 1).join('/')
          const folderId = `folder_${folderFullPath}`

          // 如果这个文件夹节点还没被创建过，就生成它
          if (!createdFolders.has(folderId)) {
            createdFolders.add(folderId)
            
            graphNodes.push({
              id: folderId,
              title: folderName,
              type: 'folder', // 使用刚注册的文件夹专属主题
              size: 8,        // 文件夹比文件大一点，比中枢小一点
              coverImage: null
            })

            // 把文件夹和它的上一级父节点连起来
            graphEdges.push({
              id: `edge_${currentParentId}_${folderId}`,
              source: currentParentId,
              target: folderId,
              relationType: 'DIR_LINK'
            })
          }
          
          // 将游标下移，下一次循环或生成文件时，就挂载到这个文件夹下面
          currentParentId = folderId
        }

        // 3. 处理最后一个部分：真正的文件节点
        const fileName = pathParts[pathParts.length - 1]
        const fileNodeId = `nas_file_${index}`
        
        graphNodes.push({
          id: fileNodeId,
          title: file.name.split('/').pop(),
          type: 'file', 
          size: 4, 
          // 👇 取消 description 的赋值，直接强行注入一个自定义属性 _nasPath
          _nasPath: file.name, 
          coverImage: null
        })

        // 将文件节点挂载到它所属的最终层级文件夹下
        graphEdges.push({
          id: `edge_${currentParentId}_${fileNodeId}`,
          source: currentParentId,
          target: fileNodeId,
          relationType: 'FILE_LINK'
        })
      })
    }
  } catch (error) {
    console.error('NAS 资源拉取失败，跳过渲染:', error)
  }

  const typesSet = new Set<string>()
  graphNodes.forEach((n: any) => {
    if (n.type) typesSet.add(n.type)
  })
  availableTypes.value = Array.from(typesSet)
  
  initEngine(graphNodes, graphEdges, showLabels.value)

  window.addEventListener('resize', handleResize)
  if (containerRef.value) {
    resizeObserver = new ResizeObserver(() => handleResize())
    resizeObserver.observe(containerRef.value)
  }
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', handleResize)
  resizeObserver?.disconnect()
  document.body.style.cursor = 'default'
  destroyEngine()
})
</script>

<style scoped>
.graph-wrapper {
  position: relative;
  width: 100%;
  height: 100%;
  overflow: hidden;
}

.graph-container {
  width: 100%;
  height: 100%;
  margin: 0;
  padding: 0;
  background: radial-gradient(circle at 50% 50%, rgba(15, 23, 42, 0.2) 0%, #020617 80%);
}

:deep(canvas) {
  display: block;
  width: 100% !important;
  height: 100% !important;
  outline: none;
}

:deep(.scene-tooltip) {
  padding: 0 !important;
  color: #e2e8f0 !important;
  background: transparent !important;
  border: 0 !important;
  box-shadow: none !important;
  pointer-events: none !important;
}

:deep(.graph-node-tooltip) {
  position: relative;
  min-width: 120px;
  padding: 10px 14px;
  background: rgba(2, 6, 23, 0.7);
  border: 1px solid rgba(56, 189, 248, 0.3);
  box-shadow: 
    0 4px 20px rgba(0, 0, 0, 0.5),
    inset 0 0 15px rgba(56, 189, 248, 0.1);
  backdrop-filter: blur(8px);
  -webkit-backdrop-filter: blur(8px);
  pointer-events: none;
  clip-path: polygon(0 0, calc(100% - 10px) 0, 100% 10px, 100% 100%, 10px 100%, 0 calc(100% - 10px));
}

:deep(.graph-node-tooltip::before) {
  content: '';
  position: absolute;
  top: 0; left: 0; width: 100%; height: 100%;
  border: 1px solid transparent;
  background: 
    linear-gradient(to right, #38bdf8 2px, transparent 2px) 0 0,
    linear-gradient(to bottom, #38bdf8 2px, transparent 2px) 0 0,
    linear-gradient(to left, #38bdf8 2px, transparent 2px) 100% 100%,
    linear-gradient(to top, #38bdf8 2px, transparent 2px) 100% 100%;
  background-repeat: no-repeat;
  background-size: 8px 8px;
  pointer-events: none;
}

:deep(.tooltip-title) {
  font-size: 14px;
  font-weight: 600;
  line-height: 1.4;
  color: #f8fafc;
  white-space: nowrap;
  letter-spacing: 1px;
}

:deep(.tooltip-type) {
  margin-top: 4px;
  color: #38bdf8;
  font-size: 9px;
  font-family: monospace;
  letter-spacing: 2px;
  text-transform: uppercase;
  opacity: 0.8;
}

:deep(.relation-tooltip) {
  padding: 4px 10px;
  color: #94a3b8;
  background: rgba(2, 6, 23, 0.9);
  border-top: 1px solid rgba(168, 85, 247, 0.5);
  border-bottom: 1px solid rgba(168, 85, 247, 0.5);
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 1px;
  text-transform: uppercase;
  pointer-events: none;
  backdrop-filter: blur(4px);
}
</style>