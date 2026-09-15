<template>
  <div class="club-game-manager">
    <header class="module-header">
      <div class="header-content">
        <h2 class="page-title">太初俱乐部中枢</h2>
        <p class="md-subtitle">陪玩游戏注册、动态字段编排与打手资质审核</p>
      </div>
    </header>

    <nav class="md-tabs">
      <span
        v-for="tab in TABS"
        :key="tab.id"
        class="tab-item"
        :class="{ active: activeTab === tab.id }"
        @click="activeTab = tab.id"
      >
        {{ tab.label }}
      </span>
    </nav>

    <!-- ================= 1. 游戏库 ================= -->
    <section v-if="activeTab === 'games'" class="tab-panel">
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
                <button class="btn-text" @click="jumpToFields(g)">字段配置</button>
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
    </section>

    <!-- ================= 2. 字段编排 ================= -->
    <section v-if="activeTab === 'fields'" class="tab-panel">
      <div class="panel-toolbar">
        <div class="game-picker">
          <label>当前编辑游戏：</label>
          <select v-model="currentGameCode" class="ink-select" @change="loadFields">
            <option v-for="g in games" :key="g.code" :value="g.code">
              {{ g.name }} ({{ g.code }})
            </option>
          </select>
        </div>
        <button
          class="btn-primary"
          :disabled="!currentGameCode"
          @click="openFieldModal()"
        >
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
              <td>
                <span class="type-tag">{{ f.type }}</span>
              </td>
              <td class="text-muted ellipsis">
                <template v-if="f.type === 'select'">
                  {{ parseOptions(f.optionsJson).join(' / ') || '—' }}
                </template>
                <template v-else>
                  {{ f.placeholder || '—' }}
                </template>
              </td>
              <td>
                <span v-if="f.isScoreDimension" class="score-flag">★ 参与</span>
                <span v-else class="text-muted">—</span>
              </td>
              <td>
                <span :class="['req-dot', f.required ? 'yes' : 'no']"></span>
              </td>
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
    </section>

    <!-- ================= 3. 订单类型 ================= -->
    <section v-if="activeTab === 'orderTypes'" class="tab-panel">
      <div class="panel-toolbar">
        <div class="game-picker">
          <label>当前游戏：</label>
          <select v-model="orderTypeGameCode" class="ink-select" @change="loadOrderTypes">
            <option v-for="g in games" :key="g.code" :value="g.code">
              {{ g.name }} ({{ g.code }})
            </option>
          </select>
        </div>
        <button
          class="btn-primary"
          :disabled="!orderTypeGameCode"
          @click="openOrderTypeModal()"
        >
          + 新增订单类型
        </button>
      </div>

      <div v-if="orderTypes.length > 0" class="stats-bar">
        <span class="stat"><i class="sdot active"></i> 在售 <b>{{ statActive }}</b></span>
        <span class="stat"><i class="sdot upcoming"></i> 未开始 <b>{{ statUpcoming }}</b></span>
        <span class="stat"><i class="sdot expired"></i> 已过期 <b>{{ statExpired }}</b></span>
        <span class="stat"><i class="sdot disabled"></i> 已停用 <b>{{ statDisabled }}</b></span>
      </div>

      <div class="table-card">
        <table class="ink-table">
          <thead>
            <tr>
              <th width="60">上架</th>
              <th width="120">代码</th>
              <th width="200">显示名</th>
              <th width="100">状态</th>
              <th width="140">价格档位</th>
              <th width="160">有效期</th>
              <th width="220" class="text-right">操作</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="t in orderTypes"
              :key="t.id"
              :class="{ 'row-disabled': !t.isActive }"
            >
              <td>
                <label class="switch">
                  <input type="checkbox" :checked="t.isActive" @change="toggleActive(t)" />
                  <span class="slider"></span>
                </label>
              </td>

              <td class="mono">{{ t.code }}</td>

              <td>
                <div class="ot-name">{{ t.name }}</div>
                <div v-if="t.description" class="ot-desc">{{ t.description }}</div>
              </td>

              <td>
                <span :class="['status-pill', getStatus(t)]">
                  {{ statusLabel(getStatus(t)) }}
                </span>
              </td>

              <td class="mono price-preview">{{ priceRange(t) }}</td>

              <td>
                <span class="editable-time" @click="openTimeModal(t)">
                  {{ formatTimeRange(t.startAt, t.endAt) }}
                </span>
              </td>

              <td class="text-right actions">
                <button class="btn-text" @click="openOrderTypeModal(t)">编辑</button>
                <button class="btn-text" @click="copyOrderType(t)">复制</button>
                <button class="btn-text danger" @click="handleDeleteOrderType(t)">删除</button>
              </td>
            </tr>
            <tr v-if="orderTypes.length === 0">
              <td colspan="7" class="empty-cell">
                {{ orderTypeGameCode ? '该游戏暂无订单类型，点击右上角新增' : '请先选择一款游戏' }}
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <!-- ================= 4. 打手审核 ================= -->
    <section v-if="activeTab === 'operators'" class="tab-panel">
      <div class="panel-toolbar">
        <div class="filter-row">
          <input
            v-model="operatorSearch"
            @keyup.enter="loadOperators"
            class="ink-input"
            placeholder="搜索昵称 / 联系方式 / GUID (回车确认)..."
          />
          <select v-model="operatorStatusFilter" class="ink-select" @change="loadOperators">
            <option value="">全部状态</option>
            <option value="pending">待审核</option>
            <option value="reviewing">复核中</option>
            <option value="approved">已通过</option>
            <option value="rejected">已拒绝</option>
            <option value="banned">已封禁</option>
          </select>
        </div>
        <button class="btn-refresh" @click="loadOperators" :disabled="loadingOperators">
          {{ loadingOperators ? '同步中...' : '刷新列表' }}
        </button>
      </div>

      <div class="operator-grid">
        <div v-for="op in operators" :key="op.userId" class="operator-card">
          <div class="op-head">
            <div class="op-avatar">{{ (op.nickname || '?').substring(0, 1) }}</div>
            <div class="op-meta">
              <div class="op-name">{{ op.nickname || '未命名打手' }}</div>
              <div class="op-contact mono">
                {{ op.contactType }} / {{ op.contactValue }}
              </div>
            </div>
            <span :class="['status-badge', op.auditStatus]">
              {{ STATUS_LABEL[op.auditStatus] || op.auditStatus }}
            </span>
          </div>

          <div class="op-reputation">
            <span>信誉分</span>
            <b :class="op.reputation >= 90 ? 'good' : 'bad'">{{ op.reputation }}</b>
            <span class="divider">|</span>
            <span>累计单量</span>
            <b>{{ op.totalOrders }}</b>
          </div>

          <div class="op-games">
            <div
              v-for="sk in op.gameSkills"
              :key="sk.id"
              class="skill-chip"
              :class="sk.auditStatus"
            >
              <span class="game-name">{{ sk.gameName || sk.gameCode }}</span>
              <span class="skill-status">{{ STATUS_LABEL[sk.auditStatus] || sk.auditStatus }}</span>
            </div>
            <span v-if="!op.gameSkills?.length" class="no-skill">暂未申报游戏</span>
          </div>

          <div class="op-actions">
            <button class="btn-text" @click="openOperatorModal(op)">详情 / 审核</button>
          </div>
        </div>

        <div v-if="operators.length === 0 && !loadingOperators" class="empty-hint">
          没有符合条件的打手申请
        </div>
      </div>
    </section>

    <!-- ================= 弹窗：游戏编辑 ================= -->
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
                placeholder="· 出高价值物资必须给板板&#10;· 打手禁止私下加老板联系方式&#10;· 绝密模式必须五套入场"
              ></textarea>
              <p class="score-hint">
                该游戏所有订单共用。前台会在每个订单类型下显示。
              </p>
            </div>

            <!-- ⭐ 系统评分维度勾选清单 -->
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
                勾选后会作为该游戏所有打手雷达图的维度。此外，在「字段编排」中勾选了「★ 参与雷达图评分」的字段也会自动成为维度。
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

    <!-- ================= 弹窗：字段编辑 ================= -->
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

    <!-- ================= 弹窗：订单类型编辑 ================= -->
    <Teleport to="body">
      <div v-if="showOrderTypeModal" class="modal-mask">
        <div class="modal-container scroll-y">
          <header class="modal-header">
            <h3>{{ orderTypeForm.isEdit ? '编辑订单类型' : '新增订单类型' }}</h3>
            <button class="close-icon" @click="closeOrderTypeModal">×</button>
          </header>

          <div class="form-grid">
            <div class="field">
              <label>代码 (Code) *</label>
              <input
                v-model="orderTypeForm.code"
                :disabled="orderTypeForm.isEdit"
                class="ink-input"
                placeholder="如 escort / fun / gamble"
              />
            </div>
            <div class="field">
              <label>显示名 *</label>
              <input v-model="orderTypeForm.name" class="ink-input" placeholder="如 护航单" />
            </div>
            <div class="field full">
              <label>说明</label>
              <textarea v-model="orderTypeForm.description" class="ink-input" rows="2"></textarea>
            </div>
            <div class="field">
              <label>计价方式</label>
              <select v-model="orderTypeForm.pricingMode" class="ink-input">
                <option value="fixed">fixed 一口价</option>
                <option value="per-round">per-round 按局</option>
                <option value="per-hour">per-hour 按小时</option>
                <option value="tiered">tiered 阶梯价</option>
              </select>
            </div>
            <div class="field">
              <label>排序权重</label>
              <input type="number" v-model.number="orderTypeForm.sortOrder" class="ink-input" />
            </div>
            <div class="field full">
              <label>下单参数结构 (JSON Schema)</label>
              <textarea
                v-model="orderTypeForm.paramsSchemaJson"
                class="ink-input mono"
                rows="4"
                placeholder='{"保底":{"type":"select","options":["2588W","4588W"]}}'
              ></textarea>
              <p class="score-hint">
                描述此订单类型需要老板填写的参数，用于下单时动态渲染表单。可以为空。
              </p>
            </div>
            <div class="field full">
              <label>价格映射 (JSON)</label>
              <textarea
                v-model="orderTypeForm.pricingJson"
                class="ink-input mono"
                rows="3"
                placeholder='{"2588W":388,"4588W":688,"7188W":1288,"1E":1788}'
              ></textarea>
              <p class="score-hint">
                把"下拉选项 → 价格"对应起来。key 必须和上面参数结构里的选项值完全一致。
                只有 <code>default</code> 一个 key 时表示无参数、固定价。
              </p>
            </div>
            <div class="field full">
              <label>订单特有规则（每行一条）</label>
              <textarea
                v-model="orderTypeForm.specificRulesText"
                class="ink-input"
                rows="5"
                placeholder="· 保底 2588W 起，撤离失败加保底 60W&#10;· 出大红保底减半"
              ></textarea>
              <p class="score-hint">
                只有此订单类型才有的规则。前台会在通用规则上方单独显示。
              </p>
            </div>
            <div class="field">
              <label>上架时间（留空=立即）</label>
              <input type="datetime-local" v-model="orderTypeForm.startAt" class="ink-input" />
            </div>
            <div class="field">
              <label>下架时间（留空=永久）</label>
              <input type="datetime-local" v-model="orderTypeForm.endAt" class="ink-input" />
            </div>
            <div class="field full">
              <label class="switch-label">
                <input type="checkbox" v-model="orderTypeForm.isActive" />
                <span>启用（前台可见）</span>
              </label>
            </div>
          </div>

          <footer class="modal-footer">
            <button class="btn-cancel" @click="closeOrderTypeModal">取消</button>
            <button class="btn-submit" @click="saveOrderType" :disabled="savingOrderType">
              {{ savingOrderType ? '保存中...' : '保存' }}
            </button>
          </footer>
        </div>
      </div>
    </Teleport>

    <!-- ================= 弹窗：快速改时效 ================= -->
    <Teleport to="body">
      <div v-if="showTimeModal" class="modal-mask">
        <div class="modal-container" style="max-width: 480px;">
          <header class="modal-header">
            <h3>调整时效 · {{ timeTarget?.name }}</h3>
            <button class="close-icon" @click="closeTimeModal">×</button>
          </header>

          <div class="form-grid">
            <div class="field full">
              <label>上架时间（留空=立即）</label>
              <input type="datetime-local" v-model="timeForm.startAt" class="ink-input" />
            </div>
            <div class="field full">
              <label>下架时间（留空=永久）</label>
              <input type="datetime-local" v-model="timeForm.endAt" class="ink-input" />
            </div>
          </div>

          <footer class="modal-footer">
            <button class="btn-cancel" @click="closeTimeModal">取消</button>
            <button class="btn-submit" @click="saveQuickTime" :disabled="savingTime">
              {{ savingTime ? '保存中...' : '保存' }}
            </button>
          </footer>
        </div>
      </div>
    </Teleport>

    <!-- ================= 弹窗：打手详情 / 审核 ================= -->
    <Teleport to="body">
      <div v-if="showOperatorModal" class="modal-mask">
        <div class="modal-container scroll-y">
          <header class="modal-header">
            <div>
              <h3>{{ targetOperator?.nickname || '打手详情' }}</h3>
              <p class="mono font-sm">GUID: {{ targetOperator?.userId }}</p>
            </div>
            <button class="close-icon" @click="closeOperatorModal">×</button>
          </header>

          <fieldset class="gov-fieldset">
            <legend>基础档案</legend>
            <div class="profile-grid">
              <div class="p-cell"><span>联系方式:</span> <b>{{ targetOperator?.contactType }} / {{ targetOperator?.contactValue }}</b></div>
              <div class="p-cell"><span>在线时段:</span> <b>{{ targetOperator?.onlineTime || '—' }}</b></div>
              <div class="p-cell"><span>信誉分:</span> <b>{{ targetOperator?.reputation }}</b></div>
              <div class="p-cell"><span>累计单量:</span> <b>{{ targetOperator?.totalOrders }}</b></div>
              <div class="p-cell full"><span>自我介绍:</span> <p class="bio-text">{{ targetOperator?.intro || '无' }}</p></div>
            </div>
          </fieldset>

          <fieldset class="gov-fieldset">
            <legend>游戏技能审核</legend>
            <div v-for="sk in targetOperator?.gameSkills || []" :key="sk.id" class="skill-row">
              <div class="skill-info">
                <b>{{ sk.gameName || sk.gameCode }}</b>
                <span class="mono metrics">{{ sk.metricsJson }}</span>
              </div>
              <div class="skill-form">
                <select v-model="sk.auditStatus" class="ink-select small">
                  <option value="pending">待审核</option>
                  <option value="reviewing">复核中</option>
                  <option value="approved">通过</option>
                  <option value="rejected">拒绝</option>
                  <option value="banned">封禁</option>
                </select>
                <input v-model="sk.code" class="ink-input small mono" placeholder="编号 OP-XXX" />
                <select v-model="sk.operatorLevel" class="ink-select small">
                  <option :value="null">未定级</option>
                  <option value="L1">L1</option>
                  <option value="L2">L2</option>
                  <option value="L3">L3</option>
                  <option value="L4">L4</option>
                  <option value="L5">L5</option>
                </select>
                <input v-model="sk.auditNote" class="ink-input small" placeholder="审核备注" />
              </div>
            </div>
            <div v-if="!targetOperator?.gameSkills?.length" class="empty-hint">该打手暂无游戏申报</div>
          </fieldset>

          <fieldset class="gov-fieldset">
            <legend>整体审核结论</legend>
            <div class="form-grid">
              <div class="field">
                <label>档案状态</label>
                <select v-model="operatorAuditForm.auditStatus" class="ink-input">
                  <option value="pending">待审核</option>
                  <option value="reviewing">复核中</option>
                  <option value="approved">已通过</option>
                  <option value="rejected">已拒绝</option>
                  <option value="banned">已封禁</option>
                </select>
              </div>
              <div class="field full">
                <label>审核备注</label>
                <textarea v-model="operatorAuditForm.auditNote" class="ink-input" rows="2"></textarea>
              </div>
            </div>
          </fieldset>

          <footer class="modal-footer">
            <button class="btn-cancel" @click="closeOperatorModal">放弃</button>
            <button class="btn-submit" @click="submitOperatorAudit" :disabled="savingOperator">
              {{ savingOperator ? '写入中...' : '提交审核结论' }}
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

