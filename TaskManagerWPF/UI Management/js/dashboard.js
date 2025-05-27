/**
 * Task Management Dashboard Script
 * Author: Grok
 * Version: 14.0 - Full Rewrite, Optimized, Synchronized, Error-Free
 * Date: May 24, 2025
 */

(function () {
    'use strict';

    const AppState = {
        usersCache: [],
        localUserMap: new Map(),
        pendingUserFetches: new Map(),
        tasks: new Map(),
        domElements: {
            columns: { Todo: null, InProgress: null, Completed: null },
            myTasksList: null,
            myTodoCountBadge: null,
            modal: null,
            modalTitle: null,
            taskForm: null,
            taskIdInput: null,
            taskNameInput: null,
            taskDescriptionInput: null,
            taskAssigneeSelect: null,
            taskStatusSelect: null,
            closeBtn: null,
            deleteTaskBtn: null
        },
        draggedTask: null,
        lastSignalRUpdate: 0,
        signalRDebounceDelay: 500
    };

    document.addEventListener('DOMContentLoaded', async () => {
        if (!window.location.pathname.endsWith('dashboard.html')) return;
        if (!getToken()) {
            window.location.href = 'login.html';
            return;
        }

        window.handleTaskUpdateFromSignalR = handleTaskUpdateFromSignalR;
        window.handleTaskDeleteFromSignalR = handleTaskDeleteFromSignalR;
        window.handleUserUpdateFromSignalR = handleUserUpdateFromSignalR;
        window.loadMyTasks = loadMyTasks;

        await initializeApp();
    });

    async function initializeApp() {
        console.log('Initializing Dashboard (v14.0)...');
        initializeDOMElements();
        setupEventListeners();
        await loadUsers();
        await loadAllTasks();
        await loadMyTasks();
        setupDragAndDrop();
        if (typeof initializeSignalR === 'function') {
            await initializeSignalR();
        } else {
            console.error('SignalR initialization failed. Real-time updates disabled.');
        }
    }

    function initializeDOMElements() {
        const D = AppState.domElements;
        D.columns.Todo = document.getElementById('toDoTasks');
        D.columns.InProgress = document.getElementById('inProgressTasks');
        D.columns.Completed = document.getElementById('completedTasks');
        D.myTasksList = document.getElementById('myAssignedTasks');
        D.myTodoCountBadge = document.getElementById('myTodoCount');
        D.modal = document.getElementById('taskModal');
        D.modalTitle = document.getElementById('modalTitle');
        D.taskForm = document.getElementById('taskForm');
        D.taskIdInput = document.getElementById('taskId');
        D.taskNameInput = document.getElementById('taskName');
        D.taskDescriptionInput = document.getElementById('taskDescription');
        D.taskAssigneeSelect = document.getElementById('taskAssignee');
        D.taskStatusSelect = document.getElementById('taskStatus');
        D.closeBtn = D.modal?.querySelector('.close-btn');
        D.deleteTaskBtn = document.getElementById('deleteTaskButton');

        if (!D.taskIdInput || !D.deleteTaskBtn || !D.taskForm || !D.modal) {
            console.error('Critical DOM elements missing. App may not function correctly.');
        }
    }

    function setupEventListeners() {
        const D = AppState.domElements;
        if (D.closeBtn) D.closeBtn.addEventListener('click', closeModal);
        if (D.taskForm) D.taskForm.addEventListener('submit', handleTaskFormSubmit);
        if (D.deleteTaskBtn) D.deleteTaskBtn.addEventListener('click', handleDeleteTask);
        window.addEventListener('click', (e) => { if (D.modal && e.target === D.modal) closeModal(); });
        document.querySelectorAll('.add-task-btn').forEach(btn => {
            btn.addEventListener('click', () => openTaskModal(null, btn.dataset.statusDefault || 'Todo'));
        });
    }

    async function loadUsers(retries = 5, delay = 2000) {
        for (let attempt = 1; attempt <= retries; attempt++) {
            try {
                const response = await apiClient.get('/tasks/users');
                if (response.data?.data && Array.isArray(response.data.data)) {
                    AppState.usersCache = response.data.data;
                    AppState.localUserMap.clear();
                    AppState.usersCache.forEach(user => {
                        if (user.userId && user.fullName) {
                            AppState.localUserMap.set(parseInt(user.userId), user.fullName);
                        }
                    });
                    updateUserDropdown();
                    return;
                }
                console.warn(`Attempt ${attempt}: Invalid user data response.`);
            } catch (error) {
                console.warn(`Attempt ${attempt}: Failed to load users:`, error);
            }
            if (attempt < retries) await new Promise(resolve => setTimeout(resolve, delay));
        }
        console.error('Failed to load users after retries. Proceeding with fallback.');
    }

    async function fetchUser(userId) {
        if (!userId) return null;
        const id = parseInt(userId);
        if (AppState.localUserMap.has(id)) return AppState.localUserMap.get(id);

        if (AppState.pendingUserFetches.has(id)) {
            return await AppState.pendingUserFetches.get(id);
        }

        const fetchPromise = (async () => {
            try {
                const response = await apiClient.get(`/tasks/users/${id}`);
                if (response.data?.data?.userId && response.data.data.fullName) {
                    const user = response.data.data;
                    AppState.localUserMap.set(id, user.fullName);
                    AppState.usersCache.push(user);
                    if (typeof broadcastUserUpdate === 'function') {
                        await broadcastUserUpdate(user);
                    }
                    return user.fullName;
                }
                return `User #${id}`;
            } catch (error) {
                console.error(`Failed to fetch user ${id}:`, error);
                return `User #${id}`;
            } finally {
                AppState.pendingUserFetches.delete(id);
            }
        })();

        AppState.pendingUserFetches.set(id, fetchPromise);
        return await fetchPromise;
    }

    function updateUserDropdown() {
        const select = AppState.domElements.taskAssigneeSelect;
        if (!select) return;
        select.innerHTML = '<option value="">Select User (Unassigned)</option>';
        AppState.usersCache.forEach(user => {
            const option = document.createElement('option');
            option.value = user.userId;
            option.textContent = `${user.fullName} (${user.email})`;
            select.appendChild(option);
        });
    }

    async function loadAllTasks() {
        const D = AppState.domElements;
        if (!D.columns.Todo || !D.columns.InProgress || !D.columns.Completed) return;
        try {
            const response = await apiClient.get('/tasks');
            if (!response.data?.data || !Array.isArray(response.data.data)) return;
            Object.values(D.columns).forEach(col => col.innerHTML = '');
            AppState.tasks.clear();
            for (const task of response.data.data) {
                if (task.assignedToUserId) {
                    task.assignedToUserName = await fetchUser(task.assignedToUserId);
                }
                AppState.tasks.set(parseInt(task.taskId), task);
                renderTask(task, "InitialLoad");
            }
        } catch (error) {
            console.error('Failed to load tasks:', error);
        }
    }

    async function loadMyTasks() {
        const D = AppState.domElements;
        if (!D.myTasksList || !D.myTodoCountBadge) return;
        const userId = parseInt(getUserId());
        if (isNaN(userId)) return;
        try {
            const response = await apiClient.get('/tasks/my-tasks');
            if (!response.data?.data || !Array.isArray(response.data.data)) return;
            D.myTasksList.innerHTML = '';
            let pendingCount = 0;
            response.data.data.forEach(task => {
                const li = document.createElement('li');
                const span = document.createElement('span');
                span.textContent = task.taskName;
                li.appendChild(span);
                li.title = `${task.taskName} - Status: ${task.status}`;
                const status = normalizeStatusString(task.status);
                if (status === 'Completed') {
                    const icon = document.createElement('i');
                    icon.className = 'fas fa-check-circle';
                    icon.style.color = '#4CAF50';
                    icon.style.marginLeft = '8px';
                    li.appendChild(icon);
                } else if (['Todo', 'InProgress'].includes(status)) {
                    pendingCount++;
                }
                D.myTasksList.appendChild(li);
            });
            D.myTodoCountBadge.textContent = pendingCount;
        } catch (error) {
            console.error('Failed to load my tasks:', error);
        }
    }

    function normalizeUserName(userId, userName) {
        if (!userId) return "Unassigned";
        const id = parseInt(userId);
        if (userName?.trim()) {
            AppState.localUserMap.set(id, userName);
            return userName;
        }
        return AppState.localUserMap.get(id) || `User #${id}`;
    }

    function formatDate(dateString) {
        if (!dateString || dateString.startsWith("0001-01-01")) {
            return new Date().toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
        }
        try {
            const date = new Date(dateString);
            return isNaN(date.getTime())
                ? new Date().toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
                : date.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
        } catch {
            return new Date().toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
        }
    }

    function renderTask(task, source = "Unknown") {
        if (!task?.taskId) return;
        const taskId = parseInt(task.taskId);
        if (isNaN(taskId)) return;

        console.log(`RenderTask (v14.0 - Source: ${source}) - Task ID ${taskId}`);

        const status = normalizeStatusString(task.status);
        const column = AppState.domElements.columns[status];
        if (!column) return;

        let taskCard = document.querySelector(`.task-card[data-task-id="${taskId}"]`);
        if (taskCard) {
            if (taskCard.parentElement !== column) {
                column.appendChild(taskCard);
            }
        } else {
            taskCard = document.createElement('div');
            taskCard.className = 'task-card';
            taskCard.setAttribute('draggable', true);
            taskCard.dataset.taskId = taskId;
            column.appendChild(taskCard);
        }

        AppState.tasks.set(taskId, task);
        taskCard._taskData = task;
        taskCard.removeEventListener('click', onTaskCardClick);
        taskCard.addEventListener('click', onTaskCardClick);

        taskCard.dataset.status = status;
        taskCard.dataset.assignedTo = task.assignedToUserId || '';

        const userName = normalizeUserName(task.assignedToUserId, task.assignedToUserName);
        taskCard.dataset.assignedToName = userName;

        if (task.assignedToUserId && !AppState.localUserMap.has(parseInt(task.assignedToUserId))) {
            fetchUser(task.assignedToUserId).then(name => {
                task.assignedToUserName = name;
                AppState.tasks.set(taskId, task);
                renderTask(task, "UserFetchUpdate");
            });
        }

        taskCard.innerHTML = `
            <h4>${task.taskName || 'Untitled Task'}</h4>
            <p>${task.description || 'No description.'}</p>
            <div class="task-meta">
                <span class="task-assignee" title="Assigned to: ${userName}">
                    <i class="fas fa-user"></i> ${userName.split(' ')[0]}
                </span>
                <span class="task-date">Created: ${formatDate(task.createdAt)}</span>
            </div>
        `;
    }

    function onTaskCardClick() {
        const taskId = parseInt(this.dataset.taskId);
        const taskData = this._taskData;
        if (!taskData || taskData.taskId !== taskId) {
            if (isNaN(taskId)) return;
            apiClient.get(`/tasks/${taskId}`)
                .then(res => openTaskModal(res.data?.data))
                .catch(err => console.error(`Error fetching task ${taskId}:`, err));
            return;
        }
        openTaskModal(taskData);
    }

    function openTaskModal(task = null, defaultStatus = 'Todo') {
        const D = AppState.domElements;
        if (!D.modal || !D.taskForm || !D.modalTitle || !D.deleteTaskBtn || !D.taskIdInput) return;

        D.taskForm.reset();
        updateUserDropdown();

        if (task?.taskId) {
            D.modalTitle.textContent = 'Edit Task';
            D.taskIdInput.value = task.taskId;
            D.taskNameInput.value = task.taskName || '';
            D.taskDescriptionInput.value = task.description || '';
            D.taskAssigneeSelect.value = task.assignedToUserId || '';
            D.taskStatusSelect.value = normalizeStatusString(task.status);
            D.deleteTaskBtn.style.display = 'inline-block';
        } else {
            D.modalTitle.textContent = 'Add New Task';
            D.taskIdInput.value = '';
            D.deleteTaskBtn.style.display = 'none';
            D.taskStatusSelect.value = normalizeStatusString(defaultStatus);
        }
        D.modal.style.display = 'block';
    }

    function closeModal() {
        const D = AppState.domElements;
        if (!D.modal || !D.taskForm || !D.taskIdInput) return;
        D.modal.style.display = 'none';
        D.taskForm.reset();
        D.taskIdInput.value = '';
    }

    async function handleTaskFormSubmit(event) {
        event.preventDefault();
        const D = AppState.domElements;
        if (!D.taskForm || !D.taskIdInput) return;

        const taskId = D.taskIdInput.value;
        const taskData = {
            taskName: D.taskNameInput.value.trim(),
            description: D.taskDescriptionInput.value.trim(),
            assignedTo: D.taskAssigneeSelect.value ? parseInt(D.taskAssigneeSelect.value) : null,
            status: normalizeStatusString(D.taskStatusSelect.value)
        };

        if (!taskData.taskName) {
            alert('Task Name is required.');
            return;
        }

        try {
            let newTask;
            if (taskId) {
                taskData.taskId = parseInt(taskId);
                await apiClient.put(`/tasks/${taskData.taskId}`, taskData);
                newTask = { ...AppState.tasks.get(taskData.taskId), ...taskData };
            } else {
                const response = await apiClient.post('/tasks', taskData);
                newTask = {
                    ...taskData,
                    taskId: response.data?.data?.taskId,
                    createdAt: new Date().toISOString(),
                    assignedToUserId: taskData.assignedTo,
                    assignedToUserName: taskData.assignedTo ? await fetchUser(taskData.assignedTo) : null
                };
            }
            AppState.tasks.set(parseInt(newTask.taskId), newTask);
            renderTask(newTask, "FormSubmit");
            closeModal();
        } catch (error) {
            console.error('Failed to save task:', error);
            alert(`Error saving task: ${error.response?.data?.message || 'Unknown error.'}`);
        }
    }

    async function handleDeleteTask() {
        const D = AppState.domElements;
        if (!D.taskIdInput) return;

        const taskId = parseInt(D.taskIdInput.value);
        if (isNaN(taskId)) {
            alert('Invalid Task ID.');
            return;
        }

        if (!confirm(`Are you sure you want to permanently delete task ${taskId}?`)) return;

        try {
            await apiClient.delete(`/tasks/${taskId}`);
            AppState.tasks.delete(taskId);
            const taskCard = document.querySelector(`.task-card[data-task-id="${taskId}"]`);
            if (taskCard) taskCard.remove();
            closeModal();
            loadMyTasks();
        } catch (error) {
            console.error(`Failed to delete task ${taskId}:`, error);
            alert(`Error deleting task: ${error.response?.data?.message || 'API error.'}`);
        }
    }

    function setupDragAndDrop() {
        const taskBoard = document.querySelector('.task-board');
        if (!taskBoard) return;

        taskBoard.addEventListener('dragstart', e => {
            if (!e.target.classList.contains('task-card') || !e.target._taskData) return;
            AppState.draggedTask = e.target;
            e.dataTransfer.effectAllowed = 'move';
            e.dataTransfer.setData('text/plain', e.target.dataset.taskId);
            setTimeout(() => AppState.draggedTask?.classList.add('dragging'), 0);
        });

        taskBoard.addEventListener('dragend', () => {
            if (AppState.draggedTask) AppState.draggedTask.classList.remove('dragging');
            AppState.draggedTask = null;
            document.querySelectorAll('.task-list.drag-over').forEach(list => list.classList.remove('drag-over'));
        });

        document.querySelectorAll('.task-list').forEach(list => {
            list.addEventListener('dragover', e => {
                e.preventDefault();
                if (AppState.draggedTask && e.currentTarget !== AppState.draggedTask.parentElement) {
                    e.currentTarget.classList.add('drag-over');
                }
                e.dataTransfer.dropEffect = 'move';
            });

            list.addEventListener('dragleave', e => e.currentTarget.classList.remove('drag-over'));

            list.addEventListener('drop', async e => {
                e.preventDefault();
                e.currentTarget.classList.remove('drag-over');
                const draggedEl = AppState.draggedTask;
                if (!draggedEl || !draggedEl._taskData) return;

                const targetColumn = e.currentTarget.closest('.task-column');
                if (!targetColumn) return;

                const newStatus = normalizeStatusString(targetColumn.dataset.status);
                const taskId = parseInt(draggedEl.dataset.taskId);
                const taskData = draggedEl._taskData;

                if (newStatus && newStatus !== normalizeStatusString(taskData.status)) {
                    e.currentTarget.appendChild(draggedEl);
                    draggedEl.dataset.status = newStatus;
                    taskData.status = newStatus;

                    const updatePayload = {
                        taskId,
                        taskName: taskData.taskName,
                        description: taskData.description,
                        assignedTo: taskData.assignedToUserId,
                        status: newStatus
                    };

                    try {
                        await apiClient.put(`/tasks/${taskId}`, updatePayload);
                        AppState.tasks.set(taskId, taskData);
                        renderTask(taskData, "DragDropUpdate");
                    } catch (error) {
                        console.error('Drag-and-drop update failed:', error);
                        alert('Failed to update task status. Reverting.');
                        const originalColumn = AppState.domElements.columns[normalizeStatusString(taskData.status)];
                        if (originalColumn) {
                            originalColumn.appendChild(draggedEl);
                            draggedEl.dataset.status = taskData.status;
                            taskData.status = taskData.status;
                        } else {
                            loadAllTasks();
                        }
                    }
                }
            });
        });
    }

    async function handleTaskUpdateFromSignalR(taskData) {
        const now = Date.now();
        if (now - AppState.lastSignalRUpdate < AppState.signalRDebounceDelay) {
            await new Promise(resolve => setTimeout(resolve, AppState.signalRDebounceDelay - (now - AppState.lastSignalRUpdate)));
        }
        AppState.lastSignalRUpdate = now;

        if (!taskData?.taskId) return;

        if (taskData.assignedToUserId && taskData.assignedToUserName?.trim()) {
            AppState.localUserMap.set(parseInt(taskData.assignedToUserId), taskData.assignedToUserName);
        } else if (taskData.assignedToUserId && !AppState.localUserMap.has(parseInt(taskData.assignedToUserId))) {
            taskData.assignedToUserName = await fetchUser(taskData.assignedToUserId);
        }

        AppState.tasks.set(parseInt(taskData.taskId), taskData);
        renderTask(taskData, "SignalRUpdate");
        loadMyTasks();
    }

    function handleTaskDeleteFromSignalR(taskId) {
        const id = parseInt(taskId);
        if (isNaN(id)) return;

        AppState.tasks.delete(id);
        const taskCard = document.querySelector(`.task-card[data-task-id="${id}"]`);
        if (taskCard) taskCard.remove();
        loadMyTasks();
    }

    function handleUserUpdateFromSignalR(userData) {
        if (!userData?.userId || !userData.fullName) return;

        const userId = parseInt(userData.userId);
        AppState.localUserMap.set(userId, userData.fullName);
        AppState.usersCache = AppState.usersCache.filter(u => parseInt(u.userId) !== userId);
        AppState.usersCache.push(userData);
        updateUserDropdown();

        AppState.tasks.forEach(task => {
            if (parseInt(task.assignedToUserId) === userId) {
                task.assignedToUserName = userData.fullName;
                renderTask(task, "UserUpdateSignalR");
            }
        });
    }
})();