<script setup lang="ts">
import { ref, watch } from 'vue';

const props = defineProps<{
  period: string;
}>();

// 模拟前三名数据
const topWorks = ref([
  { id: 1, title: '太初幻境', author: '长风大士', likes: 1240, cover: 'https://picsum.photos/800/600?r=1' },
  { id: 2, title: '灵脉核心', author: '青衫客', likes: 980, cover: 'https://picsum.photos/800/600?r=2' },
  { id: 3, title: '混沌之初', author: '云游子', likes: 850, cover: 'https://picsum.photos/800/600?r=3' }
]);

// 监听时间维度变化，触发数据重载
watch(() => props.period, (newVal) => {
  console.log(`正在请求${newVal}榜单数据...`);
  // 这里写 API 请求逻辑
});
</script>

<template>
  <div class="podium">
    <div 
      v-for="(work, index) in topWorks" 
      :key="work.id" 
      :class="['podium-item', `rank-${index + 1}`]"
    >
      <div class="crown" v-if="index === 0">👑</div>
      <div class="artwork-card">
        <div class="rank-tag">#{{ index + 1 }}</div>
        <img :src="work.cover" class="cover" />
        <div class="info">
          <h3>{{ work.title }}</h3>
          <p>@{{ work.author }}</p>
          <div class="score">影响力: {{ work.likes }}</div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.podium {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 30px;
  align-items: end; /* 模拟领奖台高度差 */
  padding-top: 40px;
}

.podium-item {
  position: relative;
  transition: transform 0.3s ease;
}

/* 领奖台高度差逻辑 */
.rank-1 { order: 2; transform: translateY(-30px); } /* 第一名居中且最高 */
.rank-2 { order: 1; }
.rank-3 { order: 3; }

.podium-item:hover {
  transform: translateY(-40px);
}

.artwork-card {
  background: #fff;
  border: 1px solid #f0f0f0;
  border-radius: 16px;
  overflow: hidden;
  box-shadow: 0 10px 30px rgba(0,0,0,0.05);
}

.rank-tag {
  position: absolute;
  top: 10px;
  left: 10px;
  background: #24292f;
  color: #fff;
  padding: 4px 12px;
  border-radius: 20px;
  font-weight: 800;
  z-index: 10;
}

.cover {
  width: 100%;
  aspect-ratio: 4/5;
  object-fit: cover;
}

.info {
  padding: 20px;
  text-align: center;
}

.crown {
  position: absolute;
  top: -40px;
  left: 50%;
  transform: translateX(-50%);
  font-size: 2rem;
}

@media (max-width: 900px) {
  .podium { grid-template-columns: 1fr; align-items: stretch; }
  .rank-1 { order: 1; transform: none; }
  .rank-2 { order: 2; }
  .rank-3 { order: 3; }
}
</style>