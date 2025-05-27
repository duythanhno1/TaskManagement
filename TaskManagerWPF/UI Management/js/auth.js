document.addEventListener('DOMContentLoaded', () => {
    const loginForm = document.getElementById('loginForm');
    const registerForm = document.getElementById('registerForm');
    const loginError = document.getElementById('loginError');
    const registerError = document.getElementById('registerError');
    const logoutButton = document.getElementById('logoutButton'); // For dashboard page

    if (loginForm) {
        // If on login page and already logged in, redirect to dashboard
        if (getToken()) {
            window.location.href = 'dashboard.html';
            return;
        }

        loginForm.addEventListener('submit', async (event) => {
            event.preventDefault();
            loginError.textContent = ''; // Clear previous errors

            const email = document.getElementById('email').value;
            const password = document.getElementById('password').value;

            if (!email || !password) {
                loginError.textContent = 'Email and password are required.';
                return;
            }

            try {
                const response = await axios.post(`${API_BASE_URL}/auth/login`, {
                    email: email,
                    password: password
                });

                if (response.data && response.data.token) {
                    localStorage.setItem('authToken', response.data.token);
                    const decodedToken = decodeJwt(response.data.token);
                    if (decodedToken && decodedToken["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"]) {
                         localStorage.setItem('userId', decodedToken["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"]);
                    } else {
                        console.warn("Could not extract userId from token. Login might proceed but 'My Tasks' might not work.");
                    }
                    window.location.href = 'dashboard.html';
                } else {
                    loginError.textContent = response.data.message || 'Login failed. Please check your credentials.';
                }
            } catch (error) {
                console.error('Login API error:', error);
                if (error.response) {
                    loginError.textContent = error.response.data.message || 'Login failed. Server error.';
                } else if (error.request) {
                    loginError.textContent = 'Login failed. No response from server.';
                } else {
                    loginError.textContent = 'Login failed. An unexpected error occurred.';
                }
            }
        });
    }

    if (registerForm) {
        // If on register page and already logged in, redirect to dashboard
        if (getToken()) {
            window.location.href = 'dashboard.html';
            return;
        }

        registerForm.addEventListener('submit', async (event) => {
            event.preventDefault();
            registerError.textContent = ''; // Clear previous errors

            const fullName = document.getElementById('fullName').value;
            const email = document.getElementById('email').value;
            const password = document.getElementById('password').value;
            const confirmPassword = document.getElementById('confirmPassword').value;

            if (!fullName || !email || !password || !confirmPassword) {
                registerError.textContent = 'All fields are required.';
                return;
            }

            if (password !== confirmPassword) {
                registerError.textContent = 'Passwords do not match.';
                return;
            }

            try {
                const response = await axios.post(`${API_BASE_URL}/auth/register`, {
                    fullName: fullName,
                    email: email,
                    password: password,
                    confirmPassword: confirmPassword  // Thêm confirmPassword vào request
                });

                if (response.data && response.data.token) {  // Kiểm tra token thay vì success
                    localStorage.setItem('authToken', response.data.token);
                    const decodedToken = decodeJwt(response.data.token);
                    if (decodedToken && decodedToken["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"]) {
                         localStorage.setItem('userId', decodedToken["http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"]);
                    }
                    window.location.href = 'dashboard.html';  // Chuyển hướng đến dashboard sau khi đăng ký thành công
                } else {
                    registerError.textContent = response.data.message || 'Registration failed. Please try again.';
                }
            } catch (error) {
                console.error('Register API error:', error);
                if (error.response) {
                    registerError.textContent = error.response.data.message || 'Registration failed. Server error.';
                } else if (error.request) {
                    registerError.textContent = 'Registration failed. No response from server.';
                } else {
                    registerError.textContent = 'Registration failed. An unexpected error occurred.';
                }
            }
        });
    }

    if (logoutButton) {
        logoutButton.addEventListener('click', () => {
            localStorage.removeItem('authToken');
            localStorage.removeItem('userId');
            // Optionally, disconnect SignalR if it's managed globally
            if (window.signalRConnection && window.signalRConnection.state === signalR.HubConnectionState.Connected) {
                window.signalRConnection.stop().then(() => console.log("SignalR connection stopped on logout."));
            }
            window.location.href = 'login.html';
        });
    }
});