<template>
  <div class="awards-admin">
    <header class="aa-header">
      <div>
        <h1>年度最佳</h1>
        <p>维护每年的获奖作品和奖品。改完点「保存全部」才会生效；未公开的年份只有管理员能在展览页看到。</p>
      </div>
      <div class="aa-header-actions">
        <RouterLink class="btn-line" to="/awards" target="_blank">查看展览 ↗</RouterLink>
        <button type="button" class="btn-line btn-primary" :disabled="saving || !dirty" @click="save">{{ saving ? '保存中…' : dirty ? '保存全部' : '已保存' }}</button>
      </div>
    </header>
    <p v-if="message" class="aa-message" :class="{ error: messageIsError }" role="status">{{ message }}</p>
    <p v-if="loading" class="aa-state" role="status">正在读取…</p>
    <p v-else-if="loadError" class="aa-state" role="alert">{{ loadError }} <button class="btn-line" @click="load">重试</button></p>

    <template v-else>
      <!-- 板块 -->
      <section class="aa-block">
        <header class="aa-block-head">
          <h2>参评板块</h2>
          <p>以后有新的板块参与评选，在这里添加即可。「作品来源」决定新建奖项时默认按哪类作品 ID 读取信息。</p>
        </header>
        <div class="cat-list">
          <div v-for="(c, i) in hall.categories" :key="i" class="cat-row">
            <label><span>图标</span><input v-model="c.icon" maxlength="8" placeholder="🎨" @input="touch" /></label>
            <label><span>名称</span><input v-model="c.name" maxlength="30" placeholder="艺术画廊" @input="touch" /></label>
            <label><span>标识</span><input v-model="c.key" maxlength="20" placeholder="gallery" :disabled="isCategoryUsed(c.key)" :title="isCategoryUsed(c.key) ? '已有奖项使用这个板块，不能修改标识' : ''" @input="touch" /></label>
            <label><span>作品来源</span>
              <select v-model="c.source" @change="touch">
                <option v-for="(label, key) in WORK_TYPE_LABELS" :key="key" :value="key">{{ label }}</option>
              </select>
            </label>
            <label class="wide"><span>简介</span><input v-model="c.description" maxlength="200" placeholder="一句话介绍这个奖项板块" @input="touch" /></label>
            <button type="button" class="text-btn danger" :disabled="isCategoryUsed(c.key)" :title="isCategoryUsed(c.key) ? '先删除这个板块下的奖项' : ''" @click="removeCategory(i)">删除</button>
          </div>
        </div>
        <button type="button" class="btn-line" @click="addCategory">＋ 添加板块</button>
      </section>

      <!-- 年份 -->
      <section class="aa-block">
        <header class="aa-block-head"><h2>年份</h2></header>
        <div class="year-row">
          <button v-for="y in sortedYears" :key="y.year" type="button" class="year-chip" :class="{ active: selectedYear === y.year }" @click="selectedYear = y.year">
            {{ y.year }}<small>{{ y.published ? '已公开' : '未公开' }}</small>
          </button>
          <span class="year-add">
            <input v-model.number="newYear" type="number" min="2000" max="2100" aria-label="新年份" />
            <button type="button" class="btn-line" @click="addYear">＋ 新增年份</button>
          </span>
        </div>

        <div v-if="year" class="year-form">
          <label><span>展览标题</span><input v-model="year.title" maxlength="60" :placeholder="`${year.year} 年度最佳`" @input="touch" /></label>
          <label><span>颁奖日期</span><input v-model="year.ceremonyDate" type="date" @input="touch" /></label>
          <label class="check"><input v-model="year.published" type="checkbox" @change="touch" /><span>在展览页公开</span></label>
          <label class="wide"><span>年度寄语</span><textarea v-model="year.intro" rows="3" maxlength="2000" placeholder="写给这一年的创作者们…" @input="touch"></textarea></label>
          <button type="button" class="text-btn danger" @click="removeYear">删除 {{ year.year }} 年</button>
        </div>
      </section>

      <!-- 奖项 -->
      <section v-if="year" class="aa-block">
        <header class="aa-block-head">
          <h2>{{ year.year }} 年的奖项</h2>
          <p>每个板块里排名 1 的奖项会在展览页大图展示。填作品 ID（或直接粘贴作品/帖子链接）后点「读取」，会自动带出标题、封面和作者，之后也可以手动修改。没有发帖的建筑可以选「手动填写」，上传截图即可。</p>
        </header>

        <div v-for="c in hall.categories" :key="c.key" class="entry-group">
          <h3><span v-if="c.icon" aria-hidden="true">{{ c.icon }}</span>{{ c.name || c.key }}<small>{{ entriesOf(c.key).length }} 项</small></h3>

          <article v-for="e in entriesOf(c.key)" :key="e.id" class="entry-card">
            <div class="entry-preview">
              <img v-if="e.coverUrl" :src="e.coverUrl" alt="" @error="($event.target as HTMLImageElement).hidden = true" />
              <span v-else>无封面</span>
            </div>
            <div class="entry-fields">
              <div class="field-row">
                <label><span>奖项名称</span><input v-model="e.awardName" maxlength="40" placeholder="年度最佳画作" @input="touch" /></label>
                <label class="narrow"><span>排名</span><input v-model.number="e.rank" type="number" min="1" max="99" @input="touch" /></label>
                <label class="narrow"><span>板块</span>
                  <select v-model="e.category" @change="touch"><option v-for="opt in hall.categories" :key="opt.key" :value="opt.key">{{ opt.name || opt.key }}</option></select>
                </label>
              </div>
              <div class="field-row">
                <label class="narrow"><span>作品来源</span>
                  <select v-model="e.workType" @change="touch"><option v-for="(label, key) in WORK_TYPE_LABELS" :key="key" :value="key">{{ label }}</option></select>
                </label>
                <label v-if="e.workType !== 'custom'"><span>作品 ID 或链接</span>
                  <span class="inline"><input v-model="e.workId" :placeholder="workIdHint(e.workType)" @input="touch" /><button type="button" class="btn-line" :disabled="!e.workId || lookingUp === e.id" @click="lookup(e)">{{ lookingUp === e.id ? '读取中…' : '读取' }}</button></span>
                </label>
              </div>
              <div class="field-row">
                <label><span>作品标题</span><input v-model="e.workTitle" maxlength="100" @input="touch" /></label>
                <label><span>作者名</span><input v-model="e.authorName" maxlength="50" @input="touch" /></label>
                <label><span>作者用户 ID</span><input v-model="e.authorId" maxlength="64" placeholder="用于点击进入主页，可留空" @input="touch" /></label>
              </div>
              <div class="field-row">
                <label><span>封面图片地址</span><span class="inline"><input v-model="e.coverUrl" maxlength="500" @input="touch" /><label class="btn-line upload">上传<input type="file" accept="image/*" hidden @change="upload($event, e, 'coverUrl')" /></label></span></label>
                <label><span>作品链接</span><input v-model="e.workLink" maxlength="500" placeholder="/gallery?workId=128 或完整网址" @input="touch" /></label>
              </div>
              <label class="wide"><span>颁奖词</span><textarea v-model="e.citation" rows="2" maxlength="1000" placeholder="为什么是这件作品？" @input="touch"></textarea></label>
              <div class="field-row prize-row">
                <label><span>奖品</span><input v-model="e.prize" maxlength="100" placeholder="例如：数位板一块 / 500 经验" @input="touch" /></label>
                <label><span>奖品图片</span><span class="inline"><input v-model="e.prizeImage" maxlength="500" @input="touch" /><label class="btn-line upload">上传<input type="file" accept="image/*" hidden @change="upload($event, e, 'prizeImage')" /></label></span></label>
                <label><span>发放说明</span><input v-model="e.prizeNote" maxlength="300" placeholder="例如：已于 1 月 5 日寄出" @input="touch" /></label>
              </div>
              <div class="entry-actions"><button type="button" class="text-btn danger" @click="removeEntry(e)">删除这个奖项</button></div>
            </div>
          </article>

          <button type="button" class="btn-line add-entry" @click="addEntry(c)">＋ 在「{{ c.name || c.key }}」添加奖项</button>
        </div>
      </section>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { awardsApi, extractWorkId, WORK_TYPE_LABELS, type AwardCategory, type AwardEntry, type AwardYear, type AwardsHall } from '@/api/awards'
