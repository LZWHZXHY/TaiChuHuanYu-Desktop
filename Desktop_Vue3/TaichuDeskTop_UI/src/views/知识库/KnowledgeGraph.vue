<!-- KnowledgeGraph.vue：知识库关系图谱，用和世界观一样的「粒子星图」。每个节点是一团粒子，阵型切换时粒子飞散重组；
     点一个节点：粒子聚成以它为中心的星环（有封面的变成粒子画像），右侧显示它的纪要和关联碎片。 -->
<template>
  <div class="kg">
    <div class="kg-bar">
      <form class="kg-search" role="search" @submit.prevent="handleSearch">
        <input v-model="searchQuery" type="search" placeholder="搜索节点（标题、简介和正文）" aria-label="搜索节点" @input="onSearchInput" @keydown.esc="clearSearch" />
        <button v-if="appliedQuery" type="button" @click="clearSearch">清除</button>
        <button v-else type="submit">搜索</button>
        <!-- 搜索结果：所有包含关键词的节点，星图里也只留这些 -->
        <div v-if="appliedQuery && showHits" class="kg-hits" role="listbox" aria-label="搜索结果">
          <p class="kg-hits-head">
            <span>{{ searchMatches.length ? `找到 ${searchMatches.length} 个节点` : `没有包含「${appliedQuery}」的节点` }}<em v-if="serverSearching"> · 正在查正文…</em></span>
            <button type="button" aria-label="收起搜索结果" @click="showHits = false">收起</button>
          </p>
          <button v-for="n in searchMatches.slice(0, 80)" :key="n.id" type="button" role="option" class="kg-hit" :style="{ '--c': getTheme(n.type).color }" @click="pickHit(n.id)">
            <i></i><span>{{ n.title || '未知节点' }}</span><small>{{ typeLabel(n.type || 'default') }}</small>
          </button>
          <p v-if="searchMatches.length > 80" class="kg-hits-more">还有 {{ searchMatches.length - 80 }} 个，星图里都已显示</p>
          <!-- 列表能搜到、但图谱里没有对应节点的（设定条目、公告、必吃榜……或者太旧没进图谱的），引导去列表看 -->
          <button v-if="listOnlyCount > 0" type="button" class="kg-hits-list" @click="openInList">还有 {{ listOnlyCount }} 条在列表里（图谱里没有对应节点），点这里去列表看 →</button>
        </div>
      </form>
      <div class="kg-types" role="group" aria-label="按类型筛选">
        <button type="button" :class="{ on: !activeTypes.length }" @click="activeTypes = []">全部</button>
        <button v-for="t in availableTypes" :key="t" type="button" :class="{ on: activeTypes.includes(t) }" :style="{ '--c': getTheme(t).color }" @click="toggleType(t)"><i></i>{{ typeLabel(t) }}</button>
      </div>
      <label class="kg-switch"><input v-model="showLabels" type="checkbox" />名字标签</label>
    </div>
    <div class="kg-body">
      <p v-if="loading" class="kg-state" role="status">正在读取关系图谱…</p>
      <p v-else-if="!rawNodes.length" class="kg-state">还没有可以显示的节点。</p>
      <SpatialWorldGraph
        v-else
        :data="viewData"
        :focus-id="focusId"
        :covers="covers"
        :formations="FORMATIONS"
        storage-key="taichu.kb.formation"
        :type-label="typeLabel"
        :containers="['author', 'world', 'organization', 'location', 'folder']"
        :neural-layers="NEURAL_LAYERS"
        :shapes="SHAPES"
        :labels="showLabels"
        intro-text="太初知识库"
        :inset-right="showDetails ? panelWidth : 0"
        @select="selectNode"
      />
      <GraphDetailsPanel :show="showDetails" :node="selectedNode" :neighbors="neighborNodes" @close="closeDetailsPanel" @navigate:node="n => selectNode(n.id)" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import type { GraphEdgeDto, GraphNode, GraphNodeDto } from './types'
import { useKnowledgeGraphData } from './useKnowledgeGraphData'
import GraphDetailsPanel from './GraphDetailsPanel.vue'
import SpatialWorldGraph from '@/views/世界观管理/世界观组件/SpatialWorldGraph.vue'
import type { Formation } from '@/views/世界观管理/世界观组件/particleWorld'
import request from '@/utils/request'
import { folderText, priceText, resourceApi } from '@/api/resources'

