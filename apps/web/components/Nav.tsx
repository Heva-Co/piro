import Link from "next/link";

export function Nav() {
  return (
    <header className="border-b bg-background/80 backdrop-blur sticky top-0 z-40">
      <div className="mx-auto max-w-3xl px-4 h-14 flex items-center justify-between">
        <Link href="/" className="font-semibold text-base tracking-tight">
          Piro
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
