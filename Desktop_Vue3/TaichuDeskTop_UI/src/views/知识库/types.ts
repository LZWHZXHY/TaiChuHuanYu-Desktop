export interface GraphNodeDto {
  id: string
  title: string
  type: string
  size: number
  coverImage: string | null
  description?: string
  _nasPath?: string // 👇 新增：用于存储 NAS 文件的真实路径
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