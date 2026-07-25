import { AutoRefreshButton } from "@/components/AutoRefreshButton";

interface Props {
  search: string;
  onSearchChange: (value: string) => void;
  onRefetch: () => void;
}

function ChecksSearchBar(props: Props) {
  const { onRefetch } = props;

  return (
    <div className="px-4 py-3 border-b flex items-center gap-3">
      <div className="flex-1"></div>
      {/* <InputGroup className="h-10 flex-1">
        <InputGroupAddon>
          <Search size={14} className="text-muted-foreground" />
        </InputGroupAddon>
        <InputGroupInput
          value={search}
          onChange={(e) => onSearchChange(e.target.value)}
          placeholder="Search checks, services, types..."
        />
      </InputGroup> */}
      <AutoRefreshButton onRefetch={onRefetch} />
    </div>
  );
}

export default ChecksSearchBar;
