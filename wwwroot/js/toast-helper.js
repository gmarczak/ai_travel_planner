// Toast Helper - Bootstrap 5 Toast Notifications
(function (window) {
    'use strict';

    const ToastHelper = {
        // Show a toast notification
        show: function (message, type = 'info', options = {}) {
            const defaults = {
                autohide: true,
                delay: 5000,
                animation: true
            };

            const settings = { ...defaults, ...options };
            const toastId = 'toast_' + Date.now() + '_' + Math.random().toString(36).substr(2, 9);

            // Icon and background based on type
            const icons = {
                success: '<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" fill="currentColor" class="bi bi-check-circle-fill" viewBox="0 0 16 16"><path d="M16 8A8 8 0 1 1 0 8a8 8 0 0 1 16 0zm-3.97-3.03a.75.75 0 0 0-1.08.022L7.477 9.417 5.384 7.323a.75.75 0 0 0-1.06 1.06L6.97 11.03a.75.75 0 0 0 1.079-.02l3.992-4.99a.75.75 0 0 0-.01-1.05z"/></svg>',
                error: '<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" fill="currentColor" class="bi bi-exclamation-circle-fill" viewBox="0 0 16 16"><path d="M16 8A8 8 0 1 1 0 8a8 8 0 0 1 16 0zM8 4a.905.905 0 0 0-.9.995l.35 3.507a.552.552 0 0 0 1.1 0l.35-3.507A.905.905 0 0 0 8 4zm.002 6a1 1 0 1 0 0 2 1 1 0 0 0 0-2z"/></svg>',
                warning: '<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" fill="currentColor" class="bi bi-exclamation-triangle-fill" viewBox="0 0 16 16"><path d="M8.982 1.566a1.13 1.13 0 0 0-1.96 0L.165 13.233c-.457.778.091 1.767.98 1.767h13.713c.889 0 1.438-.99.98-1.767L8.982 1.566zM8 5c.535 0 .954.462.9.995l-.35 3.507a.552.552 0 0 1-1.1 0L7.1 5.995A.905.905 0 0 1 8 5zm.002 6a1 1 0 1 1 0 2 1 1 0 0 1 0-2z"/></svg>',
                info: '<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" fill="currentColor" class="bi bi-info-circle-fill" viewBox="0 0 16 16"><path d="M8 16A8 8 0 1 0 8 0a8 8 0 0 0 0 16zm.93-9.412-1 4.705c-.07.34.029.533.304.533.194 0 .487-.07.686-.246l-.088.416c-.287.346-.92.598-1.465.598-.703 0-1.002-.422-.808-1.319l.738-3.468c.064-.293.006-.399-.287-.47l-.451-.081.082-.381 2.29-.287zM8 5.5a1 1 0 1 1 0-2 1 1 0 0 1 0 2z"/></svg>'
            };

            const backgrounds = {
                success: 'bg-success',
                error: 'bg-danger',
                warning: 'bg-warning',
                info: 'bg-info'
            };

            const textColors = {
                success: 'text-white',
                error: 'text-white',
                warning: 'text-dark',
                info: 'text-white'
            };

            const icon = icons[type] || icons.info;
            const bgClass = backgrounds[type] || backgrounds.info;
            const textClass = textColors[type] || textColors.info;

            // Create toast HTML
            const toastHtml = `
                <div class="toast align-items-center ${bgClass} ${textClass} border-0" role="alert" aria-live="assertive" aria-atomic="true" id="${toastId}">
                    <div class="d-flex">
                        <div class="toast-body d-flex align-items-center">
                            <span class="me-2">${icon}</span>
                            <span>${message}</span>
                        </div>
                        <button type="button" class="btn-close btn-close-${type === 'warning' ? 'dark' : 'white'} me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
                    </div>
                </div>
            `;

            // Get or create toast container
            let container = document.getElementById('globalToasts');
            if (!container) {
                container = document.createElement('div');
                container.id = 'globalToasts';
                container.className = 'toast-container position-fixed top-0 end-0 p-3';
                container.style.zIndex = '1080';
                document.body.appendChild(container);
            }

            // Add toast to container
            container.insertAdjacentHTML('beforeend', toastHtml);

            // Initialize and show toast
            const toastElement = document.getElementById(toastId);
            if (toastElement && typeof bootstrap !== 'undefined') {
                const bsToast = new bootstrap.Toast(toastElement, {
                    autohide: settings.autohide,
                    delay: settings.delay,
                    animation: settings.animation
                });

                bsToast.show();

                // Remove toast element after it's hidden
                toastElement.addEventListener('hidden.bs.toast', function () {
                    toastElement.remove();
                });

                return bsToast;
            }

            return null;
        },

        // Convenience methods
        success: function (message, options) {
            return this.show(message, 'success', options);
        },

        error: function (message, options) {
            return this.show(message, 'error', options);
        },

        warning: function (message, options) {
            return this.show(message, 'warning', options);
        },

        info: function (message, options) {
            return this.show(message, 'info', options);
        }
    };

    // Expose to window
    window.Toast = ToastHelper;

})(window);
