import {
  MDXEditor,
  listsPlugin,
  markdownShortcutPlugin,
  toolbarPlugin,
  BoldItalicUnderlineToggles,
  ListsToggle,
} from "@mdxeditor/editor";
import "@mdxeditor/editor/style.css";

interface MarkdownEditorProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
}

export function MarkdownEditor({ value, onChange, placeholder }: MarkdownEditorProps) {
  return (
    <div className="rounded-lg border border-border overflow-hidden focus-within:ring-2 focus-within:ring-ring [&_.mdxeditor]:min-h-36 [&_.mdxeditor]:bg-background [&_.mdxeditor]:text-foreground [&_.mdxeditor-toolbar]:border-b [&_.mdxeditor-toolbar]:border-border [&_.mdxeditor-toolbar]:bg-muted [&_.mdxeditor-toolbar_button]:text-foreground [&_.mdxeditor-toolbar_button:hover]:bg-muted/70 [&_[contenteditable]]:text-foreground [&_[contenteditable]]:caret-foreground">
      <MDXEditor
        markdown={value}
        onChange={onChange}
        placeholder={placeholder}
        plugins={[
          listsPlugin(),
          markdownShortcutPlugin(),
          toolbarPlugin({
            toolbarContents: () => (
              <>
                <BoldItalicUnderlineToggles />
                <ListsToggle />
              </>
            ),
          }),
        ]}
      />
    </div>
  );
}
