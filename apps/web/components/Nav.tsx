import Link from "next/link";
import { publicApi } from "@/lib/api";
import { ThemeToggle } from "@/components/ThemeToggle";

export async function Nav() {
  let siteName = "Piro";
  try {
    const cfg = await publicApi.siteConfig();
    if (cfg.name) siteName = cfg.name;
  } catch { /* use default */ }

  return (
    <header className="border-b border-border/40 bg-background/80 backdrop-blur sticky top-0 z-40">
      <div className="mx-auto w-full max-w-screen-lg px-8 h-14 flex items-center justify-between">
        <Link href="/" className="flex items-center gap-2 font-semibold text-base tracking-tight text-foreground">
          <img src="/piro.svg" alt="Piro logo" width={22} height={22} />
          {siteName}
        </Link>
        <nav className="flex items-center gap-3 text-sm">
          <Link
            href="/incidents"
            className="text-muted-foreground hover:text-foreground transition-colors"
          >
            History
          </Link>
          <Link
            href="/admin"
            className="text-muted-foreground hover:text-foreground transition-colors"
          >
            Admin
          </Link>
          <ThemeToggle />
        </nav>
      </div>
    </header>
  );
}
