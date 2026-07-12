import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";

interface Props {
    columns: string[]
}

function TableSkeleton(props: Props) {
    const { columns } = props;
    
    return (
        <Table className="min-w-full text-sm">
            <TableHeader>
                <TableRow className="border-b">
                    {columns.map((column) => <TableHead key={column}>{column}</TableHead>)}
                </TableRow>
            </TableHeader>
            <TableBody className="divide-y">
                {Array.from({ length: 4 }).map((_, i) => (
                    <TableRow key={i}>
                        {columns.map((column) => (
                            <TableCell key={column}>
                                <Skeleton className="h-4 w-24" />
                            </TableCell>
                        ))}
                    </TableRow>
                ))}
            </TableBody>
        </Table>
    )
}

export default TableSkeleton;