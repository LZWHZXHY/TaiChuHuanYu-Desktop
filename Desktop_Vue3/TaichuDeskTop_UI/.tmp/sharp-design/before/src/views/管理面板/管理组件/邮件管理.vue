<template>
  <div class="email-manage-panel">
    <div class="panel-header">
      <div class="header-title">
        <h2>邮件推送枢纽</h2>
        <span class="subtitle">向太初道友传达社区的最新动向与诚挚关怀</span>
      </div>
      <div class="header-tabs">
        <button 
          class="tab-btn" 
          :class="{ active: activeTab === 'compose' }"
          @click="activeTab = 'compose'"
        >
          信件与模板配置
        </button>
        <button 
          class="tab-btn" 
          :class="{ active: activeTab === 'history' }"
          @click="activeTab = 'history'"
        >
          投递志
        </button>
      </div>
    </div>

    <div class="panel-content">
      <!-- ===== 标签页 1：新建推送 / 模板配置 ===== -->
      <div v-if="activeTab === 'compose'" class="compose-section fade-in">
        
        <div class="form-group">
          <label class="section-label">选择信件类型</label>
          <div class="target-cards">
            <!-- ⚡ 即时任务 -->
            <div 
              class="target-card" 
              :class="{ active: form.type === 'update' }"
              @click="form.type = 'update'"
            >
              <div class="card-icon">📜</div>
              <div class="card-text">
                <h4>社区内容更新 <span class="badge manual">即时发送</span></h4>
                <p>向订阅“系统更新”的道友发送版本迭代与功能介绍。</p>
              </div>
              <div class="check-circle"></div>
            </div>

            <div 
              class="target-card" 
              :class="{ active: form.type === 'activity' }"
              @click="form.type = 'activity'"
            >
              <div class="card-icon">🏮</div>
              <div class="card-text">
                <h4>社区活动邀约 <span class="badge manual">即时发送</span></h4>
                <p>向订阅“活动资讯”的道友发送征稿、赛事等社区盛事。</p>
              </div>
              <div class="check-circle"></div>
            </div>

            <!-- 🤖 自动化模板 -->
            <div 
              class="target-card" 
              :class="{ active: form.type === 'recall' }"
              @click="form.type = 'recall'"
            >
              <div class="card-icon">🪶</div>
              <div class="card-text">
                <h4>鸿雁传书 (召回) <span class="badge auto">系统自动</span></h4>
                <p>配置模板后，系统自动向久未归属的道友发送专属问候。</p>
              </div>
              <div class="check-circle"></div>
            </div>

            <div 
              class="target-card" 
              :class="{ active: form.type === 'festival' }"
              @click="form.type = 'festival'"
            >
              <div class="card-icon">🎇</div>
              <div class="card-text">
                <h4>节庆与生辰 <span class="badge auto">系统自动</span></h4>
                <p>配置模板后，系统将在设定的佳节或道友生辰当日自动投递。</p>
              </div>
              <div class="check-circle"></div>
            </div>
          </div>
        </div>

        <div class="editor-area">
          <!-- 💡 自动化模式提示语 -->
          <transition name="slide-fade">
            <div v-if="isAutomatedMode" class="automation-alert">
              <span class="icon">⚙️</span>
              <div class="text">
                <strong>自动化模板编辑模式</strong>
                <p>您目前正在编辑后台自动化模板。保存后不会立即发送，系统会在后台每日轮询，当道友满足您设定的条件时，自动以此模板发送邮件。</p>
              </div>
            </div>
          </transition>

          <!-- 动态筛选条件区 -->
          <transition name="slide-fade">
            <div v-if="isAutomatedMode" class="dynamic-filters">
              
              <!-- 召回规则配置 -->
              <div v-if="form.type === 'recall'" class="form-group">
                <label class="section-label">触发条件：未登录天数</label>
                <select v-model="form.recallDays" class="ink-input select-input">
                  <option value="7">触发规则 A：游历在外（未登录达 7 天）</option>
                  <option value="30">触发规则 B：闭关潜修（未登录达 30 天）</option>
                  <option value="90">触发规则 C：杳无音信（未登录达 90 天）</option>
                </select>
              </div>

              <!-- 节日与生辰配置 -->
              <div v-if="form.type === 'festival'" class="form-group">
                <label class="section-label">触发情境与日期</label>
                <div class="festival-config">
                  <select v-model="form.festivalType" class="ink-input select-input flex-1">
                    <option value="birthday">🍰 每日寿星匹配 (根据道友档案自动触发)</option>
                    <option value="holiday">🎑 节庆专属贺信 (需设定具体触发日期)</option>
                  </select>
                  
                  <input 
                    v-if="form.festivalType === 'holiday'"
                    v-model="form.holidayDate"
                    type="text" 
                    class="ink-input date-input" 
                    placeholder="MM-DD (如 10-01)" 
                  />
                </div>
              </div>
            </div>
          </transition>

          <div class="form-group">
            <label class="section-label">信件主题 <span v-if="isAutomatedMode" class="hint">可以包含变量，如：{Username} 祝您生辰快乐！</span></label>
            <input 
              v-model="form.subject" 
              type="text" 
              class="ink-input title-input" 
              :placeholder="isAutomatedMode ? '例如：{Username}，太初世界许久未见你的身影' : '例如：太初系统 V2.1 更新公告'" 
            />
          </div>

          <div class="form-group">
            <label class="section-label">
              信件正文 
              <span class="hint">支持 HTML 标签，自动化模板中可用变量：{Username}, {Days}</span>
            </label>
            <textarea 
              v-model="form.content" 
              class="ink-input textarea" 
              rows="14" 
              placeholder="落笔于此..."
            ></textarea>
          </div>
        </div>

        <div class="actions">
          <!-- 更新的测试按钮 -->
          <button class="btn-outline" @click="handleTestSend" :disabled="isSaving || isTesting">
            <span v-if="isTesting" class="spinner dark"></span>
            {{ isTesting ? '发送中...' : '发送测试预览 (发给自己)' }}
          </button>
          
          <!-- 按钮文案根据模式动态变化 -->
          <button class="btn-primary" @click="handleSubmit" :disabled="isSaving || isTesting">
            <span v-if="isSaving" class="spinner"></span>
            {{ isAutomatedMode ? (isSaving ? '模板保存中...' : '保存自动化模板') : (isSaving ? '列队发送中...' : '确认执行群发') }}
          </button>
        </div>
      </div>

      <!-- ===== 标签页 2：发送记录 ===== -->
      <div v-if="activeTab === 'history'" class="history-section fade-in">
        <table class="history-table">
          <thead>
            <tr>
              <th>触发时间</th>
              <th>信件主题</th>
              <th>任务类型</th>
              <th>触达人数</th>
              <th>状态</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="record in mockHistory" :key="record.id">
              <td class="time">{{ record.time }}</td>
              <td class="subject">{{ record.subject }}</td>
              <td>
                <span class="tag" :class="record.type">{{ record.typeLabel }}</span>
              </td>
              <td>{{ record.count }} 人</td>
              <td>
                <span class="status success">已完成</span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import request from '@/utils/request';

