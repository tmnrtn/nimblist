import React, { useRef, useState, useEffect, forwardRef, useImperativeHandle } from "react";
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
    const [inputValue, setInputValue] = useState("");

    useImperativeHandle(ref, () => ({
      focus: () => selectRef.current?.focus(),
    }));

    // When the parent resets value to "" (e.g. after a successful submit),
    // also clear the controlled inputValue so the text box is fully empty.
    useEffect(() => {
      if (!value) setInputValue("");
    }, [value]);

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
        inputValue={inputValue}
        onInputChange={(newVal, { action }) => {
          setInputValue(newVal);
          // Propagate every keystroke so the parent always holds the current text.
          // Ignore react-select's own resets (menu-close, set-value, etc.) so
          // a confirmed selection isn't wiped out by the internal clear.
          if (action === "input-change") onChange(newVal);
        }}
        value={value ? { value, label: value } : null}
        onChange={(option) => {
          if (option && typeof option.value === "string") {
            onChange(option.value);
          } else if (option === null) {
            onChange("");
          }
        }}
        onCreateOption={(newValue) => {
          if (newValue) {
            onChange(newValue);
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
