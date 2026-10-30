<template>
  <transition name="slide">
    <div v-if="modelValue" class="note-settings-drawer workbench-skin editor-surface community-surface tone-library" role="dialog" aria-label="设置">
      <div class="drawer-header studio-heading">
        <div class="tab-switcher" role="tablist">
          <button role="tab" :aria-selected="activeTab === 'fragment'" :class="{ active: activeTab === 'fragment' }" @click="activeTab = 'fragment'">本篇笔记</button>
          <button role="tab" :aria-selected="activeTab === 'space'" :class="{ active: activeTab === 'space' }" @click="activeTab = 'space'">当前空间</button>
        </div>
        <button class="close-icon-btn" aria-label="关闭设置" @click="$emit('update:modelValue', false)">✕</button>
      </div>

      <div class="drawer-content">
        <div v-if="activeTab === 'fragment' && note">
          <p class="note-name">{{ note.title || '未命名笔记' }}</p>

          <section class="settings-section">
            <h4 class="section-title">所在空间</h4>
            <select :value="note.spaceId" aria-label="所在空间" @change="moveToSpace($event)">
              <option v-for="s in spaces" :key="s.id" :value="s.id">{{ s.name }}</option>
            </select>
            <p class="hint">换成其他空间后，这篇笔记会移过去，左侧列表随之切换。</p>
          </section>

          <section class="settings-section">
            <h4 class="section-title">发布</h4>
            <!-- 帖子和博客在这里直接选论坛版块；改动立即保存到这篇笔记的标签。 -->
            <div v-if="isForumType" class="board-field">
              <label class="board-row">
                <span>版块</span>
                <select :value="currentBoard.board?.tag || ''" aria-label="论坛版块" @change="changeBoard(($event.target as HTMLSelectElement).value, '')">
                  <option value="">不选版块</option>
                  <option v-for="b in FORUM_BOARDS" :key="b.tag" :value="b.tag">{{ b.icon }} {{ b.tag }}</option>
                </select>
              </label>
              <label v-if="boardSubs.length" class="board-row">
                <span>小类</span>
                <select :value="currentBoard.sub?.tag || ''" aria-label="版块小类" @change="changeBoard(currentBoard.board?.tag || '', ($event.target as HTMLSelectElement).value)">
                  <option v-for="s in boardSubs" :key="s.tag" :value="s.tag">{{ s.tag }}</option>
                </select>
              </label>
              <p class="hint">{{ note.isPublic ? '改完后点下方「更新发布」，论坛里才会显示新的版块。' : '发布后会出现在所选版块。' }}</p>
            </div>
            <template v-if="kind.destination || note.type === 'doc'">
              <p class="publish-status" :class="{ on: note.isPublic }">
                <span class="dot" aria-hidden="true"></span>
                {{ note.isPublic ? `已发布到「${kind.destination}」` : `未发布 · 发布后会出现在「${kind.destination}」` }}
              </p>
              <div class="publish-actions">
                <template v-if="note.type === 'doc'">
                  <button class="primary-btn" :disabled="publishing" @click="$emit('publish-click')">分享到项目</button>
                  <button class="plain-btn" :disabled="publishing" @click="$emit('unshare')">取消所有项目分享</button>
                </template>
                <template v-else-if="note.isPublic">
                  <button class="primary-btn" :disabled="publishing || !canPublish" @click="$emit('refresh-publish')">更新发布</button>
                  <button class="plain-btn" :disabled="publishing" @click="$emit('publish-click')">取消发布</button>
                </template>
                <button v-else class="primary-btn" :disabled="publishing || !canPublish" @click="$emit('publish-click')">发布</button>
              </div>
              <p v-if="!canPublish && note.type === 'post'" class="hint warn">帖子不能超过 500 字。删减内容，或把类型改成「博客」再发布。</p>
            </template>
            <p v-else class="hint">「{{ kind.label }}」只保存在灵脉里，不会发布。想发布的话，把类型改成帖子或博客。</p>
          </section>

          <section class="settings-section">
            <h4 class="section-title">类型</h4>
            <div class="type-grid" role="radiogroup" aria-label="笔记类型">
              <button
                v-for="t in mainKinds" :key="t.value"
                type="button" role="radio" :aria-checked="note.type === t.value" :disabled="note.isPublic && note.type !== t.value"
                :class="['type-card', { active: note.type === t.value }]"
                @click="changeType(t.value)"
              >
                <span class="type-icon" aria-hidden="true">{{ t.icon }}</span>
                <span class="type-text"><strong>{{ t.label }}</strong><small>{{ t.desc }}</small></span>
              </button>
            </div>
            <details class="more-types" :open="extraKinds.some(k => k.value === note.type)">
              <summary>更多类型</summary>
              <div class="type-grid" role="radiogroup" aria-label="更多笔记类型">
                <button
                  v-for="t in extraKinds" :key="t.value"
                  type="button" role="radio" :aria-checked="note.type === t.value" :disabled="note.isPublic && note.type !== t.value"
                  :class="['type-card', { active: note.type === t.value }]"
                  @click="changeType(t.value)"
                >
                  <span class="type-icon" aria-hidden="true">{{ t.icon }}</span>
                  <span class="type-text"><strong>{{ t.label }}</strong><small>{{ t.desc }}</small></span>
                </button>
              </div>
            </details>
            <p v-if="note.isPublic" class="hint">已发布的笔记不能改类型。需要改的话，先在上方取消发布。</p>
          </section>

          <section class="settings-section">
            <h4 class="section-title">显示</h4>
            <label class="toggle-row">
              <span>在左侧列表中显示<small>关闭后这篇笔记不会出现在左侧列表里</small></span>
              <span class="spirit-switch">
                <input type="checkbox" :checked="note.showInSidebar" @change="updateNoteField('showInSidebar', ($event.target as HTMLInputElement).checked)" />
                <span class="slider"></span>
              </span>
            </label>
          </section>

          <section class="settings-section actions">
             <button class="action-link" @click="$emit('open-history')">🕒 历史版本</button>
             <button class="action-link danger" @click="$emit('delete', note.id)">🗑️ 删除这篇笔记</button>
          </section>
        </div>

        <div v-if="activeTab === 'space' && currentSpace">
          <section class="settings-section">
            <h4 class="section-title">空间名称</h4>
            <input class="spirit-input" :value="currentSpace.name" aria-label="空间名称" placeholder="例如：创作草稿" @change="updateSpaceField('name', ($event.target as HTMLInputElement).value)" />
          </section>
          <section class="settings-section">
            <h4 class="section-title">左侧列表显示哪些类型</h4>
            <div class="filter-matrix">
              <label v-for="t in filterKinds" :key="t.value" class="matrix-item">
                <span class="matrix-label">{{ t.icon }} {{ t.label }}</span>
                <span class="spirit-switch mini">
                  <input type="checkbox" v-model="displayFilters[t.value]" />
                  <span class="slider"></span>
                </span>
              </label>
            </div>
          </section>
        </div>
      </div>

      <div class="drawer-footer">
        <button class="save-btn" @click="$emit('save')">立即保存</button>
        <span class="footer-hint">内容会自动保存</span>
      </div>
    </div>
  </transition>
