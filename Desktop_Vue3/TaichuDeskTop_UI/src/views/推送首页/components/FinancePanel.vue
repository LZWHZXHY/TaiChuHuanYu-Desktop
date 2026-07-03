<template>
  <div class="finance-panel-wrapper">
    <!-- 触发公开财报按钮 -->
    <button class="finance-trigger-btn" @click="openModal">
      <span class="btn-text">
        <span class="symbol">￥</span> 查阅寰宇财政公开报告
      </span>
      <span class="arrow">→</span>
    </button>

    <!-- 财报高级弹窗传送舱 -->
    <Teleport to="body">
      <Transition name="fade" @before-leave="destroyCharts">
        <div v-if="isOpen" class="finance-overlay" @click.self="closeModal">
          <div class="finance-modal">
            <button class="close-btn" @click="closeModal">✕</button>

            <header class="modal-header">
              <div class="meta-tag">FINANCIAL REPORT // AUDIT_STATUS: CLEARED</div>
              <h2 class="modal-title">太初寰宇·财政收支公示</h2>
              <p class="modal-subtitle">截止至当前周期的社区运营资金与全景数据分析明细</p>
            </header>

            <div v-if="loading" class="finance-loading">
              <div class="spinner"></div>
              <span>正在审计寰宇国库账目...</span>
            </div>

            <template v-else>
              <!-- 核心资产看板 -->
              <div class="assets-overview">
                <div class="asset-card">
                  <span class="asset-label">TOTAL_BALANCE // 结余国库资金</span>
                  <span class="asset-value" :class="{ 'negative-val': totalBalance < 0 }">
                    ￥{{ totalBalance.toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }}
                  </span>
                </div>
                <div class="asset-card active">
                  <span class="asset-label">MONTHLY_INCOME // 本月赞助入库</span>
                  <span class="asset-value">+￥{{ monthlyIncome.toFixed(2) }}</span>
                </div>
              </div>

              <!-- 🌟 升级：完完全全的 ECharts 三列环形图 + 走向大网格 -->
              <section class="visual-section">
                <h3 class="section-title">数据可视化分析 // VISUAL_METRICS</h3>
                
                <!-- 上方：全面重构为 3 列并排 -->
                <div class="charts-top-grid">
                  <!-- 1. 新增：收入占比分布图 -->
                  <div class="chart-box">
                    <span class="chart-sub-title">INCOME_DIST // 收入来源分布</span>
                    <div class="echart-holder">
                      <div ref="incomeChartEl" class="echart-instance"></div>
                    </div>
                  </div>

                  <!-- 2. 商户支出分布 -->
                  <div class="chart-box">
                    <span class="chart-sub-title">EXPENSE_DIST // 商户开销支出分布</span>
                    <div class="echart-holder">
                      <div ref="shouKuanChartEl" class="echart-instance"></div>
                    </div>
                  </div>
                  
                  <!-- 3. 成员出资垫付比例 -->
                  <div class="chart-box">
                    <span class="chart-sub-title">MEMBER_CONTRIB // 共建者出资比例</span>
                    <div class="echart-holder">
                      <div ref="zhiChuChartEl" class="echart-instance"></div>
                    </div>
                  </div>
                </div>

                <!-- 下方：历史年度趋势走向 -->
                <div class="chart-box trend-panel-box">
                  <span class="chart-sub-title">ANNUAL_TREND // 历史年度总开销趋势走向</span>
                  <div class="echart-holder line-holder">
                    <div ref="trendChartEl" class="echart-instance"></div>
                  </div>
                </div>
              </section>

              <!-- 账目细分流 -->
              <section class="ledger-section">
                <h3 class="section-title">收支流水摘要 // TRANSACTION_LOGS</h3>
                <div v-if="financeLogs.length === 0" class="empty-ledger">
                  暂无财政流水记录
                </div>
                <div v-else class="ledger-list-container">
                  <div class="ledger-list">
                    <div v-for="log in financeLogs" :key="'log-'+log.index" class="ledger-item">
                      <div class="ledger-meta">
                        <span class="ledger-date">{{ formatDate(log.date) }}</span>
                        <span class="ledger-desc" :title="log.zhiChuXiangMu">{{ log.zhiChuXiangMu }}</span>
                      </div>
                      <div class="ledger-right-meta">
                        <span class="ledger-payer-tag">{{ log.payReceive === 0 ? '赞助入库' : log.zhiChu }}</span>
                        <span :class="['ledger-amount', log.payReceive === 1 ? 'out' : 'in']">
                          {{ log.payReceive === 1 ? '-' : '+' }}￥{{ log.amount.toFixed(2) }}
                        </span>
                      </div>
                    </div>
                  </div>
                </div>
              </section>
            </template>

            <footer class="modal-footer">
              <p>所有款项均用于服务器续费及基础维护，数据由系统灵脉透明审计，感谢每一位行者共建。</p>
            </footer>
          </div>
        </div>
      </Transition>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onBeforeUnmount, nextTick, watch, shallowRef } from 'vue';