const activeTab = ref('compose');
const isSaving = ref(false);
const isTesting = ref(false); // 新增测试状态变量

const form = ref({
  type: 'update', // 'update' | 'activity' | 'recall' | 'festival'
  recallDays: '7',
  festivalType: 'birthday',
  holidayDate: '', // 格式 MM-DD
  subject: '',
  content: ''
});

const mockHistory = ref<any[]>([]); // 初始化为空数组，用于接收后端返回的历史记录

// 计算属性：当前是否为自动化模板编辑模式
const isAutomatedMode = computed(() => {
  return ['recall', 'festival'].includes(form.value.type);
});

// 获取投递志列表
const fetchHistory = async () => {
  try {
    const res: any = await request.get('/Admin/Email/History');
    mockHistory.value = res || [];
  } catch (error) {
    console.error("获取记录失败:", error);
  }
};

onMounted(() => {
  fetchHistory();
});

// 替换后的真实测试发送逻辑
const handleTestSend = async () => {
  if (!form.value.subject || !form.value.content) {
    alert('请先填写主题和正文！');
    return;
  }

  isTesting.value = true;
  try {
    const res: any = await request.post('/Admin/Email/TestPush', {
      subject: form.value.subject,
      content: form.value.content
    });
    
    alert(res.message || '【测试预览】已成功投递！请前往您的邮箱查收。');
  } catch (error: any) {
    console.error(error);
    alert(error.friendlyMessage || error.response?.data?.message || '测试发送失败');
  } finally {
    isTesting.value = false;
  }
};

