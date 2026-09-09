<template>
  <div class="project-deliveries-root">
    <div class="deliveries-header">
      <div class="header-info">
        <h3>履约与品控验收台账</h3>
        <p>内部考评专用：手动记录全站成员及外包人员的交稿准时率、返工频次与产出质量。</p>
      </div>
      <button v-if="canManage" class="btn-create" @click="openModal()">+ 登记验收记录</button>
    </div>

    <div v-if="isLoading" class="loading-state">
      <div class="loading-bar"></div>
    </div>

    <!-- 验收记录台账列表 -->
    <div v-else class="deliveries-list">
      <div v-if="records.length === 0" class="empty-hint">
        尚未记录任何交付与验收台账。
      </div>

      <div v-for="record in records" :key="record.id" class="delivery-card">
        <div class="card-left">
          <div class="member-info">
            <!-- 🌟 永远展示现在的名字首字母 -->
            <div class="avatar">{{ record.currentName.charAt(0) }}</div>
            <div class="name-block">
              <!-- 🌟 永远展示现在的真名 -->
              <span class="name">{{ record.currentName }}</span>
              <!-- 🌟 核心：如果他改名了，直接亮出案发时的曾用名 -->
              <span v-if="record.currentName !== record.recordedName" class="name-changed-warning">
                (当时曾用名: {{ record.recordedName }})
              </span>
              <span class="task-name">交付物: <strong>{{ record.taskName }}</strong></span>
            </div>
          </div>
        </div>

        <div class="card-middle">
          <div class="eval-tags">
            <span :class="['eval-tag', getTimingClass(record.timingStatus)]">
              ⏱ {{ record.timingStatus }}
            </span>
            <span :class="['eval-tag', getQualityClass(record.qualityStatus)]">
              🎯 {{ record.qualityStatus }}
            </span>
            <span :class="['eval-tag', getCommClass(record.commStatus)]">
              💬 {{ record.commStatus || '状态未知' }}
            </span>
            <span v-if="record.reworkCount > 0" class="eval-tag rework">
              🔄 返工 {{ record.reworkCount }} 次
            </span>
          </div>
          
          <!-- 管理员手记 -->
          <div class="admin-note" v-if="record.adminNote">
            <span class="note-label">验收纪要:</span>
            <p>{{ record.adminNote }}</p>
          </div>
        </div>

        <div class="card-right">
          <span class="record-date">{{ formatDate(record.createdAt) }}</span>
          <button v-if="canManage" class="btn-delete" @click="deleteRecord(record.id)">抹除</button>
        </div>
      </div>
    </div>

    <!-- 登记验收记录弹窗 -->
    <Transition name="fade">
      <div v-if="showModal" class="modal-overlay" @click.self="showModal = false">
        <div class="minimal-modal">
          <header class="modal-inner-header">
            <h2>登记交付验收</h2>
            <p>如实记录该成员本次协作的履约表现（支持检索全站用户）</p>
          </header>

          <div class="modal-body">
            <div class="form-row">
              <div class="input-group relative-group">
                <label>考核对象 (支持全站搜索外包/协作者)</label>
                
                <div v-if="selectedUserName" class="selected-user-box clean-input">
                  <span class="user-pill">@{{ selectedUserName }}</span>
                  <button class="clear-user-btn" @click="clearSelectedUser">重新选择</button>
                </div>

                <div v-else>
                  <input 
                    v-model="searchKeyword" 
                    class="clean-input" 
                    placeholder="输入用户名实时检索..." 
                    @input="handleSearch"
                    @focus="showDropdown = searchKeyword.trim().length > 0"
                  />
                  <!-- 联想浮层 -->
                  <div v-if="showDropdown && searchKeyword.trim()" class="search-dropdown">
                    <div v-if="isSearching" class="dropdown-hint">检索全站用户中...</div>
                    <ul v-else-if="candidateList.length > 0" class="candidate-list">
                      <li v-for="user in candidateList" :key="user.id" @click="selectUser(user)">
                        <div class="avatar-mini">{{ user.username.charAt(0) }}</div>
                        <div class="user-info-mini">
                          <span class="u-name">{{ user.username }}</span>
                          <span class="u-email">{{ user.email }}</span>
                        </div>
                      </li>
                    </ul>
                    <div v-else class="dropdown-hint">未找到该用户</div>
                  </div>
                </div>
              </div>

              <div class="input-group">
                <label>交付物 / 任务名称</label>
                <input v-model="form.taskName" class="clean-input" placeholder="如：第三章CG线稿" />
              </div>
            </div>

            <div class="form-row">
              <div class="input-group">
                <label>履约守时状态</label>
                <select v-model="form.timingStatus" class="clean-input">
                  <option value="提前交付">提前交付 (超预期)</option>
                  <option value="按时交稿">按时交稿 (符合预期)</option>
                  <option value="轻微延期">轻微延期 (可控范围内)</option>
                  <option value="严重拖稿">严重拖稿 (影响整体排期)</option>
                  <option value="失联跑路">失联跑路 (恶劣性质)</option>
                </select>
              </div>
              <div class="input-group">
                <label>产出质量定级</label>
                <select v-model="form.qualityStatus" class="clean-input">
                  <option value="免检通过">免检通过 (极佳)</option>
                  <option value="质量达标">质量达标 (合格)</option>
                  <option value="存在瑕疵">存在瑕疵 (勉强可用)</option>
                  <option value="质量低劣">质量低劣 (不可用)</option>
                </select>
              </div>
            </div>

            <div class="form-row">
              <div class="input-group">
                <label>沟通与交接</label>
                <select v-model="form.commStatus" class="clean-input">
                  <option value="丝滑交接">丝滑交接 (积极沟通)</option>
                  <option value="沟通迟缓">沟通迟缓 (经常找不到人)</option>
                  <option value="无交接抛掷">无交接抛掷 (丢下文件就消失)</option>
                  <option value="失联拒收">失联/拒收 (彻底无法联系)</option>
                </select>
              </div>
              <div class="input-group" style="width: 120px; flex: none;">
                <label>返工次数</label>
                <input type="number" min="0" v-model.number="form.reworkCount" class="clean-input" />
              </div>
            </div>

            <div class="input-group">
              <label>验收详细纪要 (管理层可见)</label>
              <textarea 
                v-model="form.adminNote" 
                rows="3" 
                class="clean-input"
                placeholder="具体记录延期的原因、质量差在哪里、沟通是否顺畅等，作为未来合作的避雷依据..."
              ></textarea>
            </div>
          </div>

          <footer class="modal-footer">
            <button class="cancel-btn" @click="showModal = false">取消</button>
            <button class="confirm-btn" :disabled="isSubmitting || !form.memberId || !form.taskName" @click="submitRecord">
              {{ isSubmitting ? '正在归档...' : '确认登记' }}
            </button>
          </footer>
        </div>
      </div>
    </Transition>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import request from '@/utils/request';

