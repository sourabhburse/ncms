/* eslint-disable perfectionist/sort-imports */

// core
import { pinia } from "@/pinia"
import { router } from "@/router"
import { installPlugins } from "@/plugins"
import App from "@/App.vue"
// css
import "@/style.css"
import "normalize.css"
import "element-plus/theme-chalk/dark/css-vars.css"
import "@@/assets/styles/index.scss"


const app = createApp(App)


installPlugins(app)


app.use(pinia).use(router)


router.isReady().then(() => {
  app.mount("#app")
})

