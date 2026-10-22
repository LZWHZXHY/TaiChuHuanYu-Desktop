<template>
  <div class="page-shell">
    <Navbar />

    <main>
      <section class="page-hero">
        <div class="section-container">
          <SectionHeading index="01" title="CS DASHBOARD" subtitle="客服工作台 / SERVICE DESK" />

          <!-- 客服状态卡 -->
          <div v-if="loadingMe" class="cs-loading">
            <span class="dot"></span> 正在读取客服信息…
          </div>

          <div v-else-if="!hasCs" class="cs-guard">
            <p class="guard-title">你还不是客服</p>
            <p class="guard-desc">请先提交客服申请，通过审核后即可进入工作台</p>
            <router-link to="/taichu/cs/register" class="guard-link">前往申请 →</router-link>
          </div>

          <div v-else-if="csAuditStatus !== 'approved'" class="cs-guard" :class="csAuditStatus">
            <p class="guard-title">{{ auditTitle }}</p>
            <p class="guard-desc">{{ auditDesc }}</p>
            <router-link to="/taichu/cs/register" class="guard-link">查看申请状态 →</router-link>
          </div>

          <div v-else class="cs-header" :class="csStatus">
            <div class="cs-header-left">
              <OnlineStatusBadge :status="csStatus" />
              <div class="cs-info">
                <div class="cs-name">{{ csInfo.name }}</div>
                <div class="cs-meta">
                  微信 {{ csInfo.wechat }}
                  · 载单 <b>{{ csInfo.currentLoad }}</b> / {{ csInfo.capacity }}
                  <span v-if="csInfo.reviewCount > 0" class="cs-rate">
                    · 评分 {{ csInfo.avgOverall.toFixed(1) }}（{{ csInfo.reviewCount }}）
                  </span>
                </div>
              </div>
            </div>

            <div class="cs-header-right">
              <button class="cs-guide-btn" @click="showGuide = true">📖 操作指南</button>
              <button class="cs-wallet-btn" @click="goWallet">💰 我的钱包</button>

              <button
                class="cs-status-btn"
                :class="{ on: csStatus === 'online' }"
                :disabled="statusChanging || csStatus === 'online'"
                @click="switchStatus('online')"
              >上线</button>
              <button
                class="cs-status-btn"
                :class="{ on: csStatus === 'offline' }"
                :disabled="statusChanging || csStatus === 'offline'"
                @click="switchStatus('offline')"
              >下线</button>
            </div>
          </div>
        </div>
      </section>

      <section v-if="hasCs && csAuditStatus === 'approved'" class="section-block alt">
        <div class="section-container">

          <!-- Tab 切换 -->
          <div class="cs-tabs">
            <button
              v-for="t in TABS"
              :key="t.value"
              class="cs-tab"
              :class="{ on: tab === t.value }"
              @click="switchTab(t.value)"
            >
              {{ t.label }}
              <span v-if="tabCounts[t.value] > 0" class="tab-count">
                {{ tabCounts[t.value] }}
              </span>
            </button>
          </div>

          <!-- ============ 客诉 tab ============ -->
          <template v-if="tab === 'complaints'">
            <div v-if="loadingComplaints" class="op-state">
              <span class="dot"></span> 正在读取客诉…
            </div>

            <div v-else-if="complaints.length === 0" class="op-state">
              当前没有客诉
            </div>

            <div v-else class="cp-list">
              <article v-for="c in complaints" :key="c.id" class="cp-card" :class="c.status">
                <header class="cpc-head">
                  <div class="cpc-left">
                    <span class="cpc-no mono">{{ c.orderNo }}</span>
                    <span class="cpc-type">{{ c.complaintTypeLabel }}</span>
                  </div>
                  <span :class="['cpc-status', c.status]">{{ complaintStatusText(c.status) }}</span>
                </header>

                <div class="cpc-body">
                  <div class="cpc-row">
                    <span>游戏</span>
                    <b>{{ c.gameName }} · {{ c.orderTypeName }}</b>
                  </div>
                  <div class="cpc-row">
                    <span>投诉对象</span>
                    <b>{{ c.targetOperatorName || '整单' }}</b>
                  </div>
                  <div class="cpc-row">
                    <span>投诉时间</span>
                    <b class="mono">{{ fmtTime(c.createdAt) }}</b>
                  </div>
                </div>

                <div class="cpc-section">
                  <div class="cps-label">投诉描述</div>
                  <p class="cps-text">{{ c.description }}</p>
                </div>

                <div v-if="c.evidenceUrl" class="cpc-section">
                  <div class="cps-label">证据截图</div>
                  <a :href="c.evidenceUrl" target="_blank" rel="noopener" class="cps-evidence">
                    <img :src="c.evidenceUrl" alt="证据" />
                  </a>
                </div>

                <div v-if="c.status !== 'pending'" class="cpc-reply" :class="c.status">
                  <div class="cpr-head">
                    <span class="cpr-name">{{ c.handlerName || '客服' }}</span>
                    <span class="cpr-time mono">{{ fmtTime(c.handledAt) }}</span>
                  </div>
                  <p class="cpr-note">{{ c.handlerNote }}</p>
                </div>

                <footer v-if="c.status === 'pending'" class="oc-foot">
                  <button class="oc-btn primary" @click="openHandleComplaint(c)">处理客诉</button>
                </footer>
              </article>
            </div>

            <div v-if="complaintsTotal > complaintsPageSize" class="cs-pager">
              <button :disabled="complaintsPage <= 1" @click="changeComplaintsPage(complaintsPage - 1)">上一页</button>
              <span class="mono">{{ complaintsPage }} / {{ Math.ceil(complaintsTotal / complaintsPageSize) }}</span>
              <button
                :disabled="complaintsPage >= Math.ceil(complaintsTotal / complaintsPageSize)"
                @click="changeComplaintsPage(complaintsPage + 1)"
              >下一页</button>
            </div>
          </template>

          <!-- ============ 订单 tab ============ -->
          <template v-else>
            <div v-if="loadingOrders" class="op-state">
              <span class="dot"></span> 正在读取订单…
            </div>

            <div v-else-if="orders.length === 0" class="op-state">
              {{ emptyText }}
            </div>

            <div v-else class="order-list">
              <article
                v-for="o in orders"
                :key="o.orderNo"
                class="order-card"
                :class="{ 'is-urgent': isUrgent(o) }"
              >
                <header class="oc-head">
                  <div class="oc-no">
                    <span class="oc-label">订单号</span>
                    <span class="oc-code mono">{{ o.orderNo }}</span>
                  </div>
                  <div class="oc-head-right">
                    <span v-if="isUrgent(o)" class="oc-urgent-tag">待处理</span>
                    <span :class="['oc-status', deriveStatus(o)]">{{ statusText(o) }}</span>
                  </div>
                </header>

                <div class="oc-body">
                  <div class="oc-info">
                    <div class="oc-row">
                      <span>游戏</span>
                      <b>{{ o.gameName }}</b>
                    </div>
                    <div class="oc-row">
                      <span>类型</span>
                      <b>{{ o.orderTypeName }}</b>
                      <span v-if="o.minOperatorCount" class="oc-op-count mono">
                        （{{ o.minOperatorCount }}-{{ o.maxOperatorCount }} 人）
                      </span>
                    </div>
                    <div v-for="(v, k) in o.params" :key="k" class="oc-row">
                      <span>{{ k }}</span>
                      <b>{{ v }}</b>
                    </div>
                    <div class="oc-row">
                      <span>老板</span>
                      <b>{{ o.customerName || shortId(o.customerId) }}</b>
                    </div>

                    <!-- ⭐ 打手（含接单进度） -->
                    <div class="oc-row">
                      <span>打手</span>
                      <b v-if="o.operators && o.operators.length > 0" class="op-linked">
                        {{ operatorText(o) }}
                        <span class="oc-op-inline">
                          （{{ o.acceptedCount || 0 }}/{{ o.totalOperatorCount || o.operators.length }} 已接）
                        </span>
                      </b>
                      <b v-else class="op-unassigned">未指派</b>
                    </div>

                    <div v-if="o.failedCount > 0" class="oc-row">
                      <span>炸单</span>
                      <b class="oc-failed">{{ o.failedCount }} 次</b>
                    </div>
                    <div v-if="o.remark" class="oc-row">
                      <span>备注</span>
                      <b class="oc-remark">{{ o.remark }}</b>
                    </div>
                    <div v-if="o.settlementProofUploaded && o.settlementProofUrl" class="oc-row">
                      <span>结算图</span>
                      <a :href="o.settlementProofUrl" target="_blank" rel="noopener" class="oc-proof-link">
                        查看老板上传的截图
                      </a>
                    </div>
                  </div>

                  <div class="oc-side">
                    <div class="oc-price">¥{{ o.price }}</div>
                    <div class="oc-time mono">{{ fmtTime(o.createdAt) }}</div>
                  </div>
                </div>

                <!-- ⭐ 接单进度（多打手时显示） -->
                <div v-if="showProgress(o)" class="oc-progress">
                  <div class="op-progress-head">
                    <span class="oph-label">接单进度</span>
                    <b class="oph-count" :class="{ done: isAllAccepted(o) }">
                      {{ o.acceptedCount || 0 }} / {{ o.totalOperatorCount || 0 }}
                    </b>
                    <span v-if="isAllAccepted(o)" class="oph-tag done">
                      ✓ 全员已接
                    </span>
                    <span v-else class="oph-tag">
                      还有 {{ (o.totalOperatorCount || 0) - (o.acceptedCount || 0) }} 人未接
                    </span>
                  </div>
                  <div class="op-progress-list">
                    <div
                      v-for="op in (o.operators || [])"
                      :key="op.userId"
                      class="op-chip"
                      :class="{ accepted: op.accepted }"
                    >
                      <span class="chip-status">{{ op.accepted ? '✓' : '·' }}</span>
                      <span class="chip-name">{{ op.name }}</span>
                    </div>
                  </div>
                </div>

                <!-- 换人历史 -->
                <div v-if="o.reassignHistory && o.reassignHistory.length > 0" class="oc-reassign">
                  <div class="ocr-title">🔄 换人记录</div>
                  <div v-for="(h, i) in o.reassignHistory" :key="'rh-'+i" class="ocr-item">
                    <div class="ocr-note">{{ h.note }}</div>
                    <div class="ocr-time">{{ fmtTime(h.at) }}</div>
                  </div>
                </div>

                <!-- 时间轴 -->
                <div class="oc-timeline">
                  <span :class="{ done: o.userPaidAt }">老板付款</span>
                  <span class="tl-arrow">→</span>
                  <span :class="{ done: o.paidConfirmedAt }">客服确认</span>
                  <span class="tl-arrow">→</span>
                  <span :class="{ done: o.operatorUserId }">指派打手</span>
                  <span class="tl-arrow">→</span>
                  <span :class="{ done: o.status === 'completed' }">完成</span>
                </div>

                <footer class="oc-foot">
                  <button
                    v-if="needsConfirm(o)"
                    class="oc-btn primary"
                    :disabled="acting === o.orderNo"
                    @click="confirmPaid(o)"
                  >
                    {{ acting === o.orderNo ? '处理中…' : '✓ 确认收款' }}
                  </button>

                  <button
                    v-if="needsAssign(o)"
                    class="oc-btn primary"
                    :disabled="acting === o.orderNo"
                    @click="openAssign(o)"
                  >指派打手</button>

                  <!-- ⭐ 服务中：换打手 / 弃单 -->
                  <template v-if="o.status === 'processing'">
                    <button
                      class="oc-btn warn"
                      :disabled="acting === o.orderNo"
                      @click="openReassign(o)"
                    >🔄 换打手</button>

                    <button
                      class="oc-btn danger"
                      :disabled="acting === o.orderNo"
                      @click="openAbandon(o)"
                    >⚠ 弃单</button>
                  </template>

                  <button
                    v-if="o.status === 'pending' && o.paidConfirmedAt"
                    class="oc-btn ghost"
                    @click="cancelOrder(o)"
                  >取消订单</button>
                </footer>
              </article>
            </div>

            <div v-if="total > pageSize" class="cs-pager">
              <button :disabled="page <= 1" @click="changePage(page - 1)">上一页</button>
              <span class="mono">{{ page }} / {{ Math.ceil(total / pageSize) }}</span>
              <button :disabled="page >= Math.ceil(total / pageSize)" @click="changePage(page + 1)">下一页</button>
            </div>
          </template>

        </div>
      </section>
    </main>

    <Footer />

    <!-- ============ 操作指南弹窗 ============ -->
    <Teleport to="body">
      <div v-if="showGuide" class="guide-mask" @click.self="showGuide = false">
        <div class="guide-modal">
          <header class="gm-head">
            <div>
              <h3>客服操作指南</h3>
              <p class="gm-sub">SERVICE DESK MANUAL</p>
            </div>
            <button class="close-icon" @click="showGuide = false">×</button>
          </header>

          <div class="gm-body">
            <nav class="gm-toc">
              <a v-for="s in GUIDE_SECTIONS" :key="s.id" :href="'#g-'+s.id" class="toc-a" @click.prevent="scrollToSection(s.id)">
                <span class="toc-num">{{ s.idx }}</span>
                {{ s.label }}
              </a>
            </nav>

            <div class="gm-content">

              <section id="g-01" class="gd-sec">
                <h4><span>01</span> 收款确认流程</h4>
                <ol class="gd-steps">
                  <li><b>收到钉钉到账通知</b><p>群里出现类似消息：<code>【收款通知】Weixin Pay 个人收款码到账 ¥388</code></p></li>
                  <li><b>打开「待确认收款」tab</b><p>列表显示所有未确认订单：订单号、金额、老板、时间。</p></li>
                  <li><b>核对金额与订单</b><p>按 <strong>金额 + 时间</strong> 匹配。金额一致 → 下一步；不一致 → 群里 @ 管理员。</p></li>
                  <li><b>点击「✓ 确认收款」</b><p>订单自动进入「待指派打手」。</p></li>
                </ol>
                <div class="gd-warn">⚠ <b>不要凭老板一句话就确认。</b>必须看到钉钉到账通知，且金额一致，再点确认。</div>
              </section>

              <section id="g-02" class="gd-sec">
                <h4><span>02</span> 指派打手流程</h4>
                <ol class="gd-steps">
                  <li><b>切到「待指派打手」tab</b><p>确认收款后的订单会出现在这里。</p></li>
                  <li><b>点「指派打手」按钮</b><p>弹窗显示该游戏所有已认证打手。</p></li>
                  <li><b>按订单类型选人数</b><p>护航单 2 位 / 陪玩单 1-2 位 / 趣味单按提示。</p></li>
                  <li><b>点「确认指派」</b><p>打手收到接单通知，全部接单后进入服务中。</p></li>
                </ol>
                <div class="gd-tip">💡 <b>优先选择在线打手</b>，列表默认在线优先排序。</div>
              </section>

              <section id="g-03" class="gd-sec">
                <h4><span>03</span> 换打手 / 弃单</h4>
                <ol class="gd-steps">
                  <li><b>老板/打手微信联系客服</b><p>说明炸单情况或弃单意图。</p></li>
                  <li><b>客服判断合理后操作</b><p>打开对应订单（状态为"服务中"）。</p></li>
                  <li><b>换打手</b><p>点「🔄 换打手」→ 选新打手组合（可保留部分原打手）→ 填换人原因 → 提交。</p></li>
                  <li><b>弃单</b><p>点「⚠ 弃单」→ 填弃单原因 → 提交。订单变已弃单，需线下退款给老板。</p></li>
                </ol>
                <div class="gd-warn">⚠ 换人/弃单都会留记录，请如实填写原因。</div>
              </section>

              <section id="g-04" class="gd-sec">
                <h4><span>04</span> 客诉处理</h4>
                <ol class="gd-steps">
                  <li><b>切到「客诉」tab</b><p>显示你负责订单的所有客诉。</p></li>
                  <li><b>点「处理客诉」</b><p>查看老板投诉详情 + 证据截图。</p></li>
                  <li><b>选择处理方式</b><p>· 已处理：确认客诉合理，回复老板<br />· 驳回：客诉不成立，回复老板原因</p></li>
                </ol>
              </section>

              <section id="g-05" class="gd-sec">
                <h4><span>05</span> 常见问题</h4>
                <div class="faq">
                  <div class="faq-item"><div class="faq-q">Q：打手/老板要求换人，客服能不能拒绝？</div><div class="faq-a">A：可以。判断依据：炸单次数、聊天记录、打手反馈等。合理则换。</div></div>
                  <div class="faq-item"><div class="faq-q">Q：换人后价格怎么算？</div><div class="faq-a">A：如果等级变化，价格会重算。差价线下多退少补。</div></div>
                  <div class="faq-item"><div class="faq-q">Q：弃单后打手有收益吗？</div><div class="faq-a">A：没有。弃单属于失败单，打手不结算。</div></div>
                </div>
              </section>

            </div>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- ============ 指派弹窗 ============ -->
    <Teleport to="body">
      <div v-if="assignOrder" class="assign-mask" @click.self="closeAssign">
        <div class="assign-modal" role="dialog" aria-modal="true" aria-label="安排订单打手">
          <header class="am-head">
            <div>
              <h3>指派打手</h3>
              <p class="am-sub">
                订单 {{ assignOrder.orderNo }} · {{ assignOrder.gameName }} · {{ assignOrder.orderTypeName }}
              </p>
            </div>
            <button class="close-icon" @click="closeAssign">×</button>
          </header>

          <div class="am-body">
            <div class="op-count-hint" :class="{ full: isOpCountMaxed, locked: isOpCountLocked }">
              <span class="och-label">打手数量</span>
              <b class="och-count">{{ selectedOpIds.length }} / {{ opCountRange }}</b>
              <span v-if="isOpCountLocked" class="och-tag locked">本单必须 {{ minOpCount }} 人</span>
              <span v-else-if="isOpCountMaxed" class="och-tag full">已达上限</span>
              <span v-else class="och-tag">请选择 {{ minOpCount }}–{{ maxOpCount }} 位</span>
            </div>

            <div v-if="isLevelBased && selectedOps.length > 0" class="price-preview-box">
              <div class="ppb-row"><span>基础价</span><b>¥{{ basePrice.toFixed(2) }}</b></div>
              <div v-for="op in selectedOps" :key="'pp-'+op.userId" class="ppb-row sub">
                <span>· {{ op.name }}（{{ op.level || 'L1' }}）</span>
                <b>+ ¥{{ opSurcharge(op).toFixed(2) }}</b>
              </div>
              <div class="ppb-row total">
                <span>指派后总价</span>
                <b class="ppb-total">¥{{ estimatedPrice }}</b>
              </div>
              <div class="ppb-note">差价由客服线下退款 / 补收（多退少补）</div>
            </div>

            <div v-if="opsError" role="alert" class="empty-state">{{ opsError }} <button class="btn-secondary" @click="loadAssignableOps(currentOpOrder.gameCode)">重新加载</button></div>
            <div v-else-if="loadingOps" class="am-empty"><span class="dot"></span> 正在加载打手…</div>
            <div v-else-if="availableOps.length === 0" class="am-empty">该游戏暂无在线打手</div>

            <div v-else class="op-picker">
              <label
                v-for="op in availableOps"
                :key="op.userId"
                class="op-item"
                :class="{ on: isSelected(op.userId), off: !canChooseOp(op) || (!isSelected(op.userId) && isOpCountMaxed) }"
              >
                <input
                  type="checkbox"
                  :value="op.userId"
                  :checked="isSelected(op.userId)"
                  :disabled="assigning || !canChooseOp(op) || (!isSelected(op.userId) && isOpCountMaxed)"
                  @change="toggleOp(op.userId)"
                />
                <div class="op-avatar">{{ (op.name || '?').charAt(0) }}</div>
                <div class="op-info">
                  <div class="op-name">
                    {{ op.name }}
                    <span class="op-level">{{ op.level || 'L1' }}</span>
                    <OnlineStatusBadge :status="op.status || 'offline'" />
                  </div>
                  <div class="op-meta">
                    信誉 {{ op.reputation }} · 接单 {{ op.totalOrders }}
                    <span v-if="op.activeOrderCount > 0" class="op-busy">· 进行中 {{ op.activeOrderCount }}</span>
                  </div>
                </div>
              </label>
            </div>
          </div>

          <footer class="am-foot">
            <button class="btn-ghost" @click="closeAssign">取消</button>
            <button class="btn-solid" :disabled="!canAssign || assigning" @click="doAssign">
              {{ assigning ? '指派中…' : assignButtonText }}
            </button>
          </footer>
        </div>
      </div>
    </Teleport>

    <!-- ============ 换人弹窗 ============ -->
    <Teleport to="body">
      <div v-if="reassignOrder" class="assign-mask" @click.self="closeReassign">
        <div class="assign-modal" role="dialog" aria-modal="true" aria-label="安排订单打手">
          <header class="am-head">
            <div>
              <h3>🔄 换打手</h3>
              <p class="am-sub">
                订单 {{ reassignOrder.orderNo }} · {{ reassignOrder.gameName }} · {{ reassignOrder.orderTypeName }}
              </p>
            </div>
            <button class="close-icon" @click="closeReassign">×</button>
          </header>

          <div class="am-body">
            <div class="old-ops-box">
              <div class="oob-title">当前打手</div>
              <div class="oob-list">
                <span v-for="op in (reassignOrder.operators || [])" :key="op.userId" class="oob-chip">
                  {{ op.name }}
                </span>
              </div>
            </div>

            <div class="op-count-hint" :class="{ full: isOpCountMaxed, locked: isOpCountLocked }">
              <span class="och-label">新组合</span>
              <b class="och-count">{{ selectedOpIds.length }} / {{ opCountRange }}</b>
              <span v-if="isOpCountLocked" class="och-tag locked">必须 {{ minOpCount }} 人</span>
              <span v-else-if="isOpCountMaxed" class="och-tag full">已达上限</span>
              <span v-else class="och-tag">请选择 {{ minOpCount }}–{{ maxOpCount }} 位</span>
            </div>

            <div v-if="isLevelBased && selectedOps.length > 0" class="price-preview-box">
              <div class="ppb-row total">
                <span>换人后总价</span>
                <b class="ppb-total">¥{{ estimatedPrice }}</b>
              </div>
              <div class="ppb-note">差价由客服线下退款 / 补收</div>
            </div>

            <div v-if="opsError" role="alert" class="empty-state">{{ opsError }} <button class="btn-secondary" @click="loadAssignableOps(currentOpOrder.gameCode)">重新加载</button></div>
            <div v-else-if="loadingOps" class="am-empty"><span class="dot"></span> 正在加载打手…</div>
            <div v-else-if="availableOps.length === 0" class="am-empty">该游戏暂无在线打手</div>

            <div v-else class="op-picker">
              <label
                v-for="op in availableOps"
                :key="op.userId"
                class="op-item"
                :class="{ on: isSelected(op.userId), off: !canChooseOp(op) || (!isSelected(op.userId) && isOpCountMaxed) }"
              >
                <input
                  type="checkbox"
                  :value="op.userId"
                  :checked="isSelected(op.userId)"
                  :disabled="reassigning || !canChooseOp(op) || (!isSelected(op.userId) && isOpCountMaxed)"
                  @change="toggleOp(op.userId)"
                />
                <div class="op-avatar">{{ (op.name || '?').charAt(0) }}</div>
                <div class="op-info">
                  <div class="op-name">
                    {{ op.name }}
                    <span class="op-level">{{ op.level || 'L1' }}</span>
                    <OnlineStatusBadge :status="op.status || 'offline'" />
                  </div>
                  <div class="op-meta">信誉 {{ op.reputation }} · 接单 {{ op.totalOrders }}</div>
                </div>
              </label>
            </div>

            <div class="reason-box">
              <label class="reason-label">换人原因 <em>*</em></label>
              <textarea
                v-model="reassignReason"
                class="reason-textarea"
                rows="3"
                maxlength="500"
                placeholder="例如：连续炸单 3 把，老板要求换人"
              ></textarea>
            </div>
          </div>

          <footer class="am-foot">
            <button class="btn-ghost" @click="closeReassign">取消</button>
            <button
              class="btn-solid"
              :disabled="!canReassign || reassigning"
              @click="doReassign"
            >
              {{ reassigning ? '提交中…' : '确认换人' }}
            </button>
          </footer>
        </div>
      </div>
    </Teleport>

    <!-- ============ 弃单弹窗 ============ -->
    <Teleport to="body">
      <div v-if="abandonOrder" class="assign-mask" @click.self="closeAbandon">
        <div class="assign-modal small">
          <header class="am-head">
            <div>
              <h3>⚠ 弃单</h3>
              <p class="am-sub">
                订单 {{ abandonOrder.orderNo }} · {{ abandonOrder.gameName }} · ¥{{ abandonOrder.price }}
              </p>
            </div>
            <button class="close-icon" @click="closeAbandon">×</button>
          </header>

          <div class="am-body">
            <div class="abandon-warn">
              ⚠ 弃单后：<br />
              · 订单状态变为「已弃单」<br />
              · 打手不再获得收益<br />
              · 需<b>线下原价退款</b>给老板
            </div>

            <div class="reason-box">
              <label class="reason-label">弃单原因 <em>*</em></label>
              <textarea
                v-model="abandonReason"
                class="reason-textarea"
                rows="3"
                maxlength="500"
                placeholder="例如：连续炸单 5 把，老板不想继续"
              ></textarea>
            </div>
          </div>

          <footer class="am-foot">
            <button class="btn-ghost" @click="closeAbandon">取消</button>
            <button
              class="btn-solid danger"
              :disabled="!abandonReason.trim() || abandoning"
              @click="doAbandon"
            >
              {{ abandoning ? '提交中…' : '确认弃单' }}
            </button>
          </footer>
        </div>
      </div>
    </Teleport>

    <!-- ============ 处理客诉弹窗 ============ -->
    <Teleport to="body">
      <div v-if="handleComplaint" class="assign-mask" @click.self="closeHandleComplaint">
        <div class="assign-modal small">
          <header class="am-head">
            <div>
              <h3>处理客诉</h3>
              <p class="am-sub">
                {{ handleComplaint.orderNo }} · {{ handleComplaint.complaintTypeLabel }}
              </p>
            </div>
            <button class="close-icon" @click="closeHandleComplaint">×</button>
          </header>

          <div class="am-body">
            <div class="complaint-preview">
              <div class="cpv-label">老板投诉</div>
              <p class="cpv-text">{{ handleComplaint.description }}</p>
            </div>

            <div class="reason-box">
              <label class="reason-label">处理方式 <em>*</em></label>
              <div class="handle-actions">
                <label class="ha-item" :class="{ on: handleAction === 'resolve' }">
                  <input type="radio" value="resolve" v-model="handleAction" />
                  <span>✅ 已处理</span>
                  <small>客诉合理，已协商解决</small>
                </label>
                <label class="ha-item" :class="{ on: handleAction === 'reject' }">
                  <input type="radio" value="reject" v-model="handleAction" />
                  <span>❌ 驳回</span>
                  <small>客诉不成立</small>
                </label>
              </div>
            </div>

            <div class="reason-box">
              <label class="reason-label">回复老板 <em>*</em></label>
              <textarea
                v-model="handleNote"
                class="reason-textarea"
                rows="3"
                maxlength="2000"
                placeholder="例如：已联系打手核实，情况属实，向您道歉并补偿 20 元"
              ></textarea>
            </div>
          </div>

          <footer class="am-foot">
            <button class="btn-ghost" @click="closeHandleComplaint">取消</button>
            <button
              class="btn-solid"
              :disabled="!canHandle || handling"
              @click="doHandleComplaint"
            >
              {{ handling ? '提交中…' : '提交' }}
            </button>
          </footer>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script>
