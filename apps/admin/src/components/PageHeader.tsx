type BreadcrumbItem =
  | { label: string; onClick: () => void }
  | { label: string };

interface Props {
  breadcrumbs: BreadcrumbItem[];
  actions?: React.ReactNode;
}

export function PageHeader({ breadcrumbs, actions }: Props) {
  return (
    <div className="flex items-center justify-between mb-6">
      <nav className="flex items-center gap-2 text-sm text-muted-foreground">
        {breadcrumbs.map((item, i) => {
          const isLast = i === breadcrumbs.length - 1;
          return (
            <span key={i} className="flex items-center gap-2">
              {i > 0 && <span>/</span>}
              {"onClick" in item ? (
                <button
                  type="button"
                  onClick={item.onClick}
                  className="hover:text-foreground transition-colors"
                >
                  {item.label}
                </button>
              ) : (
                <span className={isLast ? "text-foreground font-medium" : undefined}>
                  {item.label}
                </span>
              )}
            </span>
          );
        })}
      </nav>
      {actions && <div className="flex items-center gap-2">{actions}</div>}
    </div>
  );
}
