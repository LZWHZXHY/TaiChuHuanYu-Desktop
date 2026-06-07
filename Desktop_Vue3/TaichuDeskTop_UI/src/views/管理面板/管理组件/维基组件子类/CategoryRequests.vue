<template>
  <div class="table-card">
    <table class="ink-table">
      <thead><tr><th>名称</th><th>理由</th><th class="text-right">审批</th></tr></thead>
      <tbody>
        <tr v-for="req in data" :key="req.id">
          <td>{{ req.name }}</td>
          <td class="text-gray">{{ req.reason }}</td>
          <td class="text-right actions">
            <button class="btn-s success" @click="$emit('approve', req)">[批准]</button>
            <button class="btn-s danger" @click="$emit('reject', req)">[驳回]</button>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>
<script setup lang="ts">
defineProps<{ data: any[] }>();
defineEmits(['approve', 'reject']);
</script>

<style>
@import './Wiki子组件风格.css';
</style>