</template>

<script setup lang="ts">
import { ref, reactive, watch, computed } from 'vue';
import { NOTE_KINDS, FOLDER_KIND, noteKind } from '@/utils/NoteType';
import { FORUM_BOARDS, boardOf, findBoard, applyBoardTags } from '@/utils/forumBoards';
import { canOpenAdmin } from '@/utils/adminAccess';
import { useUserStore } from '@/stores/user';

const props = defineProps<{
  modelValue: boolean;
  note: any;
  spaces: any[];
  currentSpaceId: string;
  filters: any; 
  publishing?: boolean;
  canPublish: boolean; // 由 SpiritLayout 按类型与字数计算
}>();

const emit = defineEmits(['update:modelValue', 'update-note-meta', 'update-space-meta', 'update-filters', 'update-tags', 'delete', 'open-history', 'publish-click', 'unshare', 'refresh-publish', 'save']);

const userStore = useUserStore();
const isForumType = computed(() => props.note?.type === 'post' || props.note?.type === 'blog');
const currentBoard = computed(() => boardOf(props.note?.tags || []));
const subsOf = (tag: string) => (findBoard(tag)?.subs || []).filter(s => !s.adminOnly || canOpenAdmin(userStore.permissions));
const boardSubs = computed(() => subsOf(currentBoard.value.board?.tag || ''));
// 选版块时自动带上第一个小类，避免出现「有版块没小类」。
function changeBoard(board: string, sub: string) {
  const subs = subsOf(board);
  const nextSub = subs.some(s => s.tag === sub) ? sub : subs[0]?.tag || '';
  emit('update-tags', applyBoardTags(props.note?.tags || [], board, nextSub));
}

const activeTab = ref<'fragment' | 'space'>('fragment');
const kind = computed(() => noteKind(props.note?.type));
const MAIN_TYPES = ['note', 'post', 'blog', 'wiki', 'doc'];
const mainKinds = NOTE_KINDS.filter(k => MAIN_TYPES.includes(k.value));
const extraKinds = NOTE_KINDS.filter(k => !MAIN_TYPES.includes(k.value));
// 侧边栏筛选只针对列表里会出现的类型。
const filterKinds = computed(() => [...NOTE_KINDS.filter(k => k.value in (props.filters || {})), FOLDER_KIND]);

