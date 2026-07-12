import { useFormContext } from "react-hook-form";
import { Input } from "@base-ui/react";
import type { IntegrationFormValues } from "./types";

const inp = "rounded-lg border border-border bg-background px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-ring w-full";
const lbl = "text-sm font-semibold";

export function TelegramConfig() {
  const { register, formState: { errors } } = useFormContext<IntegrationFormValues>();
  return (
    <div className="flex flex-col gap-1.5">
      <label className={lbl}>Bot Token <span className="text-destructive">*</span></label>
      <Input type="password" {...register("tgBotToken")} placeholder="123456:ABC-DEF…" className={inp} />
      {errors.tgBotToken && <p className="text-xs text-destructive">{errors.tgBotToken.message}</p>}
      <p className="text-xs text-muted-foreground">From @BotFather. Each channel using this integration provides its own Chat ID.</p>
    </div>
  );
}
