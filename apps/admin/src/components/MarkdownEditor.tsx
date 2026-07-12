import { useEditor, EditorContent } from "@tiptap/react";
import StarterKit from "@tiptap/starter-kit";
import Underline from "@tiptap/extension-underline";
import Placeholder from "@tiptap/extension-placeholder";
import { Markdown } from "tiptap-markdown";
import { Bold, Italic, Underline as UnderlineIcon, List } from "lucide-react";
import { Toggle } from "@/components/ui/toggle";
import { Separator } from "@/components/ui/separator";

interface MarkdownEditorProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
}

export function MarkdownEditor({ value, onChange, placeholder }: MarkdownEditorProps) {
  const editor = useEditor({
    extensions: [
      StarterKit.configure({ heading: false, codeBlock: false, blockquote: false, horizontalRule: false }),
      Underline,
      Placeholder.configure({ placeholder }),
      Markdown.configure({ html: false }),
    ],
    content: value,
    editorProps: {
      attributes: {
        class: "min-h-36 px-3 py-2 text-sm text-foreground focus:outline-none prose prose-sm max-w-none [&_ul]:list-disc [&_ul]:pl-5",
      },
    },
    onUpdate: ({ editor }) => {
      // tiptap-markdown doesn't ship a Storage type augmentation for the extension it registers.
      onChange((editor.storage as unknown as { markdown: { getMarkdown(): string } }).markdown.getMarkdown());
    },
  });

  if (!editor) return null;

  return (
    <div className="rounded-lg border border-border overflow-hidden bg-background focus-within:border-ring focus-within:ring-3 focus-within:ring-ring/50">
      <div className="flex items-center gap-1 border-b border-border bg-muted/50 px-2 py-1.5">
        <Toggle
          size="sm"
          pressed={editor.isActive("bold")}
          onPressedChange={() => editor.chain().focus().toggleBold().run()}
          aria-label="Bold"
        >
          <Bold />
        </Toggle>
        <Toggle
          size="sm"
          pressed={editor.isActive("italic")}
          onPressedChange={() => editor.chain().focus().toggleItalic().run()}
          aria-label="Italic"
        >
          <Italic />
        </Toggle>
        <Toggle
          size="sm"
          pressed={editor.isActive("underline")}
          onPressedChange={() => editor.chain().focus().toggleUnderline().run()}
          aria-label="Underline"
        >
          <UnderlineIcon />
        </Toggle>
        <Separator orientation="vertical" className="mx-1 h-4" />
        <Toggle
          size="sm"
          pressed={editor.isActive("bulletList")}
          onPressedChange={() => editor.chain().focus().toggleBulletList().run()}
          aria-label="Bullet list"
        >
          <List />
        </Toggle>
      </div>
      <EditorContent editor={editor} />
    </div>
  );
}
