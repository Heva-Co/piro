import Link from "next/link";
import { publicApi } from "@/lib/api";

export async function Nav() {
  let siteName = "Piro";
  try {
    const cfg = await publicApi.siteConfig();
    if (cfg.name) siteName = cfg.name;
  } catch { /* use default */ }

  return (
    <header className="border-b border-border/40 bg-background/80 backdrop-blur sticky top-0 z-40">
      <div className="mx-auto w-full max-w-screen-lg px-8 h-14 flex items-center justify-between">
        <Link href="/" className="font-semibold text-base tracking-tight text-foreground">
          {siteName}
        </Link>
        <nav className="flex items-center gap-4 text-sm">
          <Link
            href="/admin"
            className="text-muted-foreground hover:text-foreground transition-colors"
          >
            Admin
          </Link>
        </nav>
      </div>
    </header>
  );
}