const handleSubmit = async () => {
  if (!form.value.subject || !form.value.content) {
    alert('请先填写主题和正文！');
    return;
  }
  
  if (isAutomatedMode.value) {
    alert('自动化模板功能后端尚未开启，当前仅支持即时群发。');
    return;
  } 
  
  const targetDesc = form.value.type === 'update' ? '社区内容更新' : '社区活动邀约';
  if (!confirm(`安全确认：即将向订阅了【${targetDesc}】的用户群发邮件，是否继续？`)) return;

  isSaving.value = true;
  try {
    const res: any = await request.post('/Admin/Email/Push', {
      type: form.value.type,
      subject: form.value.subject,
      content: form.value.content
    });
    
    alert(res.message || '邮件群发任务已提交！');
    
    // 发送成功后清空表单
    form.value.subject = '';
    form.value.content = '';
    
    // 重新获取最新记录并跳转到投递志面板
    await fetchHistory();
    activeTab.value = 'history';
  } catch (error: any) {
    console.error(error);
    alert(error.friendlyMessage || error.response?.data?.message || '操作失败');
  } finally {
    isSaving.value = false;
  }
};
</script>

<style scoped>
.email-manage-panel {
  background: var(--paper-card);
  border-radius: var(--radius-sm);
  padding: 32px;
  border: 1px solid var(--border-line);
  box-shadow: 0 4px 20px rgba(0,0,0,0.03);
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  margin-bottom: 32px;
  padding-bottom: 20px;
  border-bottom: 1px solid var(--border-line);
}

.header-title h2 {
  font-size: 1.4rem;
  font-weight: 600;
  color: var(--text-primary);
  margin: 0 0 6px 0;
}

.header-title .subtitle {
  font-size: 0.85rem;
  color: var(--text-secondary);
}

.header-tabs {
  display: flex;
  gap: 12px;
  background: var(--bg-main);
  padding: 4px;
  border-radius: var(--radius-sm);
}

.tab-btn {
  background: transparent;
  border: none;
  padding: 8px 20px;
  font-size: 0.9rem;
  color: var(--text-secondary);
  cursor: pointer;
  border-radius: var(--radius-sm);
  transition: all 0.3s;
}

.tab-btn.active {
  background: var(--paper-card);
  color: var(--text-primary);
  font-weight: 600;
  box-shadow: 0 2px 8px rgba(0,0,0,0.05);
}

/* 投递目标卡片 */
.target-cards {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 16px;
  margin-bottom: 32px;
}

.target-card {
  display: flex;
  align-items: flex-start;
  gap: 16px;
  padding: 20px;
  border: 1px solid var(--border-line);
  border-radius: var(--radius-sm);
  cursor: pointer;
  background: var(--paper-card);
  transition: all 0.3s ease;
  position: relative;
}

.target-card:hover {
  border-color: var(--border-line);
  background: var(--paper-card);
}

.target-card.active {
  border-color: var(--border-line);
  background: var(--paper-card);
  box-shadow: 0 4px 12px rgba(0,0,0,0.05);
}

.card-icon {
  font-size: 1.8rem;
  line-height: 1;
}

.card-text h4 {
  margin: 0 0 6px 0;
  font-size: 1rem;
  color: var(--text-primary);
  display: flex;
  align-items: center;
  gap: 8px;
}

.badge {
  font-size: 0.7rem;
  padding: 2px 6px;
  border-radius: 4px;
  font-weight: normal;
}
.badge.manual { background: var(--info-soft); color: var(--info); }
.badge.auto { background: var(--warning-soft); color: var(--warning); }

.card-text p {
  margin: 0;
  font-size: 0.8rem;
  color: var(--text-secondary);
  line-height: 1.5;
}

.check-circle {
  position: absolute;
  top: 20px;
  right: 20px;
  width: 18px;
  height: 18px;
  border: 2px solid var(--border-line);
  border-radius: 50%;
  transition: all 0.3s;
}

.target-card.active .check-circle {
  border-color: var(--border-line);
  background: var(--text-primary);
}

.target-card.active .check-circle::after {
  content: '';
  position: absolute;
  top: 4px;
  left: 4px;
  width: 6px;
  height: 6px;
  background: var(--paper-card);
  border-radius: 50%;
}

