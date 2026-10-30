<template>
  <div class="trade-manager" v-loading="loading">
    <header class="module-header">
      <div class="search-bar">
        <input v-model="searchQuery" type="text" placeholder="搜索资源编码、名称或收益..." />
      </div>
      <div class="action-btns">
        <button class="btn-line" @click="openAvatars">头像素材</button>
        <button class="btn-line" @click="openFrames">头像框素材</button>
        <button class="btn-add" @click="handleAddNew">＋ 新增资源</button>
      </div>
    </header>
    <p class="rules-note">经验什么都能买；土豆只能买填了「土豆价」并且是「直接交付」的商品（头衔、头像框、改名卡、配额）。发卡密 / 链接的商品（游戏 CDKey、资源）只能用经验兑换。</p>
    <p v-if="listError" class="error-text" role="alert">{{ listError }} <button class="btn-s" @click="loadItems">重试</button></p>

    <div class="table-card">
      <div class="table-responsive">
        <table class="ink-table">
          <thead>
            <tr>
              <th width="60">ID</th>
              <th width="200">资源定义</th>
              <th width="100">交付 / 效果</th>
              <th width="130">价格 (基数/系数)</th>
              <th width="90">库存</th>
              <th width="60">售出</th>
              <th width="80">状态</th>
              <th width="150" class="text-right">介入操作</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in filteredItems" :key="item.id" :class="{ 'dimmed': !item.isActive }">
              <td class="mono">#{{ String(item.id).padStart(3, '0') }}</td>
              <td>
                <div class="name-info">
                  <span class="name">{{ item.name }}</span>
                  <span class="category-tag">{{ getCategoryLabel(item.category) }}</span>
                  <span v-if="item.benefit" class="benefit-tag">{{ item.benefit }}</span>
                </div>
              </td>
              <td>
                <span class="badge" :class="item.delivery.toLowerCase()">{{ getDeliveryLabel(item.delivery) }}</span>
                <span v-if="item.effect" class="effect-tag">{{ effectLabel(item) }}</span>
              </td>
              <td class="mono">
                <span class="price">{{ item.baseCost }}</span><small> EXP</small>
                <span class="multiplier">/ x{{ item.priceMultiplier }}</span>
                <div v-if="item.potatoCost != null" class="potato-price">{{ item.potatoCost }} 土豆</div>
              </td>
              <td class="mono">
                <template v-if="item.delivery === 'SecretKey'">
                  <span :class="{ 'warning-text': (item.secretsUnused ?? 0) <= 5 }">{{ item.secretsUnused ?? 0 }}</span>
                  <small> / {{ item.secretsTotal ?? 0 }} 卡密</small>
                </template>
                <span v-else :class="{ 'warning-text': item.globalStock !== null && item.globalStock <= 5 }">
                  {{ item.globalStock ?? '∞' }}
                </span>
              </td>
              <td class="mono">{{ item.sold ?? 0 }}</td>
              <td>
                <span class="status-dot" :class="item.isActive ? 'on' : 'off'"></span>
                <span class="status-text">{{ item.isActive ? '流转中' : '静默' }}</span>
              </td>
              <td class="text-right actions">
                <button v-if="item.delivery === 'SecretKey'" class="btn-s" @click="openSecrets(item)">卡密</button>
                <button class="btn-s" @click="handleEdit(item)">编辑</button>
                <button class="btn-s danger" @click="handleToggle(item)">{{ item.isActive ? '下架' : '激活' }}</button>
              </td>
            </tr>
            <tr v-if="!loading && !filteredItems.length"><td colspan="8" class="empty-row">没有商品</td></tr>
          </tbody>
        </table>
      </div>
    </div>

    <Teleport to="body">
      <div 
        v-if="showEditModal" 
        class="modal-mask" 
        @mousedown="handleMaskMouseDown"
        @mouseup="handleMaskMouseUp"
      >
        <div class="modal-container mobile-full workbench-skin editor-surface community-surface tone-admin" @mousedown.stop @mouseup.stop>
          <header class="modal-header studio-heading">
            <div class="header-content">
              <h3>{{ isEdit ? '修订资源参数' : '上架太初资源' }}</h3>
              <p>配置将直接影响全站资源流向与 EXP / 土豆消耗曲线</p>
            </div>
            <button class="close-icon" @click="closeModal">×</button>
          </header>

          <div class="modal-body scroll-y">
            <div class="form-grid">
              <div class="field full">
                <label>资源名称</label>
                <input v-model="form.name" maxlength="50" placeholder="例如：空间上限扩展" />
              </div>
              <div class="field">
                <label>类别</label>
                <select v-model="form.category">
                  <option value="Quota">个人配额 (影响属性)</option>
                  <option value="Asset">数字资产 (限量发放)</option>
                  <option value="Utility">功能项 (一次性消耗)</option>
                  <option value="Social">社交 (头衔、头像框)</option>
                </select>
              </div>
              <div class="field">
                <label>交付模式</label>
                <select v-model="form.delivery" :disabled="!!form.effect">
                  <option value="None">直接交付 (配额 / 装扮 / 改名卡)</option>
                  <option value="Link">静态链接 (网盘或资产地址)</option>
                  <option value="SecretKey">独立密钥池 (游戏 CDKey 等)</option>
                </select>
                <p v-if="form.effect" class="field-hint">头衔、头像框、背景、改名卡只能直接交付</p>
              </div>

              <div class="field">
                <label>商品效果</label>
                <select v-model="effectModel">
                  <option value="">无（配额 / 卡密 / 链接）</option>
                  <option value="title">头衔</option>
                  <option value="avatar">官方头像（静态 / 动图）</option>
                  <option value="frame">头像框</option>
                  <option value="bg">网站背景</option>
                  <option value="pbg">主页横幅 / 整页背景</option>
                  <option value="rename">改名卡</option>
                </select>
              </div>
              <div class="field">
                <template v-if="form.effect === 'title'">
                  <label>头衔文字 (最多 12 字)</label>
                  <input v-model="form.effectValue" maxlength="12" placeholder="例如：莱茵研究员" />
                </template>
                <template v-else-if="form.effect === 'frame'">
                  <label>头像框样式</label>
                  <select v-model="form.effectValue">
                    <option :value="null" disabled>请选择</option>
                    <option v-for="f in frameOptions()" :key="f.key" :value="f.key">{{ f.name }} · {{ f.desc }}</option>
                  </select>
                </template>
                <template v-else-if="form.effect === 'avatar'">
                  <label>要出售的头像</label>
                  <select v-model="form.effectValue">
                    <option :value="null" disabled>{{ avatarDefs.length ? '请选择' : '还没有官方头像，先点上面「头像素材」上传' }}</option>
                    <option v-for="a in avatarDefs" :key="a.id" :value="String(a.id)">{{ a.animated ? '动图' : '静态' }} · {{ a.name }}{{ a.enabled ? '' : '（已停用）' }}</option>
                  </select>
                </template>
                <template v-else-if="form.effect === 'bg'">
                  <label>要出售的官方网站背景</label>
                  <select v-model="form.effectValue">
                    <option :value="null" disabled>{{ presets.length ? '请选择' : '还没有官方背景，先去「背景特权」上传' }}</option>
                    <option v-for="p in presets" :key="p.id" :value="String(p.id)">{{ p.title }}{{ p.enabled ? '' : '（已停用）' }}</option>
                  </select>
                </template>
                <template v-else-if="form.effect === 'pbg'">
                  <label>要出售的主页素材</label>
                  <select v-model="form.effectValue">
                    <option :value="null" disabled>{{ profilePresets.length ? '请选择' : '还没有主页素材，先去「背景特权 → 主页素材」上传' }}</option>
                    <option v-for="p in profilePresets" :key="p.id" :value="String(p.id)">{{ p.slot === 'banner' ? '横幅' : '整页' }} · {{ p.title }}{{ p.enabled ? '' : '（已停用）' }}</option>
                  </select>
                </template>
                <template v-else-if="form.effect === 'rename'">
                  <label>改名卡</label>
                  <p class="field-hint static">买一次得 1 张，在个人中心改名时用掉</p>
                </template>
              </div>
              <div v-if="form.effect === 'avatar' && selectedAvatar" class="field full frame-preview">
                <span class="fp-avatar big"><img :src="selectedAvatar.url" alt="" class="fp-img" /></span>
                <small class="field-hint static">{{ selectedAvatar.animated ? '动图头像' : '静态头像' }}<template v-if="selectedAvatar.authorName"> · 作者 {{ selectedAvatar.authorName }}</template> · 设成商品后要先兑换才能用；没上架的官方头像所有人免费用</small>
              </div>
              <div v-if="form.effect === 'frame' && form.effectValue" class="field full frame-preview">
                <AvatarFrame :frame="form.effectValue"><span class="fp-avatar">预览</span></AvatarFrame>
              </div>
              <div v-if="(form.effect === 'bg' || form.effect === 'pbg') && selectedPreset" class="field full bg-preview" :class="{ banner: (selectedPreset as ProfilePreset).slot === 'banner' }" :style="presetThumb(selectedPreset)">
                <span>{{ selectedPreset.title }}<template v-if="selectedPreset.kind === 'video'"> · 动态</template><template v-if="selectedPreset.authorName"> · 作者 {{ selectedPreset.authorName }}</template></span>
              </div>
              <p v-if="form.effect === 'bg' || form.effect === 'pbg'" class="field full field-hint static">设成商品后，这个背景要先兑换才能使用（用户可以先预览）；已经在用它的人、原作者不受影响。</p>

              <div class="field">
                <label>基础经验价 (Base EXP)</label>
                <input type="number" min="0" v-model.number="form.baseCost" />
              </div>
              <div class="field">
                <label>土豆价 (空 = 不能用土豆买)</label>
                <input type="number" min="0" v-model="potatoModel" :disabled="form.delivery !== 'None'" :placeholder="form.delivery !== 'None' ? '卡密 / 链接只能用经验' : '例如 5000'" />
              </div>
              <div class="field">
                <label>增长系数 (1 = 固定价)</label>
                <input type="number" step="0.01" min="1" max="10" v-model.number="form.priceMultiplier" />
              </div>
              <div class="field">
                <label>视觉位阶 (Weight，越大越靠前)</label>
                <input type="number" v-model.number="form.baseWeight" />
              </div>
              <div class="field">
                <label>{{ form.delivery === 'SecretKey' ? '库存 (按卡密数量，不用填)' : '全局库存 (空为无限)' }}</label>
                <input type="number" min="0" v-model="stockModel" placeholder="∞" :disabled="form.delivery === 'SecretKey'" />
              </div>
              <div class="field">
                <label>核心收益简述 (最多 20 字)</label>
                <input v-model="form.benefit" maxlength="20" placeholder="例如：空间 +1" />
              </div>

              <div class="field full" v-if="form.delivery === 'Link' || (form.category === 'Quota' && form.delivery === 'None' && !form.effect)">
                <label>
                  {{ form.delivery === 'Link' ? '资源交付数据 (链接)' : '属性注入指令 (反射 Payload)' }}
                </label>
                <textarea 
                  v-model="form.staticPayload" 
                  rows="2" 
                  :placeholder="form.delivery === 'Link'
                    ? '输入 itch.io 资源链接或网盘地址...'
                    : '格式: 字段名:增量。例如 MaxCardsPerWorld:20'"
                ></textarea>
                <p class="field-hint" v-if="form.delivery !== 'Link'">
                  * 此指令将直接修改 UserStats 表中的对应维度上限（不能改经验）。可用字段（点一下填入）：
                </p>
                <div class="payload-chips" v-if="form.delivery !== 'Link'">
                  <button v-for="f in PAYLOAD_FIELDS" :key="f.key" type="button" class="payload-chip" :title="f.tip" @click="form.staticPayload = `${f.key}:${f.step}`">{{ f.label }} <code>{{ f.key }}</code></button>
                </div>
              </div>
              <p v-if="form.delivery === 'SecretKey'" class="field full field-hint static">保存后在列表里点「卡密」补充 CDKey，一行一个。</p>

              <div class="field full">
                <label>资源详细描述 (最多 200 字)</label>
                <textarea v-model="form.description" rows="3" maxlength="200" placeholder="详细说明资源功能..."></textarea>
              </div>
            </div>
          </div>

          <footer class="modal-footer">
            <p v-if="formError" class="error-text" role="alert">{{ formError }}</p>
            <button class="btn-confirm" :disabled="saving" @click="submitForm">{{ saving ? '保存中…' : '确认并发布到太初系统' }}</button>
          </footer>
        </div>
      </div>

      <!-- 卡密池 -->
      <div v-if="secretItem" class="modal-mask" @click.self="secretItem = null">
        <div class="modal-container mobile-full workbench-skin editor-surface community-surface tone-admin">
          <header class="modal-header studio-heading">
            <div class="header-content">
              <h3>卡密池 · {{ secretItem.name }}</h3>
              <p>未发出 {{ secretsUnused }} 个 / 共 {{ secrets.length }} 个（最多显示 500 条）</p>
            </div>
            <button class="close-icon" @click="secretItem = null">×</button>
          </header>
          <div class="field full">
            <label>补充卡密（一行一个，重复的会自动跳过）</label>
            <textarea v-model="restockText" rows="5" placeholder="XXXX-XXXX-XXXX&#10;YYYY-YYYY-YYYY"></textarea>
          </div>
          <div class="secret-actions">
            <span class="field-hint static">本次 {{ restockLines.length }} 个</span>
            <button class="btn-add" :disabled="restocking || !restockLines.length" @click="doRestock">{{ restocking ? '补充中…' : '补充' }}</button>
          </div>
          <p v-if="secretMsg" class="secret-msg" :class="{ 'error-text': secretErr }">{{ secretMsg }}</p>
          <div class="secret-list">
            <p v-if="secretsLoading" class="field-hint static">读取中…</p>
            <div v-for="s in secrets" :key="s.id" class="secret-row" :class="{ used: s.isUsed }">
              <code>{{ s.code }}</code>
              <span>{{ s.isUsed ? `已发给 ${s.assignedTo ?? '?'}${s.assignedAt ? ' · ' + new Date(s.assignedAt).toLocaleDateString('zh-CN') : ''}` : '未发出' }}</span>
              <button v-if="!s.isUsed" class="btn-s danger" @click="removeSecret(s)">删除</button>
            </div>
            <p v-if="!secretsLoading && !secrets.length" class="field-hint static">还没有卡密</p>
          </div>
        </div>
      </div>
      <!-- 官方头像素材 -->
      <div v-if="showAvatars" class="modal-mask" @click.self="showAvatars = false">
        <div class="modal-container mobile-full workbench-skin editor-surface community-surface tone-admin frames-modal">
          <header class="modal-header studio-heading">
            <div class="header-content">
              <h3>官方头像素材</h3>
              <p>用户自己只能上传静态头像；GIF、动态 WebP、APNG 这类动图头像只能用这里的官方头像。没上架成商品的官方头像所有人免费用，上架后要先兑换。</p>
            </div>
            <button class="close-icon" @click="showAvatars = false">×</button>
          </header>
          <p v-if="avatarMsg" class="secret-msg" :class="{ 'error-text': avatarErr }">{{ avatarMsg }}</p>
          <ul class="frame-grid">
            <li v-for="a in avatarDefs" :key="a.id" :class="{ off: !a.enabled }">
              <span class="fp-avatar big"><img :src="a.url" alt="" class="fp-img" loading="lazy" /></span>
              <b>{{ a.name }}</b>
              <small>{{ a.animated ? '动图' : '静态' }}<template v-if="a.authorName"> · 作者 {{ a.authorName }}</template><template v-if="!a.enabled"> · 已停用</template></small>
              <span class="frame-ops"><button class="btn-s" @click="editAvatar(a)">编辑</button><button class="btn-s danger" @click="removeAvatar(a)">删除</button></span>
            </li>
            <li v-if="!avatarDefs.length" class="empty-row">还没有上传过官方头像</li>
          </ul>
          <form class="frame-form" @submit.prevent="saveAvatar">
            <h4>{{ avatarForm.id ? `编辑「${avatarForm.name}」` : '上传新的官方头像' }}</h4>
            <div class="frame-edit">
              <div class="frame-preview-box">
                <span v-if="avatarForm.url" class="fp-avatar huge"><img :src="avatarForm.url" alt="" class="fp-img" /></span>
                <span v-else class="fp-empty">还没有图片</span>
              </div>
              <div class="frame-fields">
                <input ref="avatarInput" type="file" accept="image/png,image/jpeg,image/webp,image/gif,image/apng" hidden @change="pickAvatar" />
                <button type="button" class="btn-line" :disabled="avatarUploading" @click="avatarInput?.click()">{{ avatarUploading ? '上传中…' : avatarForm.url ? '换一张' : '上传头像图片' }}</button>
                <small class="field-hint static">建议正方形，GIF / 动态 WebP / APNG 会自动识别为动图，最大 8MB。</small>
                <label>名称<input v-model.trim="avatarForm.name" maxlength="40" placeholder="例如：蹦跶的柴犬" /></label>
                <label class="check"><input v-model="avatarForm.animated" type="checkbox" /> 动图（上传时自动识别）</label>
                <label>排序<input v-model.number="avatarForm.sortOrder" type="number" /></label>
                <label class="check"><input v-model="avatarForm.enabled" type="checkbox" /> 对用户显示</label>
                <AuthorPicker v-model="avatarAuthor" :search="TradeApi.findUsers" />
                <small class="field-hint static">社区投稿被采用的头像填上原作者，作者自动免费拥有。</small>
              </div>
            </div>
            <div class="secret-actions">
              <button v-if="avatarForm.id" type="button" class="btn-s" @click="resetAvatarForm">取消编辑</button>
              <span class="grow"></span>
              <button type="submit" class="btn-add" :disabled="avatarSaving || !avatarForm.url || !avatarForm.name">{{ avatarSaving ? '保存中…' : '保存' }}</button>
            </div>
          </form>
        </div>
      </div>

      <!-- 头像框素材（图片框） -->
      <div v-if="showFrames" class="modal-mask" @click.self="showFrames = false">
        <div class="modal-container mobile-full workbench-skin editor-surface community-surface tone-admin frames-modal">
          <header class="modal-header studio-heading">
            <div class="header-content">
              <h3>头像框素材</h3>
              <p>上传透明背景的 PNG / GIF / WebP，中间留空，框会以头像为中心放大套在外面。上传后在「新增资源 → 商品效果 → 头像框」里选它上架。</p>
            </div>
            <button class="close-icon" @click="showFrames = false">×</button>
          </header>
          <p v-if="frameMsg" class="secret-msg" :class="{ 'error-text': frameErr }">{{ frameMsg }}</p>
          <ul class="frame-grid">
            <li v-for="f in frameDefs" :key="f.id" :class="{ off: !f.enabled }">
              <AvatarFrame :frame="f.key"><span class="fp-avatar">预览</span></AvatarFrame>
              <b>{{ f.name }}</b>
              <small>{{ f.key }} · ×{{ f.scale }}<template v-if="f.authorName"> · 作者 {{ f.authorName }}</template><template v-if="!f.enabled"> · 已停用</template></small>
              <span class="frame-ops"><button class="btn-s" @click="editFrame(f)">编辑</button><button class="btn-s danger" @click="removeFrame(f)">删除</button></span>
            </li>
            <li v-if="!frameDefs.length" class="empty-row">还没有上传过图片框</li>
          </ul>
          <form class="frame-form" @submit.prevent="saveFrame">
            <h4>{{ frameForm.id ? `编辑「${frameForm.name}」` : '上传新的头像框' }}</h4>
            <div class="frame-edit">
              <div class="frame-preview-box">
                <AvatarFrame v-if="frameForm.url" :frame="null"><span class="fp-avatar big">头像</span><img class="fp-overlay" :src="frameForm.url" alt="" :style="{ '--af-scale': String(frameForm.scale) }" /></AvatarFrame>
                <span v-else class="fp-empty">还没有框图</span>
                <span v-if="frameForm.url" class="fp-small"><AvatarFrame :frame="null"><span class="fp-avatar small">小</span><img class="fp-overlay" :src="frameForm.url" alt="" :style="{ '--af-scale': String(frameForm.scale) }" /></AvatarFrame></span>
              </div>
              <div class="frame-fields">
                <input ref="frameInput" type="file" accept="image/png,image/gif,image/webp,image/apng" hidden @change="pickFrame" />
                <button type="button" class="btn-line" :disabled="frameUploading" @click="frameInput?.click()">{{ frameUploading ? `上传中 ${progress}%` : frameForm.url ? '换一张框图' : '上传框图' }}</button>
                <label>名称<input v-model.trim="frameForm.name" maxlength="40" placeholder="例如：赤色烈焰" /></label>
                <label>框的大小 ×{{ frameForm.scale.toFixed(2) }}<small>框图相对头像放大多少倍，看左边预览调到刚好套住</small><input v-model.number="frameForm.scale" type="range" min="1" max="2" step="0.02" /></label>
                <label>排序<input v-model.number="frameForm.sortOrder" type="number" /></label>
                <label class="check"><input v-model="frameForm.enabled" type="checkbox" /> 可以上架</label>
                <AuthorPicker v-model="frameAuthor" :search="TradeApi.findUsers" />
                <small class="field-hint static">填了原作者，作者账号会自动免费拥有这个头像框。</small>
              </div>
            </div>
            <div class="secret-actions">
              <button v-if="frameForm.id" type="button" class="btn-s" @click="resetFrameForm">取消编辑</button>
              <span class="grow"></span>
              <button type="submit" class="btn-add" :disabled="frameSaving || !frameForm.url || !frameForm.name">{{ frameSaving ? '保存中…' : '保存' }}</button>
            </div>
          </form>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { TradeApi, type IStoreItem, type IStoreSecret } from '../../../api/Admin';
