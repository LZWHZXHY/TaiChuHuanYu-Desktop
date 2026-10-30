<template><main><h1>关系图谱密集检查</h1><p>隔离数据，不写入服务器。180 / 1000 节点，每个节点三条关系。</p><nav><button @click="theme('light')">白天模式</button><button @click="theme('dark')">夜间模式</button><button @click="count=180">180 节点</button><button @click="count=1000">1000 节点</button><button @click="narrow=!narrow">切换窄屏</button><button @click="renderer.resetCamera('all')">适应画布</button><button @click="measure">查看绘制耗时</button></nav><p role="status">{{stats}}</p><div class="fixture" :class="{narrow}"><FlatWorldGraph :key="count" ref="renderer" :data="data" @select="focus=$event"/></div><p>当前点击：{{focus||'无'}} · 滚轮缩放，方向键平移；悬停节点查看关联。</p></main></template>
<script>

import {createApp,ref,computed} from 'vue';
import FlatWorldGraph from '/src/views/世界观管理/世界观组件/FlatWorldGraph.vue';
import '/src/assets/styles/variables.css';
import '/src/assets/styles/theme-rhine.css';
const samples=[]; const nativeFrame=window.requestAnimationFrame.bind(window);
window.requestAnimationFrame=callback=>nativeFrame(time=>{const start=performance.now();callback(time);samples.push(performance.now()-start);});
export default {components:{FlatWorldGraph},setup(){
const count=ref(180),narrow=ref(false),focus=ref(''),renderer=ref(),stats=ref('尚未取样');
const data=computed(()=>({nodes:Array.from({length:count.value},(_,i)=>({id:'n'+i,title:['人物','地点','组织'][i%3]+' · 雾海设定 '+i,type:'character',color:['#22a8bb','#936fe8','#cf8263'][i%3]})),links:Array.from({length:count.value*3},(_,i)=>({source:'n'+(i%count.value),target:'n'+((i%count.value+1+Math.floor(i/count.value)*17)%count.value),label:['同伴','驻留','引用'][Math.floor(i/count.value)],isEmbed:i>=count.value*2,width:1,color:'#22a8bb'}))}));
return{count,narrow,focus,renderer,data,stats,theme:value=>document.documentElement.setAttribute('data-theme',value),measure:()=>{const values=samples.slice(-30).sort((a,b)=>a-b);stats.value=`最近 ${values.length} 次绘制回调：中位 ${values[Math.floor(values.length/2)]?.toFixed(1)} ms，最大 ${Math.max(...values).toFixed(1)} ms`;}};
}};

</script>