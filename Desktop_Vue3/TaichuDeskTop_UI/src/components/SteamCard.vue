<!-- Steam 卡片：头像、昵称、在线 / 正在玩、等级、游戏数、总时长，三角洲行动时长单独突出，最近在玩 / 玩得最久。
     两种用法：传 card（个人中心预览）；或传 userId（公开主页，自己去读，没绑定就什么都不显示）。 -->
<template>
  <section v-if="data" class="stc" :class="{ playing: !!data.playing }" aria-label="Steam">
    <header class="stc-head">
      <a class="stc-avatar" :href="profileUrl" target="_blank" rel="noopener noreferrer" :title="'打开 Steam 个人资料'">
        <img v-if="data.avatar" :src="data.avatar" alt="" loading="lazy" />
        <span v-else aria-hidden="true">S</span>
        <i v-if="data.state != null" class="stc-dot" :class="dotClass" aria-hidden="true"></i>
      </a>
      <div class="stc-who">
        <div class="stc-name-row">
          <span class="stc-tag">STEAM</span>
          <a class="stc-name" :href="profileUrl" target="_blank" rel="noopener noreferrer">{{ data.name || 'Steam 玩家' }}</a>
          <span v-if="data.level != null" class="stc-level" :title="`Steam 等级 ${data.level}`">Lv.{{ data.level }}</span>
          <span class="stc-verified" title="通过 Steam 官方登录绑定，确认是本人的号">✓ 已验证</span>
        </div>
        <p v-if="data.playing" class="stc-status playing">▶ 正在玩 <b>{{ data.playing }}</b></p>
        <p v-else-if="data.state != null" class="stc-status">{{ STEAM_STATE[data.state] ?? '离线' }}</p>
      </div>
      <dl v-if="data.gameCount != null || data.totalMinutes != null" class="stc-nums">
        <div v-if="data.gameCount != null"><dt>游戏</dt><dd>{{ data.gameCount }}</dd></div>
        <div v-if="data.totalMinutes != null"><dt>总时长</dt><dd>{{ fmtHours(data.totalMinutes) }}</dd></div>
      </dl>
    </header>

    <!-- 三角洲行动：单独拿出来 -->
    <div v-if="data.featured" class="stc-featured">
      <img v-if="data.featured.icon" :src="data.featured.icon" alt="" loading="lazy" />
      <span class="stc-feat-name">{{ data.featured.name }}</span>
      <span v-if="showTime" class="stc-feat-time">{{ fmtPlaytime(data.featured.minutes) }}<small v-if="data.featured.minutes2Weeks"> · 近两周 {{ fmtPlaytime(data.featured.minutes2Weeks) }}</small></span>
    </div>

    <p v-if="!data.profilePublic && data.gameCount == null" class="stc-note">Ta 的 Steam 游戏详情没有公开，只能看到头像和昵称。</p>

    <div v-if="lists.length" class="stc-lists">
      <div v-for="l in lists" :key="l.key" class="stc-list">
        <h4>{{ l.title }}</h4>
        <ul>
          <li v-for="g in l.games" :key="l.key + g.appId">
            <img v-if="g.icon" :src="g.icon" alt="" loading="lazy" />
            <span v-else class="stc-icon-empty" aria-hidden="true">{{ g.name.slice(0, 1) }}</span>
            <span class="stc-game">{{ g.name }}</span>
            <span v-if="showTime" class="stc-time">{{ l.key === 'recent' && g.minutes2Weeks ? fmtPlaytime(g.minutes2Weeks) : fmtPlaytime(g.minutes) }}</span>
          </li>
        </ul>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { STEAM_STATE, fmtPlaytime, steamApi, type SteamCardData } from '@/api/steam'

const props = defineProps<{ card?: SteamCardData | null; userId?: string; showPlaytime?: boolean }>()
const loaded = ref<SteamCardData | null>(null)
const loadedShowTime = ref(true)
const data = computed(() => props.card ?? loaded.value)
const showTime = computed(() => props.card ? props.showPlaytime !== false : loadedShowTime.value)
const profileUrl = computed(() => data.value?.profileUrl || `https://steamcommunity.com/profiles/${data.value?.steamId}`)
const dotClass = computed(() => data.value?.playing ? 'in-game' : (data.value?.state ?? 0) > 0 ? 'online' : 'offline')
const fmtHours = (min?: number | null) => fmtPlaytime(min)