import { fmtTime, operatorText, isAllAccepted, showProgress } from './orderHelpers'
import { useUserStore } from '@/stores/user'
import Navbar            from './Navbar.vue'
import Footer            from './Footer.vue'
import SectionHeading    from './SectionHeading.vue'
import OnlineStatusBadge from './OnlineStatusBadge.vue'
import request           from '@/utils/request'

export default {
  name: 'CsDashboard',
  components: { Navbar, Footer, SectionHeading, OnlineStatusBadge },

  data() {
    return {
      showGuide: false,
      GUIDE_SECTIONS: [
        { id: '01', idx: '01', label: '收款确认流程' },
        { id: '02', idx: '02', label: '指派打手流程' },
        { id: '03', idx: '03', label: '换打手 / 弃单' },
        { id: '04', idx: '04', label: '客诉处理' },
        { id: '05', idx: '05', label: '常见问题' }
      ],

      loadingMe: true,
      hasCs: false,
      csAuditStatus: 'pending',
      csAuditNote: '',
      csInfo: { id: '', name: '', wechat: '', status: 'offline', currentLoad: 0, capacity: 10, reviewCount: 0, avgOverall: 0 },
      csStatus: 'offline',
      statusChanging: false,

      loadingOrders: false,
      orders: [],
      tab: 'waiting-confirm',
      page: 1,
      pageSize: 20,
      total: 0,
      tabCounts: {
        'waiting-confirm': 0,
        'waiting-assign': 0,
        processing: 0,
        completed: 0,
        cancelled: 0,
        complaints: 0,
        all: 0
      },
      acting: '',

      assignOrder: null,
      availableOps: [],
      loadingOps: false,
      opsRequest: 0,
      ordersRequest: 0,
      opsError: '',
      selectedOpIds: [],
      assigning: false,

      reassignOrder: null,
      reassignReason: '',
      reassigning: false,

      abandonOrder: null,
      abandonReason: '',
      abandoning: false,

      complaints: [],
      complaintsTotal: 0,
      complaintsPage: 1,
      complaintsPageSize: 20,
      loadingComplaints: false,

      handleComplaint: null,
      handleAction: 'resolve',
      handleNote: '',
      handling: false,

      csInfoTimer: null,

      TABS: [
        { label: '待确认收款', value: 'waiting-confirm' },
        { label: '待指派打手', value: 'waiting-assign' },
        { label: '处理中',     value: 'processing' },
        { label: '客诉',       value: 'complaints' },
        { label: '已完成',     value: 'completed' },
        { label: '已取消',     value: 'cancelled' },
        { label: '全部',       value: 'all' }
      ]
    }
  },

  computed: {
    isLoggedIn() { return useUserStore().isLoggedIn },

    auditTitle() {
      return {
        pending: '客服申请审核中',
        rejected: '客服申请未通过',
        banned: '客服账号已封禁'
      }[this.csAuditStatus] || '客服申请状态异常'
    },

    auditDesc() {
      if (this.csAuditNote) return this.csAuditNote
      return {
        pending: '我们会尽快审核，请留意通知',
        rejected: '如有疑问请联系管理员，或修改信息后重新提交',
        banned: '如需申诉请联系管理员'
      }[this.csAuditStatus] || ''
    },

    emptyText() {
      return {
        'waiting-confirm': '当前没有待确认收款的订单',
        'waiting-assign':  '当前没有待指派打手的订单',
        processing:        '当前没有处理中的订单',
        completed:         '暂无已完成订单',
        cancelled:         '暂无已取消订单',
        all:               '暂无订单'
      }[this.tab] || '暂无订单'
    },

    minOpCount() { return Number(this.currentOpOrder?.minOperatorCount) || 1 },
    maxOpCount() { return Number(this.currentOpOrder?.maxOperatorCount) || 1 },
    currentOpOrder() { return this.reassignOrder || this.assignOrder },
    opCountRange() {
      return this.minOpCount === this.maxOpCount ? `${this.minOpCount}` : `${this.minOpCount}-${this.maxOpCount}`
    },
    isOpCountLocked() { return this.minOpCount === this.maxOpCount },
    isOpCountMaxed() { return this.selectedOpIds.length >= this.maxOpCount },
    isLevelBased() { return this.currentOpOrder?.pricingMode === 'level-based' },
    basePrice() {
      const b = Number(this.currentOpOrder?.basePrice)
      if (b > 0) return b
      return Number(this.currentOpOrder?.price) || 0
    },
    selectedOps() { return this.availableOps.filter(op => this.selectedOpIds.includes(op.userId)) },
    estimatedPrice() {
      if (!this.isLevelBased) return this.basePrice.toFixed(2)
      let total = this.basePrice
      for (const op of this.selectedOps) total += this.basePrice * (Number(op.levelSurchargeRate) || 0)
      return total.toFixed(2)
    },
    canAssign() {
      if (this.loadingOps || this.opsError) return false
      if (this.selectedOpIds.some(id => !this.availableOps.some(op => op.userId === id && this.canChooseOp(op)))) return false
      if (this.selectedOpIds.length < this.minOpCount) return false
      if (this.selectedOpIds.length > this.maxOpCount) return false
      return true
    },
    assignButtonText() {
      const n = this.selectedOpIds.length
      return n === 0 ? '确认指派' : `确认指派 ${n} 人`
    },
    canReassign() {
      return !!this.reassignReason.trim() && this.canAssign
    },
    canHandle() {
      if (!this.handleNote.trim()) return false
      if (!this.handleAction) return false
      return true
    }
  },

  async mounted() {
    if (!this.isLoggedIn) { this.loadingMe = false; return }
    await this.loadCsInfo()
    this.loadingMe = false
    if (this.hasCs && this.csAuditStatus === 'approved') {
      await this.refreshOrders()
      this.startCsInfoTimer()
    }
  },

  beforeUnmount() { this.stopCsInfoTimer() },

  methods: {
    scrollToSection(id) {
      const el = document.getElementById('g-' + id)
      if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' })
    },
    goWallet() { this.$router.push('/taichu/cs/wallet') },

    startCsInfoTimer() {
      this.stopCsInfoTimer()
      this.csInfoTimer = setInterval(() => {
        if (this.hasCs && this.csAuditStatus === 'approved') this.loadCsInfo()
      }, 30000)
    },
    stopCsInfoTimer() {
      if (this.csInfoTimer) { clearInterval(this.csInfoTimer); this.csInfoTimer = null }
    },

    async loadCsInfo() {
      try {
        const res = await request.get('/club/cs/me')
        const data = res?.data ?? res
        if (!data?.hasApplied) { this.hasCs = false; return }
        this.hasCs = true
        this.csAuditStatus = data.auditStatus || 'pending'
        this.csAuditNote = data.auditNote || ''
        this.csStatus = data.status || 'offline'
        this.csInfo = {
          id: data.id, name: data.name || '', wechat: data.wechat || '',
          status: data.status || 'offline', currentLoad: data.currentLoad || 0,
          capacity: data.capacity || 10, reviewCount: data.reviewCount || 0,
          avgOverall: data.avgOverall || 0
        }
      } catch (e) {
        console.error('读取客服信息失败', e)
        this.hasCs = false
      }
    },

    async switchStatus(status) {
      if (this.statusChanging || status === this.csStatus) return
      this.statusChanging = true
      try {
        const res = await request.post('/club/cs/me/status', { status })
        const data = res?.data ?? res
        this.csStatus = data?.status || status
      } catch (e) {
        alert(e?.response?.data?.message || e.message || '切换失败')
      } finally {
        this.statusChanging = false
      }
    },

    async loadOrders() {
      if (this.tab === 'complaints') return this.loadComplaints()
      const tab = this.tab
      const requestId = ++this.ordersRequest
      this.loadingOrders = true
      try {
        const res = await request.get('/club/cs/orders', {
          params: { tab, page: this.page, pageSize: this.pageSize }
        })
        const data = res?.data ?? res
        if (requestId !== this.ordersRequest || tab !== this.tab) return false
        this.orders = data?.items ?? []
        this.total = data?.total ?? 0
        this.tabCounts[tab] = this.total
        return true
      } catch (e) {
        if (requestId !== this.ordersRequest || tab !== this.tab) return false
        console.error('加载订单失败', e)
        this.orders = []
        this.total = 0
        return false
      } finally {
        if (requestId === this.ordersRequest) this.loadingOrders = false
      }
    },

    async refreshOrders() {
      const tab = this.tab
      const [loaded] = await Promise.all([this.loadOrders(), this.refreshAllTabsCount(tab)])
      // 列表失败时仍单独读取数量，保留原先统计请求独立失败/成功的行为。
      if (loaded === false) await this.loadTabCount(tab)
    },

    refreshAllTabsCount(loadedTab) {
      // 客诉标签只统计 pending，不能复用客诉列表总数。
      return Promise.all(this.TABS
        .filter(tab => tab.value !== loadedTab || tab.value === 'complaints')
        .map(tab => this.loadTabCount(tab.value)))
    },

    async loadTabCount(tab) {
      const complaints = tab === 'complaints'
      try {
        const res = await request.get(complaints ? '/club/cs/complaints' : '/club/cs/orders', {
          params: { ...(complaints ? { status: 'pending' } : { tab }), page: 1, pageSize: 1 }
        })
        const data = res?.data ?? res
        this.tabCounts[tab] = data?.total ?? 0
      } catch { this.tabCounts[tab] = 0 }
    },

    async switchTab(val) {
      if (this.tab === val) return
      this.tab = val
      this.page = 1
      this.complaintsPage = 1
      await this.loadOrders()
    },

    changePage(p) { this.page = p; this.loadOrders() },

    async loadComplaints() {
      this.loadingComplaints = true
      try {
        const res = await request.get('/club/cs/complaints', {
          params: { page: this.complaintsPage, pageSize: this.complaintsPageSize }
        })
        const data = res?.data ?? res
        this.complaints = data?.items ?? []
        this.complaintsTotal = data?.total ?? 0
      } catch (e) {
        console.error('加载客诉失败', e)
        this.complaints = []
        this.complaintsTotal = 0
      } finally {
        this.loadingComplaints = false
      }
    },

    changeComplaintsPage(p) { this.complaintsPage = p; this.loadComplaints() },

    complaintStatusText(s) {
      return { pending: '待处理', resolved: '已处理', rejected: '已驳回' }[s] || s
    },

    needsConfirm(o) {
      return o.status === 'pending' && o.userPaidAt && !o.paidConfirmedAt
    },
    needsAssign(o) {
      return o.status === 'pending' && o.paidConfirmedAt && !o.operatorUserId
    },
    isUrgent(o) { return this.needsConfirm(o) || this.needsAssign(o) },

    operatorText,

    // ⭐ 是否显示接单进度（多打手 或 已有人接）
    showProgress,

    // ⭐ 是否全员已接
    isAllAccepted,

    async confirmPaid(o) {
      if (this.acting) return
      if (!confirm(`确认订单 ${o.orderNo} 已收到款项？`)) return
      this.acting = o.orderNo
      try {
        await request.post(`/club/cs/orders/${o.orderNo}/confirm-paid`)
        await Promise.all([this.refreshOrders(), this.loadCsInfo()])
      } catch (e) {
        alert(e?.response?.data?.message || e.message || '操作失败')
      } finally { this.acting = '' }
    },

    async cancelOrder(o) {
      const reason = prompt(`取消订单【${o.orderNo}】？\n请输入取消原因（必填）:`)
      if (reason === null) return
      if (!reason.trim()) return alert('请填写取消原因')
      this.acting = o.orderNo
      try {
        await request.post(`/club/orders/${o.orderNo}/cancel`, { reason: reason.trim() })
        await Promise.all([this.refreshOrders(), this.loadCsInfo()])
      } catch (e) {
        alert(e?.response?.data?.message || e.message || '操作失败')
      } finally { this.acting = '' }
    },

    openAssign(o) {
      if (this.assigning || this.reassigning) return
      this.assignOrder = o
      this.selectedOpIds = []
      this.loadAssignableOps(o.gameCode)
    },
    closeAssign(completed = false) {
      if ((this.assigning || this.reassigning) && completed !== true) return
      this.opsRequest++
      this.loadingOps = false
      this.assignOrder = null
      this.availableOps = []
      this.selectedOpIds = []
    },

    openReassign(o) {
      if (this.assigning || this.reassigning) return
      this.reassignOrder = o
      this.reassignReason = ''
      this.selectedOpIds = (o.operators || []).map(op => op.userId)
      this.loadAssignableOps(o.gameCode)
    },
    closeReassign(completed = false) {
      if ((this.assigning || this.reassigning) && completed !== true) return
      this.opsRequest++
      this.loadingOps = false
      this.reassignOrder = null
      this.reassignReason = ''
      this.availableOps = []
      this.selectedOpIds = []
    },

    openAbandon(o) {
      this.abandonOrder = o
      this.abandonReason = ''
    },
    closeAbandon() {
      this.abandonOrder = null
      this.abandonReason = ''
    },

    async loadAssignableOps(gameCode) {
      const requestId = ++this.opsRequest
      this.availableOps = []
      this.opsError = ''
      this.loadingOps = true
      try {
        const res = await request.get('/club/cs/operators', { params: { gameCode } })
        const data = res?.data ?? res
        if (requestId !== this.opsRequest) return
        this.availableOps = data?.items ?? []
      } catch (e) {
        if (requestId !== this.opsRequest) return
        this.opsError = '打手加载失败，请重试'
        console.error('加载打手失败', e)
        this.availableOps = []
      } finally { if (requestId === this.opsRequest) this.loadingOps = false }
    },

    isSelected(opId) { return this.selectedOpIds.includes(opId) },
    canChooseOp(op) {
      return op.status !== 'offline' || !!this.reassignOrder?.operators?.some(current => current.userId === op.userId)
    },
    toggleOp(opId) {
      if (this.loadingOps || this.assigning || this.reassigning) return
      if (!this.availableOps.some(op => op.userId === opId && this.canChooseOp(op))) return
      const idx = this.selectedOpIds.indexOf(opId)
      if (idx >= 0) {
        this.selectedOpIds.splice(idx, 1)
      } else {
        if (this.selectedOpIds.length >= this.maxOpCount) {
          alert(`本订单最多选择 ${this.maxOpCount} 位打手`)
          return
        }
        this.selectedOpIds.push(opId)
      }
    },
    opSurcharge(op) { return this.basePrice * (Number(op.levelSurchargeRate) || 0) },

    async doAssign() {
      if (this.assigning || !this.canAssign || !this.assignOrder) return
      this.assigning = true
      try {
        await request.post(
          `/club/cs/orders/${this.assignOrder.orderNo}/assign-operator`,
          { operatorUserIds: [...this.selectedOpIds] }
        )
        this.closeAssign(true)
        await this.refreshOrders()
      } catch (e) {
        alert(e?.response?.data?.message || e.message || '指派失败')
      } finally { this.assigning = false }
    },

    async doReassign() {
      if (this.reassigning || !this.canReassign || !this.reassignOrder) return
      this.reassigning = true
      try {
        await request.post(
          `/club/cs/orders/${this.reassignOrder.orderNo}/reassign-operators`,
          {
            newOperatorUserIds: [...this.selectedOpIds],
            reason: this.reassignReason.trim()
          }
        )
        this.closeReassign(true)
        await this.refreshOrders()
        alert('换人成功')
      } catch (e) {
        alert(e?.response?.data?.message || e.message || '换人失败')
      } finally { this.reassigning = false }
    },

    async doAbandon() {
      if (!this.abandonOrder || !this.abandonReason.trim()) return
      if (!confirm(`确认弃单？\n订单：${this.abandonOrder.orderNo}\n原因：${this.abandonReason.trim()}\n\n弃单后需线下退款给老板。`)) return
      this.abandoning = true
      try {
        await request.post(
          `/club/cs/orders/${this.abandonOrder.orderNo}/abandon`,
          { reason: this.abandonReason.trim() }
        )
        this.closeAbandon()
        await Promise.all([this.refreshOrders(), this.loadCsInfo()])
        alert('已弃单，请线下退款给老板')
      } catch (e) {
        alert(e?.response?.data?.message || e.message || '弃单失败')
      } finally { this.abandoning = false }
    },

    openHandleComplaint(c) {
      this.handleComplaint = c
      this.handleAction = 'resolve'
      this.handleNote = ''
    },
    closeHandleComplaint() {
      this.handleComplaint = null
      this.handleAction = 'resolve'
      this.handleNote = ''
    },
    async doHandleComplaint() {
      if (!this.canHandle || !this.handleComplaint) return
      this.handling = true
      try {
        await request.post(
          `/club/cs/complaints/${this.handleComplaint.id}/handle`,
          { action: this.handleAction, note: this.handleNote.trim() }
        )
        this.closeHandleComplaint()
        await this.loadComplaints()
        await this.refreshAllTabsCount()
        alert('已提交')
      } catch (e) {
        alert(e?.response?.data?.message || e.message || '提交失败')
      } finally { this.handling = false }
    },

    deriveStatus(o) {
      if (o.status === 'cancelled') return 'cancelled'
      if (o.status === 'abandoned') return 'abandoned'
      if (o.status === 'completed') return 'completed'
      if (o.status === 'processing' || o.status === 'accepted') return 'processing'
      if (o.status === 'pending') {
        if (!o.userPaidAt) return 'unpaid'
        if (!o.paidConfirmedAt) return 'waiting-confirm'
        if (!o.operatorUserId) return 'waiting-assign'
        return 'waiting-accept'
      }
      return 'other'
    },
    statusText(o) {
      return {
        unpaid:            '待老板付款',
        'waiting-confirm': '待确认收款',
        'waiting-assign':  '待指派打手',
        'waiting-accept':  '待打手接单',
        processing:        '服务中',
        completed:         '已完成',
        abandoned:         '已弃单',
        cancelled:         '已取消'
      }[this.deriveStatus(o)] || o.status
    },
    shortId(uid) { return uid ? String(uid).substring(0, 8).toUpperCase() : '—' },
    fmtTime
  }
}
</script>

