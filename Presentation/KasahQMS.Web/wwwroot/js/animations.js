/**
 * KASAH QMS - Animations & Interaction System
 * Scroll-triggered animations, counter animations, toast notifications, loading states
 */
(function () {
    'use strict';

    /* ============================
       Intersection Observer: Scroll-triggered fade-in-up
       ============================ */
    function initScrollAnimations() {
        var elements = document.querySelectorAll('.animate-on-scroll');
        if (!elements.length) return;

        // Respect prefers-reduced-motion
        if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
            elements.forEach(function (el) {
                el.classList.add('is-visible');
            });
            return;
        }

        var observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (entry.isIntersecting) {
                    entry.target.classList.add('is-visible');
                    observer.unobserve(entry.target);
                }
            });
        }, {
            threshold: 0.1,
            rootMargin: '0px 0px -40px 0px'
        });

        elements.forEach(function (el) {
            observer.observe(el);
        });
    }

    /* ============================
       Counter Animation for Stats
       ============================ */
    function animateCounters() {
        var counters = document.querySelectorAll('[data-count-to]');
        if (!counters.length) return;

        if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
            counters.forEach(function (el) {
                el.textContent = el.getAttribute('data-count-to');
            });
            return;
        }

        var observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (entry.isIntersecting) {
                    var el = entry.target;
                    var target = parseFloat(el.getAttribute('data-count-to')) || 0;
                    var duration = parseInt(el.getAttribute('data-count-duration'), 10) || 1200;
                    var prefix = el.getAttribute('data-count-prefix') || '';
                    var suffix = el.getAttribute('data-count-suffix') || '';
                    var decimals = parseInt(el.getAttribute('data-count-decimals'), 10) || 0;
                    var startTime = null;

                    function step(timestamp) {
                        if (!startTime) startTime = timestamp;
                        var progress = Math.min((timestamp - startTime) / duration, 1);
                        // Ease-out cubic
                        var eased = 1 - Math.pow(1 - progress, 3);
                        var current = eased * target;
                        el.textContent = prefix + current.toFixed(decimals) + suffix;
                        if (progress < 1) {
                            requestAnimationFrame(step);
                        } else {
                            el.textContent = prefix + target.toFixed(decimals) + suffix;
                        }
                    }

                    requestAnimationFrame(step);
                    observer.unobserve(el);
                }
            });
        }, { threshold: 0.3 });

        counters.forEach(function (el) {
            el.textContent = '0';
            observer.observe(el);
        });
    }

    /* ============================
       Chart Reveal Animation
       ============================ */
    function initChartReveal() {
        var charts = document.querySelectorAll('.chart-container');
        if (!charts.length) return;

        var observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (entry.isIntersecting) {
                    entry.target.classList.add('is-visible');
                    observer.unobserve(entry.target);
                }
            });
        }, { threshold: 0.2 });

        charts.forEach(function (el) {
            el.style.opacity = '0';
            el.style.transform = 'translateY(16px)';
            el.style.transition = 'opacity 0.5s ease-out, transform 0.5s ease-out';
            observer.observe(el);
        });

        // Reuse the is-visible style
        var style = document.createElement('style');
        style.textContent = '.chart-container.is-visible { opacity: 1 !important; transform: translateY(0) !important; }';
        document.head.appendChild(style);
    }

    /* ============================
       Page Transition Effects
       ============================ */
    function initPageTransitions() {
        // Add fade-in to page wrapper on load
        var wrapper = document.querySelector('.page-transition-wrapper');
        if (wrapper) {
            wrapper.style.opacity = '1';
        }

        // Intercept internal navigation for smooth transition
        document.addEventListener('click', function (e) {
            var link = e.target.closest('a[href]');
            if (!link) return;
            var href = link.getAttribute('href');

            // Only handle internal links, not external or special
            if (!href || href.startsWith('#') || href.startsWith('javascript:') ||
                href.startsWith('http') || link.target === '_blank' ||
                e.ctrlKey || e.metaKey || e.shiftKey) return;

            var wrapper = document.querySelector('.page-transition-wrapper');
            if (!wrapper || window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;

            e.preventDefault();
            wrapper.style.transition = 'opacity 0.15s ease';
            wrapper.style.opacity = '0';
            setTimeout(function () {
                window.location.href = href;
            }, 150);
        });
    }

    /* ============================
       Toast Notification System
       ============================ */
    var toastCounter = 0;

    /**
     * Show a toast notification
     * @param {string} message - Toast message
     * @param {string} type - 'success' | 'danger' | 'warning' | 'info'
     * @param {number} duration - Auto-dismiss ms (0 = manual only, default 4000)
     */
    window.showToast = function (message, type, duration) {
        type = type || 'info';
        duration = duration !== undefined ? duration : 4000;

        var container = document.getElementById('toast-container');
        if (!container) {
            container = document.createElement('div');
            container.id = 'toast-container';
            container.className = 'toast-container';
            document.body.appendChild(container);
        }

        var id = 'toast-' + (++toastCounter);
        var icons = {
            success: '<svg class="toast-icon" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"/></svg>',
            danger: '<svg class="toast-icon" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/></svg>',
            warning: '<svg class="toast-icon" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"/></svg>',
            info: '<svg class="toast-icon" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/></svg>'
        };

        var toast = document.createElement('div');
        toast.id = id;
        toast.className = 'toast toast-' + type;
        toast.innerHTML =
            (icons[type] || icons.info) +
            '<span class="toast-message">' + message + '</span>' +
            '<button class="toast-close" aria-label="Close">&times;</button>';

        container.appendChild(toast);

        // Close button handler
        toast.querySelector('.toast-close').addEventListener('click', function () {
            dismissToast(id);
        });

        // Auto-dismiss
        if (duration > 0) {
            setTimeout(function () {
                dismissToast(id);
            }, duration);
        }

        return id;
    };

    function dismissToast(id) {
        var toast = document.getElementById(id);
        if (!toast) return;
        toast.classList.add('toast-exit');
        setTimeout(function () {
            if (toast.parentNode) toast.parentNode.removeChild(toast);
        }, 250);
    }

    window.dismissToast = dismissToast;

    /* ============================
       Loading State Management
       ============================ */

    /**
     * Show loading skeletons inside a container
     * @param {string|Element} container - Selector or element
     */
    window.showLoading = function (container) {
        var el = typeof container === 'string' ? document.querySelector(container) : container;
        if (el) el.classList.add('is-loading');
    };

    /**
     * Hide loading skeletons and reveal content
     * @param {string|Element} container - Selector or element
     */
    window.hideLoading = function (container) {
        var el = typeof container === 'string' ? document.querySelector(container) : container;
        if (!el) return;
        el.classList.remove('is-loading');
        // Trigger fade-in on revealed content
        var items = el.querySelectorAll('.skeleton-hide');
        items.forEach(function (item, i) {
            item.style.opacity = '0';
            item.style.animation = 'fadeIn 0.3s ease-out ' + (i * 0.05) + 's forwards';
        });
    };

    /* ============================
       Initialize All on DOMContentLoaded
       ============================ */
    function init() {
        initScrollAnimations();
        animateCounters();
        initChartReveal();
        initPageTransitions();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