import AvatarFrame from '@/components/AvatarFrame.vue';
import { frameName, frameOptions, loadImageFrames } from '@/utils/avatarFrames';
import { useCos } from '@/composables/useCos';
import AuthorPicker, { type AuthorRef } from './AuthorPicker.vue';
import type { IAvatarDef, IFrameDef } from '../../../api/Admin';
import { adminAppearanceApi, type BgPreset, type ProfilePreset } from '@/api/appearance';
// 额度类商品能改的上限字段（后端按字段名给 UserStats 加数）
const PAYLOAD_FIELDS = [
  { key: 'MaxWorldCount', step: 1, label: '世界观数量', tip: '可创建的世界观个数' },
  { key: 'MaxCardsPerWorld', step: 20, label: '世界观节点上限', tip: '每个世界观最多多少个节点，对所有世界通用' },
  { key: 'MaxSpaces', step: 1, label: '灵脉空间', tip: '灵脉空间个数' },
  { key: 'MaxNotes', step: 100, label: '灵脉笔记', tip: '灵脉笔记篇数' },
  { key: 'MaxProjectCount', step: 1, label: '协作项目', tip: '可创建的协作项目个数' },
] as const

const loading = ref(false);
const items = ref<IStoreItem[]>([]);
const listError = ref('');
const showEditModal = ref(false);
const isEdit = ref(false);
const searchQuery = ref('');
const saving = ref(false);
const formError = ref('');

