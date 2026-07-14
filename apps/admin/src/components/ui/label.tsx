import * as React from "react"

import { cn } from "@/lib/utils"

type LabelProps = React.ComponentProps<"label"> & {
  required?: boolean
}

function Label(props: LabelProps) {
  const { className, required, children, ...rest } = props
  return (
    <label
      data-slot="label"
      className={cn(
        "flex items-center gap-1 text-sm leading-none font-medium select-none group-data-[disabled=true]:pointer-events-none group-data-[disabled=true]:opacity-50 peer-disabled:cursor-not-allowed peer-disabled:opacity-50",
        className
      )}
      {...rest}
    >
      {children}
      {required && <span className="text-red-500">*</span>}
    </label>
  )
}

export { Label }
