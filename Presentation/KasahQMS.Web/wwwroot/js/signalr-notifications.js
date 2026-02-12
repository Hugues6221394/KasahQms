/**
 * SignalR Notifications hub client.
 * Connects when data-user-id is set on body (authenticated user).
 * Listens for "Notification" and shows toasts; dispatches "qms:notification" for UI updates.
 * Provides API for marking notifications as read and getting recent notifications.
 */
(function () {
  'use strict';

  var userId = document.getElementById('user-context')?.getAttribute('data-user-id') || document.body.getAttribute('data-user-id');
  if (!userId) return;

  var hubUrl = '/hubs/notifications';

  if (typeof signalR === 'undefined') {
    console.warn('SignalR not loaded; notifications hub skipped.');
    return;
  }

  var connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, { withCredentials: true })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .build();

  // Handle incoming notifications
  connection.on('Notification', function (payload) {
    var msg = (payload && (payload.Message || payload.message)) || 'New notification';
    var title = (payload && (payload.Title || payload.title)) || 'Notification';
    
    // Show toast notification
    if (typeof window.showToast === 'function') {
      window.showToast(msg, 'info', 5000);
    }
    
    // Show browser notification if permitted
    if ('Notification' in window && Notification.permission === 'granted') {
      new Notification(title, { body: msg, icon: '/favicon.ico' });
    }
    
    // Dispatch event for UI updates
    try {
      window.dispatchEvent(new CustomEvent('qms:notification', { detail: payload }));
    } catch (e) {}
  });
  
  // Handle unread count updates
  connection.on('UnreadCount', function (count) {
    try {
      window.dispatchEvent(new CustomEvent('qms:unreadCount', { detail: { count: count } }));
    } catch (e) {}
  });
  
  // Handle recent notifications response
  connection.on('RecentNotifications', function (notifications) {
    try {
      window.dispatchEvent(new CustomEvent('qms:recentNotifications', { detail: { notifications: notifications } }));
    } catch (e) {}
  });
  
  // Connection state handlers
  connection.onreconnecting(function(error) {
    console.log('Notifications hub reconnecting...', error);
    try {
      window.dispatchEvent(new CustomEvent('qms:connectionState', { detail: { state: 'reconnecting' } }));
    } catch (e) {}
  });
  
  connection.onreconnected(function(connectionId) {
    console.log('Notifications hub reconnected:', connectionId);
    try {
      window.dispatchEvent(new CustomEvent('qms:connectionState', { detail: { state: 'connected' } }));
    } catch (e) {}
  });
  
  connection.onclose(function(error) {
    console.log('Notifications hub connection closed:', error);
    try {
      window.dispatchEvent(new CustomEvent('qms:connectionState', { detail: { state: 'disconnected' } }));
    } catch (e) {}
  });

  // Start connection
  connection.start().then(function() {
    console.log('Notifications hub connected');
    try {
      window.dispatchEvent(new CustomEvent('qms:connectionState', { detail: { state: 'connected' } }));
    } catch (e) {}
  }).catch(function (err) {
    console.warn('Notifications hub connection failed:', err);
    try {
      window.dispatchEvent(new CustomEvent('qms:connectionState', { detail: { state: 'failed', error: err } }));
    } catch (e) {}
  });

  // Request browser notification permission
  if ('Notification' in window && Notification.permission === 'default') {
    Notification.requestPermission();
  }

  // API for interacting with notifications hub
  window.qmsNotifications = {
    connection: connection,
    
    markAsRead: function(notificationId) {
      if (connection.state === signalR.HubConnectionState.Connected) {
        return connection.invoke('MarkAsRead', notificationId);
      }
      return Promise.reject('Not connected');
    },
    
    markAllAsRead: function() {
      if (connection.state === signalR.HubConnectionState.Connected) {
        return connection.invoke('MarkAllAsRead');
      }
      return Promise.reject('Not connected');
    },
    
    getRecent: function(limit) {
      if (connection.state === signalR.HubConnectionState.Connected) {
        return connection.invoke('GetRecent', limit || 10);
      }
      return Promise.reject('Not connected');
    },
    
    isConnected: function() {
      return connection.state === signalR.HubConnectionState.Connected;
    }
  };

  // Legacy support
  window.qmsNotificationsConnection = connection;
})();
