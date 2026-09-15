<template>
  <aside class="spirit-right-panel">
    <!-- 🌟 常驻参考面板（多槽位） -->
    <section v-if="pinnedNoteIds.length > 0" class="panel-section pinned-block">
      <div class="section-header">
        <span class="header-title">📌 参考 ({{ pinnedNoteIds.length }})</span>
      </div>
      <div class="section-content pinned-scroll">
        <div v-if="pinnedLoading && pinnedNotes.length === 0" class="panel-mini-loading">加载中...</div>
        <div
          v-for="pn in pinnedNotes"
          :key="pn.id"
          class="pinned-card"
        >
          <div class="pinned-card-header">
            <div class="pinned-title">{{ pn.title }}</div>
            <button class="pinned-close-btn" @click="unpin(pn.id)" title="取消固定">✕</button>
          </div>

          <!-- 普通笔记：Tiptap 渲染 -->
          <SpiritPreview
            v-if="pn.type !== 'schedule' && pn.type !== 'canvas' && pn.type !== 'map'"
            :model-value="pn.content"
          />

          <!-- schedule 专用渲染 -->
          <div v-else-if="pn.type === 'schedule'" class="pinned-schedule">
            <div
              v-for="item in pn.scheduleItems"
              :key="item.id"
              class="pinned-schedule-item"
            >
              <span class="dot" :style="{ background: item.themeColor || '#86868b' }"></span>
              <span :class="{ done: item.isDone }">{{ item.title }}</span>
            </div>
            <div v-if="!pn.scheduleItems || pn.scheduleItems.length === 0" class="empty-placeholder">
              暂无日程
            </div>
          </div>

          <!-- canvas/map 暂不支持 -->
          <div v-else class="empty-placeholder">
            此类型暂不支持预览
          </div>
        </div>
      </div>
    </section>

    <!-- 🌟 目录导航 -->
    <section class="panel-section outline-block">
      <div class="section-header">
        <span class="header-title">📑 目录</span>
        <span class="outline-count">{{ outlineItems.length }}</span>
      </div>
      <div class="section-content is-scrollable outline-scroll">
        <div
          v-for="item in outlineItems"
          :key="item.id"
          class="outline-item"
          :class="`level-${item.level}`"
          :style="{ paddingLeft: `${(item.level - 1) * 14 + 10}px` }"
          @click="scrollToBlock(item.id)"
        >
          {{ item.text || '无标题' }}
        </div>
        <div v-if="outlineItems.length === 0" class="empty-placeholder">
          暂无标题
        </div>
      </div>
    </section>

    <section class="panel-section relation-block">
      <div class="section-header">
        <span class="header-title">🔗 关系引用</span>
        <div class="relation-badge-tabs">
          <span 
            :class="['badge-tab', { active: subTab === 'back' }]" 
            @click="subTab = 'back'"
          >
            反链 ({{ backlinks.length }})
          </span>
          <span 
            :class="['badge-tab', { active: subTab === 'out' }]" 
            @click="subTab = 'out'"
          >
            正链 ({{ outlinks.length }})
          </span>
        </div>
      </div>

      <div class="section-content is-scrollable">
        <div v-if="isLinksLoading" class="panel-mini-loading">感应星轨中...</div>
        <div v-else class="links-list">
          <div 
            v-for="(link, i) in (subTab === 'back' ? backlinks : outlinks) as any[]" 
            :key="link.id || i" 
            class="link-card"
            @click="$emit('select', link.id)"
          >
            <div class="link-title">{{ link.title || '无标题碎片' }}</div>
            <div v-if="link.excerpt" class="link-excerpt">{{ link.excerpt }}</div>
          </div>
          <div v-if="(subTab === 'back' ? backlinks : outlinks).length === 0" class="empty-placeholder">
            暂无引用关联
          </div>
        </div>
      </div>
    </section>

    <section class="panel-section properties-block">
      <div class="section-header">
        <span class="header-title">📋 元数据属性</span>
        <button class="ghost-add-btn" @click="addProperty">+ 新增</button>
      </div>

      <div class="section-content">
        <div class="prop-list">
          <div v-for="(prop, index) in localProperties" :key="prop.id" class="prop-row">
            <input 
              v-model="prop.key" 
              class="prop-input key-input" 
              placeholder="属性名" 
              @change="handlePropChange"
            />
            <span class="colon">:</span>
            <input 
              v-model="prop.value" 
              class="prop-input val-input" 
              placeholder="未指定" 
              @change="handlePropChange"
            />
            <button class="del-prop-btn" @click="removeProperty(index)">✕</button>
          </div>
          <div v-if="localProperties.length === 0" class="empty-placeholder">
            暂无自定义属性
          </div>
        </div>
      </div>
    </section>

    <section class="panel-section tags-block">
      <div class="section-header">
        <span class="header-title">🏷️ 灵脉标签</span>
      </div>

      <div class="section-content">
        <div class="tags-wrapper">
          <span v-for="(tag, index) in localTags" :key="index" class="spirit-tag">
            # {{ tag }}
            <span class="tag-remove" @click="removeTag(index)">×</span>
          </span>
          
          <input 
            v-model="newTagInput"
            class="tag-inline-input"
            placeholder="+ 输入标签按下回车..."
            @keyup.enter="addTag"
          />
        </div>
      </div>
    </section>
  </aside>
