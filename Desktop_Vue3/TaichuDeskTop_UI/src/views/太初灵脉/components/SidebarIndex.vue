<template>
  <aside class="spirit-sidebar">
    <div class="space-selector-area">
      <button type="button" class="current-space-label" :aria-expanded="isSpaceListOpen" @click.stop="toggleSpaceList">
        <span class="space-text">{{ currentSpaceName }}</span>
        <svg class="chevron-icon" :class="{ rotated: isSpaceListOpen }" width="8" height="8" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M6 9l6 6 6-6" /></svg>
      </button>

      <transition name="fade">
        <div v-if="isSpaceListOpen" class="space-dropdown" @click.stop>
          <div v-for="space in spaces" :key="space.id" class="space-opt" :class="{ active: currentSpaceId === space.id }" @click="switchSpace(space)">
            <input v-if="editingSpaceId === space.id" 
              class="inline-input"
              v-model="tempName" 
              @blur="saveSpaceName(space)" 
              @keyup.enter="saveSpaceName(space)"
              v-focus
            />
            <span v-else class="opt-name">{{ space.name }}</span>

            <div class="space-actions">
              <button type="button" @click.stop="startRenameSpace(space)">重命名</button>
              <button type="button" class="danger" @click.stop="startDeleteSpace(space)" v-if="spaces.length > 1">删除</button>
            </div>
          </div>

          <div v-if="isCreatingSpace" class="space-opt">
            <input class="inline-input" v-model="tempName" placeholder="输入空间名称，回车创建" @blur="cancelCreateSpace" @keyup.enter="confirmCreateSpace" v-focus />
          </div>
          <button v-else-if="quota.usedSpaces < quota.maxSpaces" type="button" class="space-footer-action" @click="startCreateSpace">
            ＋ 新建空间
          </button>
          <p v-else class="space-footer-note">空间数量已达上限（{{ quota.maxSpaces }} 个）</p>
        </div>
      </transition>
    </div>

    <!-- 生活管理入口：个人日历（月历、当天时间轴、到点提醒），独立页面 /lingmai/planner -->
    <button type="button" class="planner-entry" :class="{ active: isPlannerRoute }" :aria-current="isPlannerRoute ? 'page' : undefined" @click="$emit('open-planner')">
      <span class="planner-entry-icon" aria-hidden="true">📅</span>
      <span class="planner-entry-text"><strong>生活管理</strong><small>月历 · 每日安排 · 到点提醒</small></span>
    </button>
    <!-- 番茄钟入口：专注 / 休息计时，和桌面程序同步，独立页面 /lingmai/pomodoro -->
    <button type="button" class="planner-entry pomodoro-entry" :class="{ active: isPomodoroRoute }" :aria-current="isPomodoroRoute ? 'page' : undefined" @click="openPomodoro">
      <span class="planner-entry-icon" aria-hidden="true">🍅</span>
      <span class="planner-entry-text"><strong>番茄钟</strong><small>专注计时 · 和桌面同步</small></span>
    </button>

    <div class="sidebar-header">
      <div class="header-left">
        <span class="index-label">全部笔记</span>
        <span class="quota-text" :title="`已用 ${quota.usedNotes} 篇，上限 ${quota.maxNotes} 篇`">{{ quota.usedNotes }} / {{ quota.maxNotes }}</span>
      </div>
      
      <div class="header-actions" style="position: relative;">
        <button type="button" class="text-btn active" :aria-expanded="isCreateMenuOpen" @click.stop="toggleCreateMenu">
          ＋ 新建 <span aria-hidden="true">▾</span>
        </button>

        <transition name="fade">
          <div v-if="isCreateMenuOpen" class="create-dropdown" @click.stop>
            <p class="create-hint">新建到「{{ currentSpaceName }}」</p>
            <button v-for="kind in createKinds.common" :key="kind.value" type="button" class="create-opt" @click="handleCreateWithType(kind.value)">
              <span aria-hidden="true">{{ kind.icon }}</span><span><strong>{{ kind.label }}</strong><small>{{ kind.desc }}</small></span>
            </button>
            <details class="create-more">
              <summary>更多类型</summary>
              <button v-for="kind in createKinds.more" :key="kind.value" type="button" class="create-opt" @click="handleCreateWithType(kind.value)">
                <span aria-hidden="true">{{ kind.icon }}</span><span><strong>{{ kind.label }}</strong><small>{{ kind.desc }}</small></span>
              </button>
            </details>
            <button type="button" class="create-opt folder-opt" @click="handleCreateWithType('folder')">
              <span aria-hidden="true">📁</span><span><strong>文件夹</strong><small>用来整理笔记</small></span>
            </button>
          </div>
        </transition>
      </div>
    </div>

    <div class="sidebar-search">
      <input type="text" v-model="searchQuery" placeholder="搜索本空间的笔记标题" aria-label="搜索本空间的笔记标题" spellcheck="false" />
    </div>

    <nav class="note-list">
      <div class="section-group" @dragover.prevent @drop="onDrop($event, null)">
        <div 
          v-for="note in filteredRootNotes" 
          :key="note.id" 
          class="note-item" 
          :class="['type-' + (note.type || 'note'), { active: activeId === note.id }]" 
          @click="$emit('select', note.id)" role="button" tabindex="0" @keydown.enter.self="$emit('select', note.id)" @keydown.space.self.prevent="$emit('select', note.id)"
          draggable="true" 
          @dragstart="onDragStart($event, note.id)"
        >
          <div class="item-content">
            <span class="item-title">{{ note.title || '未命名笔记' }}</span>
            <span class="type-label">{{ noteKind(note.type).short }}</span>
          </div>
          <div class="item-hover-actions">
            <button type="button" class="danger" @click.stop="startDeleteItem(note.id)">删除</button>
          </div>
        </div>
      </div>

      <div class="section-group">
        <div v-for="folder in folders" :key="folder.id" class="folder-container">
          <div 
            class="folder-header" 
            @click="toggleFolder(folder.id)" 
            @dragover.prevent
            @drop="onDrop($event, folder.id)"
          >
            <span class="folder-arrow" :class="{ rotated: expandedFolders.has(folder.id) }"></span>
            
            <input v-if="editingFolderId === folder.id" 
              class="inline-input" v-model="tempName" 
              @blur="saveFolderName(folder)" @keyup.enter="saveFolderName(folder)" v-focus />
            <span v-else class="item-title" title="双击重命名" @dblclick.stop="startRenameFolder(folder)">📁 {{ folder.title }}</span>

            <div class="item-hover-actions">
              <button type="button" @click.stop="$emit('create', 'note', folder.id)">＋ 笔记</button>
            </div>
          </div>

          <transition name="expand">
            <div v-if="expandedFolders.has(folder.id)" class="folder-content">
              <div 
                v-for="subNote in filteredNotesInFolder(folder.id)" 
                :key="subNote.id" 
                class="note-item sub" 
                :class="['type-' + (subNote.type || 'note'), { active: activeId === subNote.id }]" 
                @click="$emit('select', subNote.id)" role="button" tabindex="0" @keydown.enter.self="$emit('select', subNote.id)" @keydown.space.self.prevent="$emit('select', subNote.id)"
                draggable="true"
                @dragstart="onDragStart($event, subNote.id)"
              >
                <div class="item-content">
                  <span class="item-title">{{ subNote.title || '未命名笔记' }}</span>
                  <span class="type-label">{{ noteKind(subNote.type).short }}</span>
                </div>
                <div class="item-hover-actions">
                  <button type="button" class="danger" @click.stop="startDeleteItem(subNote.id)">删除</button>
                </div>
              </div>
            </div>
          </transition>
        </div>
      </div>
    </nav>

    <div class="sidebar-footer">拖动笔记到文件夹即可整理</div>

    <transition name="fade">
      <div v-if="confirmDialog.visible" class="spirit-overlay" @click="confirmDialog.visible = false">
        <div class="spirit-dialog" @click.stop>
          <p class="dialog-msg">{{ confirmDialog.message }}</p>
          <div class="dialog-actions">
            <button type="button" class="dialog-btn" @click="confirmDialog.visible = false">取消</button>
            <button type="button" class="dialog-btn danger" @click="executeConfirm">确认删除</button>
          </div>
        </div>
      </div>
    </transition>
  </aside>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useSpiritData } from '../../../composables/useSpiritData';