<style scoped>
.page-hero { padding: 120px 0 40px; border-bottom: 1px solid var(--line); }

.cs-loading { padding: 32px 24px; text-align: center; font-family: monospace; font-size: 12px; color: var(--text-muted); border: 1px dashed var(--line); background: var(--panel); }
.cs-guard { padding: 40px 32px; text-align: center; border: 1px solid var(--line); background: var(--panel); }
.cs-guard.pending { border-color: rgba(255, 107, 26, 0.35); }
.cs-guard.rejected { border-color: rgba(255, 80, 80, 0.35); }
.cs-guard.banned { border-color: rgba(255, 80, 80, 0.45); }
.guard-title { margin: 0 0 8px; font-size: 16px; font-weight: 700; color: var(--text); letter-spacing: 1px; }
.guard-desc { margin: 0 0 16px; font-family: monospace; font-size: 12px; color: var(--text-muted); line-height: 1.7; }
.guard-link { display: inline-block; padding: 8px 18px; font-size: 12px; letter-spacing: 1px; color: var(--orange); text-decoration: none; border: 1px solid rgba(255, 107, 26, 0.4); }
.guard-link:hover { color: var(--on-accent); background: var(--orange); border-color: var(--orange); }

.cs-header { display: flex; align-items: center; justify-content: space-between; gap: 20px; padding: 18px 22px; border: 1px solid var(--line); background: var(--panel); border-radius: 3px; transition: border-color 0.25s; }
.cs-header.online { border-color: rgba(120, 215, 206, 0.35); background: rgba(120, 215, 206, 0.04); }
.cs-header.busy { border-color: rgba(255, 107, 26, 0.35); background: rgba(255, 107, 26, 0.04); }
.cs-header.offline { border-color: rgba(89, 102, 115, 0.35); background: rgba(89, 102, 115, 0.05); }
.cs-header-left { display: flex; align-items: center; gap: 14px; min-width: 0; }
.cs-info { min-width: 0; }
.cs-name { font-size: 16px; font-weight: 700; letter-spacing: 1px; color: var(--text); margin-bottom: 4px; }
.cs-meta { font-family: monospace; font-size: 12px; color: var(--text-muted); }
.cs-meta b { color: var(--cyan); font-weight: 700; }
.cs-rate { color: var(--warning); }
.cs-header-right { display: flex; gap: 8px; flex-shrink: 0; align-items: center; flex-wrap: wrap; }
.cs-status-btn { padding: 8px 18px; font-family: monospace; font-size: 12px; letter-spacing: 1px; color: var(--text-soft); background: transparent; border: 1px solid var(--line-bright); cursor: pointer; transition: all 0.2s; border-radius: 2px; }
.cs-status-btn:hover:not(:disabled):not(.on) { color: var(--text); border-color: var(--text-soft); }
.cs-status-btn.on { color: var(--orange); border-color: rgba(255, 107, 26, 0.5); background: rgba(255, 107, 26, 0.1); }
.cs-status-btn:disabled { opacity: 0.4; cursor: not-allowed; }