const FORMATIONS: Formation[] = ['system', 'neural', 'galaxy', 'constellation', 'tree', 'network', 'sphere', 'helix']
const TYPE_LABELS: Record<string, string> = {
  character: '角色', location: '地点', concept: '概念', event: '事件', organization: '组织', blog: '博客', post: '帖子',
  wiki: '百科', setting: '设定', folder: '文件夹', file: '资源文件', resource: '社区资源', default: '节点',
  author: '创作者', artwork: '画廊作品', world: '世界观', novel: '小说', work: '柴圈作品', oc: '柴圈人设',
}
const typeLabel = (t: string) => TYPE_LABELS[t] ?? t
// 「脉络」阵型：从创作者出发，经过设定、人设与世界，到作品和文章，最后是可下载资源
const NEURAL_LAYERS = [
  { label: '创作者', types: ['author'] },
  { label: '设定', types: ['setting', 'concept', 'location', 'organization', 'character', 'event'] },
  { label: '人设与世界', types: ['oc', 'world'] },
  { label: '作品', types: ['artwork', 'work', 'novel'] },
  { label: '文章', types: ['wiki', 'blog', 'post'] },
  { label: '其他', types: [] },
  { label: '可下载资源', types: ['folder', 'file', 'resource'], output: true },
]
const SHAPES: Record<string, string> = {
  blog: 'cloud', post: 'burst', wiki: 'sphere', setting: 'torus', concept: 'torus', folder: 'cube', file: 'crystal', resource: 'crystal', organization: 'cube',
  location: 'planet', event: 'burst', character: 'sphere', author: 'planet', artwork: 'crystal', world: 'planet', novel: 'disc', work: 'burst', oc: 'sphere',
}
// 创作者连线不写字（太多会糊成一片），其他关系显示名字
const RELATION_LABELS: Record<string, string> = { DIR_LINK: '包含', FILE_LINK: '收录', AUTHOR: '', IN_WORLD: '属于', FEATURES: '出场' }

const { fetchData, getTheme } = useKnowledgeGraphData()
const loading = ref(true)
const rawNodes = ref<GraphNodeDto[]>([])
const rawEdges = ref<GraphEdgeDto[]>([])
const searchQuery = ref('')
const showLabels = ref(true)
const showDetails = ref(false)
const selectedNode = ref<GraphNode | null>(null)
const focusId = ref('')
const availableTypes = ref<string[]>([])
const activeTypes = ref<string[]>([])
// 详情面板宽 min(520px, 100%)：星图的视觉中心往左让出这块
const panelWidth = ref(520)
const measurePanel = () => { panelWidth.value = Math.min(520, window.innerWidth) }

function toggleType(t: string) {
  const i = activeTypes.value.indexOf(t)
  activeTypes.value = i < 0 ? [...activeTypes.value, t] : activeTypes.value.filter(x => x !== t)
  focusId.value = ''
}

const nodeById = computed(() => new Map(rawNodes.value.map(n => [n.id, n])))
const toGraphNode = (n: GraphNodeDto): GraphNode => ({ id: n.id, name: n.title || '未知节点', type: n.type || 'default', val: 1, color: getTheme(n.type).color, coverImage: n.coverImage, description: '', route: n.route, _nasPath: n._nasPath })
// ---------- 搜索：和知识库列表用同一套规则 ----------
// 后端 Graph/search 和列表（catalog）共用匹配逻辑，返回列表能搜到的内容对应的图谱节点 ID，以它为准；
// 前端自己拼的节点（创作者、NAS 文件夹 / 文件、社区资源）不在列表的 catalog 里，按同样的规则在本地匹配：
// 关键词按空格分开（最多 10 个），每个词都要出现，不分大小写；资源节点搜的字段和资源列表一样（标题、介绍、标签、文件名、上传者、分类、位置 / 文件路径）。
const appliedQuery = ref('')
const showHits = ref(true)
const serverHits = ref(new Set<string>())
const serverSearching = ref(false)
/** 后端结果到了（旧后端没有这个接口时一直是 false，只用本地匹配） */
const serverReady = ref(false)
/** 列表一共能搜到多少条（不含下载资源和笔刷，它们在列表里单独列） */
const listTotal = ref(0)
let searchTimer: ReturnType<typeof setTimeout> | undefined
let searchVersion = 0
const queryTerms = computed(() => appliedQuery.value.toLowerCase().split(' ').filter(Boolean).slice(0, 10))
/** 不在列表 catalog 里、由前端自己拼出来的节点 */
const isLocalOnly = (id: string) => id === 'virtual_nas_hub' || id.startsWith('author:') || id.startsWith('folder_') || id.startsWith('nas_file_') || id.startsWith('resource_')
const localMatch = (n: GraphNodeDto, terms: string[]) => {
  const text = `${n.title || ''} ${n.description || ''} ${n.searchText || ''}`.toLowerCase()
  return terms.every(t => text.includes(t))
}
/** 后端结果没到之前先用本地匹配（马上有结果）；到了以后列表里有的内容以后端为准 */
const searchMatches = computed(() => {
  const terms = queryTerms.value
  if (!terms.length) return []
  return rawNodes.value.filter(n => (isLocalOnly(n.id) || !serverReady.value) ? localMatch(n, terms) : serverHits.value.has(n.id.toLowerCase()))
})
/** 列表能搜到、但图谱里没有对应节点的条数 */
const listOnlyCount = computed(() => {
  if (!serverReady.value || !appliedQuery.value) return 0
  const inGraph = rawNodes.value.filter(n => !isLocalOnly(n.id) && serverHits.value.has(n.id.toLowerCase())).length
  return Math.max(0, listTotal.value - inGraph)
})
const router = useRouter()
/** 去列表看同一个关键词的结果 */
function openInList() { void router.push({ name: 'KnowledgeHome', query: { q: appliedQuery.value } }) }
const matchIds = computed(() => new Set(searchMatches.value.map(n => n.id)))

