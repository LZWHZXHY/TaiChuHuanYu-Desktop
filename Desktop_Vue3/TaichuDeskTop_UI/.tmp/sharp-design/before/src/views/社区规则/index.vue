<template>
  <article class="rules-page content-page">
    <header class="rules-header">
      <div>
        <p class="eyebrow">COMMUNITY RULES · 社区规则</p>
        <h1>太初寰宇 · 社区规则</h1>
        <p class="sub">所有社区板块、群聊等区域均适用。请先阅读，再参与交流。</p>
      </div>
      <div class="rules-actions">
        <span v-if="meta" class="rules-version">规则版本 v{{ meta.version }}</span>
        <button v-if="isAdmin" class="btn primary" @click="openEditor()">+ 新增分类</button>
      </div>
    </header>

    <p v-if="loading" class="rules-empty">加载中…</p>
    <p v-else-if="!tree.length" class="rules-empty">暂无规则。</p>

    <div v-else class="rules-groups">
      <section v-for="cat in tree" :key="cat.id" class="rule-group">
        <header class="rule-group-head">
          <div class="rule-group-title-row">
            <h2 class="rule-group-title">{{ cat.title }}</h2>
            <div v-if="isAdmin" class="rule-admin">
              <button class="mini-btn" @click="openEditor(undefined, cat)">+ 条款</button>
              <button class="mini-btn" @click="openEditor(cat)">编辑</button>
              <button class="mini-btn danger" @click="removeRule(cat)">删除</button>
            </div>
          </div>
          <p v-if="cat.content" class="rule-group-desc">{{ cat.content }}</p>
          <p v-if="cat.penalty" class="rule-group-penalty">{{ cat.penalty }}</p>
        </header>

        <ul class="rule-list">
          <li v-for="r in cat.children" :key="r.id" class="rule-item">
            <div class="rule-main">
              <span v-if="r.code" class="rule-code">{{ r.code }}</span>
              <p class="rule-text">{{ r.content }}</p>
              <div v-if="isAdmin" class="rule-admin inline">
                <button class="mini-btn" @click="openEditor(undefined, r)">+ 子条款</button>
                <button class="mini-btn" @click="openEditor(r)">编辑</button>
                <button class="mini-btn danger" @click="removeRule(r)">删除</button>
              </div>
            </div>
            <p v-if="r.penalty" class="rule-penalty">处理：{{ r.penalty }}</p>
            <p class="rule-updated">最后更新 {{ formatDate(r.updatedAt) }}</p>

            <ul v-if="r.children?.length" class="rule-sublist">
              <li v-for="s in r.children" :key="s.id" class="rule-subitem">
                <span v-if="s.code" class="rule-code small">{{ s.code }}</span>
                <p class="rule-text small">{{ s.content }}</p>
                <div v-if="isAdmin" class="rule-admin inline">
                  <button class="mini-btn" @click="openEditor(s)">编辑</button>
                  <button class="mini-btn danger" @click="removeRule(s)">删除</button>
                </div>
                <p v-if="s.penalty" class="rule-penalty small">处理：{{ s.penalty }}</p>
              </li>
            </ul>
          </li>
        </ul>
      </section>
    </div>

    <!-- 规则编辑器 -->
    <div v-if="showEditor" class="rules-mask" @click.self="showEditor = false">
      <div class="rules-editor">
        <h2 class="editor-title">{{ editorId ? '编辑规则' : (form.parentId ? '新增条款' : '新增分类') }}</h2>
        <div class="field-row">
          <label class="field"><span>归属父级</span>
            <select v-model="form.parentId" @change="onParentChange">
              <option :value="null">（顶级分类）</option>
              <option v-for="o in parentOptions" :key="o.rule.id" :value="o.rule.id">{{ '　'.repeat(o.depth) }}{{ o.rule.code ? o.rule.code + ' ' : '' }}{{ o.rule.title || (o.rule.content || '').slice(0, 20) }}</option>
            </select>
          </label>
          <label class="field"><span>编号（如 1.1）</span><input v-model="form.code" placeholder="1.1" /></label>
        </div>
        <label class="field"><span>标题（顶级分类填名称）</span><input v-model="form.title" placeholder="通用规则" /></label>
        <label class="field"><span>内容</span><textarea v-model="form.content" rows="4" placeholder="规则内容或分类描述"></textarea></label>
        <label class="field"><span>处理方式（可选）</span><input v-model="form.penalty" placeholder="第一次警告，第二次封禁" /></label>
        <label class="field"><span>排序</span><input v-model.number="form.sortOrder" type="number" /></label>
        <label class="check"><input v-model="form.isActive" type="checkbox" /> 公开显示</label>
        <div class="editor-actions">
          <button class="btn" @click="showEditor = false">取消</button>
          <button class="btn primary" :disabled="saving" @click="saveRule">{{ saving ? '保存中…' : '保存' }}</button>
        </div>
      </div>
    </div>
  </article>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { communityRuleApi, type CommunityRuleItem, type CommunityRulePayload } from '@/api/communityRule'