.cs-guide-btn, .cs-wallet-btn { padding: 8px 16px; font-size: 12px; letter-spacing: 1px; cursor: pointer; transition: all 0.2s; border-radius: 2px; margin-right: 4px; }
.cs-guide-btn { color: var(--cyan); background: rgba(120, 215, 206, 0.06); border: 1px solid rgba(120, 215, 206, 0.35); }
.cs-guide-btn:hover { color: var(--text); background: var(--cyan); border-color: var(--cyan); }
.cs-wallet-btn { color: var(--warning); background: rgba(255, 209, 102, 0.06); border: 1px solid rgba(255, 209, 102, 0.35); }
.cs-wallet-btn:hover { color: var(--text-primary); background: var(--warning); border-color: var(--warning); }

.cs-tabs { display: flex; flex-wrap: wrap; gap: 6px; margin-bottom: 26px; padding-bottom: 14px; border-bottom: 1px solid var(--line); }
.cs-tab { padding: 8px 16px; font-size: 12px; letter-spacing: 1px; color: var(--text-soft); background: transparent; border: 1px solid var(--line); cursor: pointer; transition: 0.2s; display: inline-flex; align-items: center; gap: 6px; }
.cs-tab:hover { color: var(--text); border-color: var(--line-bright); }
.cs-tab.on { color: var(--orange); border-color: rgba(255, 107, 26, 0.5); background: rgba(255, 107, 26, 0.06); }
.tab-count { font-family: monospace; font-size: 12px; padding: 1px 6px; background: var(--bg-soft); border-radius: 3px; }
.cs-tab.on .tab-count { background: rgba(255, 107, 26, 0.15); }

