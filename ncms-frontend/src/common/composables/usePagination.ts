interface PaginationData {
  total?: number
  currentPage?: number
  pageSizes?: number[]
  pageSize?: number
  layout?: string
}


const DEFAULT_PAGINATION_DATA = {
  total: 0,
  currentPage: 1,
  pageSizes: [10, 20, 50],
  pageSize: 10,
  layout: "total, sizes, prev, pager, next, jumper"
}


export function usePagination(initPaginationData: PaginationData = {}) {

  const paginationData = reactive({ ...DEFAULT_PAGINATION_DATA, ...initPaginationData })


  const handleCurrentChange = (value: number) => {
    paginationData.currentPage = value
  }


  const handleSizeChange = (value: number) => {
    paginationData.pageSize = value
  }

  return { paginationData, handleCurrentChange, handleSizeChange }
}
