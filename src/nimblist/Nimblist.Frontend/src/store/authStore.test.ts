import { describe, it, expect, vi, beforeEach } from "vitest";
import useAuthStore from "./authStore";

// src/store/authStore.test.ts

describe("authStore", () => {
  beforeEach(() => {
    // Reset the store state before each test
    useAuthStore.getState();
    useAuthStore.setState({
      isAuthenticated: false,
      user: null,
      isLoading: true,
    });
    vi.restoreAllMocks();
  });

  it("should have the correct initial state", () => {
    const { isAuthenticated, user, isLoading } = useAuthStore.getState();
    expect(isAuthenticated).toBe(false);
    expect(user).toBeNull();
    expect(isLoading).toBe(true);
  });

  describe("checkAuthStatus", () => {
    it("should set isAuthenticated and user on successful authentication", async () => {
      const mockUser = { userId: "123", email: "test@example.com" };
      globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: true,
        json: async () => mockUser,
      });

      await useAuthStore.getState().checkAuthStatus();

      const { isAuthenticated, user, isLoading } = useAuthStore.getState();
      expect(isAuthenticated).toBe(true);
      expect(user).toEqual(mockUser);
      expect(isLoading).toBe(false);
    });

    it("should set isAuthenticated to false on unauthorized response", async () => {
        globalThis.fetch = vi.fn().mockResolvedValueOnce({
        ok: false,
        status: 401,
      });

      await useAuthStore.getState().checkAuthStatus();

      const { isAuthenticated, user, isLoading } = useAuthStore.getState();
      expect(isAuthenticated).toBe(false);
      expect(user).toBeNull();
      expect(isLoading).toBe(false);
    });

    it("should handle network errors gracefully", async () => {
        globalThis.fetch = vi.fn().mockRejectedValueOnce(new Error("Network Error"));

      await useAuthStore.getState().checkAuthStatus();

      const { isAuthenticated, user, isLoading } = useAuthStore.getState();
      expect(isAuthenticated).toBe(false);
      expect(user).toBeNull();
      expect(isLoading).toBe(false);
    });
  });

  describe("logout", () => {
    it("should reset state on successful logout", async () => {
        globalThis.fetch = vi.fn().mockResolvedValueOnce({ ok: true });

      await useAuthStore.getState().logout();

      const { isAuthenticated, user, isLoading } = useAuthStore.getState();
      expect(isAuthenticated).toBe(false);
      expect(user).toBeNull();
      expect(isLoading).toBe(false);
    });

    it("should handle network errors during logout", async () => {
        globalThis.fetch = vi.fn().mockRejectedValueOnce(new Error("Network Error"));

      await useAuthStore.getState().logout();

      const { isAuthenticated, user, isLoading } = useAuthStore.getState();
      expect(isAuthenticated).toBe(false);
      expect(user).toBeNull();
      expect(isLoading).toBe(false);
    });
  });
});