import * as echarts from 'echarts';
import request from '@/utils/request';

interface FinancialDto {
  index: number;
  zhiChu: string;
  zhiChuXiangMu: string;
  date: string;
  shouKuan: string;
  amount: number;
  payReceive: number;
}

const isOpen = ref(false);
const loading = ref(false);
const financeLogs = ref<FinancialDto[]>([]);

// 图表容器元素引用
const incomeChartEl = ref<HTMLDivElement | null>(null);
const shouKuanChartEl = ref<HTMLDivElement | null>(null);
const zhiChuChartEl = ref<HTMLDivElement | null>(null);
const trendChartEl = ref<HTMLDivElement | null>(null);

// 图表实例
const incomeInstance = shallowRef<echarts.ECharts | null>(null);
const shouKuanInstance = shallowRef<echarts.ECharts | null>(null);
const zhiChuInstance = shallowRef<echarts.ECharts | null>(null);
const trendInstance = shallowRef<echarts.ECharts | null>(null);

const fontMono = "'JetBrains Mono', ui-monospace, monospace";
const colorBlack = '#1f2328';

/* ==========================================
   基础财务清算矩阵
   ========================================== */
const totalBalance = computed(() => {
  return financeLogs.value.reduce((acc, cur) => cur.payReceive === 1 ? acc - cur.amount : acc + cur.amount, 0);
});

const monthlyIncome = computed(() => {
  const now = new Date();
  return financeLogs.value
    .filter(log => {
      const logDate = new Date(log.date);
      return log.payReceive === 0 && logDate.getMonth() === now.getMonth() && logDate.getFullYear() === now.getFullYear();
    })
    .reduce((acc, cur) => acc + cur.amount, 0);
});

/* ==========================================
   图表数据清洗
   ========================================== */
const incomeChartData = computed(() => {
  const map: Record<string, number> = {};
  financeLogs.value.forEach(log => {
    if (log.payReceive === 0) {
      let name = (log.zhiChuXiangMu || '社区共建赞助').replace(/[\r\n\s]+/g, '').trim();
      if (name.length > 8) name = name.substring(0, 8) + '...';
      map[name] = (map[name] || 0) + log.amount;
    }
  });
  return Object.keys(map).map(k => ({ name: k, value: Math.round(map[k]) })).sort((a, b) => b.value - a.value);
});

const shouKuanChartData = computed(() => {
  const map: Record<string, number> = {};
  financeLogs.value.forEach(log => {
    if (log.payReceive === 1) {
      let name = (log.shouKuan || '其他开销').replace(/[\r\n\s]+/g, '').trim();
      if (name.includes('[')) name = name.split('[')[0];
      map[name] = (map[name] || 0) + log.amount;
    }
  });
  const sorted = Object.keys(map).map(k => ({ name: k, value: Math.round(map[k]) })).sort((a, b) => b.value - a.value);
  if (sorted.length <= 3) return sorted;
  const finalData = sorted.slice(0, 3);
  const otherSum = sorted.slice(3).reduce((acc, c) => acc + c.value, 0);
  finalData.push({ name: '其他开销', value: otherSum });
  return finalData;
});