.op-state { padding: 60px 32px; text-align: center; font-family: monospace; font-size: 12px; letter-spacing: 1px; color: var(--text-muted); border: 1px dashed var(--line); background: var(--panel); }
.dot { display: inline-block; width: 6px; height: 6px; margin-right: 6px; border-radius: 50%; background: var(--orange); animation: pulse 1.4s infinite; }
@keyframes pulse { 50% { opacity: 0.3; } }

.order-list { display: flex; flex-direction: column; gap: 16px; }
.order-card { border: 1px solid var(--line); background: var(--panel); transition: border-color 0.2s, background 0.2s; }
.order-card:hover { border-color: var(--line-bright); }
.order-card.is-urgent { border-color: rgba(255, 107, 26, 0.4); background: linear-gradient(90deg, rgba(255, 107, 26, 0.05), var(--panel) 30%); }

.oc-head { display: flex; justify-content: space-between; align-items: center; padding: 16px 20px; border-bottom: 1px solid var(--line); }
.oc-no { display: flex; flex-direction: column; gap: 4px; }
.oc-label { font-family: monospace; font-size: 12px; letter-spacing: 1px; color: var(--text-muted); }
.oc-code { font-size: 13px; color: var(--text); }
.mono { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; }
.oc-head-right { display: flex; align-items: center; gap: 10px; }
.oc-urgent-tag { padding: 3px 10px; font-family: monospace; font-size: 12px; letter-spacing: 1px; color: var(--orange); border: 1px solid rgba(255, 107, 26, 0.45); background: rgba(255, 107, 26, 0.1); border-radius: 3px; animation: urgentPulse 2s infinite; }
@keyframes urgentPulse { 50% { opacity: 0.55; } }
.oc-status { padding: 4px 12px; font-size: 12px; font-weight: 600; letter-spacing: 1px; border-radius: 3px; }
.oc-status.unpaid { background: var(--bg-soft); color: var(--text-muted); }
.oc-status.waiting-confirm { background: rgba(255, 140, 74, 0.12); color: var(--accent-color); }
.oc-status.waiting-assign { background: rgba(165, 165, 255, 0.12); color: var(--accent-color); }
.oc-status.waiting-accept { background: rgba(120, 215, 206, 0.12); color: var(--cyan); }
.oc-status.processing { background: rgba(120, 215, 206, 0.12); color: var(--cyan); }
.oc-status.completed { background: rgba(80, 200, 120, 0.12); color: var(--success); }
.oc-status.abandoned { background: rgba(255, 140, 74, 0.15); color: var(--accent-color); }
.oc-status.cancelled { background: var(--bg-soft); color: var(--text-muted); }

