// Site-wide JavaScript functionality

// Configure toastr
toastr.options = {
    "closeButton": true,
    "debug": false,
    "newestOnTop": true,
    "progressBar": true,
    "positionClass": "toast-top-right",
    "preventDuplicates": false,
    "onclick": null,
    "showDuration": "300",
    "hideDuration": "1000",
    "timeOut": "5000",
    "extendedTimeOut": "1000",
    "showEasing": "swing",
    "hideEasing": "linear",
    "showMethod": "fadeIn",
    "hideMethod": "fadeOut"
};

// Global utility functions
window.utils = {
    // Show loading spinner
    showLoading: function (element) {
        const originalText = element.innerHTML;
        element.innerHTML = '<span class="spinner-border spinner-border-sm me-2" role="status"></span>Loading...';
        element.disabled = true;
        return originalText;
    },

    // Hide loading spinner
    hideLoading: function (element, originalText) {
        element.innerHTML = originalText;
        element.disabled = false;
    },

    // Format date
    formatDate: function (dateString) {
        const date = new Date(dateString);
        return date.toLocaleDateString('en-US', {
            year: 'numeric',
            month: 'short',
            day: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        });
    },

    // Debounce function
    debounce: function (func, wait, immediate) {
        let timeout;
        return function executedFunction() {
            const context = this;
            const args = arguments;
            const later = function () {
                timeout = null;
                if (!immediate) func.apply(context, args);
            };
            const callNow = immediate && !timeout;
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
            if (callNow) func.apply(context, args);
        };
    },

    // Copy to clipboard
    copyToClipboard: function (text) {
        if (navigator.clipboard) {
            navigator.clipboard.writeText(text).then(function () {
                toastr.success('Copied to clipboard!');
            }).catch(function () {
                toastr.error('Failed to copy to clipboard');
            });
        } else {
            // Fallback for older browsers
            const textArea = document.createElement('textarea');
            textArea.value = text;
            document.body.appendChild(textArea);
            textArea.select();
            try {
                document.execCommand('copy');
                toastr.success('Copied to clipboard!');
            } catch (err) {
                toastr.error('Failed to copy to clipboard');
            }
            document.body.removeChild(textArea);
        }
    }
};

// Initialize tooltips and popovers
$(document).ready(function () {
    // Initialize Bootstrap tooltips
    $('[data-bs-toggle="tooltip"]').tooltip();

    // Initialize Bootstrap popovers
    $('[data-bs-toggle="popover"]').popover();

    // Auto-dismiss alerts after 5 seconds
    $('.alert:not(.alert-permanent)').delay(5000).fadeOut('slow');

    // Add smooth scrolling to anchor links
    $('a[href*="#"]:not([href="#"])').click(function () {
        if (location.pathname.replace(/^\//, '') == this.pathname.replace(/^\//, '') && location.hostname == this.hostname) {
            let target = $(this.hash);
            target = target.length ? target : $('[name=' + this.hash.slice(1) + ']');
            if (target.length) {
                $('html, body').animate({
                    scrollTop: target.offset().top - 70
                }, 1000);
                return false;
            }
        }
    });

    // Form validation enhancement
    $('form').on('submit', function (e) {
        const form = this;
        if (form.checkValidity() === false) {
            e.preventDefault();
            e.stopPropagation();
            toastr.error('Please fill in all required fields correctly.');
        }
        form.classList.add('was-validated');
    });

    // Auto-resize textareas
    $('textarea[data-auto-resize]').each(function () {
        this.setAttribute('style', 'height:' + (this.scrollHeight) + 'px;overflow-y:hidden;');
    }).on('input', function () {
        this.style.height = 'auto';
        this.style.height = (this.scrollHeight) + 'px';
    });
});

// Handle AJAX errors globally
$(document).ajaxError(function (event, xhr, settings, thrownError) {
    if (xhr.status === 401) {
        toastr.error('Your session has expired. Please log in again.');
        setTimeout(() => {
            window.location.href = '/';
        }, 2000);
    } else if (xhr.status === 403) {
        toastr.error('You do not have permission to perform this action.');
    } else if (xhr.status === 404) {
        toastr.error('The requested resource was not found.');
    } else if (xhr.status >= 500) {
        toastr.error('A server error occurred. Please try again later.');
    } else if (xhr.status !== 0) { // Ignore aborted requests
        toastr.error('An unexpected error occurred.');
    }
});

// Add loading states to buttons with data-loading attribute
$(document).on('click', '[data-loading]', function () {
    const btn = $(this);
    const loadingText = btn.data('loading');
    const originalText = btn.html();

    btn.html('<span class="spinner-border spinner-border-sm me-2"></span>' + loadingText);
    btn.prop('disabled', true);

    // Re-enable after 10 seconds as fallback
    setTimeout(() => {
        btn.html(originalText);
        btn.prop('disabled', false);
    }, 10000);
});

// Keyboard shortcuts
$(document).keydown(function (e) {
    // Ctrl/Cmd + K for search (if search exists)
    if ((e.ctrlKey || e.metaKey) && e.keyCode === 75) {
        const searchInput = $('input[type="search"], input[name="search"]').first();
        if (searchInput.length) {
            e.preventDefault();
            searchInput.focus();
        }
    }

    // Escape to close modals
    if (e.keyCode === 27) {
        $('.modal.show').modal('hide');
    }
});

// Connection status indicator for SignalR
window.connectionStatus = {
    show: function (status, message) {
        let className = 'bg-success';
        let icon = 'fa-check-circle';

        if (status === 'connecting') {
            className = 'bg-warning';
            icon = 'fa-spinner fa-spin';
        } else if (status === 'disconnected') {
            className = 'bg-danger';
            icon = 'fa-exclamation-triangle';
        }

        const statusHtml = `
            <div class="position-fixed top-0 end-0 p-3" style="z-index: 9999;">
                <div class="toast show ${className} text-white" role="alert">
                    <div class="toast-body">
                        <i class="fas ${icon} me-2"></i>
                        ${message}
                    </div>
                </div>
            </div>
        `;

        // Remove existing status
        $('.connection-status').remove();

        // Add new status
        $('body').append('<div class="connection-status">' + statusHtml + '</div>');

        // Auto-remove after 3 seconds for success messages
        if (status === 'connected') {
            setTimeout(() => {
                $('.connection-status').fadeOut();
            }, 3000);
        }
    }
};