const apiError = (e: any, fallback: string) => e?.response?.data?.message || e?.friendlyMessage || fallback;

const getEmptyForm = (): IStoreItem => ({
  id: 0, name: '', category: 'Quota', delivery: 'None',
  baseCost: 1000, priceMultiplier: 1.12, baseWeight: 0, sortOrder: 0,
  globalStock: null, benefit: '', description: '', staticPayload: '', isActive: true,
  potatoCost: null, effect: null, effectValue: null,
});

const form = ref<IStoreItem>(getEmptyForm());
const isMouseDownOnMask = ref(false);

/** 数字输入框清空时是空字符串，这里统一成 null */
const nullableNumber = (key: 'potatoCost' | 'globalStock') => computed({
  get: () => form.value[key] ?? '',
  set: (v: string | number) => {
    const n = v === '' || v === null ? null : Math.floor(Number(v));
    form.value[key] = n === null || Number.isNaN(n) ? null : n;
  },
});
const potatoModel = nullableNumber('potatoCost');
const stockModel = nullableNumber('globalStock');

const effectModel = computed({
  get: () => form.value.effect ?? '',
  set: (v: string) => {
    const effect = (v || null) as IStoreItem['effect'];
    if (effect !== form.value.effect) form.value.effectValue = null;
    form.value.effect = effect;
    if (effect) {
      form.value.delivery = 'None';
      if (form.value.category === 'Quota') form.value.category = effect === 'rename' ? 'Utility' : 'Social';
      if ((effect === 'bg' || effect === 'pbg') && !presets.value.length && !profilePresets.value.length) void loadPresets();
      // 装扮类不需要反射指令
      form.value.staticPayload = '';
    }
  },
});