/* 自动化模式提示 */
.automation-alert {
  display: flex;
  gap: 12px;
  background: var(--bg-main);
  border: 1px solid var(--warning-soft);
  padding: 16px 20px;
  border-radius: var(--radius-sm);
  margin-bottom: 24px;
  align-items: flex-start;
}
.automation-alert .icon { font-size: 1.2rem; }
.automation-alert .text strong { color: #92400e; font-size: 0.95rem; display: block; margin-bottom: 4px; }
.automation-alert .text p { margin: 0; color: #b45309; font-size: 0.85rem; line-height: 1.5; }

/* 动态筛选区动画 */
.dynamic-filters {
  background: var(--paper-card);
  border-left: 4px solid var(--border-line);
  padding: 16px 20px 1px 20px;
  border-radius: var(--radius-sm);
  margin-bottom: 24px;
}

.festival-config {
  display: flex;
  gap: 12px;
}
.flex-1 { flex: 1; }
.date-input { width: 150px; text-align: center; }

.slide-fade-enter-active, .slide-fade-leave-active {
  transition: all 0.3s ease;
}
.slide-fade-enter-from, .slide-fade-leave-to {
  opacity: 0;
  transform: translateY(-10px);
}

/* 编辑器区域 */
.editor-area {
  background: var(--paper-card);
  padding: 24px;
  border: 1px solid var(--border-line);
  border-radius: var(--radius-sm);
  margin-bottom: 24px;
}

.section-label {
  display: block;
  font-size: 0.95rem;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 12px;
}

.hint {
  font-weight: normal;
  color: var(--text-secondary);
  font-size: 0.8rem;
  margin-left: 8px;
}

.form-group {
  margin-bottom: 24px;
}
.form-group:last-child {
  margin-bottom: 0;
}

.ink-input {
  width: 100%;
  padding: 14px 16px;
  border: 1px solid var(--border-line);
  border-radius: var(--radius-sm);
  font-size: 0.95rem;
  transition: all 0.2s ease;
  font-family: inherit;
  background: var(--bg-main);
}

.select-input {
  cursor: pointer;
}

.ink-input.title-input {
  font-size: 1.05rem;
  font-weight: 500;
}

.ink-input:focus {
  border-color: var(--border-line);
  background: var(--paper-card);
  outline: none;
  box-shadow: 0 0 0 3px rgba(17, 17, 17, 0.05);
}

.textarea {
  resize: vertical;
  line-height: 1.6;
}

/* 按钮组 */
.actions {
  display: flex;
  justify-content: flex-end;
  gap: 16px;
}

.btn-outline, .btn-primary {
  padding: 12px 28px;
  border-radius: var(--radius-sm);
  cursor: pointer;
  font-size: 0.95rem;
  font-weight: 600;
  transition: all 0.3s;
  display: flex;
  align-items: center;
  gap: 8px;
}

.btn-outline {
  background: transparent;
  color: var(--text-primary);
  border: 1px solid var(--border-line);
}

.btn-outline:hover:not(:disabled) {
  border-color: var(--border-line);
  background: var(--paper-card);
}

.btn-primary {
  background: var(--accent-color);
  color: var(--on-accent);
  border: 1px solid var(--border-line);
}

.btn-primary:hover:not(:disabled) {
  background: var(--text-primary);
}

.btn-primary:disabled, .btn-outline:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

/* 历史记录表格 */
.history-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.9rem;
}

.history-table th {
  text-align: left;
  padding: 16px;
  border-bottom: 1px solid var(--border-line);
  color: var(--text-secondary);
  font-weight: 500;
}

.history-table td {
  padding: 16px;
  border-bottom: 1px dashed var(--border-line);
  color: var(--text-primary);
  vertical-align: middle;
}

.history-table .time {
  color: var(--text-secondary);
  font-family: monospace;
}

.history-table .subject {
  font-weight: 600;
  color: var(--text-primary);
}

/* 标签样式 */
.tag {
  padding: 4px 12px;
  border-radius: var(--radius-sm);
  font-size: 0.8rem;
  font-weight: 500;
}
.tag.update { background: var(--info-soft); color: var(--info); }
.tag.activity { background: var(--warning-soft); color: var(--warning); }
.tag.recall { background: var(--bg-sub); color: #7e22ce; }
.tag.festival { background: var(--bg-sub); color: #be123c; }

.status.success {
  color: #15803d;
  font-weight: 500;
  display: flex;
  align-items: center;
  gap: 6px;
}
.status.success::before {
  content: '';
  display: inline-block;
  width: 6px;
  height: 6px;
  background: var(--success);
  border-radius: 50%;
}

.fade-in {
  animation: fadeIn 0.4s ease;
}

@keyframes fadeIn {
  from { opacity: 0; transform: translateY(10px); }
  to { opacity: 1; transform: translateY(0); }
}

.spinner {
  width: 16px;
  height: 16px;
  border: 2px solid rgba(255,255,255,0.3);
  border-top-color: var(--border-line);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

/* 深色 spinner 样式 */
.spinner.dark {
  border: 2px solid rgba(17, 17, 17, 0.1);
  border-top-color: var(--border-line);
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

@media (max-width: 1024px) {
  .target-cards {
    grid-template-columns: 1fr;
  }
}
</style>