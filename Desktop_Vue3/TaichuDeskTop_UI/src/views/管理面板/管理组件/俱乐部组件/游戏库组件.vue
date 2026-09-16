<template>
  <div class="panel">
    <div class="panel-toolbar">
      <span class="count-hint">已注册 {{ games.length }} 款陪玩游戏</span>
      <button class="btn-primary" @click="openGameModal()">+ 新建游戏</button>
    </div>

    <div class="table-card">
      <table class="ink-table">
        <thead>
          <tr>
            <th width="120">代码</th>
            <th width="160">名称</th>
            <th>简介</th>
            <th width="80">排序</th>
            <th width="100">状态</th>
            <th width="200" class="text-right">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="g in games" :key="g.code">
            <td class="mono">{{ g.code }}</td>
            <td><b>{{ g.name }}</b></td>
            <td class="text-muted">{{ g.description || '—' }}</td>
            <td class="mono">{{ g.sortOrder }}</td>
            <td>
              <span :class="['status-badge', g.isActive ? 'on' : 'off']">
                {{ g.isActive ? '已上架' : '已下架' }}
              </span>
            </td>
            <td class="text-right actions">
              <button class="btn-text" @click="openGameModal(g)">编辑</button>
              <button class="btn-text danger" @click="handleDeleteGame(g)">删除</button>
            </td>
          </tr>
          <tr v-if="games.length === 0 && !loadingGames">
            <td colspan="6" class="empty-cell">暂无游戏，点击右上角新建</td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- 游戏编辑弹窗 -->
    <Teleport to="body">
      <div v-if="showGameModal" class="modal-mask">
        <div class="modal-container scroll-y">
          <header class="modal-header">
            <h3>{{ gameForm.isEdit ? '编辑游戏' : '新建游戏' }}</h3>
            <button class="close-icon" @click="closeGameModal">×</button>
          </header>

          <div class="form-grid">
            <div class="field">
              <label>游戏代码 (Code) *</label>
              <input
                v-model="gameForm.code"
                :disabled="gameForm.isEdit"
                class="ink-input"
                placeholder="如 delta / apex / valorant"
              />
            </div>
            <div class="field">
              <label>显示名 *</label>
              <input v-model="gameForm.name" class="ink-input" placeholder="如 三角洲行动" />
            </div>
            <div class="field full">
              <label>简介</label>
              <textarea v-model="gameForm.description" class="ink-input" rows="3"></textarea>
            </div>
            <div class="field full">
              <label>通用规则（每行一条）</label>
              <textarea
                v-model="gameForm.commonRulesText"
                class="ink-input"
                rows="6"
                placeholder="· 出高价值物资必须给板板&#10;· 打手禁止私下加老板联系方式"
              ></textarea>
              <p class="score-hint">该游戏所有订单共用。前台会在每个订单类型下显示。</p>
            </div>

            <div class="field full">
              <label>雷达图系统评分维度</label>
              <div class="radar-sys-dims">
                <label
                  v-for="opt in radarSystemDimOptions"
                  :key="opt.code"
                  class="sys-dim-item"
                  :class="{ disabled: !opt.available }"
                >
                  <input
                    type="checkbox"
                    :value="opt.code"
                    v-model="gameForm.radarSystemDims"
                    :disabled="!opt.available"
                  />
                  <div class="sys-dim-info">
                    <span class="sys-dim-label">
                      {{ opt.label }}
                      <span v-if="!opt.available" class="sys-dim-badge">待上线</span>
                    </span>
                    <span class="sys-dim-desc">{{ opt.description }}</span>
                  </div>
                </label>
              </div>
              <p class="score-hint">
                勾选后会作为该游戏所有打手雷达图的维度。此外，「字段编排」中勾选「★ 参与雷达图评分」的字段也会自动成为维度。
              </p>
            </div>

            <div class="field">
              <label>排序权重</label>
              <input type="number" v-model.number="gameForm.sortOrder" class="ink-input" />
            </div>
            <div class="field">
              <label>上架状态</label>
              <label class="switch-label">
                <input type="checkbox" v-model="gameForm.isActive" />
                <span>启用 / 展示</span>
              </label>
            </div>
          </div>

          <footer class="modal-footer">
            <button class="btn-cancel" @click="closeGameModal">取消</button>
            <button class="btn-submit" @click="saveGame" :disabled="savingGame">
              {{ savingGame ? '保存中...' : '保存' }}
            </button>
          </footer>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue';
import request from '@/utils/request';

interface ClubGame {
  code: string;
  name: string;
  description?: string;
  commonRulesText?: string;
  radarSystemDimsJson?: string;
  isActive: boolean;
  sortOrder: number;
  createdAt?: string;
}