</template>

<script setup lang="ts">
import { ref, watchEffect, watch, computed, inject } from 'vue';
import type { PropType } from 'vue';
import { lingmaiApi } from '@/api/lingmai';
import SpiritPreview from '@/components/SpiritTextComponents/SpiritPreview.vue';

const props = defineProps({
  noteId: { type: String, required: true },
  extraData: { type: String, default: '[]' },
  tags: { type: Array as PropType<string[]>, default: () => [] } 
});

const emit = defineEmits(['update:extraData', 'update:tags', 'change', 'select']);

// ========================================================================
// 🌟 常驻预览（多槽位）
// ========================================================================
const pinnedNoteIds = inject<any>('pinnedNoteIds', ref([]))
const pinnedNotes = ref<any[]>([])
const pinnedLoading = ref(false)

const unpin = (id: string) => {
  const newIds = pinnedNoteIds.value.filter((x: string) => x !== id)
  pinnedNoteIds.value = newIds
}

watch(pinnedNoteIds, async (ids: string[]) => {
  if (!ids || ids.length === 0) {
    pinnedNotes.value = []
    return
  }

  const existingIds = new Set(pinnedNotes.value.map(p => p.id))
  const toLoad = ids.filter((id: string) => !existingIds.has(id))

  const reordered = ids
    .map((id: string) => pinnedNotes.value.find(p => p.id === id))
    .filter(Boolean) as any[]

  if (toLoad.length === 0) {
    pinnedNotes.value = reordered
    return
  }

  pinnedLoading.value = true
  try {
    const fetched = await Promise.all(
      toLoad.map(async (id: string) => {
        try {
          const res: any = await lingmaiApi.getNote(id)
          const type = res.type || 'note'

          // schedule 专用解析
          let scheduleItems: any[] = []
          if (type === 'schedule' && Array.isArray(res.blocks)) {
            scheduleItems = res.blocks
              .filter((b: any) => b.type === 'schedule-item')
              .map((b: any) => {
                try {
                  const d = typeof b.data === 'string' ? JSON.parse(b.data) : b.data
                  return { id: b.id, ...d }
                } catch { return null }
              })
              .filter(Boolean)
          }

          return {
            id,
            type,
            title: res.title || '无标题',
            content: res.tiptapContent || { type: 'doc', content: [] },
            scheduleItems,
          }
        } catch (e) {
          console.error('参考面板加载失败:', id, e)
          return null
        }
      })
    )

    const allById = new Map<string, any>()
    reordered.forEach(p => allById.set(p.id, p))
    fetched.filter(Boolean).forEach((p: any) => allById.set(p.id, p))

    pinnedNotes.value = ids
      .map((id: string) => allById.get(id))
      .filter(Boolean)
  } finally {
    pinnedLoading.value = false
  }
}, { immediate: true, deep: true })
// ========================================================================

// ========================================================================
// 🌟 目录导航
// ========================================================================
const currentBlocks = inject<any>('currentBlocks', ref([]))

const outlineItems = computed(() => {
  const blocks = currentBlocks?.value || []
  return blocks
    .filter((b: any) => b.type === 'heading')
    .map((b: any) => {
      try {
        const data = typeof b.data === 'string' ? JSON.parse(b.data) : b.data
        const level = data?.attrs?.level || 1
        const text = extractHeadingText(data?.content)
        return { id: b.id, level, text }
      } catch {
        return null
      }
    })
    .filter(Boolean) as { id: string; level: number; text: string }[]
})

const extractHeadingText = (nodes: any[]): string => {
  if (!Array.isArray(nodes)) return ''
  return nodes.map((n: any) => {
    if (n.text) return n.text
    if (Array.isArray(n.content)) return extractHeadingText(n.content)
    return ''
  }).join('')
}

const scrollToBlock = (blockId: string) => {
  const el = document.querySelector(`[data-block-id="${blockId}"]`) as HTMLElement | null
  if (el) {
    el.scrollIntoView({ behavior: 'smooth', block: 'start' })
    el.style.transition = 'background 0.3s'
    el.style.background = 'rgba(0, 102, 204, 0.08)'
    setTimeout(() => {
      el.style.background = ''
    }, 1200)
  }
}
// ========================================================================

