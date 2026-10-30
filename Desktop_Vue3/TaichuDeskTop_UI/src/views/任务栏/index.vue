<script setup lang="ts">
import SectionTrace from '@/components/SectionTrace.vue'
import { computed, onMounted, ref } from 'vue'
import request from '@/utils/request'
import { useUserStore } from '@/stores/user'
interface Project { id: string; name: string }
interface Task {
  id: string
  title: string
  description?: string
  categoryName: string
  assigneeId?: string
  dueDate?: string
}
const user = useUserStore()
const projects = ref<Project[]>([])
const projectId = ref('')
const tasks = ref<Task[]>([])
const loading = ref(false)
const error = ref('')
const search = ref('')
const mine = ref(false)
let requestVersion = 0
const filtered = computed(() => {
  const keyword = search.value.trim().toLowerCase()
  const userId = user.userId?.toLowerCase()
  return tasks.value.filter(task =>
    (!mine.value || (userId && task.assigneeId?.toLowerCase() === userId)) &&
    `${task.title} ${task.description || ''}`.toLowerCase().includes(keyword),
  )
})
async function loadProjects() {
  loading.value = true
  error.value = ''
  try {
    projects.value = await request.get<Project[]>('/Project/my')
    projectId.value = projects.value.some(p => p.id === projectId.value) ? projectId.value : projects.value[0]?.id || ''
    await loadTasks()
  } catch { error.value = '项目读取失败，请重试。' }
  finally { loading.value = false }
}
async function loadTasks() {
  const version = ++requestVersion
  tasks.value = []
  error.value = ''
  if (!projectId.value) return
  loading.value = true
  try {
    const data = await request.get<Task[]>(`/project/${projectId.value}/tasks`)
    if (version === requestVersion) tasks.value = data
  } catch { if (version === requestVersion) error.value = '任务读取失败，请重试。' }
  finally { if (version === requestVersion) loading.value = false }
}
onMounted(loadProjects)
const samples = ref([{ title: '了解任务接取流程', status: 0 }, { title: '整理自己的创作计划', status: 1 }, { title: '回顾已完成的工作', status: 2 }])
</script>
<template>
  <article class="content-page task-workspace community-surface tone-collab">
    <header class="content-header"><SectionTrace />
      <div>
        <span class="eyebrow">协作与进度</span>
        <h1>任务栏</h1>
        <p>查看参与项目的任务，进入看板继续处理。</p>
      </div>
      <div class="content-actions">
        <RouterLink to="/Project" class="primary">进入协作大厅</RouterLink>
        <button @click="loadProjects" :disabled="loading">刷新</button>
      </div>
    </header>
    <section class="task-toolbar" aria-label="任务筛选">
      <label>所属项目<select v-model="projectId" @change="loadTasks" :disabled="loading || !projects.length">
          <option v-if="!projects.length" value="">暂无项目</option>
          <option v-for="project in projects" :key="project.id" :value="project.id">{{ project.name }}</option>
        </select>
      </label>
      <label>查找任务<input v-model="search" type="search" placeholder="搜索标题或描述" />
      </label>
      <label class="mine-filter">
        <input type="checkbox" v-model="mine" /> 只看分配给我的</label>
    </section>
    <div v-if="loading" class="task-state" role="status">正在读取任务…</div>
    <div v-else-if="error" class="task-state" role="alert">{{ error }} <button class="btn-line" @click="projects.length ? loadTasks() : loadProjects()">重试</button>
    </div>
    <template v-else>
      <div class="task-summary">
        <h2>{{ mine ? '分配给我的' : '项目任务' }}</h2>
        <span>{{ filtered.length }} 项</span>
      </div>
      <div class="task-grid">
        <RouterLink v-for="task in filtered" :key="task.id" :to="`/Project/project/${projectId}`" class="task-card">
          <span class="task-category">{{ task.categoryName || '未分类' }}</span>
          <h3>{{ task.title }}</h3>
          <p>{{ task.description || '暂无描述，进入项目查看任务详情。' }}</p>
          <footer>
            <span>{{ task.dueDate ? `截止 ${new Date(task.dueDate).toLocaleDateString()}` : '未设置截止日期' }}</span>
            <span>进入看板 →</span>
          </footer>
        </RouterLink>
      </div>
      <div v-if="!filtered.length" class="task-state">
        <h3>{{ projects.length ? '当前没有匹配的任务' : '还没有参与的项目' }}</h3>
        <p>{{ projects.length ? '调整筛选条件，或进入项目看板创建任务。' : '先创建或加入协作项目，任务就会出现在这里。' }}</p>
        <RouterLink :to="projectId ? `/Project/project/${projectId}` : '/Project'">{{ projectId ? '打开项目看板' : '浏览协作项目' }} →</RouterLink>
      </div>
    </template>
    <details class="task-demo">
      <summary>操作示例 <small>仅演示，不保存到项目</small>
      </summary>
      <div v-for="sample in samples" :key="sample.title" class="sample-row">
        <span>{{ sample.title }}</span>
        <button class="btn-line" v-if="sample.status < 2" @click="sample.status++">{{ sample.status === 0 ? '接取示例' : '提交示例' }}</button>
      <span v-else>已归档</span>
    </div>
  </details>
