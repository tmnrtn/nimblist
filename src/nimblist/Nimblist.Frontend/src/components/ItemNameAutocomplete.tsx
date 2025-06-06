import React, { useState } from "react";
import AsyncCreatableSelect from "react-select/async-creatable";
import { authenticatedFetch } from "../components/HttpHelper";

interface ItemNameAutocompleteProps {
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
  onKeyDown?: (e: React.KeyboardEvent) => void;
}

const ItemNameAutocomplete: React.FC<ItemNameAutocompleteProps> = ({ value, onChange, disabled, onKeyDown }) => {
  const [inputValue, setInputValue] = useState("");

  // Load options from backend
  const loadOptions = async (input: string) => {
    const res = await authenticatedFetch("/api/items/previous-names", {
      method: "GET",
      headers: { Accept: "application/json" },
    });
    if (!res.ok) return [];
    const names: string[] = await res.json();
    // Filter by input
    return names
      .filter((n) => n.toLowerCase().includes(input.toLowerCase()))
      .map((n) => ({ value: n, label: n }));
  };

  // Always allow free text entry and submit the raw string
  // On blur or Enter, submit the inputValue if it's not already selected
  const handleBlurOrEnter = () => {
    if (inputValue && inputValue !== value) {
      onChange(inputValue);
    }
  };

  return (
    <AsyncCreatableSelect
      isClearable
      cacheOptions
      defaultOptions
      loadOptions={loadOptions}
      value={value ? { value, label: value } : null}
      onChange={(option) => {
        // Accept any string, whether from the list or typed by the user
        if (typeof option === "string") {
          onChange(option);
        } else if (option && typeof option.value === "string") {
          onChange(option.value);
        } else {
          onChange("");
        }
      }}
      onInputChange={(val) => {
        setInputValue(val);
        return val;
      }}
      inputValue={inputValue}
      placeholder="Item Name (required)"
      isDisabled={disabled}
      // Remove formatCreateLabel to avoid prompting to add new option
      aria-label="New item name"
      formatCreateLabel={undefined}
      isValidNewOption={() => false}
      onBlur={handleBlurOrEnter}
      onKeyDown={(e) => {
        if (e.key === "Enter" && inputValue && inputValue !== value) {
          e.preventDefault();
          onChange(inputValue);
        }
        if (onKeyDown) onKeyDown(e);
      }}
    />
  );
};

export default ItemNameAutocomplete;