import { lingmaiApi } from '../../../api/lingmai';
import { NOTE_KINDS, noteKind } from '@/utils/NoteType';

const createKinds = { common: NOTE_KINDS.filter(k => k.common), more: NOTE_KINDS.filter(k => !k.common && ['wiki', 'doc', 'schedule', 'canvas'].includes(k.value)) };
// 记住上次打开的空间，刷新后不再回到第一个空间。
const LAST_SPACE_KEY = 'taichu-lingmai-space';
const rememberSpace = (id: string) => { try { localStorage.setItem(LAST_SPACE_KEY, id); } catch { /* 存储不可用时仅本次有效 */ } };
const recalledSpace = () => { try { return localStorage.getItem(LAST_SPACE_KEY) || ''; } catch { return ''; } };

const vFocus = { mounted: (el: HTMLElement) => el.focus() };

// 🌟【核心修改 1】显式接收父组件传进来的全量响应式 notes
const props = defineProps<{ 
  notes: any[];
  activeId: string;
  filters?: Record<string, boolean>; 
}>();
const emit = defineEmits(['select', 'create', 'open-planner', 'open-pomodoro']);
// 当前是不是在「生活管理」页面（入口按钮高亮）
const route = useRoute();
const router = useRouter();
const isPlannerRoute = computed(() => route.name === 'SpiritPlanner');
// 番茄钟页面：这里直接跳转（外层布局不用改也能用）；同时通知外层，外层愿意的话可以顺手收起手机上的侧边栏
const isPomodoroRoute = computed(() => route.name === 'SpiritPomodoro');
const openPomodoro = () => {
  router.push({ path: '/lingmai/pomodoro' }).catch(() => {});
  emit('open-pomodoro');
};

