

export const priceGroups = [
  {
    id: 'exp',
    name: '体验单',
    code: 'EXPERIENCE',
    note: '出心出泪本单免单',
    items: [
      { id: 'exp1', name: '保 800W',  unit: '绝密', duration: '每日限一单', price: 128, hot: true  },
      { id: 'exp2', name: '保 1666W', unit: '绝密', duration: '无限制',     price: 268, hot: false }
    ]
  },
  {
    id: 'clear',
    name: '清图单',
    code: 'CLEAR MAP',
    note: '保证除丢包撤以外清图，只能拉极限闸，保证是最后一队成功撤离队伍',
    items: [
      { id: 'clear1', name: '清图服务', unit: '单局', duration: '不清图不结束', price: 198, hot: false }
    ]
  },
  {
    id: 'escort',
    name: '护航单',
    code: 'ESCORT',
    items: [
      { id: 'esc1', name: '保 2588W', unit: '绝密', duration: '标准护航', price: 388,  hot: false },
      { id: 'esc2', name: '保 4588W', unit: '绝密', duration: '标准护航', price: 688,  hot: false },
      { id: 'esc3', name: '保 7188W', unit: '绝密', duration: '高价值护航', price: 1288, hot: true  },
      { id: 'esc4', name: '保 1E',    unit: '绝密', duration: '顶级护航', price: 1788, hot: false }
    ]
  },
  {
    id: 'gamble',
    name: '堵单局',
    code: 'GAMBLE',
    note: '可卖的老太头和大红包少爷甲允许带出，六头六甲如不需要可换高耐久五套，由陪陪理包。如发现板板丢物资卡保底直接结单',
    items: [
      { id: 'gb1', name: '单局 1000W', unit: '单局', duration: '保底 1000W', price: 238,  hot: false },
      { id: 'gb2', name: '单局 1200W', unit: '单局', duration: '保底 1200W', price: 458,  hot: false },
      { id: 'gb3', name: '单局 1400W', unit: '单局', duration: '保底 1400W', price: 658,  hot: false },
      { id: 'gb4', name: '单局 1500W', unit: '单局', duration: '保底 1500W', price: 1288, hot: true  }
    ]
  },
  {
    id: 'hourly',
    name: '按小时',
    code: 'HOURLY',
    note: '可指定打手、指定模式，具体价格请咨询客服',
    items: [
      { id: 'h1', name: '普通陪玩',      unit: '每小时', duration: '随时可约',   price: 0, hot: false },
      { id: 'h2', name: '大神陪玩',      unit: '每小时', duration: '需提前预约', price: 0, hot: true  },
      { id: 'h3', name: '包夜（8 小时）', unit: '每单',   duration: '22:00-06:00', price: 0, hot: false }
    ]
  }
]

export const dividendTiers = [
  { tier: 'L1', name: '见习干员', threshold: '通过游戏基础认证',            rate: 0.60, praise: '+2%', cycle: '周结' },
  { tier: 'L2', name: '正式干员', threshold: '审核通过 · 接单稳定',         rate: 0.68, praise: '+3%', cycle: '周结' },
  { tier: 'L3', name: '精英干员', threshold: '高评分 · 复购率达标',         rate: 0.76, praise: '+4%', cycle: '日结' },
  { tier: 'L4', name: '王牌干员', threshold: '长期稳定 · 客户口碑优秀',     rate: 0.84, praise: '+5%', cycle: '日结' },
  { tier: 'L5', name: '宗师',     threshold: '平台顶级 · 官方邀请',        rate: 0.92, praise: '+6%', cycle: '日结' }
]