.oc-body { display: flex; justify-content: space-between; gap: 20px; padding: 16px 20px; }
.oc-info { flex: 1; display: flex; flex-direction: column; gap: 8px; }
.oc-row { display: flex; gap: 12px; font-size: 13px; align-items: center; }
.oc-row span { color: var(--text-muted); min-width: 60px; }
.oc-row b { color: var(--text); }
.oc-remark { color: var(--text-soft) !important; font-weight: 400; }
.op-linked { color: var(--cyan) !important; }
.op-unassigned { color: var(--text-muted) !important; font-weight: 400; }
.oc-failed { color: var(--accent-color) !important; }
.oc-op-count { font-size: 12px; color: var(--text-muted); margin-left: 4px; }
.oc-op-inline { font-size: 12px; color: var(--text-muted); margin-left: 6px; font-weight: 400; }
.oc-proof-link { font-size: 12px; color: var(--cyan); text-decoration: none; padding: 2px 8px; border: 1px solid rgba(120, 215, 206, 0.35); background: rgba(120, 215, 206, 0.06); border-radius: 3px; transition: all 0.2s; }
.oc-proof-link:hover { background: rgba(120, 215, 206, 0.15); border-color: rgba(120, 215, 206, 0.6); }

.oc-side { flex-shrink: 0; display: flex; flex-direction: column; align-items: flex-end; justify-content: center; gap: 6px; }
.oc-price { font-family: monospace; font-size: 24px; font-weight: 700; color: var(--orange); }
.oc-time { font-size: 12px; color: var(--text-muted); }

