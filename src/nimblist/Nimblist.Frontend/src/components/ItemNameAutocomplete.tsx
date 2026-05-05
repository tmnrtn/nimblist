import React, { useRef, forwardRef, useImperativeHandle } from "react";
import AsyncCreatableSelect from "react-select/async-creatable";
import type { SelectInstance } from "react-select";
import { authenticatedFetch } from "../components/HttpHelper";

interface ItemNameAutocompleteProps {
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
  onKeyDown?: (e: React.KeyboardEvent) => void;
}

export interface ItemNameAutocompleteHandle {
  focus: () => void;
}

type Option = { value: string; label: string };

const ItemNameAutocomplete = forwardRef<ItemNameAutocompleteHandle, ItemNameAutocompleteProps>(
  ({ value, onChange, disabled, onKeyDown }, ref) => {
    const selectRef = useRef<SelectInstance<Option>>(null);
    const cachedNamesRef = useRef<string[] | null>(null);

    useImperativeHandle(ref, () => ({
      focus: () => selectRef.current?.focus(),
    }));

    const loadOptions = async (input: string) => {
      if (!cachedNamesRef.current) {
        const res = await authenticatedFetch("/api/items/previous-names", {
          method: "GET",
          headers: { Accept: "application/json" },
        });
        if (!res.ok) return [];
        cachedNamesRef.current = await res.json();
      }
      return (cachedNamesRef.current ?? [])
        .filter((n) => n.toLowerCase().includes(input.toLowerCase()))
        .map((n) => ({ value: n, label: n }));
    };

    return (
      <AsyncCreatableSelect
        ref={selectRef}
        isClearable
        cacheOptions
        defaultOptions
        loadOptions={loadOptions}
        value={value ? { value, label: value } : null}
        onChange={(option) => {
          if (option && typeof option.value === "string") {
            onChange(option.value);
          } else if (option === null) {
            onChange("");
          }
        }}
        onCreateOption={(inputValue) => {
          if (inputValue) {
            onChange(inputValue);
          }
        }}
        placeholder="Item Name (required)"
        isDisabled={disabled}
        aria-label="New item name"
        formatCreateLabel={(inputValue) => inputValue}
        onKeyDown={(e) => {
          if (onKeyDown) onKeyDown(e);
        }}
      />
    );
  }
);

export default ItemNameAutocomplete;
