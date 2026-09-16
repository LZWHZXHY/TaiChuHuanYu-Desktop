<template>
  <div class="panel">
    <div class="panel-toolbar">
      <div class="game-picker">
        <label>当前编辑游戏：</label>
        <select v-model="currentGameCode" class="ink-select" @change="loadFields">
          <option v-for="g in games" :key="g.code" :value="g.code">
            {{ g.name }} ({{ g.code }})
          </option>
        </select>
      </div>
      <button class="btn-primary" :disabled="!currentGameCode" @click="openFieldModal()">
        + 添加字段
      </button>
    </div>

    <div class="table-card">
      <table class="ink-table">
        <thead>
          <tr>
            <th width="120">Key</th>
            <th width="160">显示名</th>
            <th width="100">控件类型</th>
            <th>选项 / 提示</th>
            <th width="90">评分维度</th>
            <th width="80">必填</th>
            <th width="80">排序</th>
            <th width="140" class="text-right">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="f in fields" :key="f.id">
            <td class="mono">{{ f.key }}</td>
            <td><b>{{ f.label }}</b></td>
            <td><span class="type-tag">{{ f.type }}</span></td>
            <td class="text-muted ellipsis">
              <template v-if="f.type === 'select'">
                {{ parseOptions(f.optionsJson).join(' / ') || '—' }}
              </template>
              <template v-else>{{ f.placeholder || '—' }}</template>
            </td>
            <td>
              <span v-if="f.isScoreDimension" class="score-flag">★ 参与</span>
              <span v-else class="text-muted">—</span>
            </td>
            <td><span :class="['req-dot', f.required ? 'yes' : 'no']"></span></td>
            <td class="mono">{{ f.sortOrder }}</td>
            <td class="text-right actions">
              <button class="btn-text" @click="openFieldModal(f)">编辑</button>
              <button class="btn-text danger" @click="handleDeleteField(f)">删除</button>
            </td>
          </tr>
          <tr v-if="fields.length === 0">
            <td colspan="8" class="empty-cell">
              {{ currentGameCode ? '该游戏暂无字段，请添加' : '请先选择一款游戏' }}
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- 字段编辑弹窗 -->
    <Teleport to="body">
      <div v-if="showFieldModal" class="modal-mask">
        <div class="modal-container scroll-y">
          <header class="modal-header">
            <h3>{{ fieldForm.isEdit ? '编辑字段' : '新增字段' }}</h3>
            <button class="close-icon" @click="closeFieldModal">×</button>
          </header>

          <div class="form-grid">
            <div class="field">
              <label>字段 Key *</label>
              <input v-model="fieldForm.key" class="ink-input" placeholder="kd / rank / matches" />
            </div>
            <div class="field">
              <label>显示名 *</label>
              <input v-model="fieldForm.label" class="ink-input" placeholder="KD 区间" />
            </div>
            <div class="field">
              <label>控件类型</label>
              <select v-model="fieldForm.type" class="ink-input">
                <option value="select">select 下拉</option>
                <option value="text">text 单行</option>
                <option value="textarea">textarea 多行</option>
                <option value="number">number 数值</option>
              </select>
            </div>
            <div class="field">
              <label>排序权重</label>
              <input type="number" v-model.number="fieldForm.sortOrder" class="ink-input" />
            </div>
            <div class="field full" v-if="fieldForm.type === 'select'">
              <label>选项列表 (每行一个) *</label>
              <textarea
                v-model="fieldOptionsRaw"
                class="ink-input"
                rows="5"
                placeholder="0-1&#10;1-2&#10;2-3&#10;4+"
              ></textarea>
            </div>
            <div class="field full" v-else>
              <label>提示文字 (Placeholder)</label>
              <input v-model="fieldForm.placeholder" class="ink-input" />
            </div>

            <div class="field full score-section">
              <label class="switch-label">
                <input type="checkbox" v-model="fieldForm.isScoreDimension" />
                <span>★ 参与雷达图评分</span>
              </label>
              <p class="score-hint">
                勾选后，该字段会作为打手雷达图的一个维度。需要填写「评分映射」把值转成 0-100 分。
              </p>
            </div>

            <div class="field full" v-if="fieldForm.isScoreDimension">
              <label>评分映射 (JSON)</label>
              <textarea
                v-model="fieldForm.scoreMapJson"
                class="ink-input mono"
                rows="4"
                :placeholder="scoreMapPlaceholder"
              ></textarea>
              <p class="score-hint">
                <b>select 类型</b>：<code>{"0-1":20,"1-2":40,"2-3":70,"3-4":90,"4+":100}</code><br/>
                <b>number 类型</b>：<code>{"min":0,"max":1500,"reverse":false}</code>
              </p>
            </div>

            <div class="field full">
              <label class="switch-label">
                <input type="checkbox" v-model="fieldForm.required" />
                <span>必填字段</span>
              </label>
            </div>
          </div>

          <footer class="modal-footer">
            <button class="btn-cancel" @click="closeFieldModal">取消</button>
            <button class="btn-submit" @click="saveField" :disabled="savingField">
              {{ savingField ? '保存中...' : '保存' }}
            </button>
          </footer>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue';