/* ⭐ 接单进度 */
.oc-progress { padding: 12px 20px; background: var(--panel); border-top: 1px solid var(--line); display: flex; flex-direction: column; gap: 10px; }
.op-progress-head { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
.oph-label { font-family: monospace; font-size: 12px; letter-spacing: 1px; color: var(--text-muted); }
.oph-count { font-family: monospace; font-size: 14px; font-weight: 700; color: var(--orange); }
.oph-count.done { color: var(--success); }
.oph-tag { margin-left: auto; padding: 2px 8px; font-family: monospace; font-size: 12px; border-radius: 2px; color: var(--text-secondary); border: 1px solid var(--line); }
.oph-tag.done { color: var(--success); border-color: rgba(80, 200, 120, 0.5); background: rgba(80, 200, 120, 0.1); }
.op-progress-list { display: flex; flex-wrap: wrap; gap: 8px; }
.op-chip { display: inline-flex; align-items: center; gap: 6px; padding: 4px 10px; font-size: 12px; color: var(--text-muted); border: 1px solid var(--line); background: var(--bg-soft); border-radius: 3px; }
.op-chip.accepted { color: var(--cyan); border-color: rgba(120, 215, 206, 0.4); background: rgba(120, 215, 206, 0.06); }
.chip-status { font-family: monospace; font-weight: 700; }
.chip-name { color: inherit; }

/* 换人历史 */
.oc-reassign { padding: 12px 20px; background: rgba(255, 140, 74, 0.04); border-top: 1px solid rgba(255, 140, 74, 0.2); }
.ocr-title { font-family: monospace; font-size: 12px; letter-spacing: 2px; color: var(--accent-color); margin-bottom: 10px; }
.ocr-item { padding: 8px 12px; border-left: 2px solid rgba(255, 140, 74, 0.4); background: var(--panel); border-radius: 0 3px 3px 0; margin-bottom: 6px; }
.ocr-item:last-child { margin-bottom: 0; }
.ocr-note { font-size: 12px; color: var(--text-soft); line-height: 1.6; margin-bottom: 4px; }
.ocr-time { font-family: monospace; font-size: 12px; color: var(--text-muted); text-align: right; }

.oc-timeline { display: flex; align-items: center; gap: 8px; padding: 10px 20px; font-family: monospace; font-size: 12px; color: var(--text-muted); background: var(--panel); border-top: 1px solid var(--line); overflow-x: auto; white-space: nowrap; }
.oc-timeline > span:not(.tl-arrow) { padding: 3px 10px; border: 1px solid var(--line); border-radius: 2px; transition: all 0.25s; }
.oc-timeline > span.done { color: var(--cyan); border-color: rgba(120, 215, 206, 0.4); background: rgba(120, 215, 206, 0.06); }
.tl-arrow { color: var(--text-muted); }

.oc-foot { display: flex; gap: 10px; justify-content: flex-end; padding: 14px 20px; border-top: 1px solid var(--line); flex-wrap: wrap; }
.oc-btn { padding: 8px 20px; font-size: 12px; letter-spacing: 1px; cursor: pointer; transition: all 0.2s; border-radius: 3px; }
.oc-btn:disabled { opacity: 0.5; cursor: not-allowed; }
.oc-btn.primary { color: var(--on-accent); background: var(--orange); border: 1px solid var(--orange); }
.oc-btn.primary:hover:not(:disabled) { background: var(--orange-bright); }
.oc-btn.ghost { color: var(--text-soft); background: transparent; border: 1px solid var(--line-bright); }
.oc-btn.ghost:hover:not(:disabled) { color: var(--text); border-color: var(--text-soft); }
.oc-btn.warn { color: var(--text); background: var(--accent-color); border: 1px solid var(--accent-color); }
.oc-btn.warn:hover:not(:disabled) { background: var(--accent-color); }
.oc-btn.danger { color: var(--text); background: var(--danger); border: 1px solid var(--danger); }
.oc-btn.danger:hover:not(:disabled) { background: var(--danger); }

.cs-pager { display: flex; align-items: center; justify-content: center; gap: 16px; margin-top: 22px; font-family: monospace; font-size: 12px; color: var(--text-muted); }
.cs-pager button { padding: 6px 14px; font-size: 12px; color: var(--text-soft); background: transparent; border: 1px solid var(--line-bright); cursor: pointer; }
.cs-pager button:hover:not(:disabled) { color: var(--text); border-color: var(--text-soft); }
.cs-pager button:disabled { opacity: 0.4; cursor: not-allowed; }

/* 客诉卡片 */
.cp-list { display: flex; flex-direction: column; gap: 16px; }
.cp-card { border: 1px solid var(--line); background: var(--panel); }
.cp-card:hover { border-color: var(--line-bright); }
.cp-card.pending { border-left: 3px solid var(--accent-color); }
.cp-card.resolved { border-left: 3px solid var(--success); }
.cp-card.rejected { border-left: 3px solid var(--danger); }
.cpc-head { display: flex; justify-content: space-between; align-items: center; padding: 16px 20px; border-bottom: 1px solid var(--line); }
.cpc-left { display: flex; align-items: center; gap: 14px; min-width: 0; }
.cpc-no { font-size: 13px; color: var(--text); }
.cpc-type { padding: 2px 8px; font-size: 12px; color: var(--accent-color); background: rgba(255, 140, 74, 0.1); border: 1px solid rgba(255, 140, 74, 0.3); border-radius: 3px; }
.cpc-status { padding: 4px 12px; font-size: 12px; font-weight: 600; letter-spacing: 1px; border-radius: 3px; white-space: nowrap; }
.cpc-status.pending { background: rgba(255, 140, 74, 0.12); color: var(--accent-color); }
.cpc-status.resolved { background: rgba(80, 200, 120, 0.12); color: var(--success); }
.cpc-status.rejected { background: rgba(255, 80, 80, 0.12); color: var(--danger); }
.cpc-body { padding: 14px 20px; display: flex; flex-direction: column; gap: 8px; }
.cpc-row { display: flex; gap: 12px; font-size: 13px; }
.cpc-row span { color: var(--text-muted); min-width: 70px; }
.cpc-row b { color: var(--text); font-weight: 500; }
.cpc-section { padding: 14px 20px; border-top: 1px solid var(--line); }
.cps-label { font-family: monospace; font-size: 12px; letter-spacing: 2px; color: var(--text-muted); margin-bottom: 8px; }
.cps-text { margin: 0; font-size: 13px; line-height: 1.8; color: var(--text-soft); white-space: pre-wrap; word-break: break-word; }
.cps-evidence { display: inline-block; border: 1px solid var(--line); border-radius: 3px; overflow: hidden; }
.cps-evidence img { display: block; max-width: 240px; max-height: 180px; object-fit: cover; }
.cpc-reply { padding: 14px 20px; background: var(--panel); border-top: 1px solid var(--line); }
.cpc-reply.resolved { background: rgba(80, 200, 120, 0.04); }
.cpc-reply.rejected { background: rgba(255, 80, 80, 0.04); }
.cpr-head { display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; }
.cpr-name { font-size: 13px; font-weight: 700; color: var(--cyan); }
.cpr-time { font-size: 12px; color: var(--text-muted); }
.cpr-note { margin: 0; font-size: 13px; line-height: 1.8; color: var(--text-soft); white-space: pre-wrap; }

/* 弹窗 */
.guide-mask, .assign-mask { position: fixed; inset: 0; z-index: 9999; background: var(--overlay); backdrop-filter: blur(8px); display: grid; place-items: center; padding: 24px; }
.guide-modal { width: min(760px, 100%); max-height: 85vh; overflow: hidden; display: flex; flex-direction: column; border: 1px solid var(--line); background: var(--paper-card); color: var(--text-primary); border-radius: 4px; }
.assign-modal { width: min(640px, 100%); max-height: 88dvh; overflow-y: auto; border: 1px solid var(--line); background: var(--paper-card); color: var(--text-primary); border-radius: 16px; box-shadow: 0 20px 70px var(--overlay); }
.assign-modal .am-head { position: sticky; top: 0; z-index: 2; background: var(--paper-card); }
.assign-modal .am-foot { position: sticky; bottom: 0; z-index: 2; background: var(--paper-card); }
.assign-modal button:disabled { opacity: .55; cursor: not-allowed; }
.assign-modal.small { width: min(480px, 100%); }

.gm-head, .am-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 20px; padding: 20px 24px 16px; border-bottom: 1px solid var(--bg-soft); }
.gm-head h3, .am-head h3 { margin: 0 0 6px; font-size: 17px; font-weight: 700; letter-spacing: 1px; color: var(--text-primary); }
.gm-sub, .am-sub { margin: 0; font-family: monospace; font-size: 12px; color: var(--text-secondary); }
.close-icon { background: none; border: none; font-size: 24px; color: var(--text-secondary); cursor: pointer; line-height: 1; }
.close-icon:hover { color: var(--text-primary); }

.gm-body { display: grid; grid-template-columns: 180px 1fr; overflow: hidden; flex: 1; min-height: 0; }
.gm-toc { display: flex; flex-direction: column; gap: 4px; padding: 20px 16px; border-right: 1px solid var(--bg-soft); overflow-y: auto; background: var(--panel); }
.toc-a { display: flex; align-items: center; gap: 10px; padding: 8px 12px; font-size: 12px; color: var(--text-secondary); text-decoration: none; border-radius: 3px; cursor: pointer; }
.toc-a:hover { color: var(--accent-color); background: rgba(255, 107, 26, 0.08); }
.toc-num { font-family: monospace; font-size: 12px; color: var(--text-secondary); }
.gm-content { padding: 20px 26px; overflow-y: auto; }
.gd-sec { margin-bottom: 34px; scroll-margin-top: 20px; }
.gd-sec h4 { display: flex; align-items: center; gap: 12px; margin: 0 0 14px; padding-bottom: 10px; border-bottom: 1px solid var(--bg-soft); font-size: 15px; font-weight: 700; color: var(--text-primary); }
.gd-sec h4 span { font-family: monospace; font-size: 12px; color: var(--accent-color); }
.gd-steps { list-style: none; padding: 0; margin: 0 0 14px; display: flex; flex-direction: column; gap: 10px; }
.gd-steps li { display: flex; flex-direction: column; gap: 5px; padding: 12px 14px; border-left: 2px solid rgba(255, 107, 26, 0.5); background: var(--bg-soft); border-radius: 0 3px 3px 0; }
.gd-steps li b { font-size: 13px; color: var(--text-primary); }
.gd-steps li p { margin: 0; font-size: 12px; line-height: 1.7; color: var(--text-secondary); }
.gd-steps li code { padding: 2px 6px; font-family: monospace; font-size: 12px; color: var(--success); background: rgba(120, 215, 206, 0.08); border-radius: 3px; }
.gd-warn, .gd-tip { padding: 12px 14px; margin-top: 12px; font-size: 12px; line-height: 1.7; color: var(--text-secondary); border-radius: 0 3px 3px 0; }
.gd-warn { border-left: 3px solid var(--accent-color); background: rgba(255, 140, 74, 0.06); }
.gd-warn b { color: var(--accent-color); }
.gd-tip { border-left: 3px solid var(--success); background: rgba(120, 215, 206, 0.06); }
.gd-tip b { color: var(--success); }
.faq { display: flex; flex-direction: column; gap: 10px; }
.faq-item { padding: 12px 14px; border: 1px solid var(--bg-soft); background: var(--bg-soft); border-radius: 3px; }
.faq-q { font-size: 13px; font-weight: 600; color: var(--text-primary); margin-bottom: 6px; }
.faq-a { font-size: 12px; line-height: 1.8; color: var(--text-secondary); }