const handleMaskMouseDown = (e: MouseEvent) => {
  isMouseDownOnMask.value = e.target === e.currentTarget;
};

const handleMaskMouseUp = (e: MouseEvent) => {
  if (isMouseDownOnMask.value && e.target === e.currentTarget) {
    closeModal();
  }
  isMouseDownOnMask.value = false;
};

const loadItems = async () => {
  loading.value = true;
  listError.value = '';
  try {
    items.value = await TradeApi.getAllItems();
  } catch (e) {
    listError.value = apiError(e, '商品列表读取失败');
  } finally {
    loading.value = false;
  }
};

onMounted(() => { void loadItems(); void loadPresets(); void loadAvatarDefs(); });

const filteredItems = computed(() => {
  const query = searchQuery.value.trim().toLowerCase();
  return items.value.filter(i =>
    !query ||
    String(i.id).includes(query) ||
    i.name.toLowerCase().includes(query) ||
    (i.benefit || '').toLowerCase().includes(query) ||
    (i.effectValue || '').toLowerCase().includes(query)
  );
});

const getDeliveryLabel = (t: string) => ({ 'None':'直接', 'Link':'链接', 'SecretKey':'密钥' }[t] || t);
const effectLabel = (i: IStoreItem) =>
  i.effect === 'title' ? `头衔：${i.effectValue}`
    : i.effect === 'frame' ? `头像框：${frameName(i.effectValue)}`
    : i.effect === 'bg' ? `网站背景：${presets.value.find(p => String(p.id) === i.effectValue)?.title ?? '#' + i.effectValue}`
    : i.effect === 'pbg' ? `主页素材：${profilePresets.value.find(p => String(p.id) === i.effectValue)?.title ?? '#' + i.effectValue}`
    : i.effect === 'avatar' ? `头像：${avatarDefs.value.find(a => String(a.id) === i.effectValue)?.name ?? '#' + i.effectValue}`
    : i.effect === 'rename' ? '改名卡' : '';