const props = defineProps<{
  projectId: string;
  initialData?: any;
}>();

const isLoading = ref(true);
const canManage = ref(true); 
const showModal = ref(false);
const isSubmitting = ref(false);

const records = ref<any[]>([]);

// 全站搜索专属响应式变量
const searchKeyword = ref('');
const selectedUserName = ref('');
const candidateList = ref<any[]>([]);
const isSearching = ref(false);
const showDropdown = ref(false);
let searchTimer: any = null;

// 🌟 补充 recordedName 字段，与后端 DTO 保持一致
const form = ref({
  memberId: '',
  recordedName: '', 
  taskName: '',
  timingStatus: '按时交稿',
  qualityStatus: '质量达标',
  commStatus: '丝滑交接',
  reworkCount: 0,
  adminNote: ''
});

const loadData = async () => {
  isLoading.value = true;
  try {
    const recordsRes: any = await request.get(`/project/${props.projectId}/deliveries`);
    records.value = recordsRes.data || recordsRes || [];
  } catch (err) {
    console.error('获取履约台账失败', err);
  } finally {
    isLoading.value = false;
  }
};

onMounted(loadData);

const openModal = () => {
  form.value = { memberId: '', recordedName: '', taskName: '', timingStatus: '按时交稿', qualityStatus: '质量达标', commStatus: '丝滑交接', reworkCount: 0, adminNote: '' };
  searchKeyword.value = '';
  selectedUserName.value = '';
  candidateList.value = [];
  showDropdown.value = false;
  showModal.value = true;
};

// 执行防抖全站检索
const handleSearch = () => {
  if (searchTimer) clearTimeout(searchTimer);
  const query = searchKeyword.value.trim();
  if (!query) {
    candidateList.value = [];
    showDropdown.value = false;
    return;
  }

  showDropdown.value = true;
  isSearching.value = true;

  searchTimer = setTimeout(async () => {
    try {
      // 🌟 使用全局搜索接口
      const res: any = await request.get(`/user/search`, {
  params: { keyword: query }
});
      candidateList.value = res.data || res || [];
    } catch (err) {
      console.error('检索用户失败:', err);
      candidateList.value = [];
    } finally {
      isSearching.value = false;
    }
  }, 300);
};

