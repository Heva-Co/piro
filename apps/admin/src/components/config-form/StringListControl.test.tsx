import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import StringListControl from "./StringListControl";

describe("StringListControl — per-item errors", () => {
  it("E1: flags only the offending row (aria-invalid + message)", () => {
    render(
      <StringListControl
        value={["8.8.8.8", "foo_bar", "1.1.1.1"]}
        onChange={() => {}}
        itemErrors={{ 1: "Enter a valid IP address or hostname." }}
      />
    );

    const inputs = screen.getAllByRole("textbox");
    expect(inputs[0]).not.toHaveAttribute("aria-invalid");
    expect(inputs[1]).toHaveAttribute("aria-invalid", "true");
    expect(inputs[2]).not.toHaveAttribute("aria-invalid");
    expect(screen.getByText("Enter a valid IP address or hostname.")).toBeInTheDocument();
  });

  it("E2: no itemErrors → nothing flagged, no messages", () => {
    render(<StringListControl value={["8.8.8.8", "1.1.1.1"]} onChange={() => {}} />);
    for (const input of screen.getAllByRole("textbox")) {
      expect(input).not.toHaveAttribute("aria-invalid");
    }
    expect(screen.queryByText(/valid IP/i)).not.toBeInTheDocument();
  });

  it("E3: multiple bad rows each flagged with their own message", () => {
    render(
      <StringListControl
        value={["bad one", "8.8.8.8", "bad two"]}
        onChange={() => {}}
        itemErrors={{ 0: "err A", 2: "err B" }}
      />
    );
    const inputs = screen.getAllByRole("textbox");
    expect(inputs[0]).toHaveAttribute("aria-invalid", "true");
    expect(inputs[1]).not.toHaveAttribute("aria-invalid");
    expect(inputs[2]).toHaveAttribute("aria-invalid", "true");
    expect(screen.getByText("err A")).toBeInTheDocument();
    expect(screen.getByText("err B")).toBeInTheDocument();
  });

  it("E4: removing a row calls onChange without that entry", async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <StringListControl
        value={["8.8.8.8", "foo_bar"]}
        onChange={onChange}
        itemErrors={{ 1: "msg" }}
      />
    );
    // Two rows → two delete buttons; click the second row's delete.
    const deleteButtons = screen.getAllByRole("button").filter((b) => b.querySelector("svg"));
    // The last icon button before "Add" is row 1's delete; simplest: the delete buttons are all but the last (Add) button.
    await user.click(deleteButtons[1]);
    expect(onChange).toHaveBeenCalledWith(["8.8.8.8"]);
  });

  it("empty/non-array value renders no rows, just the Add button", () => {
    render(<StringListControl value={undefined} onChange={() => {}} />);
    expect(screen.queryAllByRole("textbox")).toHaveLength(0);
    expect(screen.getByRole("button", { name: /add/i })).toBeInTheDocument();
  });
});
