<template>
  <div class="admin-feedback-container">
    <div class="header-actions">
      <h2 class="page-title">意见反馈管理</h2>
      <button class="refresh-btn" @click="fetchFeedbacks" :disabled="loading">
        {{ loading ? '刷新中...' : '刷新数据' }}
      </button>
    </div>

    <div class="table-wrapper">
      <table class="admin-table">
        <thead>
          <tr>
            <th width="120">反馈 ID</th>
            <th width="140">提交时间</th>
            <th width="180">用户信息 / 联系方式</th>
            <th>反馈内容</th>
            <th width="100">配图</th>
            <th width="100">状态</th>
            <th width="120">操作</th>
          </tr>
        </thead>
        
        <tbody>
          <tr v-if="loading && feedbackList.length === 0">
            <td colspan="7" class="empty-cell">数据加载中...</td>
          </tr>
          <tr v-else-if="feedbackList.length === 0">
            <td colspan="7" class="empty-cell">暂无反馈数据</td>
          </tr>
          
          <tr v-for="item in feedbackList" :key="item.id" class="table-row">
            <!-- ID -->
            <td class="mono-text" :title="item.id">#{{ item.id.substring(0, 8) }}</td>
            
            <!-- 时间 -->
            <td class="mono-text">{{ formatDate(item.createdAt) }}</td>
            
            <!-- 用户信息 (展示真实数据，并标记是否要求匿名) -->
            <td>
              <div class="user-info">
                <span v-if="item.isAnonymous" class="badge-anonymous" title="用户在前端选择了匿名">匿名提交</span>
                <div class="text-truncate" :title="item.userId || '未登录访客'">ID: {{ item.userId || '未登录访客' }}</div>
                <div class="text-truncate" :title="item.contactInfo || '未留联系方式'">联系: {{ item.contactInfo || '未留联系方式' }}</div>
              </div>
            </td>
            
            <!-- 内容 -->
            <td>
              <div class="content-text">{{ item.content }}</div>
            </td>
            
            <!-- 图片 -->
            <td>
              <div class="image-gallery" v-if="item.imageUrls">
                <a 
                  v-for="(img, idx) in item.imageUrls.split(',')" 
                  :key="idx" 
                  :href="img" 
                  target="_blank" 
                  title="点击查看原图"
                >
                  <img :src="img" alt="图" class="thumb-img" />
                </a>
              </div>
              <span v-else class="text-muted">-</span>
            </td>
            
            <!-- 状态 -->
            <td>
              <span :class="['status-badge', item.status === 1 ? 'status-resolved' : 'status-pending']">
                {{ item.status === 1 ? '已解决' : '待处理' }}
              </span>
            </td>
            
            <!-- 操作 -->
            <td>
              <div class="action-buttons">
                <button 
                  class="action-btn toggle-btn" 
                  @click="toggleStatus(item)"
                  :disabled="item.isUpdating"
                >
                  {{ item.status === 1 ? '标为待处理' : '标为已解决' }}
                </button>
                <button class="action-btn delete-btn" @click="handleDelete(item.id)">
                  删除
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { feedbackApi } from '@/api/Feedback'; // 确保路径正确

// 扩展类型，增加一个前端专用的 isUpdating 状态，防止重复点击
interface AdminFeedbackItem {
  id: string;
  content: string;
  contactInfo: string | null;
  userId: string | null;
  imageUrls: string | null;
  isAnonymous: boolean;
  status: number;
  createdAt: string;
  isUpdating?: boolean; // 前端控制 loading 状态
}

const feedbackList = ref<AdminFeedbackItem[]>([]);
const loading = ref(false);

// 加载所有数据 (调用没有脱敏的完整接口)
const fetchFeedbacks = async () => {
  loading.value = true;
  try {
    const data = await feedbackApi.getAllFeedbacks();
    // 注入 isUpdating 属性
    feedbackList.value = data.map(item => ({ ...item, isUpdating: false }));
  } catch (error) {
    console.error('获取反馈列表失败', error);
    alert('数据加载失败');
  } finally {
    loading.value = false;
  }
};

onMounted(fetchFeedbacks);

// 格式化时间
const formatDate = (dateString: string) => {
  if (!dateString) return '';
  const date = new Date(dateString);
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')} ${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`;
};