import { useCos } from '@/composables/useCos'

const { uploadFile } = useCos()
const hall = ref<AwardsHall>({ categories: [], years: [] })
const loading = ref(true), loadError = ref(''), saving = ref(false), dirty = ref(false)
const message = ref(''), messageIsError = ref(false)
const selectedYear = ref<number | null>(null)
const newYear = ref(new Date().getFullYear())
const lookingUp = ref('')

const sortedYears = computed(() => hall.value.years.slice().sort((a, b) => b.year - a.year))
const year = computed<AwardYear | undefined>(() => hall.value.years.find(y => y.year === selectedYear.value))
const entriesOf = (key: string) => (year.value?.entries ?? []).filter(e => e.category === key).sort((a, b) => a.rank - b.rank)
const isCategoryUsed = (key: string) => Boolean(key) && hall.value.years.some(y => y.entries.some(e => e.category === key))

function touch() { dirty.value = true; message.value = '' }
function notify(text: string, isError = false) { message.value = text; messageIsError.value = isError }

async function load() {
  loading.value = true; loadError.value = ''
  try {
    const data = await awardsApi.get()
    hall.value = { categories: data.categories, years: data.years }
    hall.value.categories.forEach(c => { c.source ||= 'custom' })
    selectedYear.value = sortedYears.value[0]?.year ?? null
    dirty.value = false
  } catch { loadError.value = '读取失败。' }
  finally { loading.value = false }
}
onMounted(load)

