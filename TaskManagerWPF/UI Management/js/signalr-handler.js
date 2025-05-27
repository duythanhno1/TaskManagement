// Make signalRConnection globally accessible for auth.js to stop it if needed
window.signalRConnection = null;
let isSignalRConnecting = false;
const MAX_RECONNECT_ATTEMPTS = 5;
let reconnectAttempts = 0;

async function initializeSignalR() {
    const token = getToken();
    if (!token) {
        console.warn("No token found, SignalR connection not started.");
        return;
    }

    if (window.signalRConnection && window.signalRConnection.state !== signalR.HubConnectionState.Disconnected) {
        console.log("SignalR connection already exists or is connecting/connected.");
        return;
    }

    window.signalRConnection = new signalR.HubConnectionBuilder()
        .withUrl(SIGNALR_HUB_URL, {
            accessTokenFactory: () => token,
            // Recommended to handle redirects and errors during negotiation
            transport: signalR.HttpTransportType.WebSockets, // Try WebSockets first
            skipNegotiation: false // Usually false unless specific server config
        })
        .withAutomaticReconnect({ // Configure automatic reconnections
            nextRetryDelayInMilliseconds: retryContext => {
                if (retryContext.previousRetryCount < 5) { // Limit retries
                    return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000); // Exponential backoff
                }
                return null; // Stop retrying after 5 attempts
            }
        })
        .configureLogging(signalR.LogLevel.Information)
        .build();

    setupSignalREventHandlers();
    await startSignalRConnection();
}

function setupSignalREventHandlers() {
    if (!window.signalRConnection) return;

    // Handler for task creation and updates
    window.signalRConnection.on("ReceiveTaskUpdate", (taskId, taskName, description, assignedToUserId, status, assignedToUserNameFromServer) => {
        console.log("SignalR: ReceiveTaskUpdate received", { taskId, taskName, status, assignedToUserNameFromServer });
        
        // Ensure dashboard functions are available
        if (typeof window.handleTaskUpdateFromSignalR === 'function') {
            const taskData = {
                taskId: taskId,
                taskName: taskName,
                description: description,
                assignedToUserId: assignedToUserId,
                assignedToUserName: assignedToUserNameFromServer || (assignedToUserId ? "Loading..." : "Unassigned"),
                status: normalizeStatusString(status), // Ensure status is normalized
                // createdAt might need to be fetched or preserved if not sent by server for updates
            };
            window.handleTaskUpdateFromSignalR(taskData);
        }
        if (typeof window.loadMyTasks === 'function') {
            window.loadMyTasks(); // Refresh "My Tasks" list
        }
    });

    // Handler for task deletion
    window.signalRConnection.on("ReceiveTaskDelete", (taskId) => {
        console.log("SignalR: ReceiveTaskDelete received for task ID:", taskId);
        if (typeof window.handleTaskDeleteFromSignalR === 'function') {
            window.handleTaskDeleteFromSignalR(taskId);
        }
        if (typeof window.loadMyTasks === 'function') {
            window.loadMyTasks(); // Refresh "My Tasks" list
        }
    });

    // Handler for assignment notifications
    window.signalRConnection.on("ReceiveTaskAssignmentNotification", (message) => {
        console.log("SignalR: ReceiveTaskAssignmentNotification", message);
        alert(`Notification: ${message}`); // Simple alert, can be improved
        if (typeof window.loadMyTasks === 'function') {
            window.loadMyTasks(); // Refresh "My Tasks" list as assignment affects it
        }
    });

    window.signalRConnection.onclose(async (error) => {
        console.warn("SignalR connection closed.", error);
        isSignalRConnecting = false;
        // Automatic reconnect handles this, but you can add custom logic if needed
    });

    window.signalRConnection.onreconnecting((error) => {
        console.warn("SignalR attempting to reconnect...", error);
        isSignalRConnecting = true;
    });

    window.signalRConnection.onreconnected((connectionId) => {
        console.log("SignalR reconnected successfully. Connection ID:", connectionId);
        isSignalRConnecting = false;
        reconnectAttempts = 0; // Reset attempts on successful reconnect
    });
}

async function startSignalRConnection() {
    if (!window.signalRConnection || isSignalRConnecting || window.signalRConnection.state === signalR.HubConnectionState.Connected) {
        console.log("SignalR connection attempt skipped (already connected, connecting, or no instance). State: " + (window.signalRConnection?.state || "N/A"));
        return;
    }

    isSignalRConnecting = true;
    console.log("Attempting to start SignalR connection...");
    try {
        await window.signalRConnection.start();
        console.log("SignalR Connected successfully. Connection ID:", window.signalRConnection.connectionId);
        isSignalRConnecting = false;
        reconnectAttempts = 0;
    } catch (err) {
        console.error("SignalR Connection Error: ", err);
        isSignalRConnecting = false;
        reconnectAttempts++;
        // Automatic reconnect handles retries, this manual retry might be redundant if withAutomaticReconnect is robust
        // Consider removing manual setTimeout if automatic reconnect is sufficient.
        // if (reconnectAttempts < MAX_RECONNECT_ATTEMPTS) {
        //     console.log(`Retrying SignalR connection in 5 seconds (attempt ${reconnectAttempts})...`);
        //     setTimeout(startSignalRConnection, 5000);
        // } else {
        //     console.error("Max SignalR reconnect attempts reached.");
        // }
    }
}