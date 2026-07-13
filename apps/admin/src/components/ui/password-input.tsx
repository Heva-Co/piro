import { useState, forwardRef } from "react"
import { Eye, EyeOff } from "lucide-react"
import { Input } from "@/components/ui/input"
import type { Input as InputPrimitive } from "@base-ui/react/input"

const PasswordInput = forwardRef<HTMLInputElement, InputPrimitive.Props>((props, ref) => {
  const [visible, setVisible] = useState(false)

  return (
    <div className="relative">
      <Input ref={ref} type={visible ? "text" : "password"} className="pr-9" {...props} />
      <button
        type="button"
        onClick={() => setVisible((v) => !v)}
        tabIndex={-1}
        className="absolute inset-y-0 right-0 flex items-center px-3 text-muted-foreground hover:text-foreground transition-colors"
      >
        {visible ? <EyeOff size={14} /> : <Eye size={14} />}
      </button>
    </div>
  )
})
PasswordInput.displayName = "PasswordInput"

export { PasswordInput }