const subTab = ref('back');
const isLinksLoading = ref(false);
const backlinks = ref<any[]>([]);
const outlinks = ref<any[]>([]);

const localProperties = ref<any[]>([]);
const localTags = ref<string[]>([]);
const newTagInput = ref('');

// 1. 深度解析外层传入的 extraData
watchEffect(() => {
  try {
    const parsed = JSON.parse(props.extraData || '[]');
    if (Array.isArray(parsed)) {
      localProperties.value = parsed.filter(p => p && typeof p === 'object' && 'key' in p);
    }
  } catch (e) {
    localProperties.value = [];
  }
});

// 2. 自动化感知双向关系链
const loadLinkRelations = async () => {
  if (!props.noteId) return;
  isLinksLoading.value = true;
  try {
    const [backRes, outRes] = await Promise.all([
      lingmaiApi.getBacklinks(props.noteId),
      lingmaiApi.getOutlinks(props.noteId),
    ]);
    backlinks.value = Array.isArray(backRes) ? backRes : [];
    outlinks.value = Array.isArray(outRes) ? outRes : [];
  } catch (e) {
    console.error("感应双链失败:", e);
    backlinks.value = [];
    outlinks.value = [];
  } finally {
    isLinksLoading.value = false;
  }
};

watch(() => props.noteId, () => {
  loadLinkRelations();
}, { immediate: true });

// 🌟 4. 监听父组件传入的真实 tags
watch(() => props.tags, (newVal) => {
  localTags.value = [...(newVal || [])];
}, { immediate: true, deep: true });


// 5. 将属性改动转化为规范的 JSON 字符串逆向贯穿给 v-model
const handlePropChange = () => {
  const validProps = localProperties.value.filter(p => p.key.trim() || p.value.trim());
  const jsonString = JSON.stringify(validProps);
  
  emit('update:extraData', jsonString);
  emit('change');
};

const addProperty = () => {
  localProperties.value.push({
    id: Math.random().toString(36).substring(2, 9),
    key: '',
    value: ''
  });
  handlePropChange();
};

const removeProperty = (index: number) => {
  localProperties.value.splice(index, 1);
  handlePropChange();
};

// 🌟 6. 新增：标签变化时的上报逻辑
const handleTagsChange = () => {
  emit('update:tags', localTags.value);
  emit('change'); 
};

// 7. 标签驱动逻辑
const addTag = () => {
  const cleanTag = newTagInput.value.trim().replace('#', '');
  if (cleanTag && !localTags.value.includes(cleanTag)) {
    localTags.value.push(cleanTag);
    handleTagsChange();
  }
  newTagInput.value = '';
};

const removeTag = (index: number) => {
  localTags.value.splice(index, 1);
  handleTagsChange();
};
</script>

