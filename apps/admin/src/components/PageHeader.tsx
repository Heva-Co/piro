import { Link } from "react-router-dom";

type BreadcrumbItem =
  /** Navigates to a route via react-router — the declarative form: just pass the path. */
  | { label: string; to: string }
  /** Runs a handler (e.g. multi-step form "go back a step" that isn't a route change). */
  | { label: string; onClick: () => void }
  /** Plain text — the current page, or a non-navigable crumb. */
  | { label: string };

interface Props {
  breadcrumbs: BreadcrumbItem[];
  subheader?: React.ReactNode;
  actions?: React.ReactNode;
}

export function PageHeader({ breadcrumbs, subheader, actions }: Props) {
  return (
    <div className="mb-6">
      <div className="flex items-center justify-between">
        <div>
          <nav className="flex items-center gap-2 text-sm text-muted-foreground">
            {breadcrumbs.map((item, i) => {
              const isLast = i === breadcrumbs.length - 1;
              return (
                <span key={i} className="flex items-center gap-2">
                  {i > 0 && <span>/</span>}
                  {"to" in item ? (
                    <Link to={item.to} className="hover:text-foreground transition-colors">
                      {item.label}
                    </Link>
                  ) : "onClick" in item ? (
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
          {subheader && <p className="text-sm text-gray-500 mt-1">{subheader}</p>}
        </div>
        {actions && <div className="flex items-center gap-2">{actions}</div>}
      </div>

    </div>
  );
}
