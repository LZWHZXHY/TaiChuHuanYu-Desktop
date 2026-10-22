import { use } from 'echarts/core'
import { PieChart, LineChart } from 'echarts/charts'
import { TooltipComponent, LegendComponent, GridComponent } from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'

// 仅在打开财务看板时加载，并注册实际使用的图表和渲染器。
use([PieChart, LineChart, TooltipComponent, LegendComponent, GridComponent, CanvasRenderer])
export { init } from 'echarts/core'
