import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { authenticatedFetch } from "./HttpHelper";

// VITE_API_BASE_URL is set to 'https://localhost:64213' in vitest.config.ts test.env

describe("authenticatedFetch", () => {
  const originalFetch = globalThis.fetch;

  beforeEach(() => {
    vi.spyOn(console, "error").mockImplementation(() => {});
  });

  afterEach(() => {
    vi.restoreAllMocks();
    globalThis.fetch = originalFetch;
  });

  function makeFetchSpy(overrides: Partial<Response> = {}) {
    const response = {
      ok: true,
      status: 200,
      url: "https://localhost:64213/api/test",
      clone: vi.fn(),
      text: vi.fn().mockResolvedValue(""),
      ...overrides,
    } as unknown as Response;
    // clone returns a response-like with a text() method
    (response.clone as ReturnType<typeof vi.fn>).mockReturnValue({
      text: vi.fn().mockResolvedValue(""),
    });
    const spy = vi.spyOn(globalThis, "fetch").mockResolvedValue(response);
    return { spy, response };
  }

  it("passes credentials: include on every request", async () => {
    const { spy } = makeFetchSpy();
    await authenticatedFetch("/api/test");
    expect(spy).toHaveBeenCalledWith(
      expect.any(String),
      expect.objectContaining({ credentials: "include" })
    );
  });

  it("prepends the base URL to relative paths", async () => {
    const { spy } = makeFetchSpy();
    await authenticatedFetch("/api/items");
    expect(spy).toHaveBeenCalledWith(
      "https://localhost:64213/api/items",
      expect.anything()
    );
  });

  it("returns response unchanged when ok is true", async () => {
    const { response } = makeFetchSpy({ ok: true, status: 200 });
    const result = await authenticatedFetch("/api/test");
    expect(result).toBe(response);
  });

  it("calls console.error with status, url, and body text when ok is false", async () => {
    const cloneText = vi.fn().mockResolvedValue("Not found");
    const response = {
      ok: false,
      status: 404,
      url: "https://localhost:64213/api/missing",
      clone: vi.fn().mockReturnValue({ text: cloneText }),
    } as unknown as Response;
    vi.spyOn(globalThis, "fetch").mockResolvedValue(response);

    await authenticatedFetch("/api/missing");

    // The console.error is called inside a .then() — flush the microtask queue
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(console.error).toHaveBeenCalledWith(
      expect.stringContaining("404")
    );
    expect(console.error).toHaveBeenCalledWith(
      expect.stringContaining("/api/missing")
    );
    expect(console.error).toHaveBeenCalledWith(
      expect.stringContaining("Not found")
    );
  });

  it("returns the original response even when ok is false (does not throw)", async () => {
    const cloneText = vi.fn().mockResolvedValue("Bad request");
    const response = {
      ok: false,
      status: 400,
      clone: vi.fn().mockReturnValue({ text: cloneText }),
    } as unknown as Response;
    vi.spyOn(globalThis, "fetch").mockResolvedValue(response);

    const result = await authenticatedFetch("/api/bad");
    expect(result).toBe(response);
  });

  it("handles network errors (fetch rejects) by propagating the rejection", async () => {
    vi.spyOn(globalThis, "fetch").mockRejectedValue(new Error("Network failure"));
    await expect(authenticatedFetch("/api/test")).rejects.toThrow(
      "Network failure"
    );
  });
});
