// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code here.

// Global functions for the travel planner
window.TravelPlanner = {
    // Copy text to clipboard
    copyToClipboard: function(text) {
        if (navigator.clipboard && window.isSecureContext) {
            return navigator.clipboard.writeText(text).then(() => {
                alert('✅ Copied to clipboard!');
            }).catch(err => {
                console.error('Failed to copy: ', err);
                this.fallbackCopyTextToClipboard(text);
            });
        } else {
            this.fallbackCopyTextToClipboard(text);
        }
    },
    
    // Fallback copy method for older browsers
    fallbackCopyTextToClipboard: function(text) {
        const textArea = document.createElement("textarea");
        textArea.value = text;
        textArea.style.top = "0";
        textArea.style.left = "0";
        textArea.style.position = "fixed";
        document.body.appendChild(textArea);
        textArea.focus();
        textArea.select();
        
        try {
            const successful = document.execCommand('copy');
            if (successful) {
                alert('✅ Copied to clipboard!');
            } else {
                alert('❌ Failed to copy');
            }
        } catch (err) {
            console.error('Fallback copy failed: ', err);
            alert('❌ Copy not supported');
        }
        
        document.body.removeChild(textArea);
    },
    
    // Print functionality
    printPage: function() {
        window.print();
    }
};