const zhiChuChartData = computed(() => {
  const map: Record<string, number> = {};
  financeLogs.value.forEach(log => {
    if (log.payReceive === 1) {
      const name = (log.zhiChu || '系统内库').trim();
      map[name] = (map[name] || 0) + log.amount;
    }
  });
  return Object.keys(map).map(k => ({ name: k, value: Math.round(map[k]) })).sort((a, b) => b.value - a.value);
});

const trendChartData = computed(() => {
  const map: Record<string, number> = {};
  financeLogs.value.forEach(log => {
    if (log.payReceive === 1) {
      const year = new Date(log.date).getFullYear().toString() + '年';
      map[year] = (map[year] || 0) + log.amount;
    }
  });
  const years = Object.keys(map).sort((a, b) => parseInt(a) - parseInt(b));
  return { years, values: years.map(y => Math.round(map[y])) };
});

/* ==========================================
   ECharts 渲染函数（核心修复：只更新数据，不重新创建实例）
   ========================================== */

// 通用图表更新函数
const updateChart = (instance: echarts.ECharts | null, option: any) => {
  if (!instance) return;
  instance.setOption(option, true); // notMerge = true 完全替换
  instance.resize();
};

// 生成图表配置
const getPieOption = (data: any[], colors: string[]) => ({
  tooltip: {
    trigger: 'item',
    backgroundColor: 'rgba(255,255,255,0.98)',
    borderColor: colorBlack,
    borderWidth: 1.5,
    textStyle: { fontFamily: fontMono, color: colorBlack, fontSize: 10 },
    formatter: '{b}: ￥{c} ({d}%)'
  },
  legend: {
    orient: 'horizontal',
    bottom: '0%',
    left: 'center',
    icon: 'circle',
    itemWidth: 6,
    itemHeight: 6,
    textStyle: { fontFamily: fontMono, fontSize: 8.5, color: colorBlack },
    formatter: (name: string) => {
      const target = data.find(d => d.name === name);
      return `${name} ￥${target ? target.value : 0}`;
    }
  },
  series: [{
    type: 'pie',
    radius: ['50%', '76%'],
    center: ['50%', '42%'],
    avoidLabelOverlap: false,
    itemStyle: { borderColor: '#fff', borderWidth: 1.5 },
    label: { show: false },
    color: colors,
    data: data.length ? data : [{ name: '暂无数据', value: 0 }]
  }]
});

const getTrendOption = (years: string[], values: number[]) => ({
  tooltip: {
    trigger: 'axis',
    backgroundColor: '#fff',
    borderColor: colorBlack,
    borderWidth: 1.5,
    textStyle: { fontFamily: fontMono, fontSize: 10 },
    axisPointer: { type: 'line', lineStyle: { color: colorBlack, type: 'dashed' } }
  },
  grid: { left: '2%', right: '3%', bottom: '8%', top: '22%', containLabel: true },
  xAxis: {
    type: 'category',
    data: years,
    axisLine: { lineStyle: { color: '#eaecef' } },
    axisTick: { show: false },
    axisLabel: { fontFamily: fontMono, color: '#6e7781', fontSize: 9.5 }
  },
  yAxis: {
    type: 'value',
    splitLine: { lineStyle: { color: '#f6f8fa', type: 'dashed' } },
    axisLabel: { fontFamily: fontMono, color: '#8c959f', fontSize: 9.5 }
  },
  series: [{
    name: '年度总支出',
    type: 'line',
    smooth: 0.12,
    data: values,
    itemStyle: { color: colorBlack },
    lineStyle: { width: 1.8, color: colorBlack },
    symbol: 'circle',
    symbolSize: 6,
    areaStyle: {
      color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
        { offset: 0, color: 'rgba(31, 35, 40, 0.12)' },
        { offset: 1, color: 'rgba(31, 35, 40, 0.01)' }
      ])
    },
    label: {
      show: true,
      position: 'top',
      fontFamily: fontMono,
      fontSize: 9.5,
      fontWeight: 'bold',
      color: colorBlack,
      formatter: '￥{c}',
      distance: 10
    }
  }]
});