// 官方背景（出售背景时选择）
const presets = ref<BgPreset[]>([]);
const profilePresets = ref<ProfilePreset[]>([]);
const loadPresets = async () => {
  const [a, b] = await Promise.allSettled([adminAppearanceApi.presets(), adminAppearanceApi.profilePresets()]);
  presets.value = a.status === 'fulfilled' ? a.value : [];
  profilePresets.value = b.status === 'fulfilled' ? b.value : [];
};
const selectedPreset = computed<BgPreset | ProfilePreset | null>(() =>
  form.value.effect === 'bg' ? presets.value.find(p => String(p.id) === form.value.effectValue) ?? null
    : form.value.effect === 'pbg' ? profilePresets.value.find(p => String(p.id) === form.value.effectValue) ?? null : null);
const presetThumb = (p: BgPreset) => {
  const url = p.kind === 'video' ? p.poster : p.url;
  return url ? { backgroundImage: `url(${JSON.stringify(url)})` } : undefined;
};

const getCategoryLabel = (c: string) => ({ 
  'Quota': '维度额度', 
  'Asset': '灵脉资产', 
  'Utility': '功能碎片', 
  'Social': '因果社交' 
}[c] || c);

const handleAddNew = () => {
  isEdit.value = false;
  form.value = getEmptyForm();
  formError.value = '';
  showEditModal.value = true;
};

const handleEdit = (item: IStoreItem) => {
  isEdit.value = true;
  form.value = { ...item, staticPayload: item.staticPayload ?? '' };
  formError.value = '';
  showEditModal.value = true;
};

const closeModal = () => {
  if (!saving.value) showEditModal.value = false;
};

const handleToggle = async (item: IStoreItem) => {
  try {
    const res = await TradeApi.toggleStatus(item.id);
    item.isActive = res.isActive;
  } catch (err) {
    listError.value = apiError(err, '状态切换失败');
  }
};

const submitForm = async () => {
  if (saving.value) return;
  const f = form.value;
  formError.value = '';
  if (!f.name.trim()) { formError.value = '请填写资源名称'; return; }
  if (f.effect === 'title' && !(f.effectValue || '').trim()) { formError.value = '请填写头衔文字'; return; }
  if (f.effect === 'frame' && !f.effectValue) { formError.value = '请选择头像框样式'; return; }
  if ((f.effect === 'bg' || f.effect === 'pbg') && !f.effectValue) { formError.value = '请选择要出售的背景'; return; }
  if (f.effect === 'avatar' && !f.effectValue) { formError.value = '请选择要出售的头像'; return; }
  const payload: Partial<IStoreItem> = {
    ...f,
    baseCost: Math.max(0, Math.floor(Number(f.baseCost) || 0)),
    priceMultiplier: Number(f.priceMultiplier) || 1,
    baseWeight: Math.floor(Number(f.baseWeight) || 0),
    sortOrder: Math.floor(Number(f.sortOrder) || 0),
    potatoCost: f.delivery === 'None' ? f.potatoCost : null,
    globalStock: f.delivery === 'SecretKey' ? null : f.globalStock,
    staticPayload: f.staticPayload || null,
  };
  delete payload.secretsTotal; delete payload.secretsUnused; delete payload.sold; delete payload.createdAt;
  saving.value = true;
  try {
    if (isEdit.value) {
      await TradeApi.updateItem(f.id, payload);
    } else {
      await TradeApi.createItem(payload);
    }
    showEditModal.value = false;
    form.value = getEmptyForm();
    await loadItems();
  } catch (err) {
    formError.value = apiError(err, '保存失败，请检查填写的内容');
  } finally {
    saving.value = false;
  }
};

