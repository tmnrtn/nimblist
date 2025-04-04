import { useState, useEffect, useRef } from 'react';
import * as signalR from '@microsoft/signalr';


// Define expected message payloads if needed (optional but good practice)

const useShoppingListHub = (listId: string | undefined) => {
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState<boolean>(false);
  // Use a ref to keep track of the listId the group was joined with, to handle leaving correctly
  const joinedListIdRef = useRef<string | null>(null);

  // Effect to create and dispose the connection object
  useEffect(() => {
    const hubUrl = `${import.meta.env.VITE_API_BASE_URL}/hubs/shoppinglist`;
    console.log(`Creating SignalR connection to: ${hubUrl}`);

    const newConnection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        // Cookies should be sent automatically if CORS is configured with AllowCredentials(true)
        // and the request is to the same site or configured correctly.
        // Explicit options like skipNegotiation or transport types can be added if needed.
      })
      .withAutomaticReconnect([0, 2000, 10000, 30000]) // Recommended: configure retry intervals (ms)
      .configureLogging(signalR.LogLevel.Warning) // Adjust log level (Trace, Debug, Information, Warning, Error, Critical, None)
      .build();

    setConnection(newConnection);

    // Cleanup when the hook unmounts (e.g., user navigates away from page using hook)
    return () => {
      console.log("Stopping SignalR connection (hook unmount)...");
      newConnection.stop()
          .then(() => console.log("SignalR connection stopped."))
          .catch(err => console.error("Error stopping SignalR connection:", err));
      setConnection(null);
      setIsConnected(false);
      joinedListIdRef.current = null;
    };
  }, []); // Empty dependency array: Create connection once when hook mounts

  // Effect to manage connection state (start/stop) and group membership
  useEffect(() => {
    // Only proceed if connection object exists and listId is valid
    if (connection && listId) {
      const startConnectionAndJoinGroup = async () => {
        try {
          // Start connection if not already connected/connecting
          if (connection.state === signalR.HubConnectionState.Disconnected) {
            console.log(`Starting SignalR connection for listId: ${listId}...`);
            await connection.start();
            console.log("SignalR Connected.");
            setIsConnected(true);
          }

          // If we successfully connected (or were already connected) and haven't joined this group yet
          if (connection.state === signalR.HubConnectionState.Connected && joinedListIdRef.current !== listId) {
             // If we were previously joined to a *different* group, leave it first
             if (joinedListIdRef.current) {
                 try {
                    console.log(`Leaving previous group for listId: ${joinedListIdRef.current}`);
                    await connection.invoke("LeaveListGroup", joinedListIdRef.current);
                 } catch (leaveErr) { console.error('SignalR LeaveGroup Error (before joining new):', leaveErr); }
             }

             // Join the new group
             console.log(`Invoking JoinListGroup for listId: ${listId}`);
             await connection.invoke("JoinListGroup", listId);
             joinedListIdRef.current = listId; // Track that we've joined this group
             console.log(`Successfully joined group for listId: ${listId}.`);
          }
        } catch (err) {
          console.error('SignalR failed to start or join group:', err);
          setIsConnected(false);
          joinedListIdRef.current = null; // Ensure we don't think we're joined
          // Consider retrying or notifying user
        }
      };

      startConnectionAndJoinGroup();

    } else if (connection && !listId && joinedListIdRef.current) {
      // Handle case where listId becomes invalid/undefined *after* joining a group
      // (e.g., navigating back but component doesn't unmount fully?)
      const listIdToLeave = joinedListIdRef.current;
      console.log(`listId removed, leaving group for listId: ${listIdToLeave}`);
      connection.invoke("LeaveListGroup", listIdToLeave)
          .then(() => console.log(`Successfully left group for listId: ${listIdToLeave} due to listId change.`))
          .catch(err => console.error('SignalR LeaveGroup Error (on listId change):', err))
          .finally(() => joinedListIdRef.current = null);
    }

    // Note: The connection stopping logic is in the *first* useEffect's cleanup.
    // This effect's cleanup only needs to handle leaving the *current* group
    // if the listId changes *before* the component unmounts. The main stop() handles disconnect.
    // However, having the leave logic here based on listId changing or component unmounting
    // (if connection is still live) provides belt-and-braces cleanup for the group.
    return () => {
        if (connection && joinedListIdRef.current && connection.state === signalR.HubConnectionState.Connected) {
             const listIdToLeave = joinedListIdRef.current;
             console.log(`Leaving group for listId: ${listIdToLeave} (effect cleanup)...`);
             // Fire and forget in cleanup is usually okay
             connection.invoke("LeaveListGroup", listIdToLeave)
                 .catch(err => console.error('SignalR LeaveGroup Error (in effect cleanup):', err))
                 .finally(() => {
                      // Only clear ref if leaving the *current* one during cleanup.
                      // If listId changed, ref might already be cleared or different.
                      if (joinedListIdRef.current === listIdToLeave) {
                           joinedListIdRef.current = null;
                      }
                  });
        }
    };

  }, [connection, listId]); // Re-run when connection object or listId changes

  // Return connection status and the connection object itself
  // The component using the hook will attach/detach message handlers (.on/.off)
  return { connection, isConnected };
};

export default useShoppingListHub;