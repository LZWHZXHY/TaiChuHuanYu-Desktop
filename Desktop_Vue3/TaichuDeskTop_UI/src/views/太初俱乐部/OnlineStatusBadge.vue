<template>
  <span class="osb" :class="status">
    <i></i>
    <span>{{ label }}</span>
  </span>
</template>

<script>
const LABELS = {
  online:  '在线',
  busy:    '接单中',
  resting: '休息中',
  offline: '离线'
}

export default {
  name: 'OnlineStatusBadge',
  props: {
    status: { type: String, default: 'offline' }
  },
  computed: {
    label() { return LABELS[this.status] || '离线' }
  }
}
</script>

<style scoped>
.osb {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding: 2px 8px;
  font-family: monospace;
  font-size: 10px;
  letter-spacing: 1px;
  border: 1px solid;
  border-radius: 2px;
  line-height: 1.4;
  white-space: nowrap;
}
.osb i {
  width: 5px; height: 5px; border-radius: 50%;
  background: currentColor;
}

.osb.online  { color: #78d7ce; border-color: rgba(120, 215, 206, 0.35); background: rgba(120, 215, 206, 0.06); }
.osb.online i { box-shadow: 0 0 6px #78d7ce; }

.osb.busy    { color: #ff6b1a; border-color: rgba(255, 107, 26, 0.35); background: rgba(255, 107, 26, 0.06); }
.osb.busy i  { box-shadow: 0 0 6px #ff6b1a; animation: osbPulse 1.4s infinite; }

.osb.resting { color: #a5a5ff; border-color: rgba(165, 165, 255, 0.3); background: rgba(165, 165, 255, 0.05); }

.osb.offline { color: #596673; border-color: rgba(89, 102, 115, 0.3); }

@keyframes osbPulse { 50% { opacity: 0.35; } }
</style>