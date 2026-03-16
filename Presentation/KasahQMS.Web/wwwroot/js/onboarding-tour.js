/**
 * KASAH QMS Onboarding Tour
 * Vanilla JS guided tour - no Alpine.js dependency
 */
(function () {
    'use strict';

    if (localStorage.getItem('kasah_onboarding_done') === '1') return;

    var steps = [
        {
            title: 'Welcome to KASAH QMS!',
            description: "Let's show you around the system. Click Next to begin the tour.",
            target: 'aside',
            position: 'right'
        },
        {
            title: 'Dashboard',
            description: 'Your dashboard gives you an overview of all activities, KPIs and recent updates.',
            target: '[data-nav="/dashboard"], [href="/Dashboard"]',
            position: 'right'
        },
        {
            title: 'Documents',
            description: 'Manage quality documents, approvals and templates here.',
            target: '[data-nav="/documents"], [href="/Documents"]',
            position: 'right'
        },
        {
            title: 'Tasks',
            description: 'Create and track tasks assigned to you or your team.',
            target: '[data-nav="/tasks"], [href="/Tasks"]',
            position: 'right'
        },
        {
            title: 'Notifications',
            description: 'Stay updated with real-time notifications here.',
            target: '#badge-notifications, #notification-dot, [href="/Notifications"]',
            position: 'bottom'
        },
        {
            title: 'Quick Add',
            description: 'Use Quick Add to rapidly create documents, tasks, or CAPAs.',
            target: '#quick-add-btn, [data-quick-add], button[onclick*="quickAdd"]',
            position: 'bottom'
        },
        {
            title: 'Privacy & Security',
            description: 'Access your security settings and privacy controls here.',
            target: '[data-nav="/security/twofactor"], [href="/Security/TwoFactor"]',
            position: 'right'
        },
        {
            title: "You're all set!",
            description: 'Click Finish to start using KASAH QMS. You can restart this tour from the Training Hub.',
            target: null,
            position: 'center'
        }
    ];

    var currentStep = 0;
    var overlay, card, highlightBox;

    function getTarget(selector) {
        if (!selector) return null;
        var selectors = selector.split(', ');
        for (var i = 0; i < selectors.length; i++) {
            try {
                var el = document.querySelector(selectors[i].trim());
                if (el) return el;
            } catch (e) { /* ignore invalid selectors */ }
        }
        return null;
    }

    function createTour() {
        // Backdrop overlay
        overlay = document.createElement('div');
        overlay.id = 'kasah-tour-overlay';
        overlay.style.cssText = 'position:fixed;inset:0;background:rgba(0,0,0,0.55);z-index:9998;pointer-events:none;transition:all 0.3s;';
        document.body.appendChild(overlay);

        // Highlight box (cut-out effect via box-shadow)
        highlightBox = document.createElement('div');
        highlightBox.id = 'kasah-tour-highlight';
        highlightBox.style.cssText = 'position:fixed;z-index:9999;border-radius:8px;box-shadow:0 0 0 9999px rgba(0,0,0,0.55);pointer-events:none;transition:all 0.3s ease;';
        document.body.appendChild(highlightBox);

        // Tour card
        card = document.createElement('div');
        card.id = 'kasah-tour-card';
        card.style.cssText = 'position:fixed;z-index:10000;background:#fff;border-radius:16px;padding:24px;max-width:340px;width:340px;box-shadow:0 20px 60px rgba(0,0,0,0.2);transition:all 0.3s ease;';
        document.body.appendChild(card);

        showStep(0);
    }

    function showStep(index) {
        currentStep = index;
        var step = steps[index];
        var target = getTarget(step.target);

        // Update card content
        card.innerHTML =
            '<div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:12px;">' +
            '<span style="font-size:12px;color:#6b7280;font-weight:600;">Step ' + (index + 1) + ' of ' + steps.length + '</span>' +
            '<button id="tour-skip" style="font-size:12px;color:#9ca3af;background:none;border:none;cursor:pointer;padding:2px 6px;">Skip Tour</button>' +
            '</div>' +
            '<div style="display:flex;gap:4px;margin-bottom:16px;">' +
            steps.map(function (_, i) {
                return '<div style="height:3px;flex:1;border-radius:2px;background:' + (i <= index ? '#059669' : '#e5e7eb') + ';transition:background 0.3s;"></div>';
            }).join('') +
            '</div>' +
            '<h3 style="font-size:16px;font-weight:700;color:#111827;margin:0 0 8px;">' + escHtml(step.title) + '</h3>' +
            '<p style="font-size:14px;color:#4b5563;line-height:1.5;margin:0 0 20px;">' + escHtml(step.description) + '</p>' +
            '<div style="display:flex;justify-content:space-between;align-items:center;">' +
            (index > 0 ? '<button id="tour-prev" style="font-size:13px;color:#6b7280;background:none;border:1px solid #d1d5db;border-radius:8px;padding:8px 14px;cursor:pointer;">Back</button>' : '<span></span>') +
            (index < steps.length - 1
                ? '<button id="tour-next" style="font-size:13px;color:#fff;background:#059669;border:none;border-radius:8px;padding:8px 18px;cursor:pointer;font-weight:600;">Next →</button>'
                : '<button id="tour-finish" style="font-size:13px;color:#fff;background:#059669;border:none;border-radius:8px;padding:8px 18px;cursor:pointer;font-weight:600;">Finish ✓</button>') +
            '</div>';

        // Bind buttons
        var skipBtn = document.getElementById('tour-skip');
        var nextBtn = document.getElementById('tour-next');
        var prevBtn = document.getElementById('tour-prev');
        var finishBtn = document.getElementById('tour-finish');

        if (skipBtn) skipBtn.addEventListener('click', closeTour);
        if (nextBtn) nextBtn.addEventListener('click', function () { showStep(currentStep + 1); });
        if (prevBtn) prevBtn.addEventListener('click', function () { showStep(currentStep - 1); });
        if (finishBtn) finishBtn.addEventListener('click', finishTour);

        // Position highlight and card
        positionElements(target, step.position);
    }

    function positionElements(target, position) {
        var padding = 8;

        if (target) {
            var rect = target.getBoundingClientRect();
            highlightBox.style.display = 'block';
            highlightBox.style.top = (rect.top - padding) + 'px';
            highlightBox.style.left = (rect.left - padding) + 'px';
            highlightBox.style.width = (rect.width + padding * 2) + 'px';
            highlightBox.style.height = (rect.height + padding * 2) + 'px';

            // Position card relative to target
            var cardW = 340;
            var cardH = 220;
            var vw = window.innerWidth;
            var vh = window.innerHeight;
            var cardLeft, cardTop;

            if (position === 'right') {
                cardLeft = rect.right + padding + 12;
                cardTop = rect.top + rect.height / 2 - cardH / 2;
                if (cardLeft + cardW > vw) cardLeft = rect.left - cardW - 12;
            } else if (position === 'bottom') {
                cardLeft = rect.left + rect.width / 2 - cardW / 2;
                cardTop = rect.bottom + padding + 12;
                if (cardTop + cardH > vh) cardTop = rect.top - cardH - 12;
            } else {
                cardLeft = vw / 2 - cardW / 2;
                cardTop = vh / 2 - cardH / 2;
            }

            cardLeft = Math.max(12, Math.min(cardLeft, vw - cardW - 12));
            cardTop = Math.max(12, Math.min(cardTop, vh - cardH - 12));

            card.style.left = cardLeft + 'px';
            card.style.top = cardTop + 'px';
        } else {
            // Center card, hide highlight
            highlightBox.style.display = 'none';
            var vw2 = window.innerWidth;
            var vh2 = window.innerHeight;
            card.style.left = (vw2 / 2 - 170) + 'px';
            card.style.top = (vh2 / 2 - 140) + 'px';
        }
    }

    function finishTour() {
        localStorage.setItem('kasah_onboarding_done', '1');
        closeTour();
    }

    function closeTour() {
        if (overlay) overlay.remove();
        if (card) card.remove();
        if (highlightBox) highlightBox.remove();
    }

    function escHtml(str) {
        var d = document.createElement('div');
        d.textContent = str;
        return d.innerHTML;
    }

    // Start tour after page is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () { setTimeout(createTour, 800); });
    } else {
        setTimeout(createTour, 800);
    }
})();