// 切换处理状态
const toggleStatus = async (item: AdminFeedbackItem) => {
  const newStatus = item.status === 1 ? 0 : 1;
  item.isUpdating = true;
  try {
    await feedbackApi.updateFeedbackStatus(item.id, newStatus);
    item.status = newStatus; // 乐观更新，UI 瞬间响应
  } catch (error) {
    alert('状态更新失败');
  } finally {
    item.isUpdating = false;
  }
};

// 删除反馈
const handleDelete = async (id: string) => {
  if (!window.confirm('确定要彻底删除这条反馈记录吗？此操作不可恢复。')) {
    return;
  }
  
  try {
    await feedbackApi.deleteFeedback(id);
    // 从列表中移除该项
    feedbackList.value = feedbackList.value.filter(item => item.id !== id);
  } catch (error) {
    alert('删除失败');
  }
};
</script>

<style scoped>
.admin-feedback-container {
  padding: 24px;
  background-color: #f8fafc; /* 极浅灰背景，适合后台大盘 */
  min-height: 100vh;
  font-family: system-ui, -apple-system, sans-serif;
}

.header-actions {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.page-title {
  margin: 0;
  font-size: 1.25rem;
  color: #1e293b;
  font-weight: 600;
}

.refresh-btn {
  background: #fff;
  border: 1px solid #e2e8f0;
  padding: 8px 16px;
  border-radius: 6px;
  color: #475569;
  cursor: pointer;
  font-size: 0.9rem;
  transition: all 0.2s;
  box-shadow: 0 1px 2px rgba(0,0,0,0.05);
}
.refresh-btn:hover:not(:disabled) {
  border-color: #cbd5e1;
  color: #1e293b;
}

/* 表格主体样式 */
.table-wrapper {
  background: #fff;
  border-radius: 8px;
  box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.05), 0 2px 4px -1px rgba(0, 0, 0, 0.03);
  overflow-x: auto;
}

.admin-table {
  width: 100%;
  border-collapse: collapse;
  text-align: left;
  font-size: 0.9rem;
}

.admin-table th {
  background-color: #f8fafc;
  color: #64748b;
  font-weight: 500;
  padding: 12px 16px;
  border-bottom: 1px solid #e2e8f0;
  white-space: nowrap;
}

.admin-table td {
  padding: 16px;
  border-bottom: 1px solid #f1f5f9;
  color: #334155;
  vertical-align: top;
}

.table-row:hover td {
  background-color: #f8fafc;
}

.empty-cell {
  text-align: center;
  padding: 40px !important;
  color: #94a3b8;
}

.mono-text {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
  font-size: 0.85rem;
  color: #64748b;
}

.text-truncate {
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 160px;
  font-size: 0.85rem;
  margin-top: 4px;
}
.text-muted { color: #cbd5e1; }

.user-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.badge-anonymous {
  align-self: flex-start;
  font-size: 0.7rem;
  background: #fef2f2;
  color: #ef4444;
  padding: 2px 6px;
  border-radius: 4px;
  border: 1px solid #fecaca;
  margin-bottom: 4px;
}

.content-text {
  white-space: pre-wrap; /* 保持换行 */
  line-height: 1.5;
  max-width: 400px; /* 限制最大宽度防止撑破表格 */
}

/* 图片缩略图 */
.image-gallery {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}
.thumb-img {
  width: 36px;
  height: 36px;
  object-fit: cover;
  border-radius: 4px;
  border: 1px solid #e2e8f0;
  cursor: zoom-in;
}

/* 状态标签 */
.status-badge {
  display: inline-block;
  padding: 4px 8px;
  border-radius: 999px;
  font-size: 0.8rem;
  font-weight: 500;
}
.status-pending {
  background-color: #f1f5f9;
  color: #64748b;
}
.status-resolved {
  background-color: #dcfce7;
  color: #16a34a;
}

/* 操作按钮 */
.action-buttons {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.action-btn {
  background: none;
  border: none;
  padding: 0;
  font-size: 0.85rem;
  cursor: pointer;
  text-align: left;
  transition: color 0.2s;
}
.toggle-btn { color: #3b82f6; }
.toggle-btn:hover { color: #2563eb; }
.toggle-btn:disabled { color: #94a3b8; cursor: not-allowed; }

.delete-btn { color: #ef4444; }
.delete-btn:hover { color: #dc2626; }
</style>