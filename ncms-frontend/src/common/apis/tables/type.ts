export interface CreateOrUpdateTableRequestData {
  id?: number
  username: string
  password?: string
}

export interface TableRequestData {
  currentPage: number
  size: number
  username?: string
  phone?: string
}

export interface TableData {
  createTime: string
  email: string
  id: number
  phone: string
  roles: string
  status: boolean
  username: string
}

export type TableResponseData = ApiResponseData<{
  list: TableData[]
  total: number
}>