// 挂载所有图表（在数据加载后调用）
const mountAllCharts = () => {
  // 清理旧实例
  [incomeInstance, shouKuanInstance, zhiChuInstance, trendInstance].forEach(inst => {
    if (inst.value) {
      inst.value.dispose();
      inst.value = null;
    }
  });

  // 重新初始化
  if (incomeChartEl.value) {
    incomeInstance.value = echarts.init(incomeChartEl.value);
    incomeInstance.value.setOption(getPieOption(incomeChartData.value, ['#2da44e', '#54b677', '#8cd1a4', '#b6e5cd']));
    incomeInstance.value.resize();
  }
  if (shouKuanChartEl.value) {
    shouKuanInstance.value = echarts.init(shouKuanChartEl.value);
    shouKuanInstance.value.setOption(getPieOption(shouKuanChartData.value, ['#1f2328', '#57606a', '#8c959f', '#cbd5e1']));
    shouKuanInstance.value.resize();
  }
  if (zhiChuChartEl.value) {
    zhiChuInstance.value = echarts.init(zhiChuChartEl.value);
    zhiChuInstance.value.setOption(getPieOption(zhiChuChartData.value, ['#1f2328', '#e68a2e', '#8c959f', '#cbd5e1']));
    zhiChuInstance.value.resize();
  }
  if (trendChartEl.value && trendChartData.value.years.length > 0) {
    trendInstance.value = echarts.init(trendChartEl.value);
    trendInstance.value.setOption(getTrendOption(trendChartData.value.years, trendChartData.value.values));
    trendInstance.value.resize();
  }
};

// 图表更新监听（数据变化时更新，但不重新创建实例）
watch(incomeChartData, () => {
  if (incomeInstance.value) {
    incomeInstance.value.setOption(getPieOption(incomeChartData.value, ['#2da44e', '#54b677', '#8cd1a4', '#b6e5cd']), true);
    incomeInstance.value.resize();
  }
}, { deep: true });

watch(shouKuanChartData, () => {
  if (shouKuanInstance.value) {
    shouKuanInstance.value.setOption(getPieOption(shouKuanChartData.value, ['#1f2328', '#57606a', '#8c959f', '#cbd5e1']), true);
    shouKuanInstance.value.resize();
  }
}, { deep: true });

watch(zhiChuChartData, () => {
  if (zhiChuInstance.value) {
    zhiChuInstance.value.setOption(getPieOption(zhiChuChartData.value, ['#1f2328', '#e68a2e', '#8c959f', '#cbd5e1']), true);
    zhiChuInstance.value.resize();
  }
}, { deep: true });

watch(trendChartData, () => {
  if (trendInstance.value && trendChartData.value.years.length > 0) {
    trendInstance.value.setOption(getTrendOption(trendChartData.value.years, trendChartData.value.values), true);
    trendInstance.value.resize();
  }
}, { deep: true });

// 窗口大小变化
const handleResize = () => {
  [incomeInstance, shouKuanInstance, zhiChuInstance, trendInstance].forEach(inst => {
    inst.value?.resize();
  });
};

// 清理
const destroyCharts = () => {
  [incomeInstance, shouKuanInstance, zhiChuInstance, trendInstance].forEach(inst => {
    if (inst.value) {
      inst.value.dispose();
      inst.value = null;
    }
  });
};

