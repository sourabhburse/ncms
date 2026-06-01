type OptionValue = string | number


interface SelectOption {
  value: OptionValue
  label: string
  disabled?: boolean
}


type ApiData = ApiResponseData<SelectOption[]>


interface FetchSelectProps {
  api: () => Promise<ApiData>
}


export function useFetchSelect(props: FetchSelectProps) {
  const { api } = props

  const loading = ref<boolean>(false)

  const options = ref<SelectOption[]>([])

  const value = ref<OptionValue>("")


  const loadData = () => {
    loading.value = true
    options.value = []
    api().then((res) => {
      options.value = res.data
    }).finally(() => {
      loading.value = false
    })
  }

  onMounted(() => {
    loadData()
  })

  return { loading, options, value }
}