interface RadarSystemDimOption {
  code: string;
  label: string;
  description: string;
  available: boolean;
}

interface GameFormState {
  isEdit: boolean;
  code: string;
  name: string;
  description: string;
  commonRulesText: string;
  radarSystemDims: string[];
  isActive: boolean;
  sortOrder: number;
}

const games = ref<ClubGame[]>([]);
const loadingGames = ref(false);
const showGameModal = ref(false);
const savingGame = ref(false);
const radarSystemDimOptions = ref<RadarSystemDimOption[]>([]);

const gameForm = reactive<GameFormState>({
  isEdit: false,
  code: '',
  name: '',
  description: '',
  commonRulesText: '',
  radarSystemDims: [],
  isActive: true,
  sortOrder: 0,
});

const loadGames = async () => {
  loadingGames.value = true;
  try {
    const res: any = await request.get('/admin/club/games');
    games.value = res.data || res || [];
  } catch (e) {
    console.error('拉取游戏列表失败', e);
  } finally {
    loadingGames.value = false;
  }
};

const loadRadarSystemDimOptions = async () => {
  try {
    const res: any = await request.get('/admin/club/radar-system-dims');
    radarSystemDimOptions.value = res.data || res || [];
  } catch (e) {
    console.error('拉取系统维度清单失败', e);
  }
};

const openGameModal = (g?: ClubGame) => {
  if (g) {
    gameForm.isEdit = true;
    gameForm.code = g.code;
    gameForm.name = g.name;
    gameForm.description = g.description ?? '';
    gameForm.commonRulesText = g.commonRulesText ?? '';
    try {
      gameForm.radarSystemDims = g.radarSystemDimsJson
        ? JSON.parse(g.radarSystemDimsJson)
        : [];
    } catch {
      gameForm.radarSystemDims = [];
    }
    gameForm.isActive = g.isActive;
    gameForm.sortOrder = g.sortOrder;
  } else {
    gameForm.isEdit = false;
    gameForm.code = '';
    gameForm.name = '';
    gameForm.description = '';
    gameForm.commonRulesText = '';
    gameForm.radarSystemDims = [];
    gameForm.isActive = true;
    gameForm.sortOrder = games.value.length + 1;
  }
  showGameModal.value = true;
};

const closeGameModal = () => {
  showGameModal.value = false;
};

const saveGame = async () => {
  if (!gameForm.code.trim() || !gameForm.name.trim()) {
    alert('请填写游戏代码与显示名');
    return;
  }
  savingGame.value = true;
  try {
    await request.post('/admin/club/game', {
      code: gameForm.code.trim(),
      name: gameForm.name.trim(),
      description: gameForm.description,
      commonRulesText: gameForm.commonRulesText.trim() || null,
      radarSystemDimsJson: gameForm.radarSystemDims.length > 0
        ? JSON.stringify(gameForm.radarSystemDims)
        : null,
      isActive: gameForm.isActive,
      sortOrder: gameForm.sortOrder,
    });
    await loadGames();
    closeGameModal();
  } catch (e: any) {
    alert(e.message || '保存游戏失败');
  } finally {
    savingGame.value = false;
  }
};

const handleDeleteGame = async (g: ClubGame) => {
  if (!confirm(`确定删除游戏【${g.name}】吗？该操作会连带删除其字段配置。`)) return;
  try {
    await request.delete(`/admin/club/game/${g.code}`);
    await loadGames();
  } catch (e: any) {
    alert(e.message || '删除失败');
  }
};

onMounted(async () => {
  await loadRadarSystemDimOptions();
  await loadGames();
});
</script>

<style scoped>
.panel { animation: fadeIn 0.3s ease; }

