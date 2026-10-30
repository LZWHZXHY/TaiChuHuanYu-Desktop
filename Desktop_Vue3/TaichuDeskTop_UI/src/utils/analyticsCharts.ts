import { use } from 'echarts/core'
import { LineChart, BarChart } from 'echarts/charts'
import { TooltipComponent, LegendComponent, GridComponent } from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'

// 仅在打开管理后台的数据页时加载，只注册实际用到的图表。
use([LineChart, BarChart, TooltipComponent, LegendComponent, GridComponent, CanvasRenderer])
export { init } from 'echarts/core'