// 🌟【核心修改 2】从解构中移除 rootNotes，脱离 Composable 数据孤岛
const { 
  folders, getNotesInFolder, 
  updateNoteTitle, deleteNote, moveNote, currentSpaceId, fetchAllNotes 
} = useSpiritData();

const quota = ref({ usedNotes: 0, maxNotes: 100, usedSpaces: 0, maxSpaces: 1 });
const isSpaceListOpen = ref(false);
const searchQuery = ref('');
const expandedFolders = ref(new Set<string>());
const spaces = ref<any[]>([]);
const editingSpaceId = ref<string | null>(null);
const editingFolderId = ref<string | null>(null);
const isCreatingSpace = ref(false);
const tempName = ref('');
const confirmDialog = ref({ visible: false, message: '', onConfirm: () => {} });

const isCreateMenuOpen = ref(false);

const toggleCreateMenu = () => {
  isCreateMenuOpen.value = !isCreateMenuOpen.value;
};

const handleCreateWithType = (type: string) => {
  emit('create', type);
  isCreateMenuOpen.value = false;
};

const closeDropdowns = () => {
  if (isCreateMenuOpen.value) isCreateMenuOpen.value = false;
  if (isSpaceListOpen.value) isSpaceListOpen.value = false;
};

// 🌟【核心修改 3】将过滤源彻底绑定到基于 Props 传入的 notes
const filteredRootNotes = computed(() => {
  if (!props.notes) return [];
  return props.notes.filter(n => {
    // 过滤文件夹、逻辑删除项、不在根目录的笔记、以及标记不显示的笔记
    if (n.type === 'folder' || n.status !== 0) return false;
    if (n.folderId) return false; 
    if (n.showInSidebar === false) return false;
    if (props.filters && props.filters[n.type] === false) return false;
    
    // 搜索词过滤
    if (searchQuery.value.trim()) {
      const q = searchQuery.value.toLowerCase();
      return (n.title || '').toLowerCase().includes(q);
    }
    return true;
  });
});

// 🌟【核心修改 4】同步改写文件夹子节点过滤源为 props.notes
const filteredNotesInFolder = (folderId: string) => {
  if (!props.notes) return [];
  return props.notes.filter(n => {
    if (n.folderId !== folderId || n.status !== 0) return false;
    if (n.showInSidebar === false) return false;
    if (props.filters && props.filters[n.type] === false) return false;
    
    if (searchQuery.value.trim()) {
      const q = searchQuery.value.toLowerCase();
      return (n.title || '').toLowerCase().includes(q);
    }
    return true;
  });
};

const currentSpaceName = computed(() => spaces.value.find(s => s.id === currentSpaceId.value)?.name || '我的空间');

const toggleSpaceList = () => isSpaceListOpen.value = !isSpaceListOpen.value;

const switchSpace = async (space: any) => {
  currentSpaceId.value = space.id;
  rememberSpace(space.id);
  isSpaceListOpen.value = false;
  await fetchAllNotes();
};