const currentSpace = ref<any>(null);

watch(() => props.currentSpaceId, (id) => {
  currentSpace.value = props.spaces.find(s => s.id === id) || null;
}, { immediate: true });

watch(() => props.spaces, () => {
  currentSpace.value = props.spaces.find(s => s.id === props.currentSpaceId) || null;
}, { deep: true });

const displayFilters = reactive({ ...props.filters });
watch(() => props.filters, (newFilters) => {
  if (newFilters) Object.assign(displayFilters, newFilters);
}, { deep: true });
watch(displayFilters, (newVal) => emit('update-filters', { ...newVal }), { deep: true });

const updateNoteField = (field: string, val: any) => emit('update-note-meta', { [field]: val?.target ? val.target.value : val });
const updateSpaceField = (field: string, val: any) => emit('update-space-meta', { id: props.currentSpaceId, [field]: val });

function moveToSpace(event: Event) {
  const select = event.target as HTMLSelectElement;
  const target = props.spaces.find(s => s.id === select.value);
  if (!target || target.id === props.note?.spaceId) return;
  if (!window.confirm(`把这篇笔记移到「${target.name}」？`)) { select.value = props.note.spaceId; return; }
  updateNoteField('spaceId', target.id);
}

function changeType(type: string) {
  if (!props.note || props.note.type === type) return;
  if (props.note.isPublic) return; // 后端要求先取消发布再改类型
  if (window.confirm(`把类型改成「${noteKind(type).label}」？编辑方式会随之变化。`)) updateNoteField('type', type);
}
</script>