// ---------- 卡密池 ----------
const secretItem = ref<IStoreItem | null>(null);
const secrets = ref<IStoreSecret[]>([]);
const secretsLoading = ref(false);
const restockText = ref('');
const restocking = ref(false);
const secretMsg = ref('');
const secretErr = ref(false);
const restockLines = computed(() => [...new Set(restockText.value.split(/\r?\n/).map(s => s.trim()).filter(Boolean))]);
const secretsUnused = computed(() => secrets.value.filter(s => !s.isUsed).length);

const loadSecrets = async () => {
  if (!secretItem.value) return;
  secretsLoading.value = true;
  try { secrets.value = await TradeApi.secrets(secretItem.value.id); }
  catch (e) { secretErr.value = true; secretMsg.value = apiError(e, '卡密读取失败'); }
  finally { secretsLoading.value = false; }
};

const openSecrets = (item: IStoreItem) => {
  secretItem.value = item;
  secrets.value = [];
  restockText.value = '';
  secretMsg.value = '';
  secretErr.value = false;
  void loadSecrets();
};

const doRestock = async () => {
  const item = secretItem.value;
  if (!item || restocking.value || !restockLines.value.length) return;
  restocking.value = true;
  secretMsg.value = '';
  try {
    const r = await TradeApi.restockSecrets(item.id, restockLines.value);
    secretErr.value = false;
    secretMsg.value = `新增 ${r.added} 个${r.skipped ? `，跳过重复 ${r.skipped} 个` : ''}，现在未发出 ${r.unused} 个`;
    restockText.value = '';
    await Promise.all([loadSecrets(), loadItems()]);
  } catch (e) {
    secretErr.value = true;
    secretMsg.value = apiError(e, '补充失败');
  } finally { restocking.value = false; }
};

// ---------- 官方头像素材 ----------
const showAvatars = ref(false);
const avatarDefs = ref<IAvatarDef[]>([]);
const avatarInput = ref<HTMLInputElement | null>(null);
const avatarUploading = ref(false);
const avatarSaving = ref(false);
const avatarMsg = ref('');
const avatarErr = ref(false);
const emptyAvatar = () => ({ id: 0, name: '', url: '', animated: false, sortOrder: 0, enabled: true });
const avatarForm = ref(emptyAvatar());
const avatarAuthor = ref<AuthorRef | null>(null);
const selectedAvatar = computed(() => form.value.effect === 'avatar' ? avatarDefs.value.find(a => String(a.id) === form.value.effectValue) ?? null : null);

const sayAvatar = (text: string, isError = false) => { avatarMsg.value = text; avatarErr.value = isError; };
const loadAvatarDefs = async () => {
  try { avatarDefs.value = await TradeApi.avatars(); } catch (e) { sayAvatar(apiError(e, '读取官方头像失败'), true); }
};
const openAvatars = () => { showAvatars.value = true; sayAvatar(''); void loadAvatarDefs(); };
const resetAvatarForm = () => { avatarForm.value = emptyAvatar(); avatarAuthor.value = null; };
const editAvatar = (a: IAvatarDef) => {
  avatarForm.value = { id: a.id, name: a.name, url: a.url, animated: a.animated, sortOrder: a.sortOrder, enabled: a.enabled };
  avatarAuthor.value = a.authorId ? { id: a.authorId, name: a.authorName ?? '' } : null;
};
const pickAvatar = async (event: Event) => {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  input.value = '';
  if (!file) return;
  if (file.size > 8 * 1024 * 1024) return sayAvatar('头像图片不能超过 8MB', true);
  avatarUploading.value = true;
  sayAvatar('');
  try {
    const r = await TradeApi.uploadAvatar(file);
    avatarForm.value.url = r.url;
    avatarForm.value.animated = r.animated;
    if (!avatarForm.value.name) avatarForm.value.name = file.name.replace(/\.[^.]+$/, '').slice(0, 40);
  } catch (e) { sayAvatar(apiError(e, '上传失败'), true); } finally { avatarUploading.value = false; }
};
const saveAvatar = async () => {
  if (avatarSaving.value) return;
  avatarSaving.value = true;
  try {
    const a = avatarForm.value;
    await TradeApi.saveAvatar({ name: a.name, url: a.url, animated: a.animated, authorId: avatarAuthor.value?.id ?? null, sortOrder: a.sortOrder, enabled: a.enabled }, a.id || undefined);
    sayAvatar(a.id ? '已保存' : '已添加。不上架就是免费头像；想卖的话去「新增资源 → 商品效果 → 官方头像」');
    resetAvatarForm();
    await loadAvatarDefs();
  } catch (e) { sayAvatar(apiError(e, '保存失败'), true); } finally { avatarSaving.value = false; }
};
const removeAvatar = async (a: IAvatarDef) => {
  if (!window.confirm(`删除官方头像「${a.name}」？已经在用的人不受影响，但不能再选。`)) return;
  try { await TradeApi.deleteAvatar(a.id); sayAvatar('已删除'); await loadAvatarDefs(); }
  catch (e) { sayAvatar(apiError(e, '删除失败'), true); }
};

// ---------- 图片头像框素材 ----------
const { uploadFile, progress } = useCos();
const showFrames = ref(false);
const frameDefs = ref<IFrameDef[]>([]);
const frameInput = ref<HTMLInputElement | null>(null);
const frameUploading = ref(false);
const frameSaving = ref(false);
const frameMsg = ref('');
const frameErr = ref(false);
const emptyFrame = () => ({ id: 0, name: '', url: '', scale: 1.4, sortOrder: 0, enabled: true });
const frameForm = ref(emptyFrame());
const frameAuthor = ref<AuthorRef | null>(null);
const MAX_FRAME = 3 * 1024 * 1024;