const startCreateSpace = () => { tempName.value = ''; isCreatingSpace.value = true; };
const cancelCreateSpace = () => { isCreatingSpace.value = false; };

const confirmCreateSpace = async () => {
  if (tempName.value.trim()) {
    const newSpace: any = await lingmaiApi.createSpace(tempName.value.trim());
    spaces.value.push(newSpace);
    await switchSpace(newSpace);
    await fetchQuota();
  }
  isCreatingSpace.value = false;
};

const startRenameSpace = (space: any) => { editingSpaceId.value = space.id; tempName.value = space.name; };
const saveSpaceName = async (space: any) => {
  if (tempName.value.trim() && tempName.value !== space.name) {
    await lingmaiApi.updateSpaceName(space.id, tempName.value.trim());
    space.name = tempName.value;
  }
  editingSpaceId.value = null;
};

const startDeleteSpace = (space: any) => {
  confirmDialog.value = {
    visible: true,
    message: `删除空间「${space.name}」？里面的所有笔记都会一起删除，无法恢复。`,
    onConfirm: async () => {
      await lingmaiApi.deleteSpace(space.id);
      spaces.value = spaces.value.filter(s => s.id !== space.id);
      if (currentSpaceId.value === space.id && spaces.value.length > 0) {
        await switchSpace(spaces.value[0]);
      }
      await fetchQuota();
    }
  };
};

const onDragStart = (e: DragEvent, noteId: string) => {
  if (e.dataTransfer) {
    e.dataTransfer.setData('noteId', noteId);
    e.dataTransfer.effectAllowed = 'move';
  }
};

const onDrop = async (e: DragEvent, targetFolderId: string | null) => {
  const noteId = e.dataTransfer?.getData('noteId');
  if (noteId) {
    await moveNote(noteId, targetFolderId);
    if (targetFolderId) expandedFolders.value.add(targetFolderId);
    await fetchAllNotes();
  }
};

const startRenameFolder = (folder: any) => { editingFolderId.value = folder.id; tempName.value = folder.title; };
const saveFolderName = async (folder: any) => {
  if (tempName.value.trim() && tempName.value !== folder.title) {
    await updateNoteTitle(folder.id, tempName.value.trim());
  }
  editingFolderId.value = null;
};

const startDeleteItem = (id: string) => {
  confirmDialog.value = {
    visible: true,
    message: "删除这篇笔记？删除后无法恢复。",
    onConfirm: async () => {
      await deleteNote(id);
      await fetchAllNotes();
      await fetchQuota();
    }
  };
};

const executeConfirm = () => { confirmDialog.value.onConfirm(); confirmDialog.value.visible = false; };

const fetchQuota = async () => {
  try { quota.value = await lingmaiApi.getQuota(); } catch (e) { console.error(e); }
};

const initSpaces = async () => {
  const res: any = await lingmaiApi.getSpaces(); 
  spaces.value = res;
  if (spaces.value.length > 0 && (!currentSpaceId.value || currentSpaceId.value.startsWith('0000'))) {
    const remembered = spaces.value.find(s => s.id === recalledSpace());
    await switchSpace(remembered || spaces.value[0]);
  }
};

const toggleFolder = (id: string) => expandedFolders.value.has(id) ? expandedFolders.value.delete(id) : expandedFolders.value.add(id);

onMounted(() => { 
  initSpaces(); 
  fetchQuota(); 
  window.addEventListener('click', closeDropdowns);
});

onUnmounted(() => {
  window.removeEventListener('click', closeDropdowns);
});
</script>

