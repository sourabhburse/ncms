import type * as Tables from "./type"
import { request } from "@/http/axios"


export function createTableDataApi(data: Tables.CreateOrUpdateTableRequestData) {
  return request({
    url: "tables",
    method: "post",
    data
  })
}


export function deleteTableDataApi(id: number) {
  return request({
    url: `tables/${id}`,
    method: "delete"
  })
}


export function updateTableDataApi(data: Tables.CreateOrUpdateTableRequestData) {
  return request({
    url: "tables",
    method: "put",
    data
  })
}


export function getTableDataApi(params: Tables.TableRequestData) {
  return request<Tables.TableResponseData>({
    url: "tables",
    method: "get",
    params
  })
}
