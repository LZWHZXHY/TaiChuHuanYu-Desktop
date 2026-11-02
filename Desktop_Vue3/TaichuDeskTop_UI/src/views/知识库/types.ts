export interface GraphNodeDto {
  id: string
  title: string
  type: string
  size: number
  coverImage: string | null
  description?: string
  /** 站内地址：全站内容节点用来「打开原内容」 */
  route?: string | null
  _nasPath?: string // 👇 新增：用于存储 NAS 文件的真实路径
  /** 额外参与搜索、但不显示的文字（资源节点的标签、文件名、位置、上传者……，和资源列表搜的字段一样） */
  searchText?: string
}

export interface GraphEdgeDto {
  id: string
  source: string
  target: string
  relationType: string
}

export interface GraphDataResponse {
  nodes: GraphNodeDto[]
  edges: GraphEdgeDto[]
}

export interface GraphNode {
  id: string
  name: string
  type: string
  val: number
  color: string
  coverImage: string | null
  description?: string
  /** 站内地址：全站内容节点用来「打开原内容」 */
  route?: string | null
  _nasPath?: string // 👇 新增：用于存储 NAS 文件的真实路径
  
  x?: number
  y?: number
  z?: number
  vx?: number
  vy?: number
  vz?: number

  __threeObject?: any
}

export interface GraphLink {
  source: string | GraphNode
  target: string | GraphNode
  name: string
}

export interface NodeTheme {
  color: string
  emissive: string
  label: string
  typeLabel: string
}