<style scoped>
.spirit-sidebar { display: flex; flex-direction: column; height: 100%; background: var(--paper-card); color: var(--text-primary); border-right: 1px solid var(--border-line); }
.inline-input { border: none; background: transparent; padding: 0; font-size: inherit; color: var(--accent-color); outline: none; width: 100%; border-bottom: 1px solid var(--accent-color); }
.space-selector-area { padding: 40px 24px 20px; position: relative; }
.current-space-label { display: flex; align-items: center; gap: 6px; cursor: pointer; font-size: 13px; font-weight: 500; }
.chevron-icon { color: var(--text-secondary); transition: transform 0.3s; }
.chevron-icon.rotated { transform: rotate(180deg); }
.space-dropdown { position: absolute; top: 65px; left: 24px; width: 200px; background: var(--paper-card); border: 1px solid var(--border-line); border-radius: 0; box-shadow: 0 4px 20px rgba(0,0,0,0.03); z-index: 100; padding: 6px; }
.space-opt { padding: 8px 12px; font-size: 12px; border-radius: 0; display: flex; justify-content: space-between; cursor: pointer; transition: background 0.2s; }
.space-opt:hover { background: var(--paper-card); }
.space-opt.active { color: var(--accent-color); font-weight: 600; }
.space-actions { font-size: 10px; display: none; gap: 8px; color: var(--text-secondary); }
.space-opt:hover .space-actions { display: flex; }
.sidebar-header { padding: 10px 24px; display: flex; justify-content: space-between; align-items: center; border-top: 1px solid var(--border-line); }
.index-label { font-size: 12px; font-weight: 600; color: var(--text-secondary); }
.quota-text { font-size: 10px; color: var(--text-secondary); margin-left: 8px; }
.header-actions { display: flex; gap: 12px; }
.text-btn { font-size: 12px; color: var(--text-secondary); cursor: pointer; white-space: nowrap; }
.text-btn.active { color: var(--accent-color); }

.create-dropdown { position: absolute; top: 30px; right: 0; width: 240px; background: var(--paper-card); border: 1px solid var(--border-line); border-radius: 0; box-shadow: 0 8px 24px rgba(0,0,0,0.06); z-index: 100; padding: 6px; }
.create-opt { padding: 8px 10px; font-size: 12px; color: var(--text-primary); border-radius: 0; cursor: pointer; transition: background 0.2s; }
.create-opt { display: flex !important; gap: 10px; align-items: flex-start; }
.create-opt strong { display: block; font-size: 13px; }
.create-opt small { display: block; margin-top: 2px; font-size: 11px; color: var(--text-secondary); line-height: 1.4; }
.create-hint { margin: 2px 10px 6px; font-size: 11px; color: var(--text-secondary); }
.create-more { border-top: 1px solid var(--border-line); margin-top: 4px; }
.create-more summary { padding: 8px 10px; font-size: 12px; color: var(--text-secondary); cursor: pointer; }
.folder-opt { border-top: 1px solid var(--border-line); margin-top: 4px; }
.space-actions button { background: none; border: none; font: inherit; color: inherit; cursor: pointer; padding: 0; }
.space-footer-action { display: block; width: 100%; margin-top: 4px; padding: 8px 12px; text-align: left; font: inherit; font-size: 12px; color: var(--accent-color); background: none; border: 0; border-top: 1px solid var(--border-line); cursor: pointer; }
.space-footer-note { margin: 4px 0 0; padding: 8px 12px; font-size: 11px; color: var(--text-secondary); border-top: 1px solid var(--border-line); }
.dialog-btn { background: none; border: none; font: inherit; }
.create-opt:hover { background: var(--bg-sub); color: var(--accent-color); }

.sidebar-search { padding: 0 24px 15px; }
.sidebar-search input { width: 100%; border: none; padding: 8px 0; font-size: 12px; border-bottom: 1px solid var(--border-line); outline: none; background: transparent; }
.note-list { flex: 1; overflow-y: auto; padding: 0 16px; }

.note-item, .folder-header { 
  border-radius: 0; 
  cursor: pointer; 
  display: flex; 
  justify-content: space-between; 
  align-items: center; 
  font-size: 13px; 
  color: var(--text-primary); 
}
.folder-header { padding: 10px 8px; }
.note-item { padding: 10px 8px 10px 16px; position: relative; }
.note-item.sub { padding-left: 28px; }

.note-item:hover, .folder-header:hover { background: var(--paper-card); }
.note-item.active { background: var(--bg-sub); color: var(--accent-color); font-weight: 500; }

.note-item::before {
  content: '';
  position: absolute;
  left: 6px;
  top: 50%;
  transform: translateY(-50%);
  width: 2px;
  height: 10px;
  border-radius: 0;
  background: var(--bg-sub);
  opacity: 0.4;
  transition: all 0.3s cubic-bezier(0.16, 1, 0.3, 1);
}
.note-item.sub::before { left: 18px; }

.note-item:hover::before, .note-item.active::before {
  height: 16px;
  opacity: 1;
}

