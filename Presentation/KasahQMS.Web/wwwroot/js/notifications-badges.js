/**
 * Real-time Notification Badges System
 * Handles badge counts for messages, tasks, documents, approvals, and notifications.
 * Uses SignalR for real-time updates.
 */
(function () {
    'use strict';

    var userId = document.getElementById('user-context')?.getAttribute('data-user-id');
    if (!userId) return;

    // Badge elements
    var messageBadge = document.getElementById('badge-messages');
    var taskBadge = document.getElementById('badge-tasks');
    var documentBadge = document.getElementById('badge-documents');
    var notificationBadge = document.getElementById('badge-notifications');
    var approvalBadge = document.getElementById('badge-approvals');
    var notificationDot = document.getElementById('notification-dot');

    // Store badge counts
    var badges = {
        messages: 0,
        tasks: 0,
        documents: 0,
        notifications: 0,
        approvals: 0
    };

    // Update badge display
    function updateBadge(element, count) {
        if (!element) return;
        if (count > 0) {
            element.textContent = count > 99 ? '99+' : count.toString();
            element.classList.remove('hidden');
        } else {
            element.classList.add('hidden');
        }
    }

    function updateAllBadges() {
        updateBadge(messageBadge, badges.messages);
        updateBadge(taskBadge, badges.tasks);
        updateBadge(documentBadge, badges.documents);
        updateBadge(notificationBadge, badges.notifications);
        updateBadge(approvalBadge, badges.approvals);

        if (notificationDot) {
            if (badges.notifications > 0) {
                notificationDot.classList.remove('hidden');
            } else {
                notificationDot.classList.add('hidden');
            }
        }

        // Update page title with total unread
        var total = badges.messages + badges.tasks + badges.notifications + badges.approvals;
        var baseTitle = document.title.replace(/^\(\d+\)\s*/, '');
        document.title = total > 0 ? '(' + total + ') ' + baseTitle : baseTitle;
    }

    // Fetch initial badge counts
    function fetchBadges() {
        fetch('/api/BadgesApi')
            .then(function (r) { return r.json(); })
            .then(function (data) {
                badges.messages = data.unreadMessages || 0;
                badges.tasks = data.pendingTasks || 0;
                badges.documents = data.pendingDocuments || 0;
                badges.notifications = data.unreadNotifications || 0;
                badges.approvals = data.pendingApprovals || 0;
                updateAllBadges();
            })
            .catch(function (e) {
                console.warn('Failed to fetch badges:', e);
            });
    }

    // Initialize SignalR connection for real-time updates
    function initSignalR() {
        if (typeof signalR === 'undefined') {
            console.warn('SignalR not loaded; real-time badges disabled.');
            return;
        }

        var connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/notifications', { withCredentials: true })
            .withAutomaticReconnect()
            .build();

        // Handle notification count updates
        connection.on('UnreadCount', function (count) {
            badges.notifications = count;
            updateAllBadges();
        });

        // Handle new notification received
        connection.on('NotificationReceived', function (notification) {
            badges.notifications++;
            updateAllBadges();
            showToast(notification.title, notification.message, notification.type, notification.relatedEntityId, notification.relatedEntityType);
        });

        // Handle new message received
        connection.on('NewMessage', function (data) {
            badges.messages++;
            updateAllBadges();
            showToast('New Message', data.senderName + ': ' + (data.content || '').substring(0, 50), 'message', data.threadId, 'chat');
        });

        // Handle new task assigned
        connection.on('TaskAssigned', function (data) {
            badges.tasks++;
            updateAllBadges();
            showToast('New Task Assigned', data.title, 'task', data.taskId, 'task');
        });

        // Handle document received
        connection.on('DocumentReceived', function (data) {
            badges.documents++;
            updateAllBadges();
            showToast('Document Received', data.title, 'document', data.documentId, 'document');
        });

        // Handle task update notification (for task assigners)
        connection.on('TaskUpdate', function (data) {
            badges.notifications++;
            updateAllBadges();
            showToast(data.title || 'Task Update', data.message || 'An update was posted on your task', 'taskupdate', data.taskId, 'task');
        });

        // Handle badge refresh request
        connection.on('RefreshBadges', function () {
            fetchBadges();
        });

        connection.start().catch(function (err) {
            console.warn('Notifications hub connection failed:', err);
        });

        // Store connection for external use
        window.notificationsHub = connection;
    }

    // Toast notification system
    function showToast(title, message, type, entityId, entityType) {
        var container = document.getElementById('toast-container');
        if (!container) {
            container = document.createElement('div');
            container.id = 'toast-container';
            container.className = 'fixed bottom-4 right-4 z-[100] space-y-3';
            document.body.appendChild(container);
        }

        var toast = document.createElement('div');
        toast.className = 'max-w-sm w-full bg-white rounded-xl shadow-lg border border-slate-200 overflow-hidden transform transition-all duration-300 translate-x-full opacity-0';

        var iconColor = type === 'message' ? 'text-blue-500' :
            type === 'task' ? 'text-amber-500' :
                type === 'taskupdate' ? 'text-indigo-500' :
                    type === 'document' ? 'text-emerald-500' : 'text-brand-500';
        var bgColor = type === 'message' ? 'bg-blue-100' :
            type === 'task' ? 'bg-amber-100' :
                type === 'taskupdate' ? 'bg-indigo-100' :
                    type === 'document' ? 'bg-emerald-100' : 'bg-brand-100';

        var link = '';
        if (entityId) {
            if (entityType === 'chat') link = '/Chat?thread=' + entityId;
            else if (entityType === 'task') link = '/Tasks/Details/' + entityId;
            else if (entityType === 'document') link = '/Documents/Details/' + entityId;
        }

        toast.innerHTML =
            '<div class="p-4 cursor-pointer" ' + (link ? 'onclick="window.location.href=\'' + link + '\'"' : '') + '>' +
            '<div class="flex items-start gap-3">' +
            '<div class="w-10 h-10 rounded-lg ' + bgColor + ' ' + iconColor + ' flex items-center justify-center flex-shrink-0">' +
            (type === 'message' ? '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 12h.01M12 12h.01M16 12h.01M21 12c0 4.418-4.03 8-9 8a9.863 9.863 0 01-4.255-.949L3 20l1.395-3.72C3.512 15.042 3 13.574 3 12c0-4.418 4.03-8 9-8s9 3.582 9 8z"/></svg>' :
                type === 'task' ? '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-6 9l2 2 4-4"/></svg>' :
                    type === 'taskupdate' ? '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z"/></svg>' :
                        type === 'document' ? '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"/></svg>' :
                            '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9"/></svg>') +
            '</div>' +
            '<div class="flex-1 min-w-0">' +
            '<p class="text-sm font-semibold text-slate-900">' + escapeHtml(title) + '</p>' +
            '<p class="text-sm text-slate-500 truncate">' + escapeHtml(message) + '</p>' +
            '</div>' +
            '<button onclick="event.stopPropagation(); this.closest(\'.toast-item\').remove();" class="text-slate-400 hover:text-slate-600">' +
            '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/></svg>' +
            '</button>' +
            '</div>' +
            '</div>';

        toast.classList.add('toast-item');
        container.appendChild(toast);

        // Animate in
        requestAnimationFrame(function () {
            toast.classList.remove('translate-x-full', 'opacity-0');
        });

        // Auto-remove after 5 seconds
        setTimeout(function () {
            toast.classList.add('translate-x-full', 'opacity-0');
            setTimeout(function () {
                toast.remove();
            }, 300);
        }, 5000);

        // Play notification sound
        playNotificationSound();
    }

    function escapeHtml(text) {
        var div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    function playNotificationSound() {
        try {
            // Use Web Audio API for a simple notification beep
            var ctx = new (window.AudioContext || window.webkitAudioContext)();
            var osc = ctx.createOscillator();
            var gain = ctx.createGain();
            osc.connect(gain);
            gain.connect(ctx.destination);
            gain.gain.value = 0.1;
            osc.frequency.value = 800;
            osc.type = 'sine';
            osc.start(ctx.currentTime);
            osc.stop(ctx.currentTime + 0.1);
        } catch (e) { }
    }

    // Expose functions globally
    window.qmsBadges = {
        refresh: fetchBadges,
        setBadge: function (type, count) {
            badges[type] = count;
            updateAllBadges();
        },
        decrementBadge: function (type) {
            if (badges[type] > 0) {
                badges[type]--;
                updateAllBadges();
            }
        }
    };

    // Initialize
    fetchBadges();
    initSignalR();

    // Refresh badges periodically (every 60 seconds as fallback)
    setInterval(fetchBadges, 60000);
})();
