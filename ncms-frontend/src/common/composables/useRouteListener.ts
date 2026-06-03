import type { Handler } from "mitt"
import type { RouteLocationNormalizedGeneric } from "vue-router"
import mitt from "mitt"


type Callback = (route: RouteLocationNormalizedGeneric) => void

const emitter = mitt()

const key = Symbol("ROUTE_CHANGE")

let latestRoute: RouteLocationNormalizedGeneric


export function setRouteChange(to: RouteLocationNormalizedGeneric) {

  emitter.emit(key, to)

  latestRoute = to
}


export function useRouteListener() {

  const callbackList: Callback[] = []

  const listenerRouteChange = (callback: Callback, immediate = false) => {

    callbackList.push(callback)

    emitter.on(key, callback as Handler)

    immediate && latestRoute && callback(latestRoute)
  }


  const removeRouteListener = (callback: Callback) => {
    emitter.off(key, callback as Handler)
  }

  onBeforeUnmount(() => {
    callbackList.forEach(removeRouteListener)
  })

  return { listenerRouteChange, removeRouteListener }
}