.am-body { padding: 20px 24px; }
.am-empty { padding: 40px 20px; text-align: center; font-family: monospace; font-size: 12px; color: var(--text-secondary); border: 1px dashed var(--line); border-radius: 3px; }

.op-count-hint { display: flex; align-items: center; gap: 10px; padding: 10px 14px; margin-bottom: 14px; border: 1px solid rgba(255, 107, 26, 0.3); background: rgba(255, 107, 26, 0.05); border-radius: 3px; font-size: 12px; }
.op-count-hint.locked { border-color: rgba(120, 215, 206, 0.35); background: rgba(120, 215, 206, 0.05); }
.op-count-hint.full { border-color: rgba(80, 200, 120, 0.5); background: rgba(80, 200, 120, 0.08); }
.och-label { font-family: monospace; font-size: 12px; color: var(--text-secondary); }
.och-count { font-family: monospace; font-size: 14px; font-weight: 700; color: var(--accent-color); }
.op-count-hint.locked .och-count { color: var(--success); }
.op-count-hint.full .och-count { color: var(--success); }
.och-tag { margin-left: auto; font-family: monospace; font-size: 12px; color: var(--text-secondary); }
.och-tag.locked { color: var(--success); }
.och-tag.full { color: var(--success); }

.old-ops-box { padding: 12px 14px; margin-bottom: 14px; border-left: 2px solid var(--accent-color); background: rgba(255, 140, 74, 0.05); border-radius: 0 3px 3px 0; }
.oob-title { font-family: monospace; font-size: 12px; color: var(--accent-color); margin-bottom: 8px; letter-spacing: 1px; }
.oob-list { display: flex; flex-wrap: wrap; gap: 6px; }
.oob-chip { padding: 3px 10px; font-size: 12px; color: var(--text-secondary); background: var(--panel); border-radius: 3px; }

.price-preview-box { padding: 12px 14px; margin-bottom: 14px; border: 1px dashed rgba(120, 215, 206, 0.3); background: rgba(120, 215, 206, 0.04); border-radius: 3px; display: flex; flex-direction: column; gap: 6px; }
.ppb-row { display: flex; justify-content: space-between; font-size: 12px; }
.ppb-row span { color: var(--text-secondary); }
.ppb-row b { color: var(--text-secondary); font-family: monospace; }
.ppb-row.sub { padding-left: 8px; font-size: 12px; }
.ppb-row.sub span { color: var(--text-secondary); font-family: monospace; }
.ppb-row.sub b { color: var(--success); }
.ppb-row.total { margin-top: 6px; padding-top: 8px; border-top: 1px dashed var(--line); }
.ppb-total { font-size: 18px !important; color: var(--accent-color) !important; }
.ppb-note { margin-top: 4px; font-family: monospace; font-size: 12px; color: var(--text-secondary); }

.op-picker { display: flex; flex-direction: column; gap: 8px; max-height: 340px; overflow-y: auto; }
.op-item { display: flex; align-items: center; gap: 12px; padding: 12px 14px; border: 1px solid var(--bg-soft); background: var(--bg-soft); border-radius: 3px; cursor: pointer; transition: 0.2s; }
.op-item:hover:not(.off) { border-color: var(--line); }
.op-item.on { border-color: rgba(255, 107, 26, 0.5); background: rgba(255, 107, 26, 0.06); }
.op-item.off { opacity: 0.35; cursor: not-allowed; }
.op-item input { width: 18px; height: 18px; flex-shrink: 0; accent-color: var(--accent-color); }
.op-item:focus-within { outline: 2px solid var(--accent-color); outline-offset: 2px; }
.op-avatar { width: 40px; height: 40px; display: grid; place-items: center; border: 1px solid var(--line); background: var(--bg-soft); color: var(--accent-color); font-weight: 700; border-radius: 3px; flex-shrink: 0; }
.op-info { flex: 1; min-width: 0; }
.op-name { display: flex; align-items: center; gap: 8px; font-size: 14px; font-weight: 700; color: var(--text-primary); margin-bottom: 3px; flex-wrap: wrap; }
.op-level { padding: 1px 6px; font-family: monospace; font-size: 12px; color: var(--accent-color); border: 1px solid rgba(255, 107, 26, 0.4); border-radius: 2px; }
.op-meta { font-size: 12px; color: var(--text-secondary); font-family: monospace; }
.op-busy { color: var(--accent-color); }

.reason-box { margin-top: 14px; display: flex; flex-direction: column; gap: 8px; }
.reason-label { font-family: monospace; font-size: 12px; color: var(--text-secondary); letter-spacing: 1px; }
.reason-label em { font-style: normal; color: var(--danger); margin-left: 3px; }
.reason-textarea { width: 100%; padding: 10px 12px; background: var(--panel); border: 1px solid var(--line); color: var(--text-primary); border-radius: 3px; font-family: inherit; font-size: 13px; outline: none; resize: vertical; }
.reason-textarea:focus { border-color: rgba(255, 107, 26, 0.5); }

.abandon-warn { padding: 12px 14px; margin-bottom: 14px; border-left: 3px solid var(--danger); background: rgba(255, 80, 80, 0.06); color: var(--text-secondary); font-size: 12px; line-height: 1.8; border-radius: 0 3px 3px 0; }
.abandon-warn b { color: var(--danger); }

.complaint-preview { padding: 12px 14px; margin-bottom: 14px; border-left: 2px solid var(--accent-color); background: rgba(255, 140, 74, 0.05); border-radius: 0 3px 3px 0; }
.cpv-label { font-family: monospace; font-size: 12px; color: var(--accent-color); margin-bottom: 6px; letter-spacing: 1px; }
.cpv-text { margin: 0; font-size: 13px; line-height: 1.7; color: var(--text-secondary); white-space: pre-wrap; }

.handle-actions { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
.ha-item { display: flex; flex-direction: column; gap: 4px; padding: 12px 14px; border: 1px solid var(--bg-soft); background: var(--bg-soft); border-radius: 3px; cursor: pointer; transition: 0.2s; }
.ha-item:hover { border-color: var(--line); }
.ha-item.on { border-color: rgba(255, 107, 26, 0.5); background: rgba(255, 107, 26, 0.06); }
.ha-item input { display: none; }
.ha-item span { font-size: 13px; font-weight: 600; color: var(--text-primary); }
.ha-item small { font-size: 12px; color: var(--text-secondary); }

.am-foot { display: flex; gap: 12px; padding: 16px 24px; border-top: 1px solid var(--bg-soft); }
.btn-ghost, .btn-solid { flex: 1; height: 42px; font-size: 13px; letter-spacing: 1px; cursor: pointer; border-radius: 3px; transition: all 0.2s; }
.btn-ghost { color: var(--text-secondary); background: transparent; border: 1px solid var(--line); }
.btn-ghost:hover { color: var(--text-primary); border-color: var(--line); }
.btn-solid { color: var(--on-accent); background: var(--accent-color); border: 1px solid var(--accent-color); }
.btn-solid:hover:not(:disabled) { background: var(--accent-color); }
.btn-solid:disabled { opacity: 0.5; cursor: not-allowed; }
.btn-solid.danger { background: var(--danger); border-color: var(--danger); }
.btn-solid.danger:hover:not(:disabled) { background: var(--danger); }

@media (max-width: 640px) {
  .cs-header { flex-direction: column; align-items: stretch; gap: 14px; }
  .cs-header-right { width: 100%; }
  .cs-status-btn { flex: 1; text-align: center; }
  .cs-guide-btn, .cs-wallet-btn { width: 100%; margin-right: 0; margin-bottom: 6px; }
  .oc-body { flex-direction: column; }
  .oc-side { align-items: flex-start; }
  .oc-head { flex-wrap: wrap; gap: 10px; }
  .oc-timeline { font-size: 12px; }
  .guide-modal { max-height: 92vh; }
  .gm-body { grid-template-columns: 1fr; }
  .gm-toc { flex-direction: row; flex-wrap: wrap; gap: 6px; padding: 10px 14px; border-right: none; border-bottom: 1px solid var(--bg-soft); max-height: 120px; }
  .toc-a { padding: 4px 8px; font-size: 12px; }
  .gm-content { padding: 16px; }
  .handle-actions { grid-template-columns: 1fr; }
}
</style>
