// AI Assistant Chat functionality
(function() {
    const chatMessages = document.getElementById('chatMessages');
    const chatInput = document.getElementById('chatInput');
    const sendBtn = document.getElementById('sendChatBtn');
    const sendBtnText = document.getElementById('sendBtnText');
    const sendBtnSpinner = document.getElementById('sendBtnSpinner');

    if (!chatMessages || !chatInput || !sendBtn) return;

    let connection = null;
    let isConnecting = false;

    async function ensureConnection() {
        if (connection && connection.state === signalR.HubConnectionState.Connected) {
            return connection;
        }

        if (isConnecting) {
            return null;
        }

        isConnecting = true;
        connection = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/assistantChat")
            .withAutomaticReconnect()
            .build();

        connection.on("StreamStart", function() {
            const msgDiv = document.createElement('div');
            msgDiv.className = 'alert alert-secondary mb-2 assistant-message';
            msgDiv.id = 'streaming-message';
            msgDiv.innerHTML = '<strong>🤖 Assistant:</strong> <span class="message-content"></span>';
            chatMessages.appendChild(msgDiv);
            chatMessages.scrollTop = chatMessages.scrollHeight;
        });

        connection.on("StreamChunk", function(chunk) {
            const streamingMsg = document.getElementById('streaming-message');
            if (streamingMsg) {
                const content = streamingMsg.querySelector('.message-content');
                content.textContent += chunk;
                chatMessages.scrollTop = chatMessages.scrollHeight;
            }
        });

        connection.on("StreamEnd", function(fullResponse) {
            const streamingMsg = document.getElementById('streaming-message');
            if (streamingMsg) {
                streamingMsg.removeAttribute('id');
            }
            enableInput();
        });

        connection.on("Error", function(errorMessage) {
            const errorDiv = document.createElement('div');
            errorDiv.className = 'alert alert-danger mb-2';
            errorDiv.innerHTML = `<strong>❌ Error:</strong> ${errorMessage}`;
            chatMessages.appendChild(errorDiv);
            chatMessages.scrollTop = chatMessages.scrollHeight;
            enableInput();
        });

        try {
            await connection.start();
            console.log("SignalR Connected");
        } catch (err) {
            console.error("SignalR Connection Error:", err);
            alert("Failed to connect to chat service. Please refresh the page.");
        } finally {
            isConnecting = false;
        }

        return connection;
    }

    function disableInput() {
        chatInput.disabled = true;
        sendBtn.disabled = true;
        sendBtnText.classList.add('d-none');
        sendBtnSpinner.classList.remove('d-none');
    }

    function enableInput() {
        chatInput.disabled = false;
        sendBtn.disabled = false;
        sendBtnText.classList.remove('d-none');
        sendBtnSpinner.classList.add('d-none');
    }

    async function sendMessage() {
        const message = chatInput.value.trim();
        if (!message) return;

        const conn = await ensureConnection();
        if (!conn) {
            alert("Connecting to chat service...");
            return;
        }

        const userDiv = document.createElement('div');
        userDiv.className = 'alert alert-primary mb-2 user-message';
        userDiv.innerHTML = `<strong>You:</strong> ${message}`;
        chatMessages.appendChild(userDiv);
        chatMessages.scrollTop = chatMessages.scrollHeight;

        disableInput();
        chatInput.value = '';

        const userId = 'user-' + Date.now();

        try {
            await conn.invoke("SendMessage", userId, message, '', 'Destination', 5, 2, 1000);
        } catch (err) {
            console.error("Error sending message:", err);
            const errorDiv = document.createElement('div');
            errorDiv.className = 'alert alert-danger mb-2';
            errorDiv.innerHTML = `<strong>❌ Error:</strong> Failed to send message.`;
            chatMessages.appendChild(errorDiv);
            enableInput();
        }
    }

    sendBtn.addEventListener('click', sendMessage);
    chatInput.addEventListener('keypress', function(e) {
        if (e.key === 'Enter') {
            sendMessage();
        }
    });

    document.querySelectorAll('[data-tab="chat"]').forEach(btn => {
        btn.addEventListener('click', function() {
            if (!connection) {
                ensureConnection();
            }
        });
    });
})();
