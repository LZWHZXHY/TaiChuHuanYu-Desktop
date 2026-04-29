<template>
  <div class="interact-actions" :class="{ 'is-vertical': vertical }">
    <button 
      class="action-item like" 
      :class="{ 'active': localStatus.isLiked }"
      @click.stop="handleAction('Like')"
    >
      <span class="icon">{{ localStatus.isLiked ? '❤️' : '🤍' }}</span>
      <span class="count" v-if="showCount">{{ localStatus.likesCount }}</span>
    </button>

    <button 
      class="action-item favorite" 
      :class="{ 'active': localStatus.isFavorited }"
      @click.stop="handleAction('Favorite')"
    >
      <span class="icon">{{ localStatus.isFavorited ? '⭐' : '☆' }}</span>
    </button>

    <button class="action-item report" @click.stop="handleReport">
      <span class="icon">🚩</span>
    </button>
  </div>
</template>

<script setup lang="ts">
import { reactive, watch, ref } from 'vue';
import { interactApi, type InteractionResponse } from '../api/interact'; // 确保导入了接口类型

const props = defineProps<{
  targetId: number | string;
  targetType: 'Artwork' | 'Post' | 'Blog';
  initialStats: {
    likesCount: number;
    isLiked?: boolean;
    isFavorited?: boolean;
  };
  showCount?: boolean;
  vertical?: boolean;
}>();

const loading = ref(false);

const localStatus = reactive({
  likesCount: props.initialStats.likesCount,
  isLiked: props.initialStats.isLiked || false,
  isFavorited: props.initialStats.isFavorited || false
});

// 监听初始值的变化（这很重要，因为列表滚动时组件会被复用）
watch(() => props.initialStats, (newVal) => {
  localStatus.likesCount = newVal.likesCount;
  localStatus.isLiked = newVal.isLiked ?? false;
  localStatus.isFavorited = newVal.isFavorited ?? false;
}, { deep: true });

const handleAction = async (actionType: 'Like' | 'Favorite') => {
  if (loading.value) return;
  
  loading.value = true;
  try {
    // 显式指定 res 的类型，防止 TS 报错
    const res: InteractionResponse = await interactApi.toggleAction(
      props.targetId, 
      props.targetType, 
      actionType
    );
    
    // 从 res 中解构出后端返回的字段
    const { isActive, newCount } = res;

    if (actionType === 'Like') {
      localStatus.isLiked = isActive;
      localStatus.likesCount = newCount;
    } else {
      localStatus.isFavorited = isActive;
      // 如果收藏以后也要显示数量，可以在这里赋值 newCount
    }
  } catch (err: any) {
    if (err.response?.status === 401) {
      alert("道友请留步！点赞收藏需先登录账号。");
    } else {
      console.error(`${actionType} 交互失败:`, err);
    }
  } finally {
    loading.value = false;
  }
};

const handleReport = () => {
  console.log('触发针对', props.targetType, 'ID:', props.targetId, '的举报流程');
};
</script>

<style scoped>
.interact-actions {
  display: flex;
  gap: 12px;
  align-items: center;
}
.is-vertical { flex-direction: column; }
.action-item {
  background: none;
  border: none;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 4px;
  transition: all 0.2s;
}
.action-item.active { transform: scale(1.1); }
.like.active { color: #ff3b30; }
.favorite.active { color: #ffcc00; }
</style>