function onSearchInput() {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(() => applySearch(searchQuery.value), 250)
}
function applySearch(raw: string) {
  const q = raw.trim().slice(0, 100)
  appliedQuery.value = q
  showHits.value = true
  focusId.value = ''
  const version = ++searchVersion
  serverHits.value = new Set()
  serverReady.value = false
  listTotal.value = 0
  if (!q) { serverSearching.value = false; return }
  serverSearching.value = true
  request.get<{ ids: string[]; listTotal?: number }>('/knowledge-base/Graph/search', { params: { q }, silentError: true })
    .then(res => {
      if (version !== searchVersion) return
      serverHits.value = new Set((res?.ids ?? []).map(id => String(id).toLowerCase()))
      listTotal.value = res?.listTotal ?? 0
      serverReady.value = true
    })
    .catch(() => { /* 旧后端没有正文搜索：只用标题和简介 */ })
    .finally(() => { if (version === searchVersion) serverSearching.value = false })
}
function clearSearch() {
  clearTimeout(searchTimer)
  searchQuery.value = ''
  applySearch('')
}
function pickHit(id: string) {
  showHits.value = false
  void selectNode(id)
}

/** 星图数据：类型筛选 + 搜索后的节点；聚焦时只留它和直接相连的 */
const fullData = computed(() => {
  // 聚焦某个节点时不受搜索限制，好看到它的全部关系
  // 一个都没搜到时不清空星图（下拉里会提示没找到）
  const searching = Boolean(appliedQuery.value) && !focusId.value && matchIds.value.size > 0
  const nodes = rawNodes.value.filter(n => (!activeTypes.value.length || activeTypes.value.includes(n.type || 'default'))
    && (!searching || matchIds.value.has(n.id)))
  const ids = new Set(nodes.map(n => n.id))
  return {
    nodes: nodes.map(n => ({ id: n.id, title: n.title || '未知节点', type: n.type || 'default', color: getTheme(n.type).color })),
    links: rawEdges.value.filter(e => ids.has(e.source) && ids.has(e.target) && e.source !== e.target).map(e => ({
      source: e.source, target: e.target, label: RELATION_LABELS[e.relationType] ?? e.relationType ?? '',
      color: getTheme(nodeById.value.get(e.source)?.type ?? 'default').color, width: 1, isEmbed: false,
    })),
  }
})
const viewData = computed(() => {
  const d = fullData.value
  if (!focusId.value || !d.nodes.some(n => n.id === focusId.value)) return d
  const keep = new Set([focusId.value])
  for (const l of d.links) { if (l.source === focusId.value) keep.add(l.target); if (l.target === focusId.value) keep.add(l.source) }
  return { nodes: d.nodes.filter(n => keep.has(n.id)), links: d.links.filter(l => keep.has(l.source) && keep.has(l.target)) }
})
const covers = computed(() => {
  const out: Record<string, string> = {}
  for (const n of rawNodes.value) if (n.coverImage) out[n.id] = n.coverImage
  return out
})
const neighborNodes = computed<GraphNode[]>(() => {
  const id = selectedNode.value?.id
  if (!id) return []
  const ids = new Set<string>()
  for (const e of rawEdges.value) { if (e.source === id) ids.add(e.target); else if (e.target === id) ids.add(e.source) }
  return [...ids].map(x => nodeById.value.get(x)).filter((n): n is GraphNodeDto => !!n).map(toGraphNode)
})

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
    return `<span style="color: var(--info); font-weight: 600; background: rgba(56, 189, 248, 0.1); padding: 2px 6px; border-radius: 0; margin: 0 2px;">【${escapeHtml(node.attrs?.title || '关联碎片')}】</span>`;
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

