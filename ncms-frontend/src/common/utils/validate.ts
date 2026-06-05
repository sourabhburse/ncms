
export function isArray<T>(arg: T) {
  return Array.isArray ? Array.isArray(arg) : Object.prototype.toString.call(arg) === "[object Array]"
}

export function isString(str: unknown) {
  return typeof str === "string" || str instanceof String
}

export function isExternal(path: string) {
  const reg = /^(https?:|mailto:|tel:)/
  return reg.test(path)
}
