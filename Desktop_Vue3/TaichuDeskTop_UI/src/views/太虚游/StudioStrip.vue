<template>
  <!-- 游戏页底部的小横幅：太初寰宇出品的 Steam 游戏 -->
  <aside v-if="game" class="studio-strip" aria-label="太初寰宇出品的游戏">
    <img v-if="game.cover" :src="game.cover" alt="" class="ss-cover" loading="lazy" />
    <div class="ss-copy">
      <p class="ss-eyebrow">太初寰宇出品 · {{ statusLabel(game.status) }}</p>
      <b>{{ game.title }}</b>
      <span>{{ game.tagline || game.description }}</span>
    </div>
    <div class="ss-actions">
      <a v-if="game.storeUrl" :href="game.storeUrl" class="ss-steam" @click.prevent="openExternal(game.storeUrl)">{{ game.status === 'released' ? `Steam ${game.price ?? ''}` : '加入愿望单' }} ↗</a>
      <button type="button" class="ss-more" @click="emit('lobby')">更多我们的游戏</button>
    </div>
  </aside>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { openExternal, showcaseApi, statusLabel, type ShowcaseGame } from '@/api/gameShowcase'

const emit = defineEmits<{ lobby: [] }>()
const games = ref<ShowcaseGame[]>([])
const game = computed(() => games.value.find(g => g.featured) ?? games.value[0] ?? null)
onMounted(async () => { try { games.value = await showcaseApi.list() } catch { games.value = [] } })
</script>

<style scoped>
.studio-strip { display: grid; grid-template-columns: 184px minmax(0, 1fr) auto; gap: 18px; align-items: center; margin-top: 28px; padding: 14px; color: #f3f6f7; background: linear-gradient(100deg, #0d1417 0%, #12232a 100%); border-top: 2px solid var(--signal-mint); }
.ss-cover { width: 184px; aspect-ratio: 460 / 215; object-fit: cover; box-shadow: 0 0 0 1px rgba(255, 255, 255, .12); }
.ss-copy { display: grid; gap: 4px; min-width: 0; }
.ss-eyebrow { margin: 0; font: 600 10px var(--font-mono, monospace); letter-spacing: .14em; color: var(--signal-mint); }
.ss-copy b { font-size: 16px; }
.ss-copy span { color: rgba(243, 246, 247, .7); font-size: 13px; line-height: 1.6; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.ss-actions { display: flex; flex-wrap: wrap; gap: 8px; }
.ss-steam { padding: 9px 14px; background: linear-gradient(90deg, #06bfff, #2d73ff); color: #fff; text-decoration: none; font-size: 13px; font-weight: 600; white-space: nowrap; }
.ss-more { padding: 8px 12px; border: 1px solid rgba(255, 255, 255, .3); background: transparent; color: inherit; font: inherit; font-size: 13px; cursor: pointer; white-space: nowrap; }
.ss-more:hover { border-color: var(--signal-mint); color: var(--signal-mint); }
@media (max-width: 760px) { .studio-strip { grid-template-columns: minmax(0, 1fr); } .ss-cover { width: 100%; } .ss-copy span { white-space: normal; } }
</style>