import { useUserStore } from '@/stores/user'

const userStore = useUserStore()
const isAdmin = computed(() => userStore.permissions?.includes('SuperAdmin') ?? false)

const rules = ref<CommunityRuleItem[]>([])
const loading = ref(false)
const showEditor = ref(false)
const editorId = ref<number | null>(null)
const saving = ref(false)

const tree = computed(() => {
  const byParent = new Map<number | null, CommunityRuleItem[]>()
  for (const r of rules.value) {
    const key = r.parentId ?? null
    if (!byParent.has(key)) byParent.set(key, [])
    byParent.get(key)!.push(r)
  }
  const build = (parentId: number | null): Array<CommunityRuleItem & { children: any[] }> =>
    (byParent.get(parentId) || []).map(r => ({ ...r, children: build(r.id) as any }))
  return build(null)
})

/** 展开成带层级的列表（用于「归属父级」下拉，缩进显示） */
function flattenRules(list: CommunityRuleItem[], parentId: number | null = null, depth = 0, out: Array<{ rule: CommunityRuleItem; depth: number }> = []) {
  for (const r of list.filter(x => (x.parentId ?? null) === parentId).sort((a, b) => a.sortOrder - b.sortOrder || a.id - b.id)) {
    out.push({ rule: r, depth })
    flattenRules(list, r.id, depth + 1, out)
  }
  return out
}
// 父级只允许「顶级分类」和「二级条款」（最多三层，与展示页一致）
const parentOptions = computed(() => flattenRules(rules.value).filter(o => o.depth <= 1))

/** 按同级已有编号算下一个（2.1 下已有 2.1.1 → 2.1.2） */
function nextChildCode(parent: CommunityRuleItem): string {
  const siblings = rules.value.filter(r => r.parentId === parent.id)
  const last = siblings.reduce((max, r) => {
    const m = (r.code || '').match(/(\d+)\s*$/)
    return Math.max(max, m ? parseInt(m[1], 10) : 0)
  }, 0)
  const base = (parent.code || '').trim()
  return base ? `${base}.${last + 1}` : String(last + 1)
}
function onParentChange() {
  if (editorId.value) return
  const p = rules.value.find(r => r.id === form.value.parentId)
  form.value.code = p ? nextChildCode(p) : ''
}
// 规则文档版本（后端自动升级：新增条款 +0.1，修改/删除 +0.0.1）
const meta = ref<{ version: string; updatedAt: string } | null>(null)

const emptyForm = (): CommunityRulePayload => ({
  parentId: null, code: null, title: '', content: '', penalty: '', sortOrder: 0, isActive: true, version: '1.0'
})
const form = ref<CommunityRulePayload>(emptyForm())

function openEditor(rule?: CommunityRuleItem, parent?: CommunityRuleItem) {
  editorId.value = rule?.id ?? null
  if (rule) {
    form.value = {
      parentId: rule.parentId, code: rule.code, title: rule.title, content: rule.content,
      penalty: rule.penalty, sortOrder: rule.sortOrder, isActive: rule.isActive, version: rule.version
    }
  } else if (parent) {
    form.value = { ...emptyForm(), parentId: parent.id, code: nextChildCode(parent), sortOrder: ((parent as any).children?.length ?? 0) + 1, version: parent.version }
  } else {
    form.value = { ...emptyForm(), sortOrder: rules.value.length + 1 }
  }
  showEditor.value = true
}

async function saveRule() {
  if (!form.value.parentId && !form.value.title.trim()) { alert('顶级分类请填写标题'); return }
  if (form.value.parentId && !form.value.content?.trim()) { alert('请填写规则内容'); return }
  saving.value = true
  try {
    if (editorId.value) await communityRuleApi.update(editorId.value, form.value)
    else await communityRuleApi.create(form.value)
    showEditor.value = false
    await load()
  } catch (e: any) { alert(e.friendlyMessage || e.message || '保存失败') } finally { saving.value = false }
}

async function removeRule(rule: CommunityRuleItem) {
  if (!confirm(`删除「${rule.title || rule.code || rule.content?.slice(0, 20)}」？`)) return
  await communityRuleApi.remove(rule.id)
  await load()
}

const formatDate = (v: string) => v ? new Date(v).toLocaleDateString('zh-CN', { year: 'numeric', month: 'long', day: 'numeric' }) : ''

