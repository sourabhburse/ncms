import { resolve } from "node:path"
import vue from "@vitejs/plugin-vue"
import AutoImport from "unplugin-auto-import/vite"
import SvgComponent from "unplugin-svg-component/vite"
import { ElementPlusResolver } from "unplugin-vue-components/resolvers"
import Components from "unplugin-vue-components/vite"
import { defineConfig, loadEnv } from "vite"
import { ViteMcp } from "vite-plugin-mcp"
import svgLoader from "vite-svg-loader"
import tailwindcss from "@tailwindcss/vite"

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const { VITE_PUBLIC_PATH } = loadEnv(mode, process.cwd(), "") as Record<string, string>
  return {
    base: VITE_PUBLIC_PATH,
    resolve: {
      alias: {
        
        "@": resolve(__dirname, "src"),

        "@@": resolve(__dirname, "src/common")
      }
    },

    server: {
      host: true,
      port: 3333,
      strictPort: false,
      open: true,
      proxy: {
        "/api/v1": {
          target: "https://localhost:7206",
          ws: false,
          changeOrigin: true,
          secure: false
        }
      },
      cors: true,
      warmup: {
        clientFiles: [
          "./src/layouts/**/*.*",
          "./src/pinia/**/*.*",
          "./src/router/**/*.*"
        ]
      }
    },
    build: {
      reportCompressedSize: false,
      chunkSizeWarningLimit: 2048
    },

    optimizeDeps: {
      include: ["element-plus/es/components/*/style/css"]
    },

    css: {
      preprocessorMaxWorkers: true,
      preprocessorOptions: {
        scss: {
          silenceDeprecations: ["import", "global-builtin", "color-functions"]
        }
      }
    },

    plugins: [
      vue(),
      tailwindcss(),

      svgLoader({
        defaultImport: "url",
        svgoConfig: {
          plugins: [
            {
              name: "preset-default",
              params: {
                overrides: {
                  // @see https://github.com/svg/svgo/issues/1128
                  removeViewBox: false
                }
              }
            }
          ]
        }
      }),

      SvgComponent({
        iconDir: [resolve(__dirname, "src/common/assets/icons")],
        preserveColor: resolve(__dirname, "src/common/assets/icons/preserve-color"),
        dts: true,
        dtsDir: resolve(__dirname, "types/auto")
      }),

      AutoImport({
        imports: ["vue", "vue-router", "pinia"],
        dts: "types/auto/auto-imports.d.ts",
        resolvers: [ElementPlusResolver()]
      }),

      Components({
        dts: "types/auto/components.d.ts",
        resolvers: [ElementPlusResolver()]
      }),

      ViteMcp()
    ]
  }
})