</article>
</template>
<style scoped>
.task-toolbar {
  display: flex;
  flex-wrap: wrap;
  align-items: end;
  gap: 16px;
  padding: 20px;
  background: var(--paper-card);
  border: 1px solid var(--border-line);
  border-radius: 0;
}
.task-toolbar label {
  display: grid;
  gap: 8px;
  flex: 1 1 220px;
  font-size: 13px;
  color: var(--text-secondary);
  min-width: 0;
}
.task-toolbar input, .task-toolbar select {
  width: 100%;
  padding: 10px 12px;
  border: 1px solid var(--border-line);
  border-radius: 0;
  background: var(--bg-main);
  color: var(--text-primary);
  min-width: 0;
}
.task-toolbar .mine-filter {
  display: flex;
  align-items: center;
  flex: 0 1 auto;
  padding: 10px 0;
}
.mine-filter input {
  width: auto;
  accent-color: var(--accent-color);
}
.task-summary {
  display: flex;
  align-items: center;
  gap: 12px;
  margin: 24px 0 16px;
}
.task-summary h2 {
  font-size: 20px;
  margin: 0;
}
.task-summary span, .task-card p, .task-card footer {
  color: var(--text-secondary);
}
.task-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill,minmax(min(100%,300px),1fr));
  gap: 16px;
}
.task-card {
  display: flex;
  flex-direction: column;
  padding: 24px;
  border: 1px solid var(--border-line);
  border-radius: 0;
  background: var(--paper-card);
  color: var(--text-primary);
  text-decoration: none;
  min-width: 0;
}
.task-card:hover {
  border-color: var(--accent-color);
}
.task-category {
  align-self: start;
  background: var(--accent-soft);
  color: var(--accent-color);
  padding: 3px 8px;
  border-radius: 0;
  font-size: 12px;
}
.task-card h3 {
  margin: 16px 0 8px;
  overflow-wrap: anywhere;
}
.task-card p {
  flex: 1;
  font-size: 14px;
  overflow-wrap: anywhere;
}
.task-card footer {
  display: flex;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 8px;
  border-top: 1px solid var(--border-line);
  padding-top: 16px;
  font-size: 12px;
}
.task-state {
  text-align: center;
  padding: 48px 20px;
  color: var(--text-secondary);
}
.task-state a {
  color: var(--accent-color);
}
.task-demo {
  border-top: 1px solid var(--border-line);
  padding-top: 20px;
  margin-top: 32px;
  color: var(--text-secondary);
}
.task-demo summary {
  cursor: pointer;
}
.task-demo small {
  margin-left: 12px;
}
.sample-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px;
  gap: 12px;
}
</style>
