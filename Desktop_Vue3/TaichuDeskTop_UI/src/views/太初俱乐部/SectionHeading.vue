<template>
  <div class="sec-head">
    <div class="sec-left">
      <span class="sec-index">{{ index }} /</span>
      <div>
        <h2>{{ title }}</h2>
        <span v-if="subtitle">{{ subtitle }}</span>
      </div>
    </div>
    <slot name="action" />
  </div>
</template>

<script>
export default {
  name: 'SectionHeading',
  props: {
    index: { type: String, required: true },
    title: { type: String, required: true },
    subtitle: { type: String, default: '' }
  }
}
</script>

<style scoped>
.sec-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 20px;
  margin-bottom: 40px;
}
.sec-left {
  display: flex;
  align-items: center;
  gap: 14px;
}
.sec-index {
  color: var(--orange);
  font-family: monospace;
  font-size: 10px;
  letter-spacing: 1px;
}
.sec-left h2 {
  margin: 0;
  font-size: 21px;
  letter-spacing: 2px;
  font-weight: 700;
}
.sec-left > div > span {
  display: block;
  margin-top: 5px;
  color: var(--text-muted);
  font-family: monospace;
  font-size: 8px;
  letter-spacing: 1px;
}
@media (max-width: 520px) {
  .sec-head { flex-direction: column; align-items: flex-start; gap: 18px; }
}
</style>