// 数据加载
const fetchFinanceData = async () => {
  loading.value = true;
  try {
    const data = await request.get<FinancialDto[]>('/financial/report');
    financeLogs.value = data || [];
    // 等待 DOM 更新
    await nextTick();
    // 延迟一帧确保容器已渲染
    requestAnimationFrame(() => {
      mountAllCharts();
    });
  } catch (error) {
    console.error('组件层捕获财务流失败:', error);
  } finally {
    loading.value = false;
  }
};

// 打开弹窗
const openModal = () => {
  isOpen.value = true;
  document.body.style.overflow = 'hidden';
  window.addEventListener('resize', handleResize);
  // 重置数据
  financeLogs.value = [];
  // 等待弹窗渲染完成后加载数据
  nextTick(() => {
    fetchFinanceData();
  });
};

// 关闭弹窗
const closeModal = () => {
  isOpen.value = false;
  document.body.style.overflow = '';
  window.removeEventListener('resize', handleResize);
  destroyCharts();
};

const formatDate = (dateStr: string) => {
  if (!dateStr) return '';
  const d = new Date(dateStr);
  return `${d.getFullYear()}.${String(d.getMonth() + 1).padStart(2, '0')}.${String(d.getDate()).padStart(2, '0')}`;
};

onBeforeUnmount(() => {
  destroyCharts();
  window.removeEventListener('resize', handleResize);
});
</script>

