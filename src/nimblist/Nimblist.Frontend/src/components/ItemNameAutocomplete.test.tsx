import { render, screen, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import type { MockedFunction } from "vitest";
import { authenticatedFetch } from "./HttpHelper";
import ItemNameAutocomplete from "./ItemNameAutocomplete";

vi.mock("./HttpHelper");

// Mock react-select/async-creatable so we can drive it with plain inputs
vi.mock("react-select/async-creatable", () => ({
  default: vi.fn(({ loadOptions, onChange, onCreateOption, value, isDisabled, "aria-label": ariaLabel, placeholder }: {
    loadOptions: (input: string) => Promise<{ value: string; label: string }[]>;
    onChange: (option: { value: string; label: string } | null) => void;
    onCreateOption: (value: string) => void;
    value: { value: string; label: string } | null;
    isDisabled?: boolean;
    "aria-label"?: string;
    placeholder?: string;
  }) => {
    const handleLoad = async () => {
      const opts = await loadOptions("");
      const list = document.getElementById("test-options-list");
      if (list) list.innerHTML = opts.map((o: { value: string; label: string }) => `<li>${o.label}</li>`).join("");
    };
    return (
      <div>
        <span aria-label={ariaLabel}>{value?.label ?? placeholder}</span>
        <button onClick={handleLoad} data-testid="load-opts">Load</button>
        <ul id="test-options-list" />
        <button onClick={() => onChange({ value: "Milk", label: "Milk" })} data-testid="select-opt">Select Milk</button>
        <button onClick={() => onChange(null)} data-testid="clear-opt">Clear</button>
        <button onClick={() => onCreateOption("New Item")} data-testid="create-opt">Create</button>
        {isDisabled && <span data-testid="disabled-indicator">disabled</span>}
      </div>
    );
  }),
}));

const mockFetch = authenticatedFetch as MockedFunction<typeof authenticatedFetch>;

function jsonResponse(data: unknown, ok = true) {
  return Promise.resolve({ ok, json: () => Promise.resolve(data) } as Response);
}

describe("ItemNameAutocomplete", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders with placeholder when no value", () => {
    render(<ItemNameAutocomplete value="" onChange={vi.fn()} />);
    expect(screen.getByText("Item Name (required)")).toBeInTheDocument();
  });

  it("renders current value as label", () => {
    render(<ItemNameAutocomplete value="Apples" onChange={vi.fn()} />);
    expect(screen.getByLabelText("New item name")).toHaveTextContent("Apples");
  });

  it("loadOptions fetches names and filters by input", async () => {
    const names = ["Milk", "Eggs", "Butter"];
    mockFetch.mockReturnValue(jsonResponse(names));

    render(<ItemNameAutocomplete value="" onChange={vi.fn()} />);
    screen.getByTestId("load-opts").click();

    await waitFor(() => {
      expect(screen.getByText("Milk")).toBeInTheDocument();
      expect(screen.getByText("Eggs")).toBeInTheDocument();
    });
    expect(mockFetch).toHaveBeenCalledWith("/api/items/previous-names", expect.anything());
  });

  it("returns empty array when fetch fails", async () => {
    mockFetch.mockReturnValue(jsonResponse(null, false));

    render(<ItemNameAutocomplete value="" onChange={vi.fn()} />);
    screen.getByTestId("load-opts").click();

    await waitFor(() => {
      expect(document.getElementById("test-options-list")?.innerHTML).toBe("");
    });
  });

  it("caches names so fetch is called only once across loads", async () => {
    mockFetch.mockReturnValue(jsonResponse(["Milk"]));
    const { rerender } = render(<ItemNameAutocomplete value="" onChange={vi.fn()} />);

    screen.getByTestId("load-opts").click();
    await waitFor(() => expect(mockFetch).toHaveBeenCalledTimes(1));

    // Trigger loadOptions again (same instance, cache should be warm)
    screen.getByTestId("load-opts").click();
    await waitFor(() => expect(mockFetch).toHaveBeenCalledTimes(1)); // still 1
    rerender(<ItemNameAutocomplete value="" onChange={vi.fn()} />);
  });

  it("calls onChange with selected value", () => {
    const onChange = vi.fn();
    render(<ItemNameAutocomplete value="" onChange={onChange} />);
    screen.getByTestId("select-opt").click();
    expect(onChange).toHaveBeenCalledWith("Milk");
  });

  it("calls onChange with empty string when cleared", () => {
    const onChange = vi.fn();
    render(<ItemNameAutocomplete value="Milk" onChange={onChange} />);
    screen.getByTestId("clear-opt").click();
    expect(onChange).toHaveBeenCalledWith("");
  });

  it("calls onChange when a new option is created", () => {
    const onChange = vi.fn();
    render(<ItemNameAutocomplete value="" onChange={onChange} />);
    screen.getByTestId("create-opt").click();
    expect(onChange).toHaveBeenCalledWith("New Item");
  });

  it("passes disabled prop to select", () => {
    render(<ItemNameAutocomplete value="" onChange={vi.fn()} disabled />);
    expect(screen.getByTestId("disabled-indicator")).toBeInTheDocument();
  });
});
