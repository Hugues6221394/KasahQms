/**
 * SignalR Chat hub client.
 * Connects when data-user-id is set on body. Exposes qmsChat for department/thread/direct chat.
 */
(function () {
    'use strict';

    var userId = document.getElementById('user-context')?.getAttribute('data-user-id') || document.body.getAttribute('data-user-id');
    if (!userId) return;

    if (typeof signalR === 'undefined') {
        console.warn('SignalR not loaded; chat hub skipped.');
        return;
    }

    var connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/chat', { withCredentials: true })
        .withAutomaticReconnect()
        .build();

    var receiveHandlers = [];

    connection.on('ReceiveMessage', function (msg) {
        receiveHandlers.forEach(function (h) { try { h(msg); } catch (e) {} });
    });

    function ensureStarted() {
        if (connection.state === signalR.HubConnectionState.Connected) return Promise.resolve();
        return connection.start();
    }

    window.qmsChat = {
        joinDepartment: function (orgUnitId) {
            return ensureStarted().then(function () {
                return connection.invoke('JoinDepartment', orgUnitId);
            });
        },
        leaveDepartment: function (orgUnitId) {
            return connection.invoke('LeaveDepartment', orgUnitId);
        },
        joinThread: function (threadId) {
            return ensureStarted().then(function () {
                return connection.invoke('JoinThread', threadId);
            });
        },
        leaveThread: function (threadId) {
            return connection.invoke('LeaveThread', threadId);
        },
        sendToDepartment: function (orgUnitId, message) {
            return ensureStarted().then(function () {
                return connection.invoke('SendToDepartment', orgUnitId, message || '');
            });
        },
        sendToThread: function (threadId, message) {
            return ensureStarted().then(function () {
                return connection.invoke('SendToThread', threadId, message || '');
            });
        },
        sendToUser: function (targetUserId, message, taskId) {
            return ensureStarted().then(function () {
                return connection.invoke('SendToUser', targetUserId, message || '', taskId || null);
            });
        },
        onReceiveMessage: function (cb) {
            if (typeof cb === 'function') receiveHandlers.push(cb);
        }
    };

    connection.start().catch(function (err) { return console.warn('Chat hub connection failed:', err); });
})();
