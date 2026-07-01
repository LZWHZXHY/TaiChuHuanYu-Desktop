<template>
  <div class="discussion-board">
    <h4 class="board-title"><i class="fas fa-comments"></i> 自由讨论区</h4>
    
    <!-- 发表新讨论 -->
    <div class="new-post">
      <textarea 
        v-model="newContent" 
        placeholder="说点什么..." 
        rows="2"
        @keydown.ctrl.enter="submitPost"
      ></textarea>
      <div class="post-actions">
        <span class="char-count">{{ newContent.length }}/2000</span>
        <button 
          class="btn-post" 
          @click="submitPost" 
          :disabled="!newContent.trim() || newContent.length > 2000 || submitting"
        >
          <i class="fas fa-paper-plane"></i> 发布
        </button>
      </div>
    </div>

    <!-- 讨论列表 -->
    <div class="post-list">
      <div v-if="loading" class="empty-posts">
        <i class="fas fa-spinner fa-spin"></i>
        <p>加载中...</p>
      </div>
      <div v-else-if="posts.length === 0" class="empty-posts">
        <i class="fas fa-comment-slash"></i>
        <p>还没有讨论，快来发表第一条吧！</p>
      </div>
      <div 
        v-for="post in sortedPosts" 
        :key="post.id" 
        class="post-item"
      >
        <div class="post-avatar">
          <span>{{ post.author.charAt(0).toUpperCase() }}</span>
        </div>
        <div class="post-content">
          <div class="post-header">
            <span class="post-author">{{ post.author }}</span>
            <span class="post-time">{{ formatTime(post.createdAt) }}</span>
          </div>
          <div class="post-body">{{ post.content }}</div>
          <div class="post-actions-bar">
            <button @click="toggleReply(post.id)" class="action-reply">
              <i class="fas fa-reply"></i> 回复
            </button>
            <span class="reply-count">{{ post.replyCount || post.replies?.length || 0 }} 条回复</span>
          </div>
          <!-- 回复列表 -->
          <div v-if="showReplies[post.id]" class="replies">
            <div 
              v-for="reply in (post.replies || [])" 
              :key="reply.id" 
              class="reply-item"
            >
              <span class="reply-author">{{ reply.author }}</span>
              <span class="reply-time">{{ formatTime(reply.createdAt) }}</span>
              <p class="reply-content">{{ reply.content }}</p>
            </div>
            <div class="reply-input" v-if="showReplyInput[post.id]">
              <input 
                v-model="replyContent[post.id]" 
                placeholder="写下回复..."
                @keydown.enter="submitReply(post.id)"
              />
              <button @click="submitReply(post.id)" class="btn-reply-submit">
                回复
              </button>
            </div>
            <button 
              v-if="!showReplyInput[post.id]" 
              @click="showReplyInput[post.id] = true" 
              class="btn-show-reply-input"
            >
              写回复
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue';
import request from '@/utils/request';

const props = defineProps<{
  activityId: number;
}>();

// ---- 状态 ----
const posts = ref<any[]>([]);
const loading = ref(false);
const submitting = ref(false);
const newContent = ref('');
const maxLength = 2000;
const showReplies = ref<Record<number, boolean>>({});
const showReplyInput = ref<Record<number, boolean>>({});
const replyContent = ref<Record<number, string>>({});

// ---- 加载帖子 ----
const loadPosts = async () => {
  if (!props.activityId) return;
  loading.value = true;
  try {
    const data = await request.get(`/activities/${props.activityId}/posts`);
    posts.value = data || [];
  } catch (error) {
    console.error('加载讨论失败:', error);
  } finally {
    loading.value = false;
  }
};

// ---- 发表帖子 ----
const submitPost = async () => {
  const content = newContent.value.trim();
  if (!content || content.length > maxLength || submitting.value) return;
  submitting.value = true;
  try {
    const newPost = await request.post(`/activities/${props.activityId}/posts`, { content });
    posts.value.unshift(newPost);
    newContent.value = '';
  } catch (error) {
    console.error('发表失败:', error);
  } finally {
    submitting.value = false;
  }
};

// ---- 提交回复 ----
const submitReply = async (postId: number) => {
  const content = replyContent.value[postId]?.trim();
  if (!content) return;
  try {
    const newReply = await request.post(
      `/activities/${props.activityId}/posts/${postId}/replies`,
      { content }
    );
    // 更新本地
    const post = posts.value.find(p => p.id === postId);
    if (post) {
      if (!post.replies) post.replies = [];
      post.replies.push(newReply);
      post.replyCount = (post.replyCount || 0) + 1;
    }
    replyContent.value[postId] = '';
    showReplies.value[postId] = true;
    showReplyInput.value[postId] = false;
  } catch (error) {
    console.error('回复失败:', error);
  }
};

// ---- 切换回复 ----
const toggleReply = (postId: number) => {
  showReplies.value[postId] = !showReplies.value[postId];
};

// ---- 排序 ----
const sortedPosts = computed(() => {
  return [...posts.value].sort((a, b) => 
    new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
  );
});