// 有未保存的修改时，离开页面前提醒。
const beforeUnload = (event: BeforeUnloadEvent) => { if (dirty.value) { event.preventDefault(); event.returnValue = '' } }
onMounted(() => window.addEventListener('beforeunload', beforeUnload))
onBeforeUnmount(() => window.removeEventListener('beforeunload', beforeUnload))

function addCategory() {
  hall.value.categories.push({ key: `section${hall.value.categories.length + 1}`, name: '', icon: '', description: '', source: 'custom' })
  touch()
}
function removeCategory(index: number) { hall.value.categories.splice(index, 1); touch() }

function addYear() {
  const y = Number(newYear.value)
  if (!Number.isInteger(y) || y < 2000 || y > 2100) return notify('请输入 2000 到 2100 之间的年份', true)
  if (hall.value.years.some(item => item.year === y)) { selectedYear.value = y; return notify(`${y} 年已经存在`, true) }
  hall.value.years.push({ year: y, title: `${y} 年度最佳`, intro: '', ceremonyDate: '', published: false, entries: [] })
  selectedYear.value = y
  touch()
}
function removeYear() {
  if (!year.value) return
  if (!confirm(`确定删除 ${year.value.year} 年的全部奖项吗？保存后无法恢复。`)) return
  hall.value.years = hall.value.years.filter(y => y.year !== selectedYear.value)
  selectedYear.value = sortedYears.value[0]?.year ?? null
  touch()
}

function addEntry(c: AwardCategory) {
  if (!year.value) return
  const rank = entriesOf(c.key).length + 1
  year.value.entries.push({
    id: Math.random().toString(36).slice(2) + Date.now().toString(36),
    category: c.key, awardName: rank === 1 ? `年度最佳${c.name || ''}` : '', rank,
    workType: (c.source as AwardEntry['workType']) || 'custom', workId: '', workTitle: '', coverUrl: '', workLink: '',
    authorId: '', authorName: '', citation: '', prize: '', prizeImage: '', prizeNote: '',
  })
  touch()
}
function removeEntry(e: AwardEntry) {
  if (!year.value || !confirm(`删除奖项「${e.awardName || '未命名'}」？`)) return
  year.value.entries = year.value.entries.filter(item => item.id !== e.id)
  touch()
}

const workIdHint = (type?: string | null) => type === 'artwork' ? '画廊作品编号，或粘贴作品链接' : type === 'post' ? '粘贴论坛帖子链接（含 post=…）' : '柴圈作品 ID，或粘贴作品链接'

async function lookup(e: AwardEntry) {
  if (!e.workId || !e.workType || e.workType === 'custom') return
  e.workId = extractWorkId(e.workId)
  lookingUp.value = e.id
  try {
    const found = await awardsApi.lookup(e.workType, e.workId.trim())
    Object.assign(e, { workTitle: found.workTitle, coverUrl: found.coverUrl, authorId: found.authorId, authorName: found.authorName, workLink: found.workLink })
    touch()
    notify(`已读取「${found.workTitle}」`)
  } catch (err: any) {
    notify(err?.response?.data?.message || '没有找到这件作品，请检查 ID', true)
  } finally { lookingUp.value = '' }
}

async function upload(event: Event, e: AwardEntry, field: 'coverUrl' | 'prizeImage') {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  try {
    const result = await uploadFile(file, 'awards')
    e[field] = result.url
    touch()
  } catch { notify('图片上传失败，请重试', true) }
  finally { input.value = '' }
}

async function save() {
  if (hall.value.categories.some(c => !c.key?.trim() || !c.name?.trim())) return notify('每个板块都需要填写名称和标识', true)
  const missing = hall.value.years.flatMap(y => y.entries.filter(e => !e.awardName?.trim()).map(() => y.year))
  if (missing.length) { selectedYear.value = missing[0]; return notify(`${missing[0]} 年有奖项没有填写奖项名称`, true) }
  saving.value = true
  try {
    await awardsApi.save({ categories: hall.value.categories, years: hall.value.years })
    dirty.value = false
    notify('已保存，展览页已更新')
  } catch (err: any) {
    notify(err?.response?.data?.message || '保存失败，请重试', true)
  } finally { saving.value = false }
}
</script>