const lists = computed(() => {
  const d = data.value
  if (!d) return []
  const out: { key: string; title: string; games: NonNullable<SteamCardData['top']> }[] = []
  if (d.recent?.length) out.push({ key: 'recent', title: '最近在玩', games: d.recent.slice(0, 5) })
  if (d.top?.length) out.push({ key: 'top', title: '玩得最久', games: d.top.slice(0, 5) })
  return out
})

let version = 0
watch(() => props.userId, async id => {
  const v = ++version
  loaded.value = null
  if (!id || props.card) return
  try {
    const r = await steamApi.ofUser(id)
    if (v !== version || !r) return
    loaded.value = r.card
    loadedShowTime.value = r.showPlaytime
  } catch { /* 没绑定或后端没更新：不显示 */ }
}, { immediate: true })
</script>

<style scoped>
.stc {
  --steam: #66c0f4; --steam-deep: #1b2838;
  position: relative; display: grid; gap: 12px; padding: 16px 18px; margin: 0 0 20px;
  border: 1px solid var(--border-line); border-top: 2px solid var(--steam); background: var(--paper-card); color: var(--text-primary);
}
.stc-head { display: flex; align-items: center; gap: 14px; flex-wrap: wrap; }
.stc-avatar { position: relative; flex-shrink: 0; width: 56px; height: 56px; display: grid; place-items: center; background: var(--steam-deep); color: var(--steam); font-weight: 700; text-decoration: none; }
.stc-avatar img { width: 100%; height: 100%; object-fit: cover; display: block; }
.stc-dot { position: absolute; right: -3px; bottom: -3px; width: 12px; height: 12px; border: 2px solid var(--paper-card); background: #8f98a0; }
.stc-dot.online { background: #57cbde; }
.stc-dot.in-game { background: #90ba3c; }
.stc-who { flex: 1; min-width: 180px; display: grid; gap: 4px; }
.stc-name-row { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
.stc-tag { padding: 1px 6px; font: 600 10px/1.6 var(--font-mono, monospace); letter-spacing: .12em; color: var(--steam); background: var(--steam-deep); }
.stc-name { font-size: 17px; font-weight: 700; color: var(--text-primary); text-decoration: none; overflow-wrap: anywhere; }
.stc-name:hover { color: var(--accent-color); }
.stc-level { padding: 0 6px; border: 1px solid var(--steam); color: var(--steam); font: 600 11px/1.6 var(--font-mono, monospace); }
.stc-verified { font-size: 11px; color: var(--success, #2f9e44); }
.stc-status { margin: 0; font-size: 13px; color: var(--text-secondary); }
.stc-status.playing { color: #6f9a1f; }
.stc-status b { font-weight: 600; }
.stc-nums { display: flex; gap: 18px; margin: 0; }
.stc-nums div { display: grid; gap: 2px; text-align: right; }
.stc-nums dt { font-size: 11px; color: var(--text-secondary); }
.stc-nums dd { margin: 0; font: 600 18px/1.2 var(--font-mono, monospace); }
.stc-featured { display: flex; align-items: center; gap: 10px; padding: 8px 12px; border: 1px solid color-mix(in srgb, var(--warning, #d97706) 45%, var(--border-line)); border-left: 3px solid var(--warning, #d97706); background: color-mix(in srgb, var(--warning, #d97706) 7%, var(--paper-card)); }
.stc-featured img { width: 28px; height: 28px; }
.stc-feat-name { flex: 1; min-width: 0; font-weight: 600; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.stc-feat-time { font: 600 14px var(--font-mono, monospace); white-space: nowrap; }
.stc-feat-time small { font-weight: 400; font-size: 12px; color: var(--text-secondary); }
.stc-note { margin: 0; font-size: 13px; color: var(--text-secondary); }
.stc-lists { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 14px; }
.stc-list h4 { margin: 0 0 6px; font-size: 12px; font-weight: 600; letter-spacing: .08em; color: var(--text-secondary); }
.stc-list ul { list-style: none; margin: 0; padding: 0; display: grid; gap: 4px; }
.stc-list li { display: flex; align-items: center; gap: 8px; min-width: 0; font-size: 13px; }
.stc-list img, .stc-icon-empty { flex-shrink: 0; width: 20px; height: 20px; }
.stc-icon-empty { display: grid; place-items: center; background: var(--bg-sub); font-size: 11px; color: var(--text-secondary); }
.stc-game { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.stc-time { font: 12px var(--font-mono, monospace); color: var(--text-secondary); white-space: nowrap; }
@media (max-width: 560px) { .stc-nums { width: 100%; justify-content: flex-start; } .stc-nums div { text-align: left; } }
</style>
