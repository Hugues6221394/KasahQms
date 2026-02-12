/**
 * KASAH QMS - Site JavaScript
 * Enterprise Quality Management System
 */

(function() {
  'use strict';

  // ===========================================
  // Navigation Active State
  // ===========================================
  function initNavigation() {
    const path = window.location.pathname.toLowerCase();
    const navLinks = document.querySelectorAll('[data-nav]');

    navLinks.forEach(el => {
      const target = (el.getAttribute('data-nav') || '').toLowerCase();
      if (target && path.startsWith(target)) {
        el.classList.add('nav-active');
        el.classList.remove('text-slate-600', 'hover:bg-brand-50', 'hover:text-brand-700');
      }
    });
  }

  // ===========================================
  // Keyboard Shortcuts
  // ===========================================
  function initKeyboardShortcuts() {
    document.addEventListener('keydown', function(e) {
      // Cmd/Ctrl + K for search focus
      if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
        e.preventDefault();
        const searchInput = document.querySelector('input[type="search"]');
        if (searchInput) {
          searchInput.focus();
        }
      }

      // Escape to close modals/dropdowns
      if (e.key === 'Escape') {
        // Close any open Alpine.js dropdowns
        document.querySelectorAll('[x-data]').forEach(el => {
          if (el.__x) {
            el.__x.$data.open = false;
            el.__x.$data.userMenuOpen = false;
            el.__x.$data.sidebarOpen = false;
          }
        });
      }
    });
  }

  // ===========================================
  // Form Validation Helpers
  // ===========================================
  function initFormValidation() {
    // Add visual feedback for invalid form fields
    document.querySelectorAll('input, select, textarea').forEach(input => {
      input.addEventListener('invalid', function(e) {
        this.classList.add('border-accent-500', 'ring-2', 'ring-accent-200');
      });

      input.addEventListener('input', function() {
        if (this.validity.valid) {
          this.classList.remove('border-accent-500', 'ring-2', 'ring-accent-200');
        }
      });
    });
  }

  // ===========================================
  // Loading States
  // ===========================================
  function setLoadingState(element, isLoading) {
    if (isLoading) {
      element.disabled = true;
      element.classList.add('opacity-75', 'cursor-wait');

      // Store original content
      element.dataset.originalContent = element.innerHTML;

      // Add spinner
      element.innerHTML = `
        <svg class="animate-spin h-4 w-4 mr-2" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
          <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
          <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
        </svg>
        Loading...
      `;
    } else {
      element.disabled = false;
      element.classList.remove('opacity-75', 'cursor-wait');

      // Restore original content
      if (element.dataset.originalContent) {
        element.innerHTML = element.dataset.originalContent;
        delete element.dataset.originalContent;
      }
    }
  }

  // ===========================================
  // Toast Notifications
  // ===========================================
  const toastContainer = document.createElement('div');
  toastContainer.id = 'toast-container';
  toastContainer.className = 'fixed bottom-4 right-4 z-50 flex flex-col gap-2';
  document.body.appendChild(toastContainer);

  function showToast(message, type = 'info', duration = 4000) {
    const toast = document.createElement('div');

    const bgColors = {
      success: 'bg-brand-600',
      error: 'bg-accent-600',
      warning: 'bg-amber-500',
      info: 'bg-slate-700'
    };

    const icons = {
      success: '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"/>',
      error: '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2m7-2a9 9 0 11-18 0 9 9 0 0118 0z"/>',
      warning: '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"/>',
      info: '<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/>'
    };

    toast.className = `${bgColors[type]} text-white px-4 py-3 rounded-xl shadow-xl flex items-center gap-3 transform translate-x-full transition-transform duration-300 ease-out`;
    toast.innerHTML = `
      <svg class="w-5 h-5 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">${icons[type]}</svg>
      <span class="text-sm font-medium">${message}</span>
      <button class="ml-2 opacity-70 hover:opacity-100" onclick="this.parentElement.remove()">
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/>
        </svg>
      </button>
    `;

    toastContainer.appendChild(toast);

    // Trigger animation
    requestAnimationFrame(() => {
      toast.classList.remove('translate-x-full');
      toast.classList.add('translate-x-0');
    });

    // Auto remove
    setTimeout(() => {
      toast.classList.remove('translate-x-0');
      toast.classList.add('translate-x-full');
      setTimeout(() => toast.remove(), 300);
    }, duration);
  }

  // Make toast function globally available
  window.showToast = showToast;

  // ===========================================
  // Copy to Clipboard
  // ===========================================
  function copyToClipboard(text) {
    navigator.clipboard.writeText(text).then(() => {
      showToast('Copied to clipboard', 'success', 2000);
    }).catch(err => {
      showToast('Failed to copy', 'error', 2000);
    });
  }

  window.copyToClipboard = copyToClipboard;

  // ===========================================
  // Date Formatting
  // ===========================================
  function formatDate(date, format = 'short') {
    const d = new Date(date);
    const options = {
      short: { month: 'short', day: 'numeric', year: 'numeric' },
      long: { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric' },
      time: { hour: '2-digit', minute: '2-digit' }
    };

    return d.toLocaleDateString('en-US', options[format] || options.short);
  }

  window.formatDate = formatDate;

  // ===========================================
  // Debounce Helper
  // ===========================================
  function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
      const later = () => {
        clearTimeout(timeout);
        func(...args);
      };
      clearTimeout(timeout);
      timeout = setTimeout(later, wait);
    };
  }

  window.debounce = debounce;

  // ===========================================
  // Auto-save Draft (for forms)
  // ===========================================
  function initAutoSave() {
    const forms = document.querySelectorAll('[data-autosave]');

    forms.forEach(form => {
      const formId = form.getAttribute('data-autosave');
      const saveIndicator = form.querySelector('.autosave-indicator');

      // Restore draft
      const savedData = localStorage.getItem(`draft_${formId}`);
      if (savedData) {
        const data = JSON.parse(savedData);
        Object.entries(data).forEach(([name, value]) => {
          const field = form.querySelector(`[name="${name}"]`);
          if (field) field.value = value;
        });
      }

      // Auto-save on change
      const saveDebounced = debounce(() => {
        const formData = new FormData(form);
        const data = {};
        formData.forEach((value, key) => data[key] = value);
        localStorage.setItem(`draft_${formId}`, JSON.stringify(data));

        if (saveIndicator) {
          saveIndicator.textContent = 'Draft saved';
          saveIndicator.classList.remove('opacity-0');
          setTimeout(() => saveIndicator.classList.add('opacity-0'), 2000);
        }
      }, 1000);

      form.addEventListener('input', saveDebounced);

      // Clear draft on submit
      form.addEventListener('submit', () => {
        localStorage.removeItem(`draft_${formId}`);
      });
    });
  }

  // ===========================================
  // Initialize
  // ===========================================
  document.addEventListener('DOMContentLoaded', function() {
    initNavigation();
    initKeyboardShortcuts();
    initFormValidation();
    initAutoSave();

    // Log initialization (remove in production)
    console.log('KASAH QMS initialized');
  });

})();