<style scoped>
.awards-admin { display: grid; gap: 24px; color: var(--text-primary); }
.aa-header { display: flex; justify-content: space-between; align-items: flex-end; gap: 20px; flex-wrap: wrap; }
.aa-header h1 { margin: 0; font-size: 26px; letter-spacing: -.03em; }
.aa-header p { margin: 8px 0 0; color: var(--text-secondary); font-size: 13px; max-width: 640px; }
.aa-header-actions { display: flex; gap: 10px; }
.aa-message { margin: 0; padding: 10px 14px; border-left: 3px solid var(--signal-mint); background: var(--accent-soft); font-size: 13px; }
.aa-message.error { border-left-color: var(--danger, #c0392b); background: var(--danger-soft, rgba(192,57,43,.08)); }
.aa-state { color: var(--text-secondary); }

.aa-block { display: grid; gap: 14px; padding: 18px 20px; border: 1px solid var(--border-line); border-top: 2px solid var(--accent-color); background: var(--paper-card); }
.aa-block-head h2 { margin: 0; font-size: 17px; }
.aa-block-head p { margin: 6px 0 0; color: var(--text-secondary); font-size: 12px; }

label { display: grid; gap: 4px; min-width: 0; font-size: 12px; }
label > span:first-child { color: var(--text-secondary); font-size: 11px; letter-spacing: .04em; }
input, select, textarea { box-sizing: border-box; width: 100%; padding: 7px 9px; border: 1px solid var(--border-line); border-radius: 0; background: var(--paper-bg); color: var(--text-primary); font: inherit; font-size: 13px; }
input:focus, select:focus, textarea:focus { outline: none; border-color: var(--accent-color); }
input:disabled { opacity: .6; }
textarea { resize: vertical; line-height: 1.6; }
.wide { grid-column: 1 / -1; }
.inline { display: flex; gap: 6px; }
.inline input { flex: 1; }
.btn-line.upload { display: inline-flex; align-items: center; white-space: nowrap; cursor: pointer; }
.text-btn { border: 0; background: none; padding: 0; color: var(--text-secondary); font: inherit; font-size: 12px; cursor: pointer; justify-self: start; }
.text-btn.danger:hover:not(:disabled) { color: var(--danger, #c0392b); }
.text-btn:disabled { opacity: .4; cursor: not-allowed; }

.cat-list { display: grid; gap: 10px; }
.cat-row { display: grid; grid-template-columns: 70px 1fr 120px 130px 2fr auto; gap: 10px; align-items: end; padding-bottom: 10px; border-bottom: 1px dashed var(--border-line); }
.cat-row .wide { grid-column: auto; }

.year-row { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; }
.year-chip { display: inline-flex; align-items: baseline; gap: 6px; padding: 7px 14px; border: 1px solid var(--border-line); background: transparent; color: var(--text-secondary); font: 600 14px var(--font-mono); cursor: pointer; }
.year-chip small { font: 10px var(--font-sans, inherit); }
.year-chip.active { background: var(--inverse-bg); color: var(--inverse-text); border-color: var(--inverse-bg); }
.year-add { display: inline-flex; gap: 6px; margin-left: auto; }
.year-add input { width: 96px; }
.year-form { display: grid; grid-template-columns: 2fr 1fr auto; gap: 12px; align-items: end; }
.check { display: flex; align-items: center; gap: 6px; font-size: 13px; padding-bottom: 8px; }
.check input { width: auto; }

.entry-group { display: grid; gap: 12px; padding-top: 6px; }
.entry-group h3 { display: flex; align-items: center; gap: 8px; margin: 0; font-size: 15px; }
.entry-group h3 small { color: var(--text-secondary); font-size: 12px; font-weight: 400; }
.entry-card { display: grid; grid-template-columns: 120px minmax(0, 1fr); gap: 14px; padding: 14px; border: 1px solid var(--border-line); border-left: 3px solid var(--signal-mint); background: var(--paper-bg); }
.entry-preview { width: 120px; aspect-ratio: 4 / 3; display: grid; place-items: center; background: var(--bg-sub); color: var(--text-secondary); font-size: 12px; overflow: hidden; }
.entry-preview img { width: 100%; height: 100%; object-fit: cover; }
.entry-fields { display: grid; gap: 10px; min-width: 0; }
.field-row { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 10px; }
.field-row .narrow { max-width: 140px; }
.prize-row { padding-top: 8px; border-top: 1px dashed var(--border-line); }
.entry-actions { display: flex; justify-content: flex-end; }
.add-entry, .aa-block > .btn-line { justify-self: start; }

@media (max-width: 900px) {
  .cat-row { grid-template-columns: 70px 1fr 1fr; }
  .cat-row .wide { grid-column: 1 / -1; }
  .year-form { grid-template-columns: 1fr; }
  .entry-card { grid-template-columns: 1fr; }
  .entry-preview { width: 100%; max-width: 240px; }
  .year-add { margin-left: 0; }
}
</style>