// ---- 格式化时间 ----
const formatTime = (timestamp: string | number) => {
  const time = typeof timestamp === 'string' ? new Date(timestamp).getTime() : timestamp;
  const diff = Date.now() - time;
  const minutes = Math.floor(diff / 60000);
  if (minutes < 1) return '刚刚';
  if (minutes < 60) return `${minutes}分钟前`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}小时前`;
  const days = Math.floor(hours / 24);
  return `${days}天前`;
};

// ---- 监听活动ID变化 ----
watch(() => props.activityId, (newId) => {
  if (newId) loadPosts();
}, { immediate: true });
</script>

<style scoped>
.discussion-board {
  margin-top: 32px;
  border-top: 1px solid #eee;
  padding-top: 24px;
}
.board-title {
  font-size: 1rem;
  font-weight: 600;
  color: #1f2937;
  margin-bottom: 16px;
  display: flex;
  align-items: center;
  gap: 8px;
}
.board-title i { color: #6366f1; }

.new-post {
  background: #f9fafb;
  border-radius: 10px;
  padding: 16px;
  border: 1px solid #e5e7eb;
  margin-bottom: 20px;
}
.new-post textarea {
  width: 100%;
  border: none;
  background: transparent;
  padding: 0;
  font-size: 0.9rem;
  resize: vertical;
  font-family: inherit;
  outline: none;
  color: #1f2937;
}
.new-post textarea::placeholder { color: #9ca3af; }
.post-actions {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 8px;
}
.char-count {
  font-size: 0.7rem;
  color: #9ca3af;
}
.btn-post {
  background: #6366f1;
  color: #fff;
  border: none;
  padding: 6px 16px;
  border-radius: 20px;
  font-weight: 500;
  font-size: 0.8rem;
  cursor: pointer;
  transition: background 0.2s;
  display: inline-flex;
  align-items: center;
  gap: 4px;
}
.btn-post:hover:not(:disabled) { background: #4f46e5; }
.btn-post:disabled { opacity: 0.5; cursor: not-allowed; }

.post-list { display: flex; flex-direction: column; gap: 16px; }
.empty-posts {
  text-align: center;
  padding: 30px 0;
  color: #9ca3af;
}
.empty-posts i { font-size: 2rem; display: block; margin-bottom: 8px; }

.post-item {
  display: flex;
  gap: 12px;
  padding: 12px 0;
  border-bottom: 1px solid #f3f4f6;
}
.post-item:last-child { border-bottom: none; }
.post-avatar {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background: #6366f1;
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 600;
  font-size: 0.75rem;
  flex-shrink: 0;
}
.post-content { flex: 1; }
.post-header {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
  margin-bottom: 4px;
}
.post-author {
  font-weight: 600;
  font-size: 0.85rem;
  color: #1f2937;
}
.post-time {
  font-size: 0.7rem;
  color: #9ca3af;
}
.post-body {
  font-size: 0.9rem;
  line-height: 1.5;
  color: #374151;
  white-space: pre-wrap;
  word-break: break-word;
}

.post-actions-bar {
  display: flex;
  gap: 16px;
  margin-top: 6px;
  align-items: center;
}
.action-reply {
  background: none;
  border: none;
  color: #6b7280;
  font-size: 0.75rem;
  cursor: pointer;
  transition: color 0.2s;
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 2px 6px;
  border-radius: 4px;
}
.action-reply:hover { color: #6366f1; background: #f3f4f6; }
.reply-count { font-size: 0.7rem; color: #9ca3af; }

.replies {
  margin-top: 10px;
  padding-left: 12px;
  border-left: 2px solid #e5e7eb;
}
.reply-item {
  margin-bottom: 8px;
  padding: 6px 0;
}
.reply-author {
  font-weight: 500;
  font-size: 0.8rem;
  color: #1f2937;
  margin-right: 8px;
}
.reply-time {
  font-size: 0.65rem;
  color: #9ca3af;
}
.reply-content {
  font-size: 0.85rem;
  color: #374151;
  margin: 2px 0 0;
}
.reply-input {
  display: flex;
  gap: 8px;
  margin-top: 6px;
}
.reply-input input {
  flex: 1;
  padding: 6px 12px;
  border: 1px solid #e5e7eb;
  border-radius: 20px;
  font-size: 0.8rem;
  outline: none;
  background: #fafafa;
}
.reply-input input:focus { border-color: #6366f1; background: #fff; }
.btn-reply-submit {
  background: #6366f1;
  color: #fff;
  border: none;
  padding: 4px 14px;
  border-radius: 20px;
  font-size: 0.75rem;
  cursor: pointer;
  transition: background 0.2s;
}
.btn-reply-submit:hover { background: #4f46e5; }
.btn-show-reply-input {
  background: none;
  border: none;
  color: #6366f1;
  font-size: 0.75rem;
  cursor: pointer;
  padding: 2px 0;
}
.btn-show-reply-input:hover { text-decoration: underline; }
</style>