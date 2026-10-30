<template>
  <div class="zone-home community-surface tone-play">
    <header class="zone-header surface-heading"><SectionTrace />
      <span class="eyebrow">参与 · 记录 · 分享</span>
      <h1>让每一次参与都有回响</h1>
      <p>加入一个打卡挑战，或用一份问卷分享你的想法。</p>
    </header>
    <div class="zone-grid">
      <RouterLink to="/activity/checkin" class="zone-card">
        <span class="zone-icon">日</span>
        <div class="zone-info">
          <h2>打卡挑战</h2>
          <p>找到同伴，用持续的记录见证进步。</p>
          <span class="badge">{{ checkinCount === null ? '浏览挑战' : `${checkinCount} 个进行中的挑战` }}</span>
        </div>
        <span aria-hidden="true">→</span>
      </RouterLink>
      <RouterLink to="/activity/survey" class="zone-card">
        <span class="zone-icon">问</span>
        <div class="zone-info">
          <h2>社区问卷</h2>
          <p>参与调查，了解社区里不同的声音。</p>
          <span class="badge">{{ surveyCount === null ? '浏览问卷' : `${surveyCount} 份发布中的问卷` }}</span>
        </div>
        <span aria-hidden="true">→</span>
      </RouterLink>
    </div>
    <section class="activity-shortcuts">
      <div>
        <h2>从自己的节奏开始</h2>
        <p>发起挑战前写清规则、周期和记录要求，方便伙伴参与。</p>
      </div>
      <RouterLink to="/activity/checkin/create">发起打卡 →</RouterLink>
      <RouterLink to="/activity/checkin/my">查看我的打卡 →</RouterLink>
    </section>
  </div>
</template>

<script setup lang="ts">
import SectionTrace from '@/components/SectionTrace.vue'
import { ref, onMounted } from 'vue'
import request from '@/utils/request'

const checkinCount = ref<number | null>(null)
const surveyCount = ref<number | null>(null)

onMounted(async () => {
  // 获取进行中的打卡活动数量
  try {
    const res = await request.get('/activities?status=进行中')
    checkinCount.value = res?.length || 0
  } catch (e) {
    console.error('获取打卡活动数量失败:', e)
  }

  // 获取发布中的问卷数量（状态为 1）
  try {
    const res = await request.get('/survey/list?status=1')
    // 根据返回的数据结构判断是数组还是分页对象
    if (Array.isArray(res)) {
      surveyCount.value = res.length || 0
    } else if (res?.data && Array.isArray(res.data)) {
      surveyCount.value = res.data.length || 0
    } else {
      surveyCount.value = null
    }
  } catch (e) {
    console.error('获取问卷数量失败:', e)
    // 静默失败，不影响页面展示
    surveyCount.value = null
  }
})
</script>
