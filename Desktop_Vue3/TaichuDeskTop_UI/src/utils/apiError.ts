import axios from 'axios'

export interface ApiErrorNotice {
  title: string
  msg: string
  detail: string
  type: 'error'
}

/**
 * 把接口错误变成用户看得懂的一句中文。
 * 以前会原样显示后端返回的东西：英文校验提示（The Password field is required.）、
 * 整页报错 HTML、「后端异常 (系统异常)」……用户不知道发生了什么。现在：
 * 1. 后端给了中文 message 就用它；
 * 2. 英文校验提示翻成中文；
 * 3. 其他按状态码给一句话（连不上、超时、服务器出错、太频繁……）；
 * 4. 都没有才用调用方给的 fallback。
 */
const FIELD_NAMES: Record<string, string> = {
  username: '名号', password: '密码', newpassword: '新密码', email: '邮箱', verificationcode: '验证码',
  identifier: '名号或邮箱', title: '标题', content: '内容', name: '名称', description: '简介',
}
function fieldName(key: string) {
  const k = key.split(/[.[\]]/).filter(s => s && s !== '$').pop()?.toLowerCase() ?? ''
  return FIELD_NAMES[k] ?? '填写的内容'
}
/** ASP.NET 默认的英文校验提示 → 中文 */
function translateValidation(errors: Record<string, unknown>): string {
  const out: string[] = []
  for (const [key, value] of Object.entries(errors)) {
    const field = fieldName(key)
    for (const raw of ([] as unknown[]).concat(value)) {
      if (typeof raw !== 'string') continue
      const text = /[一-龥]/.test(raw) ? raw
        : /non-empty request body/i.test(raw) ? '提交的内容是空的，请刷新页面再试'
        : /required/i.test(raw) ? (field === '填写的内容' ? '还有必填的内容没填' : `请填写${field}`)
        : /json|convert/i.test(raw) ? `${field}的格式不对`
        : /length/i.test(raw) ? `${field}的长度不对`
        : `${field}不对，请检查一下`
      if (!out.includes(text)) out.push(text)
    }
  }
  return out.slice(0, 3).join('；')
}
/** 看起来像后端内部报错（HTML 页面、异常堆栈、很长的英文），不直接给用户看 */
function looksInternal(text: string) {
  return /<\/?[a-z][\s\S]*>/i.test(text) || /exception|stack ?trace|\bat [A-Z]\w+\./.test(text) || (text.length > 160 && !/[一-龥]/.test(text))
}
const STATUS_MESSAGES: Record<number, string> = {
  404: '要找的内容不存在了（也可能网站正在更新），请刷新页面再试',
  408: '请求超时了，请检查网络后再试',
  409: '数据刚被别人改过了，请刷新后再试',
  413: '上传的内容太大了',
  429: '操作太频繁了，请稍等一会儿再试',
  500: '服务器出错了，请稍后再试；一直这样请联系管理员',
  502: '服务器暂时连不上（可能正在更新），请过一会儿再试',
  503: '服务器暂时连不上（可能正在更新），请过一会儿再试',
  504: '服务器处理超时了，请稍后再试',
}

export function getErrorMessage(error: unknown, fallback = '请求失败，请稍后重试'): string {
  if (axios.isAxiosError(error)) {
    if (!error.response) {
      if (error.code === 'ECONNABORTED' || /timeout/i.test(error.message)) return '请求超时了，请检查网络后再试'
      return '连不上服务器，请检查网络（也可能服务器正在维护），稍后再试'
    }
    const status = error.response.status
    const data = error.response.data as unknown
    if (typeof data === 'string' && data.trim() && !looksInternal(data.trim())) return data.trim()
    if (data && typeof data === 'object') {
      const d = data as Record<string, unknown>
      if (typeof d.message === 'string' && d.message) {
        // 旧版后端的统一异常：「后端异常 (系统异常)」+ 英文 detail，换成好懂的说法，detail 截一小段方便报给管理员
        if (d.message.startsWith('后端异常')) {
          const detail = typeof d.detail === 'string' ? d.detail.slice(0, 80) : ''
          return `服务器出错了，请稍后再试；一直这样请截图告诉管理员${detail ? `（${detail}）` : ''}`
        }
        return d.message
      }
      if (d.errors && typeof d.errors === 'object') {
        const text = translateValidation(d.errors as Record<string, unknown>)
        if (text) return text
      }
      // ProblemDetails 的 title 是英文（One or more validation errors occurred. / Not Found），只用中文的
      if (typeof d.title === 'string' && /[一-龥]/.test(d.title)) return d.title
    }
    if (STATUS_MESSAGES[status]) return STATUS_MESSAGES[status]
    if (status >= 500) return STATUS_MESSAGES[500]
  }
  return fallback
}

export function describeApiError(error: unknown): ApiErrorNotice {
  const status = axios.isAxiosError(error) ? error.response?.status : undefined
  const data = axios.isAxiosError(error) ? error.response?.data : undefined
  const titles: Record<number, string> = {
    401: '身份验证失败',
    403: '权限受阻',
    500: '服务暂时不可用',
  }
  return {
    title: titles[status ?? 0] || '请求失败',
    msg: getErrorMessage(error, status === 403 ? '您当前权限不足，无权操作此项' : undefined),
    detail: data && typeof data === 'object' ? JSON.stringify(data, null, 2) : String(data || ''),
    type: 'error',
  }
}
