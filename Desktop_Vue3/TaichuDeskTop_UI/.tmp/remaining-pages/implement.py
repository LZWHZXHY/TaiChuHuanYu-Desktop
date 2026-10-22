from pathlib import Path
import re

def edit(name, fn):
 p=Path('src')/name; s=p.read_text(encoding='utf-8-sig'); p.write_text(fn(s),encoding='utf-8')

# A single activity request client respects the same environment and session as the rest of the site.
def storage(s):
 s=s.replace("import { useUserStore } from '@/stores/user';", "import { useUserStore } from '@/stores/user';\nimport request from '@/utils/request';")
 s=re.sub(r"const API_BASE = .*?;\n",'',s)
 start=s.index('// 通用请求函数');end=s.index('// ------ 登录/注册',start)
 s=s[:start]+'''// 兼容现有调用参数；认证、错误提示和 API 地址统一交给共享客户端。
async function apiRequest<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
  const data = typeof options.body === 'string' ? JSON.parse(options.body) : options.body;
  switch (options.method || 'GET') {
    case 'POST': return request.post<T>(endpoint, data);
    case 'PUT': return request.put<T>(endpoint, data);
    case 'DELETE': return request.delete<T>(endpoint);
    default: return request.get<T>(endpoint);
  }
}

'''+s[end:]
 return s
edit('views/活动中心/打卡中心/storage.ts', storage)
edit('views/任务栏/index.vue',lambda s:s.replace('onMounted, ref, watch','onMounted, ref'))
edit('views/活动中心/打卡中心/MyActivities.vue',lambda s:s.replace('`/activity/detail/${id}`','`/activity/checkin/detail/${id}`').replace("$router.push('/activity')","$router.push('/activity/checkin')"))

def shell(s):
 start=s.index('      <div class="user-profile">');end=s.index('    </aside>',start)
 s=s[:start]+s[end:]
 s=s.replace('互动中心','活动中心').replace('exact-active-class="active"','exact-active-class="active"').replace(' active-class="active"',' exact-active-class="active"')
 s=re.sub(r'\s*<!-- ===== 预留扩展 ===== -->[\s\S]*?-->\s*</nav>', '\n      </nav>',s)
 s=s[:s.index('<style scoped>')]+'''<style src="./theme.css"></style>
'''
 return s
edit('views/活动中心/index.vue',shell)

def home(s):
 script=s[s.index('<script setup'):s.index('<style scoped>')]
 script=script.replace('ref(0)','ref<number | null>(null)').replace('surveyCount.value = 0','surveyCount.value = null')
 return '''<template>
  <div class="zone-home">
    <header class="zone-header"><span class="eyebrow">参与 · 记录 · 分享</span><h1>让每一次参与都有回响</h1><p>加入一个打卡挑战，或用一份问卷分享你的想法。</p></header>
    <div class="zone-grid">
      <RouterLink to="/activity/checkin" class="zone-card"><span class="zone-icon">日</span><div class="zone-info"><h2>打卡挑战</h2><p>找到同伴，用持续的记录见证进步。</p><span class="badge">{{ checkinCount === null ? '浏览挑战' : `${checkinCount} 个进行中的挑战` }}</span></div><span aria-hidden="true">→</span></RouterLink>
      <RouterLink to="/activity/survey" class="zone-card"><span class="zone-icon">问</span><div class="zone-info"><h2>社区问卷</h2><p>参与调查，了解社区里不同的声音。</p><span class="badge">{{ surveyCount === null ? '浏览问卷' : `${surveyCount} 份发布中的问卷` }}</span></div><span aria-hidden="true">→</span></RouterLink>
    </div>
    <section class="activity-shortcuts"><div><h2>从自己的节奏开始</h2><p>发起挑战前写清规则、周期和记录要求，方便伙伴参与。</p></div><RouterLink to="/activity/checkin/create">发起打卡 →</RouterLink><RouterLink to="/activity/checkin/my">查看我的打卡 →</RouterLink></section>
  </div>
</template>

'''+script
edit('views/活动中心/ActivityHome.vue',home)

def square(s):
 s=s.replace('    <!-- 正常内容 -->','    <div v-else-if="loadError" class="empty-state" role="alert">{{ loadError }} <button class="btn-primary" @click="loadActivities">重新加载</button></div>\n    <!-- 正常内容 -->')
 s=s.replace("const loading = ref(true);","const loading = ref(true);\nconst loadError = ref('');")
 s=s.replace('  loading.value = true;','  loading.value = true; loadError.value = \'\';')
 s=s.replace('    activities.value = [];',"    loadError.value = '活动读取失败，请重试。';")
 s=s.replace(' @input="filterActivities"','').replace("const filterActivities = () => {\n  // 搜索逻辑在 computed 中自动执行\n};\n",'')
 return s
edit('views/活动中心/打卡中心/Square.vue',square)

def surveylist(s):
 s=s.replace("import { useRouter } from 'vue-router'","import { useRouter } from 'vue-router'\nimport { useUserStore } from '@/stores/user'")
 s=s.replace('const router = useRouter()','const router = useRouter()\nconst userStore = useUserStore()')
 s=s.replace('class="btn-create"','v-if="userStore.canManageSurvey" class="btn-create"').replace('class="btn-primary" @click="$router.push(\'/activity/survey/create\')"','v-if="userStore.canManageSurvey" class="btn-primary" @click="$router.push(\'/activity/survey/create\')"')
 s=s.replace('v-if="survey.status === 0"','v-if="survey.status === 0 && userStore.canManageSurvey"')
 return s
edit('views/活动中心/问卷中心/SurveyList.vue',surveylist)
