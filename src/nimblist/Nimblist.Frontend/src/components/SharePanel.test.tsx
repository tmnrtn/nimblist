import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import type { MockedFunction } from "vitest";
import { authenticatedFetch } from "./HttpHelper";
import SharePanel from "./SharePanel";

vi.mock("./HttpHelper");

const mockFetch = authenticatedFetch as MockedFunction<typeof authenticatedFetch>;

function jsonResponse(data: unknown, ok = true, status = 200) {
  return Promise.resolve({ ok, status, json: () => Promise.resolve(data) } as Response);
}

const defaultProps = {
  endpoint: "/api/listshares?listId=list-1",
  postEndpoint: "/api/listshares",
  resourceId: "list-1",
  resourceKey: "listId" as const,
  isOwner: true,
};

const family1 = { id: "fam-1", name: "Smith Family", ownerId: "u1", members: [] };
const share1 = {
  id: "share-1", listId: "list-1",
  sharedWithUserId: null, sharedWithEmail: null,
  sharedWithFamilyId: "fam-1", sharedWithFamilyName: "Smith Family",
  sharedAt: "2026-01-01",
};

describe("SharePanel", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders read-only message for non-owners", () => {
    render(<SharePanel {...defaultProps} isOwner={false} />);
    expect(screen.getByText(/only the owner can manage sharing/i)).toBeInTheDocument();
  });

  it("does not fetch data for non-owners", () => {
    render(<SharePanel {...defaultProps} isOwner={false} />);
    expect(mockFetch).not.toHaveBeenCalled();
  });

  it("shows 'not shared' when shares list is empty", async () => {
    mockFetch
      .mockReturnValueOnce(jsonResponse([]))
      .mockReturnValueOnce(jsonResponse([]));

    render(<SharePanel {...defaultProps} />);
    await waitFor(() => expect(screen.getByText(/not shared with anyone yet/i)).toBeInTheDocument());
  });

  it("renders existing shares with remove button", async () => {
    mockFetch
      .mockReturnValueOnce(jsonResponse([share1]))
      .mockReturnValueOnce(jsonResponse([]));

    render(<SharePanel {...defaultProps} />);
    await waitFor(() => expect(screen.getByText(/Smith Family/)).toBeInTheDocument());
    expect(screen.getByRole("button", { name: /remove/i })).toBeInTheDocument();
  });

  it("shows 'create a family first' when owner has no families", async () => {
    mockFetch
      .mockReturnValueOnce(jsonResponse([]))
      .mockReturnValueOnce(jsonResponse([]));

    render(<SharePanel {...defaultProps} />);
    await waitFor(() => expect(screen.getByText(/create a family first/i)).toBeInTheDocument());
  });

  it("shows family dropdown and Share button when families exist", async () => {
    mockFetch
      .mockReturnValueOnce(jsonResponse([]))
      .mockReturnValueOnce(jsonResponse([family1]));

    render(<SharePanel {...defaultProps} />);
    await waitFor(() => expect(screen.getByRole("button", { name: /share/i })).toBeInTheDocument());
    expect(screen.getByRole("option", { name: "Smith Family" })).toBeInTheDocument();
  });

  it("shows 'shared with all families' when all families already shared", async () => {
    mockFetch
      .mockReturnValueOnce(jsonResponse([share1]))
      .mockReturnValueOnce(jsonResponse([family1]));

    render(<SharePanel {...defaultProps} />);
    await waitFor(() => expect(screen.getByText(/shared with all your families/i)).toBeInTheDocument());
  });

  it("posting a share adds it to the list", async () => {
    const newShare = { ...share1, id: "share-2", sharedWithFamilyName: "Jones Family" };
    mockFetch
      .mockReturnValueOnce(jsonResponse([]))
      .mockReturnValueOnce(jsonResponse([family1]))
      .mockReturnValueOnce(jsonResponse(newShare, true, 200));

    render(<SharePanel {...defaultProps} />);
    await waitFor(() => screen.getByRole("button", { name: /share/i }));
    fireEvent.click(screen.getByRole("button", { name: /share/i }));

    await waitFor(() => expect(screen.getByText(/Jones Family/)).toBeInTheDocument());
  });

  it("shows conflict error on 409 response", async () => {
    mockFetch
      .mockReturnValueOnce(jsonResponse([]))
      .mockReturnValueOnce(jsonResponse([family1]))
      .mockReturnValueOnce(jsonResponse({}, false, 409));

    render(<SharePanel {...defaultProps} />);
    await waitFor(() => screen.getByRole("button", { name: /share/i }));
    fireEvent.click(screen.getByRole("button", { name: /share/i }));

    await waitFor(() => expect(screen.getByText(/already shared with this family/i)).toBeInTheDocument());
  });

  it("shows network error when share POST throws", async () => {
    mockFetch
      .mockReturnValueOnce(jsonResponse([]))
      .mockReturnValueOnce(jsonResponse([family1]))
      .mockRejectedValueOnce(new Error("Network fail"));

    render(<SharePanel {...defaultProps} />);
    await waitFor(() => screen.getByRole("button", { name: /share/i }));
    fireEvent.click(screen.getByRole("button", { name: /share/i }));

    await waitFor(() => expect(screen.getByText(/network error/i)).toBeInTheDocument());
  });

  it("remove button calls DELETE and removes share from UI", async () => {
    mockFetch
      .mockReturnValueOnce(jsonResponse([share1]))
      .mockReturnValueOnce(jsonResponse([]))
      .mockReturnValueOnce(Promise.resolve({ ok: true, status: 204 } as Response));

    render(<SharePanel {...defaultProps} />);
    await waitFor(() => screen.getByRole("button", { name: /remove/i }));
    fireEvent.click(screen.getByRole("button", { name: /remove/i }));

    await waitFor(() => expect(screen.queryByText(/Smith Family/)).not.toBeInTheDocument());
    expect(mockFetch).toHaveBeenCalledWith("/api/listshares/share-1", { method: "DELETE" });
  });
});
