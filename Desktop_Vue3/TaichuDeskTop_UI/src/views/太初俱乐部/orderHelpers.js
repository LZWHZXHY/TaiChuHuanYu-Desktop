export function fmtTime(iso) {
  if (!iso) return '—'
  const d = new Date(iso)
  const pad = n => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

export function operatorText(order) {
  return order.operators?.length ? order.operators.map(operator => operator.name).join('、') : order.operatorName || '—'
}

export function isAllAccepted(order) {
  if (!order.totalOperatorCount) return false
  return (order.acceptedCount || 0) >= order.totalOperatorCount
}

export function showProgress(order) {
  const total = order.totalOperatorCount || 0
  return total > 1 || ((order.acceptedCount || 0) > 0 && total > 0)
}