.panel-toolbar {
  display: flex; justify-content: space-between; align-items: center;
  margin-bottom: 16px; gap: 16px;
}
.count-hint { font-size: 0.85rem; color: #888; }

.ink-input, .ink-select {
  border: 1px solid #e0e0e0; padding: 10px 14px; border-radius: 4px;
  font-size: 0.85rem; outline: none; background: #fff;
  font-family: inherit;
}
.ink-input:focus, .ink-select:focus { border-color: #1a1a1a; }

.btn-primary {
  background: #111; color: #fff; border: none; padding: 10px 20px;
  border-radius: 4px; cursor: pointer; font-size: 0.85rem; font-weight: 500;
}
.btn-primary:hover:not(:disabled) { background: #333; }

.table-card {
  background: #fff; border: 1px solid #f0f0f0; border-radius: 8px;
  box-shadow: 0 4px 20px rgba(0,0,0,0.01); overflow: hidden;
}
.ink-table { width: 100%; border-collapse: collapse; font-size: 0.88rem; }
.ink-table th {
  padding: 16px; background: #fcfcfc; color: #888; text-align: left;
  font-size: 0.75rem; text-transform: uppercase; letter-spacing: 0.5px;
  border-bottom: 2px solid #111;
}
.ink-table td { padding: 16px; border-bottom: 1px solid #f7f7f7; vertical-align: middle; }
.ink-table tr:hover td { background: #fafafa; }
.text-right { text-align: right; }
.text-muted { color: #999; }
.mono { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; }
.empty-cell { text-align: center; padding: 60px !important; color: #bbb; font-style: italic; }

.status-badge {
  font-size: 0.7rem; padding: 3px 8px; border-radius: 4px; font-weight: 600;
}
.status-badge.on { background: #e3fcef; color: #00875a; }
.status-badge.off { background: #f5f5f5; color: #999; }

.btn-text {
  background: none; border: none; color: #2563eb; cursor: pointer;
  font-size: 0.8rem; font-weight: 700; margin-left: 12px; padding: 0;
}
.btn-text:hover { text-decoration: underline; }
.btn-text.danger { color: #dc2626; }

/* Modal */
.modal-mask {
  position: fixed; inset: 0; background: rgba(255,255,255,0.85);
  backdrop-filter: blur(12px); z-index: 9999;
  display: flex; justify-content: center; align-items: center;
}
.modal-container {
  background: #fff; border: 1px solid #000; width: 100%; max-width: 640px;
  padding: 32px; box-shadow: 25px 25px 0 rgba(0,0,0,0.05);
  border-radius: 4px;
}
.modal-container.scroll-y { max-height: 85vh; overflow-y: auto; }
.modal-header {
  display: flex; justify-content: space-between; align-items: flex-start;
  margin-bottom: 24px; border-bottom: 1px solid #eee; padding-bottom: 16px;
}
.modal-header h3 { font-size: 1.25rem; font-weight: 600; margin: 0; }
.close-icon { background: none; border: none; font-size: 1.8rem; cursor: pointer; color: #ccc; line-height: 1; }
.close-icon:hover { color: #000; }

.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; margin-bottom: 24px; }
.field { display: flex; flex-direction: column; gap: 6px; }
.field.full { grid-column: span 2; }
.field label { font-size: 0.7rem; text-transform: uppercase; color: #aaa; font-weight: 700; }
.field textarea.ink-input { resize: vertical; font-family: inherit; }
.switch-label { display: flex; align-items: center; gap: 8px; font-size: 0.85rem; cursor: pointer; padding-top: 8px; }

.radar-sys-dims {
  display: grid; grid-template-columns: repeat(2, 1fr); gap: 10px;
}
.sys-dim-item {
  display: flex; align-items: flex-start; gap: 10px;
  padding: 10px 12px; border: 1px solid #e0e0e0; border-radius: 4px;
  cursor: pointer; transition: 0.2s; background: #fff;
}
.sys-dim-item:hover:not(.disabled) { border-color: #111; background: #fafafa; }
.sys-dim-item.disabled { opacity: 0.45; cursor: not-allowed; background: #fafafa; }
.sys-dim-item input { margin-top: 3px; cursor: pointer; accent-color: #111; }
.sys-dim-info { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
.sys-dim-label {
  font-size: 0.85rem; font-weight: 600; color: #111;
  display: flex; align-items: center; gap: 6px;
}
.sys-dim-badge {
  padding: 1px 6px; font-size: 0.65rem; font-weight: 500;
  color: #999; background: #f0f0f0; border-radius: 2px;
}
.sys-dim-desc { font-size: 0.72rem; color: #999; line-height: 1.4; }

.score-hint { margin: 4px 0 0; font-size: 0.72rem; color: #888; line-height: 1.6; }

.modal-footer {
  display: flex; justify-content: flex-end; gap: 14px;
  border-top: 1px solid #eee; padding-top: 20px;
}
.btn-cancel {
  background: none; border: 1px solid #e0e0e0; padding: 12px 24px;
  cursor: pointer; color: #666; font-size: 0.85rem; border-radius: 4px;
}
.btn-cancel:hover { background: #fbfbfb; }
.btn-submit {
  background: #111; color: #fff; border: none; padding: 12px 30px;
  font-weight: 700; cursor: pointer; font-size: 0.85rem; border-radius: 4px;
}
.btn-submit:disabled { opacity: 0.5; cursor: not-allowed; }

@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
</style>