/* --- 左侧呼吸灯指示条颜色 --- */
.note-item.type-blog::before { background: #af52de; }
.note-item.type-post::before { background: #32ade6; }
.note-item.type-schedule::before { background: var(--warning); }
.note-item.type-wiki::before { background: var(--success); }
.note-item.type-note::before { background: var(--border-line); }
.note-item.type-canvas::before { background: #5856d6; }

.item-content {
  display: flex;
  align-items: baseline;
  gap: 8px;
  flex: 1;
  min-width: 0; 
}

.item-title {
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  transition: color 0.2s;
}

/* --- 标题特殊样式区分 --- */
.note-item.type-post .item-title { font-style: italic; color: var(--text-secondary); }
.note-item.type-wiki .item-title { font-weight: 600; letter-spacing: 0.02em; }
.note-item.type-blog .item-title { font-weight: 600; color: var(--text-primary); }

/* --- 右侧极简字样标签颜色 --- */
.type-label {
  font-size: 10px;
  font-weight: 600;
  opacity: 0.7;
  flex-shrink: 0;
  transition: opacity 0.2s;
}
.note-item.type-blog .type-label { color: #af52de; }
.note-item.type-post .type-label { color: #32ade6; }
.note-item.type-schedule .type-label { color: var(--warning); }
.note-item.type-wiki .type-label { color: var(--success); }
.note-item.type-note .type-label { color: var(--text-secondary); }
.note-item.type-canvas .type-label { color: #5856d6; }

.item-hover-actions { display: none; gap: 8px; font-size: 10px; color: var(--text-secondary); }
.danger { color: var(--danger) !important; }

.note-item:hover .type-label { display: none; }
.note-item:hover .item-hover-actions, .folder-header:hover .item-hover-actions { display: flex; }

.spirit-overlay { position: fixed; inset: 0; background: var(--paper-card); backdrop-filter: blur(8px); z-index: 5000; display: flex; align-items: center; justify-content: center; }
.spirit-dialog { background: var(--paper-card); padding: 40px; border: 1px solid var(--border-line); border-radius: 0; box-shadow: 0 15px 50px rgba(0,0,0,0.05); text-align: center; max-width: 320px; }
.dialog-msg { font-size: 14px; margin-bottom: 30px; color: var(--text-primary); line-height: 1.6; }
.dialog-actions { display: flex; justify-content: center; gap: 40px; }
.dialog-btn { font-size: 12px; color: var(--text-secondary); cursor: pointer; border-bottom: 1px solid transparent; }
.dialog-btn:hover { color: var(--text-primary); border-bottom-color: var(--border-line); }
.dialog-btn.danger:hover { color: var(--danger); border-bottom-color: var(--danger); }

.sidebar-footer { padding: 14px 24px; font-size: 11px; color: var(--text-secondary); }

.fade-enter-active, .fade-leave-active { transition: opacity 0.3s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
.current-space-label { width: 100%; background: none; color: inherit; border: none; font: inherit; text-align: left; }
.text-btn, .create-opt, .item-hover-actions button { background: none; border: none; font-family: inherit; cursor: pointer; }
.header-actions { gap: 8px; white-space: nowrap; }.sidebar-header { padding: 10px 16px; }
.create-opt { display: block; width: 100%; text-align: left; }
.item-hover-actions .danger { color: var(--danger); }
.note-item:focus-within .item-hover-actions { opacity: 1; }
@media (hover: none) { .item-hover-actions { opacity: 1; } }
/* 生活管理入口：放在空间选择下面，显眼一点 */
.planner-entry { display: flex; align-items: center; gap: 10px; margin: 0 16px 12px; padding: 10px 12px; width: calc(100% - 32px); box-sizing: border-box; background: var(--accent-soft); border: 1px solid var(--accent-color); border-left-width: 3px; color: var(--text-primary); cursor: pointer; text-align: left; border-radius: 0; font-family: inherit; }
.planner-entry:hover { background: var(--bg-sub); }
.planner-entry.active { background: var(--accent-color); color: var(--paper-card); }
.planner-entry.active small { color: inherit; opacity: .85; }
.planner-entry-icon { font-size: 18px; line-height: 1; }
.planner-entry-text { display: flex; flex-direction: column; min-width: 0; }
.planner-entry-text strong { font-size: 13px; }
.planner-entry-text small { font-size: 11px; color: var(--text-secondary); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
/* 番茄钟入口紧跟在生活管理下面，上移一点贴近 */
.pomodoro-entry { margin-top: -6px; }
</style>