/* ===================== 类型定义 ===================== */

interface ClubGame {
  code: string;
  name: string;
  description?: string;
  commonRulesText?: string;
  radarSystemDimsJson?: string;    // ⭐ 新增
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

interface GameFormState {
  isEdit: boolean;
  code: string;
  name: string;
  description: string;
  commonRulesText: string;
  radarSystemDims: string[];       // ⭐ 新增（前端数组）
  isActive: boolean;
  sortOrder: number;
}

interface ClubOrderType {
  id?: number;
  gameCode: string;
  code: string;
  name: string;
  description?: string;
  paramsSchemaJson?: string;
  pricingJson?: string;
  specificRulesText?: string;
  pricingMode: string;
  isActive: boolean;
  sortOrder: number;
  startAt?: string | null;
  endAt?: string | null;
}

interface OrderTypeFormState {
  isEdit: boolean;
  id?: number;
  code: string;
  name: string;
  description: string;
  paramsSchemaJson: string;
  pricingJson: string;
  specificRulesText: string;
  pricingMode: string;
  isActive: boolean;
  sortOrder: number;
  startAt: string;
  endAt: string;
}

interface OperatorSkill {
  id: number;
  userId: string;
  gameCode: string;
  gameName: string;
  metricsJson: string;
  auditStatus: string;
  auditNote?: string;
  code?: string;
  operatorLevel?: string | null;
  recheckStatus?: string;
  ordersInGame?: number;
}

interface OperatorProfile {
  userId: string;
  nickname: string;
  contactType: string;
  contactValue: string;
  onlineTime: string;
  intro?: string;
  auditStatus: string;
  auditNote?: string;
  reputation: number;
  totalOrders: number;
  appliedAt?: string;
  gameSkills: OperatorSkill[];
}

/* ===================== Tab 配置 ===================== */

const TABS = [
  { id: 'games',      label: '游戏库' },
  { id: 'fields',     label: '字段编排' },
  { id: 'orderTypes', label: '订单类型' },
  { id: 'operators',  label: '打手审核' },
] as const;

type TabId = (typeof TABS)[number]['id'];
const activeTab = ref<TabId>('games');

const STATUS_LABEL: Record<string, string> = {
  pending: '待审核',
  reviewing: '复核中',
  approved: '已通过',
  rejected: '已拒绝',
  banned: '已封禁',
  none: '未复查',
  rechecking: '复查中',
  flagged: '标记异常',
  demoted: '已降级',
  suspended: '已暂停',
};

/* =================== 游戏库 =================== */
const games = ref<ClubGame[]>([]);
const loadingGames = ref(false);
const showGameModal = ref(false);
const savingGame = ref(false);

// ⭐ 系统评分维度清单
const radarSystemDimOptions = ref<RadarSystemDimOption[]>([]);

const gameForm = reactive<GameFormState>({
  isEdit: false,
  code: '',
  name: '',
  description: '',
  commonRulesText: '',
  radarSystemDims: [],             // ⭐
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

// ⭐ 拉系统维度清单
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
    // ⭐ 解析 JSON 字符串为数组
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
    gameForm.radarSystemDims = [];   // ⭐
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
      // ⭐ 序列化数组为 JSON
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

/* =================== 字段编排 =================== */
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

const jumpToFields = (g: ClubGame) => {
  currentGameCode.value = g.code;
  activeTab.value = 'fields';
  loadFields();
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
        ? fieldOptionsRaw.value
            .split('\n')
            .map((s) => s.trim())
            .filter(Boolean)
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

/* =================== 订单类型 =================== */
const orderTypeGameCode = ref('');
const orderTypes = ref<ClubOrderType[]>([]);
const showOrderTypeModal = ref(false);
const savingOrderType = ref(false);

const orderTypeForm = reactive<OrderTypeFormState>({
  isEdit: false,
  id: undefined,
  code: '',
  name: '',
  description: '',
  paramsSchemaJson: '',
  pricingJson: '',
  specificRulesText: '',
  pricingMode: 'fixed',
  isActive: true,
  sortOrder: 0,
  startAt: '',
  endAt: '',
});

const toLocalInput = (iso?: string | null): string => {
  if (!iso) return '';
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
};

const toIso = (local?: string): string | null => {
  if (!local) return null;
  return new Date(local).toISOString();
};

const getStatus = (t: ClubOrderType): 'active' | 'upcoming' | 'expired' | 'disabled' => {
  if (!t.isActive) return 'disabled';
  const now = Date.now();
  if (t.startAt && new Date(t.startAt).getTime() > now) return 'upcoming';
  if (t.endAt && new Date(t.endAt).getTime() < now) return 'expired';
  return 'active';
};

const statusLabel = (s: string) => ({
  active: '在售',
  upcoming: '未开始',
  expired: '已过期',
  disabled: '已停用',
}[s] || s);

const priceRange = (t: ClubOrderType): string => {
  if (!t.pricingJson) return '—';
  try {
    const obj = JSON.parse(t.pricingJson);
    const nums = Object.values(obj).filter((v): v is number => typeof v === 'number');
    if (nums.length === 0) return '—';
    const min = Math.min(...nums);
    const max = Math.max(...nums);
    return min === max ? `¥${min}` : `¥${min}~${max}`;
  } catch {
    return '—';
  }
};

const formatTimeRange = (start?: string | null, end?: string | null): string => {
  if (!start && !end) return '永久';
  const fmt = (iso?: string | null) => {
    if (!iso) return null;
    const d = new Date(iso);
    return `${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  };
  const s = fmt(start) ?? '立即';
  const e = fmt(end) ?? '永久';
  return `${s} ~ ${e}`;
};

const statActive = computed(() =>
  orderTypes.value.filter(t => getStatus(t) === 'active').length);
const statUpcoming = computed(() =>
  orderTypes.value.filter(t => getStatus(t) === 'upcoming').length);
const statExpired = computed(() =>
  orderTypes.value.filter(t => getStatus(t) === 'expired').length);
const statDisabled = computed(() =>
  orderTypes.value.filter(t => getStatus(t) === 'disabled').length);

const loadOrderTypes = async () => {
  if (!orderTypeGameCode.value) {
    orderTypes.value = [];
    return;
  }
  try {
    const res: any = await request.get('/admin/club/order-types', {
      params: { gameCode: orderTypeGameCode.value },
    });
    orderTypes.value = res.data || res || [];
  } catch (e) {
    console.error('拉取订单类型失败', e);
  }
};

const openOrderTypeModal = (t?: ClubOrderType) => {
  if (!orderTypeGameCode.value) {
    alert('请先选择游戏');
    return;
  }
  if (t) {
    orderTypeForm.isEdit = true;
    orderTypeForm.id = t.id;
    orderTypeForm.code = t.code;
    orderTypeForm.name = t.name;
    orderTypeForm.description = t.description ?? '';
    orderTypeForm.paramsSchemaJson = t.paramsSchemaJson ?? '';
    orderTypeForm.pricingJson = t.pricingJson ?? '';
    orderTypeForm.specificRulesText = t.specificRulesText ?? '';
    orderTypeForm.pricingMode = t.pricingMode || 'fixed';
    orderTypeForm.isActive = t.isActive;
    orderTypeForm.sortOrder = t.sortOrder;
    orderTypeForm.startAt = toLocalInput(t.startAt);
    orderTypeForm.endAt = toLocalInput(t.endAt);
  } else {
    orderTypeForm.isEdit = false;
    orderTypeForm.id = undefined;
    orderTypeForm.code = '';
    orderTypeForm.name = '';
    orderTypeForm.description = '';
    orderTypeForm.paramsSchemaJson = '';
    orderTypeForm.pricingJson = '';
    orderTypeForm.specificRulesText = '';
    orderTypeForm.pricingMode = 'fixed';
    orderTypeForm.isActive = true;
    orderTypeForm.sortOrder = orderTypes.value.length + 1;
    orderTypeForm.startAt = '';
    orderTypeForm.endAt = '';
  }
  showOrderTypeModal.value = true;
};

const closeOrderTypeModal = () => {
  showOrderTypeModal.value = false;
};

const saveOrderType = async () => {
  if (!orderTypeForm.code.trim() || !orderTypeForm.name.trim()) {
    alert('请填写代码与显示名');
    return;
  }
  if (orderTypeForm.paramsSchemaJson.trim()) {
    try { JSON.parse(orderTypeForm.paramsSchemaJson); }
    catch { alert('下单参数结构不是合法的 JSON'); return; }
  }
  if (orderTypeForm.pricingJson.trim()) {
    try { JSON.parse(orderTypeForm.pricingJson); }
    catch { alert('价格映射不是合法的 JSON'); return; }
  }
  savingOrderType.value = true;
  try {
    await request.post('/admin/club/order-type', {
      id: orderTypeForm.id,
      gameCode: orderTypeGameCode.value,
      code: orderTypeForm.code.trim(),
      name: orderTypeForm.name.trim(),
      description: orderTypeForm.description,
      paramsSchemaJson: orderTypeForm.paramsSchemaJson.trim() || null,
      pricingJson: orderTypeForm.pricingJson.trim() || null,
      specificRulesText: orderTypeForm.specificRulesText.trim() || null,
      pricingMode: orderTypeForm.pricingMode,
      isActive: orderTypeForm.isActive,
      sortOrder: orderTypeForm.sortOrder,
      startAt: toIso(orderTypeForm.startAt),
      endAt: toIso(orderTypeForm.endAt),
    });
    await loadOrderTypes();
    closeOrderTypeModal();
  } catch (e: any) {
    alert(e.message || '保存失败');
  } finally {
    savingOrderType.value = false;
  }
};

const handleDeleteOrderType = async (t: ClubOrderType) => {
  if (!t.id) return;
  if (!confirm(`确定删除订单类型【${t.name}】吗？`)) return;
  try {
    await request.delete(`/admin/club/order-type/${t.id}`);
    await loadOrderTypes();
  } catch (e: any) {
    alert(e.message || '删除失败');
  }
};

const toggleActive = async (t: ClubOrderType) => {
  if (!t.id) return;
  const before = t.isActive;
  t.isActive = !before;
  try {
    const res: any = await request.patch(`/admin/club/order-type/${t.id}/toggle`);
    const payload = res?.data ?? res;
    if (typeof payload?.isActive === 'boolean') {
      t.isActive = payload.isActive;
    }
  } catch (e: any) {
    t.isActive = before;
    alert(e.message || '操作失败');
  }
};

const copyOrderType = async (t: ClubOrderType) => {
  if (!t.id) return;
  if (!confirm(`复制订单类型【${t.name}】？`)) return;
  try {
    await request.post(`/admin/club/order-type/${t.id}/copy`);
    await loadOrderTypes();
  } catch (e: any) {
    alert(e.message || '复制失败');
  }
};

const showTimeModal = ref(false);
const savingTime = ref(false);
const timeTarget = ref<ClubOrderType | null>(null);
const timeForm = reactive({ startAt: '', endAt: '' });

const openTimeModal = (t: ClubOrderType) => {
  timeTarget.value = t;
  timeForm.startAt = toLocalInput(t.startAt);
  timeForm.endAt = toLocalInput(t.endAt);
  showTimeModal.value = true;
};

const closeTimeModal = () => {
  showTimeModal.value = false;
  timeTarget.value = null;
};

const saveQuickTime = async () => {
  if (!timeTarget.value?.id) return;
  savingTime.value = true;
  try {
    await request.patch(`/admin/club/order-type/${timeTarget.value.id}/time`, {
      startAt: toIso(timeForm.startAt),
      endAt: toIso(timeForm.endAt),
    });
    await loadOrderTypes();
    closeTimeModal();
  } catch (e: any) {
    alert(e.message || '保存失败');
  } finally {
    savingTime.value = false;
  }
};

/* =================== 打手审核 =================== */
const operators = ref<OperatorProfile[]>([]);
const loadingOperators = ref(false);
const operatorSearch = ref('');
const operatorStatusFilter = ref('');
const showOperatorModal = ref(false);
const savingOperator = ref(false);
const targetOperator = ref<OperatorProfile | null>(null);

const operatorAuditForm = reactive({
  auditStatus: 'pending',
  auditNote: '',
});

const loadOperators = async () => {
  loadingOperators.value = true;
  try {
    const res: any = await request.get('/admin/club/operators', {
      params: {
        search: operatorSearch.value.trim() || undefined,
        status: operatorStatusFilter.value || undefined,
      },
    });
    operators.value = res.data || res || [];
  } catch (e) {
    console.error('拉取打手列表失败', e);
  } finally {
    loadingOperators.value = false;
  }
};

const openOperatorModal = (op: OperatorProfile) => {
  targetOperator.value = JSON.parse(JSON.stringify(op)) as OperatorProfile;
  operatorAuditForm.auditStatus = op.auditStatus || 'pending';
  operatorAuditForm.auditNote = op.auditNote || '';
  showOperatorModal.value = true;
};

const closeOperatorModal = () => {
  showOperatorModal.value = false;
  targetOperator.value = null;
};

const submitOperatorAudit = async () => {
  if (!targetOperator.value) return;
  savingOperator.value = true;
  try {
    await request.post('/admin/club/operator/audit', {
      userId: targetOperator.value.userId,
      auditStatus: operatorAuditForm.auditStatus,
      auditNote: operatorAuditForm.auditNote,
      gameSkills: (targetOperator.value.gameSkills || []).map((sk) => ({
        id: sk.id,
        auditStatus: sk.auditStatus,
        auditNote: sk.auditNote,
        code: sk.code,
        operatorLevel: sk.operatorLevel,
      })),
    });
    await loadOperators();
    closeOperatorModal();
  } catch (e: any) {
    alert(e.message || '审核提交失败');
  } finally {
    savingOperator.value = false;
  }
};

/* =================== 初始化 =================== */
onMounted(async () => {
  await loadRadarSystemDimOptions();   // ⭐ 新增
  await loadGames();
  await loadOperators();
  if (games.value.length > 0) {
    orderTypeGameCode.value = games.value[0].code;
  }
});
</script>

<style scoped>
.club-game-manager { display: flex; flex-direction: column; animation: slideIn 0.35s cubic-bezier(0.16, 1, 0.3, 1); }
.module-header { margin-bottom: 30px; }
.page-title { font-size: 1.6rem; font-weight: 700; color: #111; margin: 0; }
.md-subtitle { font-size: 0.85rem; color: #888; margin: 6px 0 0; }

/* Tabs */
.md-tabs { display: flex; gap: 32px; border-bottom: 1px solid #f2f2f7; margin-bottom: 24px; }
.tab-item {
  cursor: pointer; color: #86868b; padding-bottom: 12px;
  font-size: 0.95rem; font-weight: 600;
  transition: all 0.2s ease; position: relative;
}
.tab-item.active { color: #111; }
.tab-item.active::after {
  content: ''; position: absolute; left: 0; right: 0; bottom: -1px;
  height: 2px; background: #111;
}

.tab-panel { animation: fadeIn 0.3s ease; }

/* Toolbar */
.panel-toolbar {
  display: flex; justify-content: space-between; align-items: center;
  margin-bottom: 16px; gap: 16px;
}
.count-hint { font-size: 0.85rem; color: #888; }
.game-picker { display: flex; align-items: center; gap: 10px; font-size: 0.85rem; color: #555; }
.filter-row { display: flex; gap: 12px; flex: 1; max-width: 720px; }

.ink-input, .ink-select {
  border: 1px solid #e0e0e0; padding: 10px 14px; border-radius: 4px;
  font-size: 0.85rem; outline: none; background: #fff; transition: 0.25s;
  font-family: inherit;
}
.ink-input:focus, .ink-select:focus { border-color: #1a1a1a; }
.ink-input.small, .ink-select.small { padding: 6px 10px; font-size: 0.8rem; }
.ink-select { cursor: pointer; min-width: 140px; }

.btn-primary {
  background: #111; color: #fff; border: none; padding: 10px 20px;
  border-radius: 4px; cursor: pointer; font-size: 0.85rem; font-weight: 500;
  transition: 0.2s;
}
.btn-primary:hover:not(:disabled) { background: #333; }
.btn-primary:disabled { opacity: 0.5; cursor: not-allowed; }

.btn-refresh {
  background: #fff; border: 1px solid #e0e0e0; padding: 10px 20px;
  border-radius: 4px; color: #555; cursor: pointer; font-size: 0.85rem;
}
.btn-refresh:hover { border-color: #1a1a1a; color: #000; }

/* Table */
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

.status-badge {
  font-size: 0.7rem; padding: 3px 8px; border-radius: 4px; font-weight: 600;
}
.status-badge.on { background: #e3fcef; color: #00875a; }
.status-badge.off { background: #f5f5f5; color: #999; }
.status-badge.pending { background: #fff7ed; color: #c2410c; }
.status-badge.reviewing { background: #e0f2fe; color: #0369a1; }
.status-badge.approved { background: #e3fcef; color: #00875a; }
.status-badge.rejected { background: #fef2f2; color: #dc2626; }
.status-badge.banned { background: #111; color: #fff; }

.type-tag {
  font-family: monospace; font-size: 0.75rem; padding: 2px 8px;
  background: #f1f5f9; color: #475569; border-radius: 3px;
}

.score-flag {
  display: inline-block; padding: 2px 8px;
  font-size: 0.75rem; font-weight: 700;
  color: #c2410c; background: #fff7ed; border-radius: 3px;
}

.req-dot {
  display: inline-block; width: 8px; height: 8px; border-radius: 50%;
}
.req-dot.yes { background: #dc2626; }
.req-dot.no { background: #ddd; }

.btn-text {
  background: none; border: none; color: #2563eb; cursor: pointer;
  font-size: 0.8rem; font-weight: 700; margin-left: 12px; padding: 0;
}
.btn-text:hover { text-decoration: underline; }
.btn-text.danger { color: #dc2626; }

/* 订单类型专用 */
.row-disabled td { opacity: 0.55; }
.ot-name { font-weight: 600; color: #111; }
.ot-desc { margin-top: 4px; font-size: 0.75rem; color: #999; }

.switch { position: relative; display: inline-block; width: 34px; height: 18px; }
.switch input { opacity: 0; width: 0; height: 0; }
.slider {
  position: absolute; cursor: pointer; inset: 0;
  background: #ddd; transition: 0.3s; border-radius: 18px;
}
.slider::before {
  position: absolute; content: '';
  height: 14px; width: 14px; left: 2px; bottom: 2px;
  background: #fff; transition: 0.3s; border-radius: 50%;
}
.switch input:checked + .slider { background: #16a34a; }
.switch input:checked + .slider::before { transform: translateX(16px); }

.status-pill {
  display: inline-block; padding: 3px 10px;
  font-size: 0.72rem; font-weight: 600; border-radius: 3px;
}
.status-pill.active   { background: #e3fcef; color: #00875a; }
.status-pill.upcoming { background: #fff7ed; color: #c2410c; }
.status-pill.expired  { background: #f5f5f5; color: #888; }
.status-pill.disabled { background: #fef2f2; color: #dc2626; }

.price-preview { color: #c2410c; font-weight: 600; }

.editable-time {
  padding: 2px 6px;
  border-bottom: 1px dashed #ddd;
  cursor: pointer;
  color: #555;
  font-size: 0.82rem;
}
.editable-time:hover {
  color: #2563eb;
  border-bottom-color: #2563eb;
}

.stats-bar {
  display: flex; flex-wrap: wrap; gap: 24px;
  padding: 12px 20px; margin-bottom: 16px;
  background: #fafafa; border-radius: 6px;
  font-size: 0.85rem;
}
.stat { display: flex; align-items: center; gap: 8px; color: #666; }
.stat b { color: #111; font-weight: 700; }
.sdot { width: 8px; height: 8px; border-radius: 50%; }
.sdot.active   { background: #16a34a; }
.sdot.upcoming { background: #f97316; }
.sdot.expired  { background: #999; }
.sdot.disabled { background: #dc2626; }

/* Operator cards */
.operator-grid {
  display: grid; grid-template-columns: repeat(auto-fill, minmax(360px, 1fr));
  gap: 16px;
}
.operator-card {
  background: #fff; border: 1px solid #f0f0f0; border-radius: 10px;
  padding: 20px; display: flex; flex-direction: column; gap: 14px;
  transition: 0.25s;
}
.operator-card:hover { border-color: #ddd; box-shadow: 0 6px 24px rgba(0,0,0,0.04); }

.op-head { display: flex; align-items: center; gap: 12px; }
.op-avatar {
  width: 42px; height: 42px; border-radius: 50%;
  background: #111; color: #fff; display: flex;
  align-items: center; justify-content: center;
  font-weight: 700; font-size: 1.1rem;
}
.op-meta { flex: 1; min-width: 0; }
.op-name { font-weight: 700; color: #111; font-size: 0.95rem; }
.op-contact { font-size: 0.72rem; color: #999; }

.op-reputation {
  display: flex; align-items: center; gap: 8px; font-size: 0.8rem; color: #666;
}
.op-reputation b { color: #111; }
.op-reputation .good { color: #16a34a; }
.op-reputation .bad { color: #dc2626; }
.op-reputation .divider { color: #eee; }

.op-games { display: flex; flex-wrap: wrap; gap: 6px; }
.skill-chip {
  display: inline-flex; align-items: center; gap: 6px;
  font-size: 0.72rem; padding: 4px 10px; border-radius: 4px;
  background: #f5f5f5; color: #333;
}
.skill-chip.approved { background: #e3fcef; color: #00875a; }
.skill-chip.rejected { background: #fef2f2; color: #dc2626; }
.skill-chip.pending { background: #fff7ed; color: #c2410c; }
.skill-chip .skill-status { opacity: 0.7; font-weight: 500; }
.no-skill { font-size: 0.75rem; color: #bbb; }

.op-actions { text-align: right; border-top: 1px solid #f5f5f5; padding-top: 12px; }

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
.modal-header p { margin: 4px 0 0; color: #888; }
.close-icon { background: none; border: none; font-size: 1.8rem; cursor: pointer; color: #ccc; line-height: 1; }
.close-icon:hover { color: #000; }

.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; margin-bottom: 24px; }
.field { display: flex; flex-direction: column; gap: 6px; }
.field.full { grid-column: span 2; }
.field label { font-size: 0.7rem; text-transform: uppercase; color: #aaa; font-weight: 700; }
.field textarea.ink-input { resize: vertical; font-family: inherit; }
.switch-label { display: flex; align-items: center; gap: 8px; font-size: 0.85rem; cursor: pointer; padding-top: 8px; }

/* ⭐ 系统评分维度 */
.radar-sys-dims {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 10px;
}
.sys-dim-item {
  display: flex; align-items: flex-start; gap: 10px;
  padding: 10px 12px;
  border: 1px solid #e0e0e0;
  border-radius: 4px;
  cursor: pointer;
  transition: 0.2s;
  background: #fff;
}
.sys-dim-item:hover:not(.disabled) {
  border-color: #111;
  background: #fafafa;
}
.sys-dim-item.disabled {
  opacity: 0.45;
  cursor: not-allowed;
  background: #fafafa;
}
.sys-dim-item input {
  margin-top: 3px;
  cursor: pointer;
  accent-color: #111;
}
.sys-dim-info {
  display: flex; flex-direction: column; gap: 2px;
  min-width: 0;
}
.sys-dim-label {
  font-size: 0.85rem;
  font-weight: 600;
  color: #111;
  display: flex; align-items: center; gap: 6px;
}
.sys-dim-badge {
  padding: 1px 6px;
  font-size: 0.65rem;
  font-weight: 500;
  color: #999;
  background: #f0f0f0;
  border-radius: 2px;
}
.sys-dim-desc {
  font-size: 0.72rem;
  color: #999;
  line-height: 1.4;
}

.score-section {
  padding: 14px 16px;
  border: 1px dashed #e0e0e0;
  border-radius: 4px;
  background: #fafafa;
}
.score-hint {
  margin: 4px 0 0;
  font-size: 0.72rem;
  color: #888;
  line-height: 1.6;
}
.score-hint code {
  padding: 1px 6px;
  background: #f0f0f0;
  border-radius: 3px;
  font-family: monospace;
  font-size: 0.7rem;
  color: #c2410c;
}

.gov-fieldset {
  border: 1px solid #eee; margin-bottom: 20px; padding: 18px; border-radius: 4px;
}
.gov-fieldset legend {
  font-size: 0.72rem; text-transform: uppercase; font-weight: 800;
  color: #999; padding: 0 8px; letter-spacing: 0.5px;
}
.profile-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; font-size: 0.85rem; }
.p-cell { display: flex; gap: 8px; color: #666; }
.p-cell b { color: #111; }
.p-cell.full { grid-column: span 2; flex-direction: column; gap: 4px; }
.bio-text { background: #fafafa; padding: 10px; border: 1px dashed #e0e0e0; margin: 0; font-size: 0.8rem; line-height: 1.5; }

.skill-row {
  display: flex; flex-direction: column; gap: 10px;
  padding: 12px 0; border-bottom: 1px solid #f5f5f5;
}
.skill-row:last-child { border-bottom: none; }
.skill-info { display: flex; align-items: center; gap: 12px; font-size: 0.85rem; }
.skill-info b { color: #111; }
.metrics { font-size: 0.75rem; color: #888; }
.skill-form { display: grid; grid-template-columns: 110px 1fr 90px 1fr; gap: 8px; align-items: center; }

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

.font-sm { font-size: 0.8rem; }

@keyframes slideIn { from { opacity: 0; transform: translateY(8px); } to { opacity: 1; transform: translateY(0); } }
@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
</style>