// 🌟 选中搜索到的候选人，同时记录 ID 和当时的真名 (快照)
const selectUser = (user: any) => {
  form.value.memberId = user.id;
  form.value.recordedName = user.username; 
  selectedUserName.value = user.username;
  showDropdown.value = false;
  searchKeyword.value = '';
};

const clearSelectedUser = () => {
  form.value.memberId = '';
  form.value.recordedName = '';
  selectedUserName.value = '';
};

const submitRecord = async () => {
  isSubmitting.value = true;
  try {
    await request.post(`/project/${props.projectId}/deliveries`, form.value);
    showModal.value = false;
    await loadData();
  } catch (err) {
    alert('登记失败，请确认权限');
  } finally {
    isSubmitting.value = false;
  }
};

const deleteRecord = async (id: string) => {
  if (!confirm('确定要抹除这条验收记录吗？')) return;
  try {
    await request.delete(`/project/${props.projectId}/deliveries/${id}`);
    await loadData();
  } catch (err) {
    alert('抹除失败');
  }
};

const getTimingClass = (status: string) => {
  if (!status) return 'success';
  if (status.includes('拖稿') || status.includes('跑路')) return 'danger';
  if (status.includes('延期')) return 'warning';
  return 'success';
};

const getQualityClass = (status: string) => {
  if (!status) return 'success';
  if (status.includes('低劣')) return 'danger';
  if (status.includes('瑕疵')) return 'warning';
  return 'success';
};

const getCommClass = (status: string) => {
  if (!status) return 'success';
  if (status.includes('失联') || status.includes('无交接')) return 'danger';
  if (status.includes('迟缓')) return 'warning';
  return 'success';
};

const formatDate = (dateStr: string) => {
  if (!dateStr) return '';
  return new Date(dateStr).toLocaleDateString();
};
</script>