const sayFrame = (text: string, isError = false) => { frameMsg.value = text; frameErr.value = isError; };
const loadFrameDefs = async () => {
  try { frameDefs.value = await TradeApi.frames(); } catch (e) { sayFrame(apiError(e, '读取头像框素材失败'), true); }
};
const openFrames = () => { showFrames.value = true; sayFrame(''); void loadFrameDefs(); };
const resetFrameForm = () => { frameForm.value = emptyFrame(); frameAuthor.value = null; };
const editFrame = (f: IFrameDef) => {
  frameForm.value = { id: f.id, name: f.name, url: f.url, scale: f.scale, sortOrder: f.sortOrder, enabled: f.enabled };
  frameAuthor.value = f.authorId ? { id: f.authorId, name: f.authorName ?? '' } : null;
};
const pickFrame = async (event: Event) => {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  input.value = '';
  if (!file) return;
  // 原样上传，保留透明和动画
  if (!/^image\/(png|gif|webp|apng)$/.test(file.type)) return sayFrame('只支持 PNG、GIF、WebP（要透明背景）', true);
  if (file.size > MAX_FRAME) return sayFrame('框图不能超过 3MB', true);
  frameUploading.value = true;
  sayFrame('');
  try {
    const { url } = await uploadFile(file, 'frames');
    frameForm.value.url = url;
    if (!frameForm.value.name) frameForm.value.name = file.name.replace(/\.[^.]+$/, '').slice(0, 40);
  } catch { sayFrame('上传失败，请稍后再试', true); } finally { frameUploading.value = false; }
};
const saveFrame = async () => {
  if (frameSaving.value) return;
  frameSaving.value = true;
  try {
    const f = frameForm.value;
    await TradeApi.saveFrame({ name: f.name, url: f.url, scale: f.scale, authorId: frameAuthor.value?.id ?? null, sortOrder: f.sortOrder, enabled: f.enabled }, f.id || undefined);
    sayFrame(f.id ? '已保存' : '已添加，可以去新增资源里上架了');
    resetFrameForm();
    await Promise.all([loadFrameDefs(), loadImageFrames(true)]);
  } catch (e) { sayFrame(apiError(e, '保存失败'), true); } finally { frameSaving.value = false; }
};
const removeFrame = async (f: IFrameDef) => {
  if (!window.confirm(`删除头像框「${f.name}」？戴着它的人会自动取下，已经卖出去的也不再显示。`)) return;
  try { await TradeApi.deleteFrame(f.id); sayFrame('已删除'); await Promise.all([loadFrameDefs(), loadImageFrames(true)]); }
  catch (e) { sayFrame(apiError(e, '删除失败'), true); }
};

const removeSecret = async (s: IStoreSecret) => {
  const item = secretItem.value;
  if (!item || !window.confirm(`删除卡密 ${s.code}？`)) return;
  try {
    await TradeApi.deleteSecret(item.id, s.id);
    secrets.value = secrets.value.filter(x => x.id !== s.id);
    void loadItems();
  } catch (e) {
    secretErr.value = true;
    secretMsg.value = apiError(e, '删除失败');
  }
};
</script>

<style scoped>
.trade-manager { display: flex; flex-direction: column; gap: 24px; animation: fadeIn 0.4s ease; }
.module-header { display: flex; justify-content: space-between; align-items: center; gap: 20px; }
.search-bar { flex: 1; max-width: 400px; }
.search-bar input { width: 100%; border: 1px solid var(--border-line); padding: 10px 16px; border-radius: 0; outline: none; transition: 0.3s; }
.search-bar input:focus { border-color: var(--border-line); }
.btn-add { background: var(--accent-color); color: var(--on-accent); border: none; padding: 10px 24px; cursor: pointer; font-weight: 600; }
.table-card { background: var(--paper-card); border: 1px solid var(--border-line); border-radius: 0; }
.table-responsive { width: 100%; overflow-x: auto; }
.ink-table { width: 100%; border-collapse: collapse; min-width: 800px; }
.ink-table th { padding: 16px; text-align: left; font-size: 0.7rem; color: var(--text-secondary); text-transform: uppercase; border-bottom: 2px solid var(--border-line); }
.ink-table td { padding: 16px; border-bottom: 1px solid var(--border-line); font-size: 0.85rem; vertical-align: middle; }
.name-info .name { display: block; font-weight: 700; margin-bottom: 2px; }
.category-tag { font-size: 9px; color: var(--accent-color); background: var(--accent-soft); padding: 1px 4px; border-radius: 0; margin-right: 6px; }
.benefit-tag { font-size: 10px; background: var(--bg-sub); color: var(--text-secondary); padding: 2px 6px; border-radius: 0; }
.mono { font-family: "JetBrains Mono", monospace; }
.multiplier { color: var(--text-secondary); font-size: 0.75rem; margin-left: 4px; }
.warning-text { color: var(--danger); font-weight: 800; }
.status-dot { width: 6px; height: 6px; border-radius: 50%; display: inline-block; margin-right: 8px; }
.status-dot.on { background: var(--success); box-shadow: 0 0 8px rgba(52,199,89,0.4); }
.status-dot.off { background: var(--danger); }
.status-text { font-size: 0.75rem; color: var(--text-secondary); }
.btn-s { background: none; border: none; font-size: 0.75rem; font-weight: 700; cursor: pointer; margin-left: 12px; transition: 0.2s; }
.btn-s.danger { color: var(--danger); }
.modal-mask { position: fixed; top: 0; left: 0; width: 100vw; height: 100vh; background: var(--overlay); backdrop-filter: blur(10px); z-index: 999; display: flex; justify-content: center; align-items: center; }
.modal-container { background: var(--paper-card); width: 600px; padding: 40px; border: 1px solid var(--border-line); box-shadow: 30px 30px 0 rgba(0,0,0,0.05); 
  max-height: 90dvh; overflow-y: auto; border-radius: 0;
}
.modal-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 30px; }
.modal-header h3 { font-size: 1.5rem; font-weight: 200; letter-spacing: 1px; }
.modal-header p { font-size: 0.75rem; color: var(--text-secondary); margin-top: 4px; }
.close-icon { background: none; border: none; font-size: 1.5rem; cursor: pointer; color: var(--text-secondary); }
.scroll-y { max-height: 60vh; overflow-y: auto; padding-right: 10px; }
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 24px; }
.field.full { grid-column: span 2; }
.field label { display: block; font-size: 0.65rem; color: var(--text-secondary); text-transform: uppercase; margin-bottom: 8px; font-weight: 700; }
.field input, .field select, .field textarea { width: 100%; border: 1px solid var(--border-line); padding: 10px; font-size: 0.9rem; outline: none; transition: 0.3s; }
.field input:focus { border-color: var(--border-line); }
.field-hint { font-size: 0.65rem; color: var(--accent-color); margin-top: 6px; font-style: italic; }
.btn-confirm { width: 100%; background: var(--accent-color); color: var(--on-accent); border: none; padding: 14px; font-weight: 700; margin-top: 30px; cursor: pointer; }