<style scoped>
.finance-trigger-btn {
  width: 100%;
  display: flex;
  justify-content: space-between;
  align-items: center;
  background: #1f2328;
  color: #fff;
  border: none;
  border-radius: 6px;
  padding: 16px 20px;
  font-size: 0.9rem;
  font-weight: 400;
  cursor: pointer;
  transition: all 0.3s cubic-bezier(0.16, 1, 0.3, 1);
  letter-spacing: 0.05em;
}
.finance-trigger-btn:hover { background: #33383e; transform: translateY(-1px); }
.symbol { font-family: ui-monospace, monospace; margin-right: 4px; color: #8c959f; }
.arrow { transition: transform 0.3s; }
.finance-trigger-btn:hover .arrow { transform: translateX(4px); }

.finance-overlay {
  position: fixed;
  inset: 0;
  background: rgba(255, 255, 255, 0.6);
  backdrop-filter: blur(20px);
  -webkit-backdrop-filter: blur(20px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 9999;
}

/* ==========================================
   🧱 宽屏剧院级总开仓舱：最大宽度拓宽到 1160px
   ========================================== */
.finance-modal {
  background: #ffffff;
  border: 1px solid #e1e4e8;
  width: 95%;
  max-width: 1160px; /* 🌟 深度拓宽到 1160px 彻底消除任何侧边挤压感 */
  height: 88vh; 
  border-radius: 16px;
  padding: 36px 40px;
  position: relative;
  box-shadow: 0 30px 60px rgba(0, 0, 0, 0.06);
  color: #1f2328;
  display: flex;
  flex-direction: column;
}

.close-btn { position: absolute; top: 20px; right: 24px; background: none; border: none; font-size: 1.1rem; color: #8c959f; cursor: pointer; }

.modal-header { margin-bottom: 16px; flex-shrink: 0; }
.meta-tag { font-family: ui-monospace, monospace; font-size: 0.7rem; font-weight: 700; color: #8c959f; letter-spacing: 2px; margin-bottom: 4px; }
.modal-title { font-size: 1.35rem; font-weight: 500; margin: 0 0 4px 0; }
.modal-subtitle { font-size: 0.85rem; color: #6e7781; margin: 0; }

.assets-overview {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
  margin-bottom: 16px;
  flex-shrink: 0;
}
.asset-card { background: #f6f8fa; border: 1px solid #e1e4e8; border-radius: 6px; padding: 12px 20px; display: flex; flex-direction: column; gap: 2px; }
.asset-card.active { background: #fafcf9; border-color: #d2ebd4; }
.asset-label { font-size: 0.72rem; color: #6e7781; }
.asset-value { font-size: 1.35rem; font-weight: 400; font-family: ui-monospace, monospace; }
.asset-value.negative-val { color: #cf222e; } 
.asset-card.active .asset-value { color: #2da44e; }

.visual-section { margin-bottom: 16px; flex-shrink: 0; }
.section-title { font-size: 0.78rem; color: #8c959f; font-weight: 600; text-transform: uppercase; letter-spacing: 0.05em; margin-bottom: 10px; border-bottom: 1px solid #f6f8fa; padding-bottom: 4px; }

/* 🌟 升级：上层双列合并扩展为 3 列并排布局 */
.charts-top-grid {
  display: grid;
  grid-template-columns: 1fr 1fr 1fr; /* 🌟 3列均匀平铺 */
  gap: 14px;
  margin-bottom: 12px;
}
.chart-box { background: #ffffff; border: 1px solid #eaecef; border-radius: 8px; padding: 14px; display: flex; flex-direction: column; min-width: 0; }
.chart-sub-title { font-size: 0.7rem; color: #6e7781; margin-bottom: 8px; font-weight: 500; display: block;}

/* ECharts 底衬渲染锚定 */
.echart-holder {
  width: 100% !important;
  height: 175px !important; /* 🌟 下移 legend 后，高度拔高到 175px 让圆环展现得更圆润壮观 */
  position: relative;
}
.echart-holder.line-holder {
  height: 135px !important;
}
.echart-instance {
  width: 100%;
  height: 100%;
}

/* 细分流水摘要 */
.ledger-section { margin-bottom: 4px; flex: 1; display: flex; flex-direction: column; min-height: 0; }
.ledger-list-container { flex: 1; overflow-y: auto; padding-right: 4px; }
.ledger-list-container::-webkit-scrollbar { width: 4px; }
.ledger-list-container::-webkit-scrollbar-thumb { background: #eaecef; border-radius: 4px; }

.ledger-list { display: flex; flex-direction: column; }
.ledger-item { display: flex; justify-content: space-between; align-items: center; padding: 10px 0; border-bottom: 1px solid #f6f8fa; gap: 20px; }
.ledger-item:last-child { border-bottom: none; }
.ledger-meta { display: flex; flex-direction: column; gap: 2px; min-width: 0; flex: 1; }
.ledger-date { font-family: ui-monospace, monospace; font-size: 0.75rem; color: #8c959f; }
.ledger-desc { font-size: 0.88rem; color: #24292f; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }

.ledger-right-meta { display: flex; align-items: center; gap: 16px; flex-shrink: 0; }
.ledger-payer-tag { font-size: 0.72rem; background: #f1f1f1; border: 1px solid #e1e4e8; padding: 1px 6px; border-radius: 3px; color: #57606a; font-weight: bold; }
.ledger-amount { font-family: ui-monospace, monospace; font-size: 0.9rem; font-weight: 600; }
.ledger-amount.in { color: #2da44e; }
.ledger-amount.out { color: #57606a; }

.empty-ledger { text-align: center; color: #b9c3ce; padding: 20px 0; font-size: 0.85rem; }
.finance-loading { display: flex; flex-direction: column; align-items: center; gap: 12px; padding: 40px 0; color: #8c959f; font-size: 0.85rem; }
.spinner { width: 22px; height: 22px; border: 2px solid #eaecef; border-top-color: #1f2328; border-radius: 50%; animation: spin 0.8s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }

.modal-footer { border-top: 1px solid #f6f8fa; padding-top: 14px; margin-top: auto; font-size: 0.72rem; color: #8c959f; text-align: center; flex-shrink: 0; }

@media (max-width: 1024px) {
  .charts-top-grid { grid-template-columns: 1fr; }
  .finance-modal { padding: 24px; height: 92vh; }
  .echart-holder { height: 150px !important; }
}

/* 动效 */
.fade-enter-active, .fade-leave-active { transition: opacity 0.25s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
</style>