async function selectNode(id: string) {
  if (!id) { closeDetailsPanel(); return }
  const raw = nodeById.value.get(id)
  if (!raw) return
  const node = toGraphNode(raw)
  focusId.value = id
  if (selectedNode.value?.id === id && showDetails.value) return
  selectedNode.value = node
  showDetails.value = true
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
      <p style="color: var(--info); font-size: 13px; margin-top: 16px; line-height: 1.6;">
        这是通过太初寰宇空间网络直接挂载的物理资源。底层由绿联 NAS 对象存储引擎提供高并发读取支持。
      </p>
    `;

    selectedNode.value = { ...node, description: customHtml };
    return;
  }

  // 全站内容节点（博客、作品、世界观……）：简介已经带在节点里，再给一个「打开原内容」
  if (raw.route || node.type === 'author') {
    const intro = raw.description ? `<p>${escapeHtml(raw.description)}</p>` : ''
    const open = raw.route ? `<p><a class="kb-open" href="${escapeHtml(raw.route)}" data-route="${escapeHtml(raw.route)}">打开原内容 →</a></p>` : ''
    selectedNode.value = { ...node, description: intro + open }
    return
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

function handleSearch() {
  clearTimeout(searchTimer)
  applySearch(searchQuery.value)
}

function closeDetailsPanel() {
  showDetails.value = false
  selectedNode.value = null
  focusId.value = ''
}

let disposed = false
onMounted(async () => {
  const res = await fetchData()
  const graphNodes: GraphNodeDto[] = [...res.nodes]
  const graphEdges: GraphEdgeDto[] = [...res.edges]

  // 官方资源和社区资源共用同一个「资源中枢」和同一套文件夹节点（id = folder_路径）
  const rootHubId = 'virtual_nas_hub'
  let hubCreated = false
  const ensureHub = () => {
    if (hubCreated) return
    hubCreated = true
    graphNodes.push({ id: rootHubId, title: '太初资源中枢', type: 'organization', size: 15, coverImage: null })
  }
  // 用一个 Set 记录已经创建过的文件夹路径，避免重复生成相同的文件夹节点
  const createdFolders = new Set<string>()
  /** 确保「a/b/c」这一串文件夹节点都在，返回最里层文件夹的节点 id（空路径返回资源中枢） */
  const ensureFolderPath = (path: string) => {
    ensureHub()
    let parent = rootHubId
    const parts = path.split('/').filter(Boolean)
    for (let i = 0; i < parts.length; i++) {
      const folderId = `folder_${parts.slice(0, i + 1).join('/')}`
      if (!createdFolders.has(folderId)) {
        createdFolders.add(folderId)
        graphNodes.push({ id: folderId, title: parts[i] ?? '', type: 'folder', size: 8, coverImage: null })
        graphEdges.push({ id: `edge_${parent}_${folderId}`, source: parent, target: folderId, relationType: 'DIR_LINK' })
      }
      parent = folderId
    }
    return parent
  }

  try {
    const nasRes: any = await request.get('/Resource/list')

    if (nasRes.success && nasRes.data && nasRes.data.length > 0) {
      // 1. 创建最顶层的“资源中枢”节点
      ensureHub()

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
          // 和资源列表搜官方资源的字段一样：完整路径 + 「官方」
          searchText: `${file.name} 官方`,
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

  // 社区资源（用户上传、审核通过的）：按「位置」挂到和官方资源同一套文件夹节点下。
  // 点开走站内资源详情（route），不走 _nasPath 匿名下载——付费、计数都在详情里处理。
  try {
    const res = await resourceApi.list({ kind: 'community', pageSize: 1000 })
    for (const r of res.items) {
      const parent = ensureFolderPath(r.folder || r.category)
      const id = `resource_${r.id}`
      const uploader = r.uploader?.name ?? ''
      graphNodes.push({
        id, title: r.title, type: 'resource', size: 4, coverImage: null,
        description: `社区资源 · ${r.category} · ${priceText(r.price)}${uploader ? ` · ${uploader} 上传` : ''} · 位置：${folderText(r.folder)}${r.description ? `\n${r.description}` : ''}`,
        searchText: [r.tags.join(' '), r.fileName, uploader, r.category, r.folder].join(' '),
        route: `/knowledge?type=resource&res=${encodeURIComponent(r.id)}`,
      })
      graphEdges.push({ id: `edge_${parent}_${id}`, source: parent, target: id, relationType: 'FILE_LINK' })
    }
  } catch (error) {
    console.error('社区资源拉取失败，跳过渲染:', error)
  }

  if (disposed) return
  rawNodes.value = graphNodes
  rawEdges.value = graphEdges
  availableTypes.value = [...new Set(graphNodes.map(n => n.type || 'default'))]
  loading.value = false
})
onMounted(() => { measurePanel(); window.addEventListener('resize', measurePanel) })
onBeforeUnmount(() => { disposed = true; clearTimeout(searchTimer); searchVersion++; window.removeEventListener('resize', measurePanel) })
</script>

<style scoped>
.kg { position: relative; display: flex; flex-direction: column; width: 100%; height: 100%; min-height: 0; background: var(--bg-main); color: var(--text-primary); }
.kg-bar { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 14px; padding: 10px 12px; border-bottom: 1px solid var(--border-line); background: var(--paper-card); }
.kg-search { position: relative; display: flex; }
.kg-hits { position: absolute; top: calc(100% + 6px); left: 0; z-index: 20; width: min(360px, 90vw); max-height: 360px; overflow: auto; padding: 6px; border: 1px solid var(--border-line); background: var(--paper-card); box-shadow: 0 10px 28px #0003; }
.kg-hits-head { display: flex; justify-content: space-between; align-items: center; gap: 8px; margin: 2px 4px 6px; font-size: 12px; color: var(--text-secondary); }
.kg-hits-head em { font-style: normal; opacity: .8; }
.kg-hits-head button { border: 0; background: none; color: var(--text-secondary); font: inherit; font-size: 12px; cursor: pointer; }
.kg-hit { display: flex; align-items: center; gap: 8px; width: 100%; padding: 7px 8px; border: 0; background: none; color: var(--text-primary); font: inherit; font-size: 13px; text-align: left; cursor: pointer; }
.kg-hit:hover, .kg-hit:focus-visible { background: color-mix(in srgb, var(--c) 12%, transparent); }
.kg-hit i { flex-shrink: 0; width: 7px; height: 7px; border-radius: 50%; background: var(--c); }
.kg-hit span { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.kg-hit small { flex-shrink: 0; color: var(--text-secondary); font-size: 11px; }
.kg-hits-more { margin: 6px 4px 2px; font-size: 11px; color: var(--text-secondary); }
.kg-hits-list { display: block; width: 100%; margin-top: 6px; padding: 8px; border: 1px dashed var(--border-line); background: none; color: var(--accent-color); font: inherit; font-size: 12px; text-align: left; cursor: pointer; }
.kg-hits-list:hover, .kg-hits-list:focus-visible { border-color: var(--accent-color); }
.kg-search > input { width: 200px; padding: 6px 10px; border: 1px solid var(--border-line); background: var(--bg-main); color: var(--text-primary); font: inherit; font-size: 13px; }
.kg-search > button { padding: 6px 12px; border: 1px solid var(--accent-color); border-left: 0; background: var(--accent-color); color: var(--on-accent, #fff); font: inherit; font-size: 13px; cursor: pointer; }
.kg-types { display: flex; flex-wrap: wrap; gap: 4px; }
.kg-types button { display: inline-flex; align-items: center; gap: 5px; padding: 4px 9px; border: 1px solid var(--border-line); background: transparent; color: var(--text-secondary); font: inherit; font-size: 12px; cursor: pointer; }
.kg-types button i { width: 7px; height: 7px; border-radius: 50%; background: var(--c); }
.kg-types button.on { border-color: var(--c, var(--accent-color)); color: var(--text-primary); background: color-mix(in srgb, var(--c, var(--accent-color)) 12%, transparent); }
.kg-switch { display: inline-flex; align-items: center; gap: 5px; margin-left: auto; font-size: 12px; color: var(--text-secondary); cursor: pointer; }
.kg-switch input { accent-color: var(--accent-color); }
.kg-body { position: relative; flex: 1; min-height: 0; overflow: hidden; }
.kg-state { position: absolute; inset: 0; display: grid; place-items: center; margin: 0; color: var(--text-secondary); font-size: 13px; }
@media (max-width: 640px) { .kg-search > input { width: 140px; } .kg-switch { margin-left: 0; } }
</style>
