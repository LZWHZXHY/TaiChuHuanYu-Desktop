<!-- src/components/KnowledgeGraph/components/GraphControlPanel.vue -->
<template>
  <div class="control-panel">
    <div class="panel-item search-box-container">
      <div class="search-input-wrapper">
        <svg class="search-icon" viewBox="0 0 24 24" width="16" height="16" stroke="currentColor" stroke-width="2" fill="none">
          <circle cx="11" cy="11" r="8"></circle>
          <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
        </svg>
        <input 
          type="text" 
          :value="searchQuery" 
          @input="$emit('update:searchQuery', ($event.target as HTMLInputElement).value)"
          @keyup.enter="$emit('search')"
          placeholder="检索寰宇节点 (Enter)..." 
        />
      </div>
      <button class="search-btn" @click="$emit('search')">锁定</button>
    </div>

    <div class="panel-item slider-box">
      <div class="label-group">
        <span class="main-label">斥力场引擎</span>
        <span class="sub-label">REPULSION FIELD</span>
      </div>
      <input 
        type="range" 
        :value="repulsionForce"
        @input="$emit('update:repulsionForce', Number(($event.target as HTMLInputElement).value))"
        min="10" max="800" step="10"
        class="cyber-slider"
      />
    </div>

    <div class="panel-item switch-box">
      <div class="label-group">
        <span class="main-label">全息标识符</span>
        <span class="sub-label">HOLOGRAPHIC TAGS</span>
      </div>
      <div class="hud-switch" :class="{ active: showLabels }" @click="$emit('toggle:labels')">
        <div class="switch-track"></div>
        <div class="switch-thumb"></div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  searchQuery: string;
  showLabels: boolean;
  repulsionForce: number;
}>()

defineEmits<{
  'update:searchQuery': [value: string];
  'update:repulsionForce': [value: number];
  'toggle:labels': [];
  'search': [];
}>()
</script>

<style scoped>
.control-panel {
  position: absolute;
  top: 32px;
  left: 32px;
  z-index: 10;
  display: flex;
  flex-direction: column;
  gap: 16px;
  width: 320px;
}

.panel-item {
  position: relative;
  background: linear-gradient(135deg, rgba(15, 23, 42, 0.75) 0%, rgba(2, 6, 23, 0.85) 100%);
  border: 1px solid rgba(56, 189, 248, 0.15);
  border-radius: 12px;
  backdrop-filter: blur(16px);
  -webkit-backdrop-filter: blur(16px);
  box-shadow: 
    0 8px 32px rgba(0, 0, 0, 0.4),
    inset 0 1px 0 rgba(255, 255, 255, 0.05);
  overflow: hidden;
  transition: border-color 0.3s ease, box-shadow 0.3s ease;
}

.panel-item::before {
  content: '';
  position: absolute;
  top: 0;
  left: 10%;
  width: 80%;
  height: 1px;
  background: linear-gradient(90deg, transparent, rgba(56, 189, 248, 0.5), transparent);
  opacity: 0.5;
}

.panel-item:hover {
  border-color: rgba(56, 189, 248, 0.3);
  box-shadow: 
    0 8px 32px rgba(0, 0, 0, 0.5),
    0 0 15px rgba(56, 189, 248, 0.1),
    inset 0 1px 0 rgba(255, 255, 255, 0.08);
}

.search-box-container {
  display: flex;
  padding: 8px;
  align-items: center;
  gap: 8px;
}

.search-input-wrapper {
  flex: 1;
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 12px;
  background: rgba(0, 0, 0, 0.2);
  border-radius: 8px;
  border: 1px solid transparent;
  transition: all 0.3s ease;
}

.search-input-wrapper:focus-within {
  border-color: rgba(56, 189, 248, 0.4);
  background: rgba(0, 0, 0, 0.4);
  box-shadow: inset 0 0 10px rgba(56, 189, 248, 0.1);
}

.search-icon {
  color: #64748b;
  transition: color 0.3s ease;
}
.search-input-wrapper:focus-within .search-icon {
  color: #38bdf8;
}

.search-input-wrapper input {
  width: 100%;
  background: transparent;
  border: none;
  color: #f8fafc;
  font-size: 13px;
  font-weight: 500;
  letter-spacing: 0.5px;
  outline: none;
}
.search-input-wrapper input::placeholder {
  color: #475569;
}

.search-btn {
  padding: 0 16px;
  height: 36px;
  background: rgba(56, 189, 248, 0.1);
  border: 1px solid rgba(56, 189, 248, 0.3);
  border-radius: 8px;
  color: #38bdf8;
  font-size: 12px;
  font-weight: 600;
  letter-spacing: 2px;
  cursor: pointer;
  transition: all 0.2s ease;
}
.search-btn:hover {
  background: rgba(56, 189, 248, 0.2);
  border-color: #38bdf8;
  color: #fff;
  box-shadow: 0 0 12px rgba(56, 189, 248, 0.4);
}
.search-btn:active {
  transform: scale(0.95);
}

.slider-box {
  display: flex;
  flex-direction: column;
  align-items: stretch;
  gap: 14px;
  padding: 14px 18px;
}

.cyber-slider {
  -webkit-appearance: none;
  width: 100%;
  height: 4px;
  background: rgba(0, 0, 0, 0.4);
  border: 1px solid rgba(56, 189, 248, 0.2);
  border-radius: 2px;
  outline: none;
  transition: border-color 0.3s;
}

.cyber-slider:focus, .cyber-slider:hover {
  border-color: rgba(56, 189, 248, 0.6);
}

.cyber-slider::-webkit-slider-thumb {
  -webkit-appearance: none;
  width: 14px;
  height: 14px;
  background: #38bdf8;
  border-radius: 50%;
  cursor: pointer;
  box-shadow: 0 0 10px #38bdf8, 0 0 20px rgba(56, 189, 248, 0.4);
  transition: transform 0.2s cubic-bezier(0.175, 0.885, 0.32, 1.275);
}

.cyber-slider::-webkit-slider-thumb:hover {
  transform: scale(1.4);
  background: #fff;
}

.switch-box {
  padding: 14px 18px;
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.label-group {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.main-label {
  font-size: 13px;
  color: #e2e8f0;
  font-weight: 500;
  letter-spacing: 1px;
}
.sub-label {
  font-size: 9px;
  color: #64748b;
  letter-spacing: 1.5px;
}

.hud-switch {
  position: relative;
  width: 44px;
  height: 22px;
  cursor: pointer;
}
.switch-track {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  background: rgba(0, 0, 0, 0.4);
  border: 1px solid rgba(148, 163, 184, 0.2);
  border-radius: 12px;
  transition: all 0.3s ease;
}
.switch-thumb {
  position: absolute;
  top: 3px;
  left: 3px;
  width: 16px;
  height: 16px;
  background: #64748b;
  border-radius: 50%;
  transition: transform 0.4s cubic-bezier(0.175, 0.885, 0.32, 1.275), background 0.3s;
}
.hud-switch.active .switch-track {
  background: rgba(56, 189, 248, 0.15);
  border-color: rgba(56, 189, 248, 0.5);
}
.hud-switch.active .switch-thumb {
  transform: translateX(22px);
  background: #38bdf8;
  box-shadow: 0 0 10px #38bdf8, 0 0 20px rgba(56, 189, 248, 0.5);
}
</style>