@media (max-width: 768px) {
  .modal-container.mobile-full { width: 100%; height: 100%; padding: 30px 20px; border: none; box-shadow: none; 
  max-height: 90dvh; overflow-y: auto; border-radius: 0;
}
  .form-grid { grid-template-columns: 1fr; gap: 16px; }
  .field.full { grid-column: span 1; }
}
.action-btns { display: flex; gap: 10px; }
.btn-line { padding: 10px 18px; border: 1px solid var(--accent-color); background: transparent; color: var(--accent-color); font: inherit; font-weight: 600; cursor: pointer; }
.btn-line:disabled { opacity: .55; }
.frames-modal { width: 760px; }
.frame-grid { list-style: none; margin: 0 0 16px; padding: 0; display: grid; grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); gap: 10px; }
.frame-grid li { display: grid; justify-items: center; gap: 6px; padding: 18px 8px 10px; border: 1px solid var(--border-line); text-align: center; }
.frame-grid li.off { opacity: .55; }
.frame-grid li small { font-size: 11px; color: var(--text-secondary); }
.frame-grid li.empty-row { grid-column: 1 / -1; }
.frame-ops { display: flex; gap: 4px; }
.frame-ops .btn-s { margin-left: 0; }
.frame-form { display: grid; gap: 12px; padding-top: 14px; border-top: 1px solid var(--border-line); }
.frame-form h4 { margin: 0; }
.frame-edit { display: grid; grid-template-columns: 220px minmax(0, 1fr); gap: 18px; }
.frame-preview-box { position: relative; display: grid; place-items: center; min-height: 220px; border: 1px dashed var(--border-line); background: var(--bg-main); }
.fp-small { position: absolute; right: 12px; bottom: 12px; }
.fp-avatar.big { width: 96px; height: 96px; }
.fp-avatar.small { width: 32px; height: 32px; font-size: 11px; }
.fp-overlay { position: absolute; left: 50%; top: 50%; width: calc(100% * var(--af-scale)); height: calc(100% * var(--af-scale)); max-width: none; transform: translate(-50%, -50%); object-fit: contain; pointer-events: none; }
.fp-avatar.huge { width: 160px; height: 160px; }
.fp-img { width: 100%; height: 100%; object-fit: cover; }
.fp-avatar { overflow: hidden; }
.fp-empty { color: var(--text-secondary); font-size: 13px; }
.frame-fields { display: grid; gap: 10px; align-content: start; }
.frame-fields label { display: grid; gap: 4px; font-size: 12px; color: var(--text-secondary); }
.frame-fields label small { font-size: 11px; }
.frame-fields label.check { display: flex; align-items: center; gap: 6px; }
.frame-fields input:not([type=checkbox]):not([type=range]) { padding: 8px; border: 1px solid var(--border-line); font: inherit; font-size: 13px; }
.grow { flex: 1; }
@media (max-width: 768px) { .frame-edit { grid-template-columns: 1fr; } }
.rules-note { margin: -12px 0 0; font-size: 12px; color: var(--text-secondary); }
.error-text { margin: 0; color: var(--danger); font-size: 13px; }
.effect-tag { display: block; margin-top: 4px; font-size: 11px; color: var(--accent-color); }
.potato-price { font-size: 0.75rem; color: var(--text-secondary); }
.empty-row { text-align: center; color: var(--text-secondary); }
.field select:disabled, .field input:disabled { opacity: .55; }
.field-hint.static { font-style: normal; color: var(--text-secondary); margin: 0; }
.frame-preview { display: grid; place-items: center; padding: 16px; border: 1px dashed var(--border-line); }
.bg-preview { display: flex; align-items: flex-end; min-height: 140px; padding: 0; background: var(--bg-sub) center / cover no-repeat; border: 1px solid var(--border-line); }
.bg-preview.banner { min-height: 0; aspect-ratio: 3 / 1; }
.bg-preview span { padding: 4px 10px; background: rgb(0 0 0 / 60%); color: #fff; font-size: 12px; }
.fp-avatar { display: grid; place-items: center; width: 56px; height: 56px; background: linear-gradient(135deg, var(--accent-color), var(--signal-mint, #2dd4bf)); color: #fff; font-size: 12px; }
.modal-footer .error-text { margin-top: 16px; }
.btn-confirm:disabled, .btn-add:disabled { opacity: .55; cursor: not-allowed; }
.secret-actions { display: flex; justify-content: space-between; align-items: center; gap: 12px; margin: 12px 0; }
.secret-msg { margin: 0 0 12px; font-size: 13px; color: var(--success); }
.secret-list { display: grid; gap: 4px; max-height: 40vh; overflow-y: auto; border-top: 1px solid var(--border-line); padding-top: 8px; }
.secret-row { display: grid; grid-template-columns: minmax(0, 1fr) auto auto; gap: 10px; align-items: center; padding: 6px 0; border-bottom: 1px dashed var(--border-line); font-size: 12px; }
.secret-row code { font-family: "JetBrains Mono", monospace; overflow-wrap: anywhere; }
.secret-row.used { color: var(--text-secondary); }
@keyframes fadeIn { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }
.payload-chips { display: flex; flex-wrap: wrap; gap: 8px; margin-top: 8px; }
.payload-chip { padding: 5px 10px; border: 1px solid var(--border-line); border-radius: 0; background: var(--bg-main); color: var(--text-primary); font-size: 12px; cursor: pointer; }
.payload-chip:hover { border-color: var(--accent-color); color: var(--accent-color); }
.payload-chip code { margin-left: 4px; opacity: .6; font-size: 11px; }
</style>