async function load() {
  loading.value = true
  try {
    const [list, m] = await Promise.all([
      communityRuleApi.list(),
      communityRuleApi.meta().catch(() => null)
    ])
    rules.value = list
    meta.value = m
  } catch { rules.value = [] } finally { loading.value = false }
}

onMounted(load)
</script>

<style scoped>
.rules-page { max-width: 900px; margin: 0 auto; padding-bottom: 56px; }
.rules-header { display: flex; justify-content: space-between; align-items: flex-end; gap: 16px; flex-wrap: wrap; padding: 28px 0 24px; }
.rules-header h1 { margin: 6px 0 8px; font-size: clamp(1.7rem, 3.2vw, 2.3rem); }
.rules-header .sub { margin: 0; color: var(--text-secondary); font-size: .9rem; }
.rules-actions { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
.rules-version { font-size: .78rem; color: var(--text-secondary); border: 1px solid var(--border-line); border-radius: 12px; padding: 3px 12px; }
.rules-empty { text-align: center; color: var(--text-secondary); padding: 48px 0; }
.rules-groups { display: grid; gap: 32px; }
.rule-group { border: 1px solid var(--border-line); border-radius: 12px; background: var(--paper-card); padding: 22px 24px; }
.rule-group-head { border-bottom: 1px dashed var(--border-line); padding-bottom: 14px; margin-bottom: 16px; }
.rule-group-title-row { display: flex; align-items: center; gap: 10px; justify-content: space-between; flex-wrap: wrap; }
.rule-group-title { margin: 0; font-size: 1.2rem; }
.rule-group-desc { margin: 8px 0 0; font-size: .86rem; color: var(--text-secondary); line-height: 1.7; white-space: pre-wrap; }
.rule-group-penalty { margin: 6px 0 0; font-size: .78rem; color: var(--text-secondary); }
.rule-list { list-style: none; margin: 0; padding: 0; display: grid; gap: 14px; }
.rule-item { padding-left: 2px; }
.rule-main { display: flex; gap: 10px; align-items: flex-start; flex-wrap: wrap; }
.rule-code { flex-shrink: 0; font-family: ui-monospace, monospace; font-size: .78rem; font-weight: 700; color: var(--accent-color); background: var(--accent-soft); border-radius: 6px; padding: 2px 8px; }
.rule-code.small { font-size: .72rem; }
.rule-text { margin: 0; font-size: .92rem; line-height: 1.7; color: var(--text-primary); white-space: pre-wrap; flex: 1; min-width: 200px; }
.rule-text.small { font-size: .86rem; color: var(--text-secondary); }
.rule-penalty { margin: 6px 0 0 0; font-size: .76rem; color: var(--text-secondary); }
.rule-penalty.small { margin-left: 0; }
.rule-updated { margin: 4px 0 0; font-size: .7rem; color: var(--border-line); }
.rule-sublist { list-style: none; margin: 10px 0 0; padding: 0 0 0 18px; border-left: 2px solid var(--border-line); display: grid; gap: 10px; }
.rule-subitem { display: flex; gap: 8px; align-items: flex-start; flex-wrap: wrap; }
.rule-admin { display: flex; gap: 6px; flex-shrink: 0; }
.rule-admin.inline { margin-left: auto; }
.mini-btn { padding: 2px 9px; border: 1px solid var(--border-line); border-radius: 6px; background: var(--paper-card); color: var(--text-secondary); cursor: pointer; font-size: .72rem; }
.mini-btn.danger { color: var(--danger); }
.rules-mask { position: fixed; inset: 0; z-index: 100; background: rgba(0,0,0,.45); display: flex; align-items: center; justify-content: center; padding: 20px; }
.rules-editor { width: min(560px, 100%); max-height: 90vh; overflow-y: auto; background: var(--paper-card); border-radius: 12px; padding: 24px; display: grid; gap: 14px; }
.editor-title { margin: 0; font-size: 1.15rem; }
.field { display: grid; gap: 6px; font-size: .8rem; color: var(--text-secondary); }
.field input, .field select, .field textarea { padding: 9px 12px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--bg-main); color: var(--text-primary); font-size: .88rem; font-family: inherit; }
.field-row { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
.check { display: flex; align-items: center; gap: 6px; font-size: .85rem; cursor: pointer; }
.editor-actions { display: flex; justify-content: flex-end; gap: 10px; }
.btn { padding: 8px 18px; border: 1px solid var(--border-line); border-radius: 8px; background: var(--paper-card); color: var(--text-primary); cursor: pointer; font-size: .84rem; }
.btn.primary { background: var(--accent-color); color: var(--on-accent); border-color: var(--accent-color); }
.btn:disabled { opacity: .5; cursor: default; }
</style>
