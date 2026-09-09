<template>
  <div class="workdesk-container fade-in">
    <!-- 顶部状态栏 -->
    <div class="workdesk-sub-header">
      <div class="sub-tabs">
        <button :class="{ active: subTab === 'kanban' }" @click="subTab = 'kanban'">◈ 进度看板</button>
        <button :class="{ active: subTab === 'logs' }" @click="subTab = 'logs'">◒ 研发工作日志 ({{ workLogs.length }})</button>
      </div>
      <div class="action-buttons">
        <button class="btn-primary" @click="showLogModal = true">+ 登记今日干活</button>
      </div>
    </div>

    <!-- 1. 任务看板 (Kanban) -->
    <div v-if="subTab === 'kanban'" class="kanban-grid">
      <div v-for="col in columns" :key="col.key" class="kanban-col">
        <div class="col-title-bar">
          <span>{{ col.title }}</span>
          <span class="count">{{ getTasks(col.key).length }}</span>
        </div>
        <div class="task-cards">
          <div v-for="task in getTasks(col.key)" :key="task.id" class="task-card">
            <div class="task-meta">
              <span class="project-pill">{{ task.project }}</span>
              <span class="priority" :class="task.priority">{{ task.priorityText }}</span>
            </div>
            <h4 class="task-title">{{ task.title }}</h4>
            <p class="task-desc">{{ task.desc }}</p>
            <div class="task-bottom">
              <span class="assignee">{{ task.assignee }}</span>
              <span class="due">{{ task.due }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- 2. 工作日志流水 -->
    <div v-if="subTab === 'logs'" class="logs-stream">
      <div v-for="log in workLogs" :key="log.id" class="log-card">
        <div class="log-card-header">
          <div class="author-area">
            <span class="avatar-tag">{{ log.author.charAt(0) }}</span>
            <div>
              <strong>{{ log.author }}</strong>
              <span class="time">{{ log.time }} · {{ log.module }}</span>
            </div>
          </div>
          <span class="hours-tag">投入 {{ log.hours }}h</span>
        </div>
        <div class="log-card-body">{{ log.content }}</div>
        <div v-if="log.images.length" class="log-card-gallery">
          <img v-for="(img, idx) in log.images" :key="idx" :src="img" alt="截图" @click="openImg(img)" />
        </div>
      </div>
    </div>

    <!-- 登记工作日志弹窗 -->
    <div v-if="showLogModal" class="modal-mask" @click.self="showLogModal = false">
      <div class="modal-card">
        <div class="modal-title">
          <h3>登记干活记录</h3>
          <button class="close-btn" @click="showLogModal = false">×</button>
        </div>
        <div class="modal-form">
          <div class="form-row">
            <div class="form-group flex-1">
              <label>所属模块 / 系统</label>
              <input v-model="newLog.module" placeholder="例如: 灵脉空间 / 手册后端" />
            </div>
            <div class="form-group w-100">
              <label>耗时 (小时)</label>
              <input type="number" v-model.number="newLog.hours" />
            </div>
          </div>
          <div class="form-group">
            <label>今日进展 / 攻关内容</label>
            <textarea v-model="newLog.content" rows="4" placeholder="做了什么、解决了什么阻塞..."></textarea>
          </div>
          <div class="form-group">
            <label>现场存证 (支持 Ctrl+V 直传 COS)</label>
            <div class="paste-zone" @paste="handlePaste">
              <span v-if="!isUploading">光标置于此处直接 Ctrl+V 粘贴截图附件</span>
              <span v-else>正在直传腾讯云 COS...</span>
            </div>
            <div v-if="newLog.images.length" class="previews">
              <img v-for="(url, i) in newLog.images" :key="i" :src="url" alt="preview" />
            </div>
          </div>
        </div>
        <div class="modal-actions">
          <button class="btn-cancel" @click="showLogModal = false">取消</button>
          <button class="btn-submit" @click="submitLog">提交记录</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
// 注意：如果你的 useCos 不在这个路径，请修改成你实际的 composables/useCos 路径
import { useCos } from '../../composables/useCos'; 

// 定义任务与日志的类型接口
interface TaskItem {
  id: number;
  col: string;
  project: string;
  priority: string;
  priorityText: string;
  title: string;
  desc: string;
  assignee: string;
  due: string;
}

interface WorkLog {
  id: number;
  author: string;
  module: string;
  hours: number;
  content: string;
  images: string[];
  time: string;
}

const { uploadFile, isUploading } = useCos(); 
const subTab = ref<'kanban' | 'logs'>('kanban');

const columns = [
  { key: 'todo', title: '待开发 / 排期' },
  { key: 'doing', title: '正在干活' },
  { key: 'review', title: '验收联调' },
  { key: 'done', title: '已交付' }
];

const tasks = ref<TaskItem[]>([
  { id: 1, col: 'done', project: '官方基建', priority: 'high', priorityText: '核心', title: '用户手册 COS 链路闭环', desc: '完成 Markdown 编辑与腾讯云直传。', assignee: 'Junjie', due: '已上线' },
  { id: 2, col: 'doing', project: '太初寰宇', priority: 'high', priorityText: '进行中', title: '展厅展示与工作台拆分', desc: '重组业务架构，区分门面与协同。', assignee: 'Junjie', due: '09-10' },
  { id: 3, col: 'todo', project: '灵脉 2.0', priority: 'med', priorityText: '排期中', title: '卡片关系渲染优化', desc: '海量节点引力交互加速。', assignee: 'Junjie', due: '09-18' }
]);

const workLogs = ref<WorkLog[]>([
  { id: 1, author: 'Junjie', module: '用户手册基建', hours: 3.5, content: '解决前台 Users/ManualController 404 问题，打通分类树。', images: [], time: '今天 15:30' },
  { id: 2, author: '太初总司', module: '世界观卡片', hours: 2, content: '完成了三大门派引力关系编排，去除了两处死锁。', images: [], time: '昨天 18:20' }
]);

const getTasks = (colKey: string) => tasks.value.filter(t => t.col === colKey);

const showLogModal = ref(false);
const newLog = ref<{ module: string; hours: number; content: string; images: string[] }>({
  module: '',
  hours: 2,
  content: '',
  images: []
});

const handlePaste = async (e: ClipboardEvent) => {
  const items = e.clipboardData?.items;
  if (!items) return;
  for (const item of items) {
    if (item.type.includes('image')) {
      e.preventDefault();
      const file = item.getAsFile();
      if (!file) continue;
      const res = await uploadFile(file, 'worklog'); 
      newLog.value.images.push(res.url); 
      break;
    }
  }
};

const submitLog = () => {
  if (!newLog.value.content.trim()) return alert('请填写日志内容');
  workLogs.value.unshift({
    id: Date.now(),
    author: 'Junjie',
    module: newLog.value.module || '常规干活',
    hours: newLog.value.hours || 1,
    content: newLog.value.content,
    images: [...newLog.value.images],
    time: '刚刚'
  });
  newLog.value = { module: '', hours: 2, content: '', images: [] };
  showLogModal.value = false;
};

const openImg = (url: string) => window.open(url, '_blank');
</script>

<style scoped>
.workdesk-sub-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
.sub-tabs { display: flex; gap: 8px; }
.sub-tabs button { background: #f6f8fa; border: none; padding: 6px 14px; border-radius: 6px; font-weight: 600; cursor: pointer; color: #555; }
.sub-tabs button.active { background: #111; color: #fff; }
.btn-primary { background: #111; color: #fff; border: none; padding: 7px 16px; border-radius: 6px; font-weight: 600; cursor: pointer; font-size: 0.85rem; }

.kanban-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 16px; }
.kanban-col { background: #f6f8fa; border-radius: 8px; padding: 12px; min-height: 460px; }
.col-title-bar { display: flex; justify-content: space-between; font-size: 0.85rem; font-weight: 700; margin-bottom: 12px; color: #555; }
.col-title-bar .count { background: #e5e5e5; padding: 1px 6px; border-radius: 10px; font-size: 0.75rem; }
.task-cards { display: flex; flex-direction: column; gap: 10px; }
.task-card { background: #fff; border: 1px solid #e1e4e8; border-radius: 6px; padding: 12px; }
.task-meta { display: flex; justify-content: space-between; margin-bottom: 6px; font-size: 0.75rem; }
.priority.high { color: #cf222e; }
.priority.med { color: #9a6700; }
.task-title { font-size: 0.9rem; margin: 0 0 4px; }
.task-desc { font-size: 0.75rem; color: #666; margin: 0 0 10px; }
.task-bottom { display: flex; justify-content: space-between; font-size: 0.7rem; color: #888; border-top: 1px solid #f5f5f5; padding-top: 6px; }

.logs-stream { display: flex; flex-direction: column; gap: 14px; max-width: 800px; }
.log-card { background: #fff; border: 1px solid #e1e4e8; border-radius: 8px; padding: 16px; }
.log-card-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
.author-area { display: flex; gap: 8px; align-items: center; font-size: 0.85rem; }
.avatar-tag { width: 28px; height: 28px; border-radius: 50%; background: #111; color: #fff; display: flex; align-items: center; justify-content: center; font-size: 0.8rem; }
.time { font-size: 0.75rem; color: #888; display: block; }
.hours-tag { background: #dafbe1; color: #1a7f37; font-size: 0.75rem; padding: 2px 6px; border-radius: 4px; font-weight: 600; }
.log-card-body { font-size: 0.85rem; line-height: 1.5; color: #333; margin-bottom: 8px; }
.log-card-gallery { display: flex; gap: 8px; }
.log-card-gallery img { width: 90px; height: 60px; object-fit: cover; border-radius: 4px; cursor: pointer; }

.modal-mask { position: fixed; inset: 0; background: rgba(0,0,0,0.4); display: flex; align-items: center; justify-content: center; z-index: 1000; }
.modal-card { background: #fff; border-radius: 8px; padding: 20px; width: 480px; }
.modal-title { display: flex; justify-content: space-between; margin-bottom: 16px; }
.close-btn { background: none; border: none; font-size: 1.2rem; cursor: pointer; }
.form-row { display: flex; gap: 10px; }
.flex-1 { flex: 1; }
.w-100 { width: 100px; }
.form-group { margin-bottom: 12px; }
.form-group label { display: block; font-size: 0.8rem; font-weight: 600; margin-bottom: 4px; }
.form-group input, .form-group textarea { width: 100%; box-sizing: border-box; padding: 8px; border: 1px solid #ddd; border-radius: 4px; outline: none; }
.paste-zone { border: 1px dashed #ddd; border-radius: 4px; padding: 14px; text-align: center; color: #888; font-size: 0.8rem; }
.previews { display: flex; gap: 6px; margin-top: 6px; }
.previews img { width: 60px; height: 40px; object-fit: cover; border-radius: 3px; }
.modal-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 16px; }
.btn-cancel { background: #eee; border: none; padding: 6px 14px; border-radius: 4px; cursor: pointer; }
.btn-submit { background: #111; color: #fff; border: none; padding: 6px 14px; border-radius: 4px; cursor: pointer; }
.fade-in { animation: fadeIn 0.3s ease; }
@keyframes fadeIn { from { opacity: 0; transform: translateY(4px); } to { opacity: 1; transform: translateY(0); } }
</style>