<style scoped>
.project-deliveries-root { max-width: 1000px; animation: fadeIn 0.4s ease; }
.deliveries-header { display: flex; justify-content: space-between; align-items: flex-end; margin-bottom: 32px; border-bottom: 1px solid #eee; padding-bottom: 24px; }
.header-info h3 { font-size: 1.2rem; font-weight: 500; margin: 0 0 8px 0; color: #1a1a1a; }
.header-info p { font-size: 0.85rem; color: #888; margin: 0; }
.btn-create { background: #1a1a1a; color: #fff; border: none; padding: 10px 24px; font-size: 0.85rem; cursor: pointer; border-radius: 2px; transition: background 0.3s; }
.btn-create:hover { background: #333; }

.loading-state { height: 120px; display: flex; align-items: center; justify-content: center; }
.loading-bar { width: 40px; height: 1px; background: #1a1a1a; animation: pulse 1.5s infinite; }

.deliveries-list { display: flex; flex-direction: column; gap: 16px; }
.empty-hint { text-align: center; padding: 60px; color: #bbb; font-size: 0.9rem; border: 1px dashed #eee; }

.delivery-card { background: #fff; border: 1px solid #f0f0f0; padding: 20px 24px; display: flex; justify-content: space-between; gap: 24px; transition: border-color 0.2s; }
.delivery-card:hover { border-color: #ddd; }

.card-left { flex: 0 0 200px; }
.member-info { display: flex; gap: 12px; align-items: flex-start; }
.avatar { width: 36px; height: 36px; border-radius: 50%; background: #1a1a1a; color: #fff; display: flex; align-items: center; justify-content: center; font-size: 0.9rem; font-weight: bold; flex-shrink: 0; }
.name-block { display: flex; flex-direction: column; gap: 4px; }
.name-block .name { font-size: 1rem; font-weight: 500; color: #1a1a1a; }
/* 🌟 新增：曾用名警告色样式 */
.name-changed-warning { font-size: 0.7rem; color: #d9363e; font-style: italic; margin-top: -2px; margin-bottom: 2px; }
.task-name { font-size: 0.75rem; color: #777; }
.task-name strong { color: #444; }

.card-middle { flex: 1; display: flex; flex-direction: column; gap: 12px; border-left: 1px dashed #eee; padding-left: 24px; }
.eval-tags { display: flex; gap: 8px; flex-wrap: wrap; }
.eval-tag { font-size: 0.7rem; padding: 3px 8px; border-radius: 2px; font-weight: 500; }
.eval-tag.success { background: #dafbe1; color: #1a7f37; }
.eval-tag.warning { background: #fff8c5; color: #9a6700; }
.eval-tag.danger { background: #ffebe9; color: #cf222e; }
.eval-tag.rework { background: #f6f8fa; color: #57606a; border: 1px solid #d0d7de; }

.admin-note { background: #fafafa; padding: 12px; border-left: 2px solid #1a1a1a; }
.note-label { font-size: 0.7rem; color: #1a1a1a; font-weight: 600; margin-bottom: 4px; display: block; }
.admin-note p { font-size: 0.85rem; color: #555; margin: 0; line-height: 1.5; }

.card-right { display: flex; flex-direction: column; align-items: flex-end; justify-content: space-between; }
.record-date { font-size: 0.75rem; color: #bbb; }
.btn-delete { background: none; border: none; color: #ccc; font-size: 0.75rem; cursor: pointer; transition: color 0.2s; }
.btn-delete:hover { color: #ff4757; }

.modal-overlay { position: fixed; inset: 0; background: rgba(255,255,255,0.85); backdrop-filter: blur(8px); display: flex; align-items: center; justify-content: center; z-index: 1000; }
.minimal-modal { background: #fff; width: 100%; max-width: 600px; padding: 40px; border: 1px solid #eee; box-shadow: 0 40px 100px rgba(0,0,0,0.05); }
.modal-inner-header { margin-bottom: 32px; }
.modal-inner-header h2 { font-size: 1.3rem; margin: 0 0 8px 0; }
.modal-inner-header p { font-size: 0.85rem; color: #888; margin: 0; }

.form-row { display: flex; gap: 16px; margin-bottom: 16px; }
.input-group { flex: 1; display: flex; flex-direction: column; gap: 8px; margin-bottom: 16px; }
.input-group label { font-size: 0.75rem; color: #555; font-weight: 500; }
.clean-input { width: 100%; border: 1px solid #e0e0e0; padding: 10px 12px; font-size: 0.9rem; outline: none; transition: border-color 0.2s; background: #fff; box-sizing: border-box; }
.clean-input:focus { border-color: #1a1a1a; }
textarea.clean-input { resize: vertical; }

.relative-group { position: relative; }
.selected-user-box { display: flex; justify-content: space-between; align-items: center; background: #fafafa; border-color: #1a1a1a; }
.user-pill { font-weight: 500; color: #1a1a1a; }
.clear-user-btn { background: none; border: none; color: #888; font-size: 0.75rem; cursor: pointer; transition: color 0.2s; }
.clear-user-btn:hover { color: #ff4757; }
.search-dropdown { position: absolute; top: calc(100% - 10px); left: 0; right: 0; background: #fff; border: 1px solid #eee; box-shadow: 0 10px 30px rgba(0,0,0,0.1); max-height: 200px; overflow-y: auto; z-index: 10; border-radius: 2px; }
.dropdown-hint { padding: 12px; font-size: 0.8rem; color: #999; text-align: center; }
.candidate-list { list-style: none; padding: 0; margin: 0; }
.candidate-list li { display: flex; align-items: center; gap: 10px; padding: 10px 12px; cursor: pointer; border-bottom: 1px solid #f9f9f9; transition: background 0.2s; }
.candidate-list li:hover { background: #fcfcfc; }
.avatar-mini { width: 24px; height: 24px; border-radius: 50%; background: #1a1a1a; color: #fff; display: flex; align-items: center; justify-content: center; font-size: 0.7rem; }
.user-info-mini { display: flex; flex-direction: column; }
.u-name { font-size: 0.85rem; color: #1a1a1a; }
.u-email { font-size: 0.7rem; color: #999; }

.modal-footer { margin-top: 32px; display: flex; justify-content: flex-end; gap: 16px; }
.cancel-btn { background: none; border: none; color: #888; cursor: pointer; padding: 10px 20px; font-size: 0.85rem; }
.confirm-btn { background: #1a1a1a; color: #fff; border: none; padding: 10px 32px; cursor: pointer; font-size: 0.85rem; }
.confirm-btn:disabled { background: #ccc; cursor: not-allowed; }

@keyframes fadeIn { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }
@keyframes pulse { 0% { transform: scaleX(0.5); opacity: 0.2; } 50% { transform: scaleX(1.5); opacity: 1; } 100% { transform: scaleX(0.5); opacity: 0.2; } }
</style>