import request from '@/utils/request';

interface ClubGame {
  code: string;
  name: string;
  isActive: boolean;
  sortOrder: number;
}

interface ClubGameField {
  id?: number;
  gameCode: string;
  key: string;
  label: string;
  type: 'select' | 'text' | 'textarea' | 'number';
  optionsJson?: string;
  required: boolean;
  placeholder?: string;
  sortOrder: number;
  isScoreDimension?: boolean;
  scoreMapJson?: string;
}

interface FieldFormState {
  isEdit: boolean;
  id?: number;
  gameCode: string;
  key: string;
  label: string;
  type: 'select' | 'text' | 'textarea' | 'number';
  optionsJson?: string;
  required: boolean;
  placeholder?: string;
  sortOrder: number;
  isScoreDimension: boolean;
  scoreMapJson: string;
}

const games = ref<ClubGame[]>([]);
const currentGameCode = ref('');
const fields = ref<ClubGameField[]>([]);
const showFieldModal = ref(false);
const savingField = ref(false);
const fieldOptionsRaw = ref('');

const fieldForm = reactive<FieldFormState>({
  isEdit: false,
  id: undefined,
  gameCode: '',
  key: '',
  label: '',
  type: 'select',
  optionsJson: undefined,
  required: true,
  placeholder: '',
  sortOrder: 0,
  isScoreDimension: false,
  scoreMapJson: '',
});

const scoreMapPlaceholder = computed(() => {
  if (fieldForm.type === 'number') {
    return '{"min":0,"max":1500,"reverse":false}';
  }
  return '{"0-1":20,"1-2":40,"2-3":70,"3-4":90,"4+":100}';
});

const parseOptions = (json?: string): string[] => {
  if (!json) return [];
  try {
    const arr = JSON.parse(json);
    return Array.isArray(arr) ? arr : [];
  } catch {
    return [];
  }
};

const loadGames = async () => {
  try {
    const res: any = await request.get('/admin/club/games');
    games.value = res.data || res || [];
    if (games.value.length > 0 && !currentGameCode.value) {
      currentGameCode.value = games.value[0].code;
      await loadFields();
    }
  } catch (e) {
    console.error('拉取游戏列表失败', e);
  }
};

const loadFields = async () => {
  if (!currentGameCode.value) {
    fields.value = [];
    return;
  }
  try {
    const res: any = await request.get('/admin/club/fields', {
      params: { gameCode: currentGameCode.value },
    });
    fields.value = res.data || res || [];
  } catch (e) {
    console.error('拉取字段失败', e);
  }
};

const openFieldModal = (f?: ClubGameField) => {
  if (!currentGameCode.value) {
    alert('请先选择游戏');
    return;
  }

  if (f) {
    fieldForm.isEdit = true;
    fieldForm.id = f.id;
    fieldForm.gameCode = currentGameCode.value;
    fieldForm.key = f.key;
    fieldForm.label = f.label;
    fieldForm.type = f.type;
    fieldForm.optionsJson = f.optionsJson;
    fieldForm.required = f.required;
    fieldForm.placeholder = f.placeholder ?? '';
    fieldForm.sortOrder = f.sortOrder;
    fieldForm.isScoreDimension = f.isScoreDimension ?? false;
    fieldForm.scoreMapJson = f.scoreMapJson ?? '';
    fieldOptionsRaw.value = parseOptions(f.optionsJson).join('\n');
  } else {
    fieldForm.isEdit = false;
    fieldForm.id = undefined;
    fieldForm.gameCode = currentGameCode.value;
    fieldForm.key = '';
    fieldForm.label = '';
    fieldForm.type = 'select';
    fieldForm.optionsJson = undefined;
    fieldForm.required = true;
    fieldForm.placeholder = '';
    fieldForm.sortOrder = fields.value.length + 1;
    fieldForm.isScoreDimension = false;
    fieldForm.scoreMapJson = '';
    fieldOptionsRaw.value = '';
  }
  showFieldModal.value = true;
};

