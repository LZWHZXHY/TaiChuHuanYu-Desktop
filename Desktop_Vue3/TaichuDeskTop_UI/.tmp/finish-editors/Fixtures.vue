<script setup>
import {createApp,ref,computed} from 'vue';
import {createPinia} from 'pinia';
import {createRouter,createMemoryHistory} from 'vue-router';
import request from '/src/utils/request.ts';
import '/src/assets/styles/variables.css';
import '/src/assets/styles/base.css';
import '/src/assets/styles/content.css';
import '/src/assets/styles/theme-rhine.css';
import WorkspaceCanvas from '/src/views/太初灵脉/components/WorkspaceCanvas.vue';
import WorkspaceSchedule from '/src/views/太初灵脉/components/WorkspaceSchedule.vue';
import Novel from '/src/views/世界观管理/Novels/Detail.vue';
import Member from '/src/views/太初协作/协作组件/ProjectMember.vue';
import FlatGraph from '/src/views/世界观管理/世界观组件/FlatWorldGraph.vue';
import {useSpiritData} from '/src/composables/useSpiritData.ts';
import {worldNovelApi} from '/src/api/worldNovel.ts';
request.get=async url=>url.includes('members')?[{id:'fake-member',name:'示例协作者',email:'',isOwner:false,roleIds:['role_system_member']}]:[];
worldNovelApi.detail=async()=>({id:'fixture',title:'雾海行记 · 编辑检查',authorName:'示例作者',description:'隔离数据，不会保存到社区。',isOwner:true,isPublic:false,status:'ongoing',wordCount:35,chapters:[{id:'one',number:1,title:'灯塔',wordCount:35}]});
worldNovelApi.chapter=async()=>({id:'one',number:1,title:'灯塔',content:'# 雾海的清晨\n\n旅人站在灯塔下，等待第一束光。\n\n> 这是只读检查用的示例章节。',index:1,total:1});
const spirit=useSpiritData();
const block=(id,type,data)=>({id,type,data:JSON.stringify(data)});
const notes=[{id:'one',title:'旅人',type:'note',content:{},blocks:[]},{id:'two',title:'灯塔',type:'note',content:{},blocks:[]}];
const scheduleBlocks=[block('col1','schedule-column',{name:'待办',color:'#258c9d',sortOrder:0}),block('col2','schedule-column',{name:'完成',color:'#8564ba',sortOrder:1}),block('task1','schedule-item',{title:'整理世界观设定',isDone:false,themeColor:'#258c9d',columnId:'col1',date:'2026-10-27'})];
spirit.notes.value=[...notes,{id:'schedule',title:'创作计划',type:'schedule',blocks:scheduleBlocks,content:{}}];spirit.currentNoteId.value='schedule';
const canvasBlocks=[block('a','canvas-node',{attrs:{refNoteId:'one',x:100,y:100}}),block('b','canvas-node',{attrs:{refNoteId:'two',x:420,y:240}}),block('c','canvas-node',{attrs:{refNoteId:'schedule',x:100,y:360}}),block('e','canvas-edge',{source:'one',target:'two',sourceHandle:'right',targetHandle:'left',label:'前往'})];

const view=ref('canvas'),narrow=ref(false),theme=ref('light'),graph=ref(),focus=ref(''),many=ref(false),far=ref(true),changes=ref(0);const data=computed(()=>({nodes:Array.from({length:many.value?1000:12},(_,i)=>({id:String(i),title:'设定 '+i,type:'lore',color:'#258c9d'})),links:many.value?Array.from({length:999},(_,i)=>({source:String(i),target:String(i+1),isEmbed:false})):[]}));
const changeTheme=()=>document.documentElement.dataset.theme=theme.value;
</script>
<template><nav class="fixture-nav"><strong>隔离检查 · 禁止联网写入</strong><select aria-label="检查主题" v-model="theme" @change="changeTheme"><option value="light">白天</option><option value="dark">夜间</option></select><button @click="narrow=!narrow">{{narrow?'桌面宽度':'390px 宽度'}}</button><button v-for="(label,key) in {canvas:'灵脉画布',schedule:'计划表',novel:'小说章节',member:'成员弹窗',graph:'图谱压力'}" @click="view=key">{{label}}</button><span>本地变化 {{changes}}</span></nav><main :style="{width:narrow?'390px':'100%',maxWidth:'1100px',height:'650px',margin:'auto'}"><WorkspaceCanvas v-if="view==='canvas'" title="灵脉画布" note-id="canvas" :blocks="canvasBlocks" @change="changes++"/><WorkspaceSchedule v-if="view==='schedule'" title="创作计划" note-id="schedule" @change="changes++"/><Novel v-if="view==='novel'"/><Member v-if="view==='member'" project-id="fixture"/><template v-if="view==='graph'"><button @click="many=!many">{{many?'12 个节点':'1000 个节点'}}</button><button @click="focus='';graph.resetCamera()">全部适应</button><button @click="graph.resetCamera()">选中适应</button><button @click="focus='0'">选择设定 0</button><button @click="far=!far;focus=''">切换固定坐标</button><div style="position:relative;height:550px"><FlatGraph :key="String(far)" ref="graph" :data="data" :focus-id="focus" :positions="far?{'0':{x:-5000,y:3000},'1':{x:5000,y:-3000}}:{}" @select="focus=$event"/></div></template></main></template>