<style scoped>
/* 保持你的精美垂直分层 CSS 样式不变 */
.spirit-right-panel { width: 310px; border-left: 1px solid #f2f2f7; background: #ffffff; display: flex; flex-direction: column; height: 100%; box-sizing: border-box; }
.panel-section { display: flex; flex-direction: column; border-bottom: 1px solid #f2f2f7; padding: 16px; box-sizing: border-box; }
.relation-block { flex: 1; min-height: 200px; overflow: hidden; }
.properties-block { max-height: 350px; overflow-y: auto; }
.tags-block { border-bottom: none; background: #fafafa; }
.section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; }
.header-title { font-size: 12px; font-weight: 700; color: #1d1d1f; letter-spacing: 0.05em; }
.relation-badge-tabs { display: flex; background: #f2f2f7; padding: 2px; border-radius: 6px; }
.badge-tab { font-size: 11px; padding: 3px 8px; border-radius: 4px; color: #86868b; cursor: pointer; transition: all 0.2s; }
.badge-tab.active { background: #ffffff; color: #0066cc; font-weight: 600; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
.section-content { width: 100%; }
.section-content.is-scrollable { flex: 1; overflow-y: auto; scrollbar-width: none; }
.section-content.is-scrollable::-webkit-scrollbar { display: none; }
.prop-list { display: flex; flex-direction: column; gap: 8px; }
.prop-row { display: flex; align-items: center; position: relative; gap: 4px; }
.prop-input { border: 1px solid transparent; background: transparent; padding: 4px 6px; font-size: 12px; width: 42%; border-radius: 4px; }
.prop-input:hover { background: #f5f5f7; }
.prop-input:focus { background: #ffffff; border-color: #0066cc; box-shadow: 0 0 0 2px rgba(0,102,204,0.08); }
.key-input { text-align: right; color: #86868b; font-weight: 500; }
.val-input { color: #1d1d1f; }
.colon { color: #d2d2d7; font-size: 12px; }
.ghost-add-btn { background: none; border: none; color: #0066cc; font-size: 11px; font-weight: 600; cursor: pointer; padding: 2px 6px; border-radius: 4px; }
.ghost-add-btn:hover { background: rgba(0,102,204,0.05); }
.del-prop-btn { background: none; border: none; color: #c7c7cc; cursor: pointer; opacity: 0; transition: opacity 0.2s; font-size: 12px; }
.prop-row:hover .del-prop-btn { opacity: 1; }
.del-prop-btn:hover { color: #ff3b30; }
.link-card { padding: 10px; background: #f9f9fb; border-radius: 8px; margin-bottom: 8px; cursor: pointer; border: 1px solid transparent; transition: all 0.2s; }
.link-card:hover { border-color: #d2d2d7; background: #ffffff; }
.link-title { font-size: 12px; font-weight: 600; color: #1d1d1f; }
.link-excerpt { font-size: 11px; color: #86868b; margin-top: 2px; display: -webkit-box; -webkit-line-clamp: 1; -webkit-box-orient: vertical; overflow: hidden; }
.tags-wrapper { display: flex; flex-wrap: wrap; gap: 6px; align-items: center; }
.spirit-tag { font-size: 11px; background: rgba(0, 102, 204, 0.06); color: #0066cc; padding: 4px 8px; border-radius: 6px; display: inline-flex; align-items: center; gap: 4px; font-weight: 500; }
.tag-remove { cursor: pointer; color: #a1a1a6; font-weight: bold; transition: color 0.2s; }
.tag-remove:hover { color: #ff3b30; }
.tag-inline-input { border: none; background: transparent; outline: none; font-size: 11px; color: #86868b; padding: 4px; flex: 1; min-width: 100px; }
.empty-placeholder, .panel-mini-loading { font-size: 11px; color: #c7c7cc; text-align: center; padding: 16px 0; }

/* ==================== 🌟 目录导航 ==================== */
.outline-block {
  flex: 0 0 auto;
  max-height: 240px;
  border-bottom: 1px solid #f2f2f7;
}

.outline-count {
  font-size: 10px;
  color: #c7c7cc;
  background: #f5f5f7;
  padding: 1px 6px;
  border-radius: 8px;
}

.outline-scroll {
  max-height: 200px;
}

.outline-item {
  font-size: 12px;
  color: #3a3a3c;
  padding: 5px 8px;
  border-radius: 6px;
  cursor: pointer;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  transition: all 0.15s;
  border-left: 2px solid transparent;
}

.outline-item:hover {
  background: #f5f5f7;
  color: #0066cc;
}

.outline-item.level-1 {
  font-weight: 700;
  color: #1d1d1f;
}

.outline-item.level-2 {
  font-weight: 500;
}

.outline-item.level-3 {
  font-size: 11px;
  color: #6e6e73;
}

/* ==================== 🌟 常驻参考面板（多槽位） ==================== */
.pinned-block {
  flex: 0 0 auto;
  max-height: 420px;
  border-bottom: 2px solid #e5e5ea;
  background: #fbfbfd;
}

.pinned-scroll {
  max-height: 380px;
  overflow-y: auto;
  padding-right: 4px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.pinned-card {
  background: #ffffff;
  border: 1px solid #e5e5ea;
  border-radius: 10px;
  padding: 10px 12px;
  transition: border-color 0.15s;
}

.pinned-card:hover {
  border-color: #d2d2d7;
}

.pinned-card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
  padding-bottom: 6px;
  border-bottom: 1px dashed #e5e5ea;
}

.pinned-title {
  font-size: 13px;
  font-weight: 700;
  color: #1d1d1f;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  flex: 1;
}

.pinned-close-btn {
  background: none;
  border: none;
  color: #a1a1a6;
  cursor: pointer;
  font-size: 12px;
  padding: 2px 6px;
  border-radius: 4px;
  transition: all 0.15s;
  flex-shrink: 0;
}

.pinned-close-btn:hover {
  color: #ff3b30;
  background: rgba(255, 59, 48, 0.08);
}

/* ==================== 🌟 schedule 专用渲染 ==================== */
.pinned-schedule {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 4px 0;
}

.pinned-schedule-item {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  color: #3a3a3c;
  line-height: 1.5;
}

.pinned-schedule-item .dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  flex-shrink: 0;
}

.pinned-schedule-item .done {
  text-decoration: line-through;
  color: #a1a1a6;
}
</style>