const closeFieldModal = () => {
  showFieldModal.value = false;
};

const saveField = async () => {
  if (!fieldForm.key.trim() || !fieldForm.label.trim()) {
    alert('请填写字段 Key 与显示名');
    return;
  }

  if (fieldForm.isScoreDimension) {
    if (!fieldForm.scoreMapJson.trim()) {
      alert('勾选了"参与雷达图评分"，必须填写评分映射 JSON');
      return;
    }
    try {
      JSON.parse(fieldForm.scoreMapJson);
    } catch {
      alert('评分映射不是合法的 JSON，请检查格式');
      return;
    }
  }

  savingField.value = true;
  try {
    const options =
      fieldForm.type === 'select'
        ? fieldOptionsRaw.value.split('\n').map(s => s.trim()).filter(Boolean)
        : [];

    await request.post('/admin/club/field', {
      id: fieldForm.id,
      gameCode: currentGameCode.value,
      key: fieldForm.key.trim(),
      label: fieldForm.label.trim(),
      type: fieldForm.type,
      optionsJson: options.length ? JSON.stringify(options) : null,
      required: fieldForm.required,
      placeholder: fieldForm.placeholder || null,
      sortOrder: fieldForm.sortOrder,
      isScoreDimension: fieldForm.isScoreDimension,
      scoreMapJson: fieldForm.isScoreDimension ? fieldForm.scoreMapJson.trim() : null,
    });
    await loadFields();
    closeFieldModal();
  } catch (e: any) {
    alert(e.message || '保存字段失败');
  } finally {
    savingField.value = false;
  }
};

const handleDeleteField = async (f: ClubGameField) => {
  if (!f.id) return;
  if (!confirm(`确定删除字段【${f.label}】吗？`)) return;
  try {
    await request.delete(`/admin/club/field/${f.id}`);
    await loadFields();
  } catch (e: any) {
    alert(e.message || '删除失败');
  }
};

onMounted(loadGames);
</script>

<style scoped>
.panel { animation: fadeIn 0.3s ease; }

.panel-toolbar {
  display: flex; justify-content: space-between; align-items: center;
  margin-bottom: 16px; gap: 16px;
}
.game-picker { display: flex; align-items: center; gap: 10px; font-size: 0.85rem; color: #555; }

.ink-input, .ink-select {
  border: 1px solid #e0e0e0; padding: 10px 14px; border-radius: 4px;
  font-size: 0.85rem; outline: none; background: #fff;
  font-family: inherit;
}
.ink-input:focus, .ink-select:focus { border-color: #1a1a1a; }
.ink-select { cursor: pointer; min-width: 140px; }

.btn-primary {
  background: #111; color: #fff; border: none; padding: 10px 20px;
  border-radius: 4px; cursor: pointer; font-size: 0.85rem; font-weight: 500;
}
.btn-primary:hover:not(:disabled) { background: #333; }
.btn-primary:disabled { opacity: 0.5; cursor: not-allowed; }

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
.ellipsis { max-width: 300px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.empty-cell { text-align: center; padding: 60px !important; color: #bbb; font-style: italic; }

.type-tag {
  font-family: monospace; font-size: 0.75rem; padding: 2px 8px;
  background: #f1f5f9; color: #475569; border-radius: 3px;
}
.score-flag {
  display: inline-block; padding: 2px 8px;
  font-size: 0.75rem; font-weight: 700;
  color: #c2410c; background: #fff7ed; border-radius: 3px;
}
.req-dot { display: inline-block; width: 8px; height: 8px; border-radius: 50%; }
.req-dot.yes { background: #dc2626; }
.req-dot.no { background: #ddd; }

.btn-text {
  background: none; border: none; color: #2563eb; cursor: pointer;
  font-size: 0.8rem; font-weight: 700; margin-left: 12px; padding: 0;
}
.btn-text:hover { text-decoration: underline; }
.btn-text.danger { color: #dc2626; }

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

.score-section {
  padding: 14px 16px; border: 1px dashed #e0e0e0;
  border-radius: 4px; background: #fafafa;
}
.score-hint { margin: 4px 0 0; font-size: 0.72rem; color: #888; line-height: 1.6; }
.score-hint code {
  padding: 1px 6px; background: #f0f0f0; border-radius: 3px;
  font-family: monospace; font-size: 0.7rem; color: #c2410c;
}

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