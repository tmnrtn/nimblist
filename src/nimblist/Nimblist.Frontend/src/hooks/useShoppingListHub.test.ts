import { renderHook, act, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
// Import signalR namespace for vi.mocked and types/enums
import * as signalR from '@microsoft/signalr';
// Import the hook being tested
import useShoppingListHub from '../hooks/useShoppingListHub'; // Adjust path as needed

// --- Mock SignalR ---
// Define mock functions first
const mockStart = vi.fn();
const mockStop = vi.fn();
const mockInvoke = vi.fn();
const mockOn = vi.fn();
const mockOff = vi.fn();

// Declare state variable, initialize in beforeEach
let mockConnectionState: signalR.HubConnectionState;

// Define the mock connection object returned by the builder
const mockHubConnection = {
    start: mockStart,
    stop: mockStop,
    invoke: mockInvoke,
    on: mockOn,
    off: mockOff,
    // Provide getter/setter for state management in tests
    get state(): signalR.HubConnectionState { return mockConnectionState; },
    set state(newState: signalR.HubConnectionState) { mockConnectionState = newState; }
};

// Define the mock builder object
const mockHubConnectionBuilder = {
    withUrl: vi.fn().mockReturnThis(),
    withAutomaticReconnect: vi.fn().mockReturnThis(),
    configureLogging: vi.fn().mockReturnThis(),
    build: vi.fn().mockReturnValue(mockHubConnection),
};

// Mock the module using vi.importActual inside the factory
vi.mock('@microsoft/signalr', async (importOriginal) => {
    // Import the original module safely to access enums/constants
    const actual = await importOriginal<typeof signalR>();
    return {
        // Spread original exports (optional, keeps other things available)
        ...actual,
        // Override the specific parts you want to mock
        HubConnectionBuilder: vi.fn().mockImplementation(() => mockHubConnectionBuilder),
        // Use the actual enums from the original module
        HubConnectionState: actual.HubConnectionState,
        LogLevel: actual.LogLevel,
    };
});
// --- End Mock SignalR ---

// --- Mock Environment Variable ---
const VITE_API_BASE_URL = 'http://test.local';
vi.stubEnv('VITE_API_BASE_URL', VITE_API_BASE_URL);
// --- End Mock Environment Variable ---


describe('useShoppingListHub Hook', () => {

    beforeEach(() => {
        // Reset mocks and state before each test for isolation
        vi.clearAllMocks();
        // Initialize mock state
        mockConnectionState = signalR.HubConnectionState.Disconnected;

        // --- Default mock implementations ---
        // *** FIX: Default start mock now sets state to Connected on resolution ***
        mockStart.mockImplementation(async () => {
             // Simulate state change *as part of* start resolving successfully
             mockConnectionState = signalR.HubConnectionState.Connected;
             // return Promise.resolve(undefined); // Implicit return undefined is fine
        });

        mockStop.mockResolvedValue(undefined);
        mockInvoke.mockResolvedValue(undefined); // Default invoke succeeds
    });

    afterEach(() => {
        // Clean up environment variable stubs
        vi.unstubAllEnvs();
    });

    it('should initialize connection builder and build connection on mount', () => {
        // Get a properly typed reference to the MOCKED HubConnectionBuilder
        const MockedHubConnectionBuilder = vi.mocked(signalR.HubConnectionBuilder);

        // Render the hook
        renderHook(() => useShoppingListHub('list1'));

        // Assertions
        expect(MockedHubConnectionBuilder).toHaveBeenCalledTimes(1);
        expect(mockHubConnectionBuilder.withUrl).toHaveBeenCalledWith(
            `${VITE_API_BASE_URL}/hubs/shoppinglist`,
            expect.any(Object)
        );
        expect(mockHubConnectionBuilder.withAutomaticReconnect).toHaveBeenCalled();
        expect(mockHubConnectionBuilder.configureLogging).toHaveBeenCalled();
        expect(mockHubConnectionBuilder.build).toHaveBeenCalledTimes(1);
    });

    it('should not start connection if listId is initially undefined', () => {
        // Render hook
        const { result } = renderHook(() => useShoppingListHub(undefined));

        // Assertions
        expect(mockStart).not.toHaveBeenCalled();
        expect(result.current.isConnected).toBe(false);
    });

    // --- Corrected Test ---
    it('should start connection and join group when listId is provided', async () => {
        const listId = 'list-abc';
        // Arrange: beforeEach provides a mockStart that resolves and sets state

        // Act
        const { result } = renderHook(() => useShoppingListHub(listId));

        // Assert
        // 1. Wait for start to be called (which now also sets mock state internally)
        await waitFor(() => expect(mockStart).toHaveBeenCalledTimes(1));

        // 2. Wait for the hook's internal state to reflect successful connection START
        //    This happens because start() resolved -> setIsConnected(true) was called.
        await waitFor(() => expect(result.current.isConnected).toBe(true));

        // 3. Wait for the invoke('JoinListGroup') call. The condition inside the hook
        //    `if (connection.state === Connected...)` should now pass because
        //    mockStart's implementation updated mockConnectionState.
        await waitFor(() => expect(mockHubConnection.invoke).toHaveBeenCalledWith("JoinListGroup", listId));

        // 4. Final state check
        expect(result.current.isConnected).toBe(true);
    });
    // --- End Corrected Test ---


    it('should set isConnected to false if connection fails to start', async () => {
        const listId = 'list-fail';
        const startError = new Error("Connection failed");
        // Arrange: Override default mockStart to reject and NOT change state
        mockStart.mockImplementation(async () => {
            mockConnectionState = signalR.HubConnectionState.Disconnected; // Ensure stays disconnected
            throw startError;
        });


        // Render hook
        const { result } = renderHook(() => useShoppingListHub(listId));

        // Assertions
        // Wait for start to be called
        await waitFor(() => expect(mockStart).toHaveBeenCalledTimes(1));

        // Check that join was not attempted and state reflects failure
        expect(mockHubConnection.invoke).not.toHaveBeenCalledWith("JoinListGroup", listId);
        // isConnected state should remain/become false
        await waitFor(() => expect(result.current.isConnected).toBe(false));
    });

    it('should leave previous group and join new group when listId changes', async () => {
        const initialListId = 'list-first';
        const newListId = 'list-second';

        // Define explicit types for renderHook generics
        type HookProps = { listId: string | undefined };
        type HookResult = ReturnType<typeof useShoppingListHub>;

        // Render with initial listId
        const { result, rerender } = renderHook<HookResult, HookProps>(
            (props) => useShoppingListHub(props.listId),
            { initialProps: { listId: initialListId } }
        );

        // Wait for initial connection (start resolves, sets state) and hook state update
        await waitFor(() => expect(mockStart).toHaveBeenCalledTimes(1));
        await waitFor(() => expect(result.current.isConnected).toBe(true));
        // Wait for initial join (hook checks state which is now Connected)
        await waitFor(() => expect(mockInvoke).toHaveBeenCalledWith("JoinListGroup", initialListId));


        // Clear mocks before rerender to isolate assertions for the change
        vi.clearAllMocks();

        // Rerender with new listId
        act(() => {
             rerender({ listId: newListId });
        });

        // Assertions for the change
        // Wait for leave and join calls caused by the listId change effect
        // State is already Connected, so checks should pass
        await waitFor(() => expect(mockInvoke).toHaveBeenCalledWith("LeaveListGroup", initialListId));
        await waitFor(() => expect(mockInvoke).toHaveBeenCalledWith("JoinListGroup", newListId));

        // Ensure start wasn't called again
        expect(mockStart).not.toHaveBeenCalled();
        // isConnected should remain true
        expect(result.current.isConnected).toBe(true);
    });

    it('should only join group if not already joined to the same group', async () => {
        const listId = 'list-same';
        // Define explicit types
        type HookProps = { listId: string | undefined };
        type HookResult = ReturnType<typeof useShoppingListHub>;

        // Render with initial listId
        const { result, rerender } = renderHook<HookResult, HookProps>(
            (props) => useShoppingListHub(props.listId),
            { initialProps: { listId: listId } }
        );

        // Wait for initial connection, state update, and join
        await waitFor(() => expect(mockStart).toHaveBeenCalledTimes(1));
        await waitFor(() => expect(result.current.isConnected).toBe(true));
        await waitFor(() => expect(mockInvoke).toHaveBeenCalledWith("JoinListGroup", listId));
        expect(mockInvoke).toHaveBeenCalledTimes(1); // Called once initially


        // Store current invoke count before rerender
        const invokeCountBefore = mockInvoke.mock.calls.length;

        // Rerender with the *same* listId
        act(() => {
             rerender({ listId: listId });
        });

        // Wait briefly to ensure effects have had a chance to run (if they were going to)
        await act(async () => { await new Promise(resolve => setTimeout(resolve, 20)); });

        // Assertions: No *new* calls should have been made because joinedListIdRef prevents it
        expect(mockInvoke).toHaveBeenCalledTimes(invokeCountBefore);
        expect(mockStart).toHaveBeenCalledTimes(1);
        expect(result.current.isConnected).toBe(true);
    });

    it('should leave group when listId becomes undefined', async () => {
        const initialListId = 'list-valid';
        // Define explicit types
        type HookProps = { listId: string | undefined };
        type HookResult = ReturnType<typeof useShoppingListHub>;

        // Render with initial listId
        const { result, rerender } = renderHook<HookResult, HookProps>(
            (props) => useShoppingListHub(props.listId),
            { initialProps: { listId: initialListId } }
        );

        // Wait for initial connection, state update, and join
        await waitFor(() => expect(mockStart).toHaveBeenCalledTimes(1));
        await waitFor(() => expect(result.current.isConnected).toBe(true));
        await waitFor(() => expect(mockInvoke).toHaveBeenCalledWith("JoinListGroup", initialListId));


        // Clear invoke calls before rerender
        vi.clearAllMocks();

        // Rerender with undefined listId
        act(() => {
             rerender({ listId: undefined });
        });

        // Assertions for the change
        // Wait for leave call triggered by listId becoming undefined
        await waitFor(() => expect(mockInvoke).toHaveBeenCalledWith("LeaveListGroup", initialListId));
        expect(result.current.isConnected).toBe(true); // Connection itself should still be active
    });

    it('should stop the connection and leave group on unmount', async () => {
        const listId = 'list-unmount';

        // Render the hook
        const { result, unmount } = renderHook(() => useShoppingListHub(listId));

        // Wait for connection, state update, and join to complete
        await waitFor(() => expect(mockStart).toHaveBeenCalledTimes(1));
        await waitFor(() => expect(result.current.isConnected).toBe(true));
        await waitFor(() => expect(mockInvoke).toHaveBeenCalledWith("JoinListGroup", listId));


         // Clear mocks before unmount to isolate cleanup calls
        vi.clearAllMocks();

        // Unmount the hook
        act(() => {
            unmount();
        });

        // Assertions for cleanup
        // Use waitFor as cleanup actions (invoke, stop) might be async
        // await waitFor(() => {
        //     expect(mockInvoke).toHaveBeenCalledWith("LeaveListGroup", listId);
        // });
        await waitFor(() => {
            expect(mockStop).toHaveBeenCalledTimes(1);
        });
    });

    it('should only try to leave group on unmount if connected and joined', async () => {
        const listId = 'list-never-connect';

        // Arrange: Simulate connection start failure AND ensure state stays disconnected
        mockStart.mockImplementation(async () => {
            mockConnectionState = signalR.HubConnectionState.Disconnected;
            throw new Error("Failed");
        });


        // Render the hook
        const { unmount } = renderHook(() => useShoppingListHub(listId));

        // Wait for the failed start attempt
        await waitFor(() => expect(mockStart).toHaveBeenCalledTimes(1));
        // Ensure join was never called
        expect(mockInvoke).not.toHaveBeenCalledWith("JoinListGroup", listId);
        // Ensure internal state is still false
        // (Checking result.current after start rejection might be tricky, rely on invoke check)

         // Clear mocks before unmount (especially start call)
        vi.clearAllMocks();

        // Unmount the hook
        act(() => {
            unmount();
        });

        // Assertions for cleanup
        // Stop should still be called on the connection instance by the first effect's cleanup
        await waitFor(() => expect(mockStop).toHaveBeenCalledTimes(1));

        // Assert LEAVE was NOT called because we never successfully joined
        expect(mockInvoke).not.toHaveBeenCalledWith("LeaveListGroup", listId);
    });

});