<style scoped>
.note-settings-drawer {
  position: fixed; top: 0; right: 0; bottom: 0; width: min(400px, 100vw);
  background: var(--paper-card); z-index: 3000; padding: 24px;
  box-shadow: -10px 0 40px rgba(0,0,0,0.05); display: flex; flex-direction: column;
}
.drawer-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
.note-name { margin: 0 0 24px; font-size: 18px; font-weight: 700; color: var(--text-primary); overflow-wrap: anywhere; }
.hint { margin: 8px 0 0; font-size: 12px; line-height: 1.6; color: var(--text-secondary); }
.hint.warn { color: var(--warning); }
.type-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 8px; }
.type-card { display: flex; align-items: flex-start; gap: 8px; padding: 10px; text-align: left; border: 1px solid var(--border-line); background: var(--paper-card); color: var(--text-primary); cursor: pointer; font: inherit; }
.type-card:hover:not(:disabled) { border-color: var(--accent-color); }
.type-card:disabled { opacity: .45; cursor: not-allowed; }
.type-card.active { border-color: var(--accent-color); background: var(--accent-soft); box-shadow: inset 3px 0 var(--accent-color); }
.type-icon { font-size: 16px; line-height: 1.3; }
.more-types { margin-top: 8px; }
.more-types summary { padding: 8px 0; font-size: 12px; color: var(--text-secondary); cursor: pointer; }
.type-text { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
.type-text strong { font-size: 13px; }
.type-text small { font-size: 11px; line-height: 1.5; color: var(--text-secondary); }
.board-field { margin: 0 0 16px; padding: 12px; border: 1px solid var(--border-line); background: var(--bg-sub); }
.board-row { display: grid; grid-template-columns: 40px minmax(0, 1fr); align-items: center; gap: 8px; margin-bottom: 8px; font-size: 13px; color: var(--text-secondary); }
.board-field .hint { margin-top: 4px; }
.publish-status { display: flex; align-items: center; gap: 8px; margin: 0 0 12px; font-size: 13px; color: var(--text-secondary); }
.publish-status .dot { width: 8px; height: 8px; background: var(--border-line); flex-shrink: 0; }
.publish-status.on { color: var(--success); }
.publish-status.on .dot { background: var(--success); }
.publish-actions { display: flex; flex-wrap: wrap; gap: 8px; }
.primary-btn, .plain-btn { padding: 9px 16px; font: inherit; font-size: 13px; font-weight: 600; cursor: pointer; border: 1px solid var(--accent-color); }
.primary-btn { background: var(--accent-color); color: var(--on-accent); }
.plain-btn { background: var(--paper-card); color: var(--text-primary); border-color: var(--border-line); }
.primary-btn:disabled, .plain-btn:disabled { opacity: .45; cursor: not-allowed; }
.toggle-row small { display: block; margin-top: 4px; font-size: 11px; color: var(--text-secondary); }
.footer-hint { align-self: center; font-size: 11px; color: var(--text-secondary); }
.tab-switcher { display: flex; gap: 20px; }
.tab-switcher button { border: none; background: none; font-size: 14px; color: var(--text-secondary); cursor: pointer; padding: 0; transition: color 0.2s; }
.tab-switcher button.active { color: var(--text-primary); font-weight: 700; }
.drawer-content { flex: 1; overflow-y: auto; padding-right: 4px; }
.settings-section { margin-bottom: 28px; }
.section-title { font-size: 12px; font-weight: 700; letter-spacing: 0.04em; color: var(--text-secondary); margin: 0 0 12px; }
.field-group { margin-bottom: 24px; }
.field-group label { font-size: 12px; color: var(--text-secondary); margin-bottom: 8px; display: block; }
select, .spirit-input { width: 100%; border: 1px solid var(--border-line); padding: 8px 12px; border-radius: 0; outline: none; transition: 0.2s; font-size: 13px; }
select:focus, .spirit-input:focus { border-color: var(--accent-color); }
.type-chips { display: flex; flex-wrap: wrap; gap: 8px; }
.type-chips button { border: 1px solid var(--border-line); background: var(--paper-card); padding: 6px 12px; border-radius: 0; cursor: pointer; font-size: 12px; transition: 0.2s; color: var(--text-secondary); }
.type-chips button.active { border-color: var(--border-line); color: var(--text-primary); background: var(--bg-sub); font-weight: 600; }
.type-dest { margin: 10px 0 0; font-size: 12px; color: var(--accent-color); line-height: 1.6; }
.toggle-row { display: flex; justify-content: space-between; align-items: center; gap: 16px; margin-bottom: 20px; font-size: 13px; color: var(--text-primary); cursor: pointer; }

/* 过滤矩阵排版增强 */
.filter-matrix { display: flex; flex-direction: column; gap: 12px; }
.matrix-item { display: flex; justify-content: space-between; align-items: center; font-size: 13px; }
.matrix-label { color: var(--text-primary); }

.action-link { display: block; background: none; border: none; padding: 10px 0; font-size: 13px; color: var(--text-secondary); cursor: pointer; width: 100%; text-align: left; }
.action-link.danger { color: var(--danger); }
.drawer-footer { margin-top: auto; display: flex; gap: 12px; padding-top: 20px; border-top: 1px solid var(--border-line); }
.save-btn { padding: 10px 20px; background: var(--text-primary); color: var(--paper-card); border: none; border-radius: 0; font-weight: 600; cursor: pointer; font-size: 13px; }
.publish-btn { flex: 1; padding: 10px; background: var(--bg-sub); border: none; border-radius: 0; font-weight: 600; cursor: pointer; font-size: 13px; color: var(--text-secondary); transition: all 0.2s; }
.publish-btn:disabled { opacity: 0.4; cursor: not-allowed; }
.publish-btn.is-active { background: var(--success); color: var(--on-accent); }

.spirit-switch { position: relative; display: inline-block; width: 32px; height: 18px; flex-shrink: 0; }
.spirit-switch input { opacity: 0; width: 0; height: 0; }
.slider { position: absolute; cursor: pointer; inset: 0; background-color: var(--bg-sub); transition: .3s; border-radius: 0; }
.slider:before { position: absolute; content: ""; height: 14px; width: 14px; left: 2px; bottom: 2px; background-color: var(--paper-card); transition: .3s; border-radius: 50%; }
input:checked + .slider { background-color: var(--accent-color); }
input:checked + .slider:before { transform: translateX(14px); }
.close-icon-btn { background: none; border: none; font-size: 16px; cursor: pointer; color: var(--text-secondary); }
.slide-enter-active, .slide-leave-active { transition: transform 0.4s cubic-bezier(0.16, 1, 0.3, 1); }
.slide-enter-from, .slide-leave-to { transform: translateX(100%); }

/* 发布去向「?」提示 */
.row-label { display: inline-flex; align-items: center; gap: 6px; }
.help-tip { position: relative; width: 16px; height: 16px; border-radius: 0; border: 1px solid var(--border-line); background: var(--bg-sub); color: var(--text-secondary); font-size: 11px; line-height: 1; cursor: help; display: inline-flex; align-items: center; justify-content: center; padding: 0; }
.help-bubble { position: absolute; right: -6px; bottom: 135%; width: 220px; background: rgba(20, 20, 24, 0.92); color: #fff; font-size: 12px; line-height: 1.6; padding: 10px 12px; border-radius: 0; opacity: 0; visibility: hidden; pointer-events: none; transition: opacity 0.15s, visibility 0.15s; z-index: 20; text-align: left; white-space: normal; }
.help-tip:hover .help-bubble, .help-tip:focus .help-bubble { opacity: 1; visibility: visible; }
</style>
