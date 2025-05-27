// Base URL for your API.
const API_BASE_URL = 'https://localhost:7182/api/v1'; // Adjust to your actual API URL (HTTPS)
// const API_BASE_URL = 'http://localhost:5124/api/v1'; // Adjust to your actual API URL (HTTP)

// SignalR Hub URL
const SIGNALR_HUB_URL = 'https://localhost:7182/taskhub'; // Adjust to your actual SignalR Hub URL (HTTPS)
// const SIGNALR_HUB_URL = 'http://localhost:5124/taskhub'; // Adjust to your actual SignalR Hub URL (HTTP)

// Function to get the JWT token from localStorage
function getToken() {
    return localStorage.getItem('authToken');
}

// Function to get the User ID from localStorage
function getUserId() {
    return localStorage.getItem('userId');
}

// Axios instance with default settings
const apiClient = axios.create({
    baseURL: API_BASE_URL,
    timeout: 15000, // 15 seconds timeout
    headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json'
    }
});

// Interceptor to add JWT token to requests
apiClient.interceptors.request.use(
    config => {
        const token = getToken();
        if (token) {
            config.headers['Authorization'] = `Bearer ${token}`;
        }
        console.log(`API Request: ${config.method.toUpperCase()} ${config.url}`, config.data || '');
        return config;
    },
    error => {
        console.error('Request error:', error);
        return Promise.reject(error);
    }
);

// Interceptor to handle responses and errors
apiClient.interceptors.response.use(
    response => {
        console.log(`API Response: ${response.config.method.toUpperCase()} ${response.config.url}`, response.status, response.data);
        return response;
    },
    error => {
        console.error('API Response Error:', error.config?.method?.toUpperCase(), error.config?.url, error.response?.status, error.response?.data || error.message);
        if (error.response) {
            if (error.response.status === 401) {
                console.error("Unauthorized access - 401. Redirecting to login.");
                localStorage.removeItem('authToken');
                localStorage.removeItem('userId');
                // Avoid redirect loops if already on login page
                if (!window.location.pathname.endsWith('login.html') && !window.location.pathname.endsWith('index.html')) {
                    window.location.href = 'login.html';
                }
            }
        } else if (error.request) {
            console.error('No response received:', error.request);
        } else {
            console.error('Request setup error:', error.message);
        }
        return Promise.reject(error);
    }
);

// Function to decode JWT token (basic, without signature verification)
function decodeJwt(token) {
    try {
        const base64Url = token.split('.')[1];
        const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
        const jsonPayload = decodeURIComponent(atob(base64).split('').map(function(c) {
            return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
        }).join(''));
        return JSON.parse(jsonPayload);
    } catch (e) {
        console.error("Error decoding JWT: ", e);
        return null;
    }
}

// Helper to normalize status strings consistently
function normalizeStatusString(statusString) {
    if (!statusString || typeof statusString !== 'string') return 'Todo'; // Default
    const lower = statusString.toLowerCase().replace(/\s+/g, ''); // Remove spaces
    if (lower === 'todo') return 'Todo';
    if (lower === 'inprogress') return 'InProgress';
    if (lower === 'completed') return 'Completed';
    console.warn(`Unknown status string: "${statusString}", defaulting to "Todo".`);
    return 'Todo'; // Safe default
}