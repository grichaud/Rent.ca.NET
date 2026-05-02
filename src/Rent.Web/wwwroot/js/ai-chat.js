(function () {
    "use strict";

    const root = document.getElementById("ai-chat-root");
    if (!root) return;

    const trigger = document.getElementById("ai-chat-trigger");
    const panel = document.getElementById("ai-chat-panel");
    const closeBtn = document.getElementById("ai-chat-close");
    const clearBtn = document.getElementById("ai-chat-clear");
    const messagesEl = document.getElementById("ai-chat-messages");
    const emptyEl = document.getElementById("ai-chat-empty");
    const form = document.getElementById("ai-chat-form");
    const input = document.getElementById("ai-chat-input");
    const sendBtn = document.getElementById("ai-chat-send");
    const statusEl = document.getElementById("ai-chat-status");

    let conversationId = null;
    let isStreaming = false;
    let activeAbortController = null;
    let conversationLoaded = false;

    function getCsrfToken() {
        const inp = document.querySelector('#rentca-csrf input[name="__RequestVerificationToken"]');
        return inp ? inp.value : "";
    }

    function showPanel() {
        panel.classList.remove("hidden");
        panel.classList.add("flex");
        if (!conversationLoaded) {
            conversationLoaded = true;
            loadActiveConversation();
        }
        setTimeout(() => input.focus(), 50);
    }

    function hidePanel() {
        panel.classList.add("hidden");
        panel.classList.remove("flex");
    }

    function setStatus(text) { statusEl.textContent = text; }
    const onlineText = (statusEl && statusEl.dataset.onlineText) || "Online";
    const thinkingText = (statusEl && statusEl.dataset.thinkingText) || "Thinking...";

    function refreshEmptyState() {
        const hasMessages = messagesEl.children.length > 0;
        if (hasMessages) {
            emptyEl.classList.add("hidden");
            messagesEl.classList.remove("hidden");
        } else {
            emptyEl.classList.remove("hidden");
            messagesEl.classList.add("hidden");
        }
    }

    function escapeHtml(s) {
        return s.replace(/[&<>"']/g, c => ({
            "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;", "'": "&#39;"
        })[c]);
    }

    function isSafeUrl(url) {
        if (!url) return false;
        if (url.startsWith("/")) return true;
        if (/^https?:\/\//i.test(url)) return true;
        return false;
    }

    function renderMarkdown(text) {
        let html = escapeHtml(text);
        html = html.replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>");
        html = html.replace(/\[([^\]]+)\]\(([^)]+)\)/g, (m, label, url) => {
            if (!isSafeUrl(url)) return escapeHtml(label);
            const safeUrl = escapeHtml(url);
            const isExternal = /^https?:\/\//i.test(url);
            const target = isExternal ? ' target="_blank" rel="noopener"' : "";
            return `<a href="${safeUrl}"${target} class="text-brand-600 dark:text-brand-400 underline hover:no-underline">${escapeHtml(label)}</a>`;
        });
        const lines = html.split("\n");
        let inList = false;
        const out = [];
        for (const line of lines) {
            const m = line.match(/^\s*-\s+(.*)$/);
            if (m) {
                if (!inList) { out.push("<ul class='list-disc pl-5 space-y-0.5'>"); inList = true; }
                out.push(`<li>${m[1]}</li>`);
            } else {
                if (inList) { out.push("</ul>"); inList = false; }
                if (line.trim().length > 0) out.push(`<p>${line}</p>`);
            }
        }
        if (inList) out.push("</ul>");
        return out.join("");
    }

    function appendMessage(role, content) {
        const wrapper = document.createElement("div");
        wrapper.className = role === "user"
            ? "flex justify-end"
            : "flex justify-start";

        const bubble = document.createElement("div");
        bubble.className = role === "user"
            ? "max-w-[85%] rounded-2xl rounded-br-sm px-3 py-2 text-sm text-white shadow-sm"
            : "max-w-[85%] rounded-2xl rounded-bl-sm px-3 py-2 text-sm text-slate-800 dark:text-white/90 bg-white/70 dark:bg-white/10 border border-white/20 dark:border-white/10 shadow-sm space-y-1";
        if (role === "user") {
            bubble.style.background = "linear-gradient(135deg, #338dff 0%, #06b6d4 100%)";
        }

        bubble.dataset.role = role;
        if (role === "assistant") {
            bubble.innerHTML = renderMarkdown(content);
        } else {
            bubble.textContent = content;
        }

        wrapper.appendChild(bubble);
        messagesEl.appendChild(wrapper);
        refreshEmptyState();
        messagesEl.scrollTop = messagesEl.scrollHeight;
        return bubble;
    }

    async function loadActiveConversation() {
        try {
            const resp = await fetch("/api/ai/conversations/active", { credentials: "same-origin" });
            if (!resp.ok) return;
            const data = await resp.json();
            if (!data.conversation) {
                refreshEmptyState();
                return;
            }
            conversationId = data.conversation.id;
            for (const msg of data.conversation.messages) {
                if (msg.role === "user" || msg.role === "assistant") {
                    appendMessage(msg.role, msg.content);
                }
            }
            refreshEmptyState();
        } catch (e) {
            console.warn("Failed to load conversation", e);
        }
    }

    async function sendMessage(text) {
        if (isStreaming) return;
        const trimmed = text.trim();
        if (!trimmed) return;

        appendMessage("user", trimmed);
        input.value = "";
        autoresizeInput();
        setStatus(thinkingText);
        sendBtn.disabled = true;
        isStreaming = true;

        const assistantBubble = appendMessage("assistant", "");
        let assistantText = "";

        const payload = {
            conversationId: conversationId,
            message: trimmed,
            locale: root.dataset.locale || "en",
            context: {
                currentPage: root.dataset.currentPage || null,
                currentCity: root.dataset.currentCity || null,
                currentPropertyId: root.dataset.currentPropertyId || null
            }
        };

        activeAbortController = new AbortController();

        try {
            const resp = await fetch("/api/ai/chat", {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Content-Type": "application/json",
                    "Accept": "text/event-stream",
                    "RequestVerificationToken": getCsrfToken()
                },
                body: JSON.stringify(payload),
                signal: activeAbortController.signal
            });

            if (!resp.ok) {
                if (resp.status === 429) {
                    assistantBubble.innerHTML = renderMarkdown("Slow down — too many requests. Try again in a few minutes.");
                } else {
                    assistantBubble.innerHTML = renderMarkdown("Sorry, I couldn't reach the assistant. Please try again.");
                }
                return;
            }

            const reader = resp.body.getReader();
            const decoder = new TextDecoder();
            let buffer = "";

            while (true) {
                const { value, done } = await reader.read();
                if (done) break;
                buffer += decoder.decode(value, { stream: true });

                let idx;
                while ((idx = buffer.indexOf("\n\n")) !== -1) {
                    const rawEvent = buffer.slice(0, idx);
                    buffer = buffer.slice(idx + 2);

                    let eventName = "message";
                    let dataLine = "";
                    for (const line of rawEvent.split("\n")) {
                        if (line.startsWith("event:")) eventName = line.slice(6).trim();
                        else if (line.startsWith("data:")) dataLine += line.slice(5).trim();
                    }
                    if (!dataLine) continue;

                    let parsed;
                    try { parsed = JSON.parse(dataLine); } catch { continue; }

                    if (eventName === "message" && parsed.content) {
                        assistantText += parsed.content;
                        assistantBubble.innerHTML = renderMarkdown(assistantText);
                        messagesEl.scrollTop = messagesEl.scrollHeight;
                    } else if (eventName === "done") {
                        if (parsed.conversationId) conversationId = parsed.conversationId;
                    } else if (eventName === "error") {
                        assistantText = parsed.message || "Something went wrong.";
                        assistantBubble.innerHTML = renderMarkdown(assistantText);
                    }
                }
            }
        } catch (e) {
            if (e.name !== "AbortError") {
                console.error(e);
                assistantBubble.innerHTML = renderMarkdown("Sorry, the assistant is unavailable. Please try again.");
            }
        } finally {
            isStreaming = false;
            activeAbortController = null;
            setStatus(onlineText);
            sendBtn.disabled = false;
            input.focus();
        }
    }

    function autoresizeInput() {
        input.style.height = "auto";
        input.style.height = Math.min(input.scrollHeight, 128) + "px";
    }

    trigger.addEventListener("click", () => {
        if (panel.classList.contains("hidden")) showPanel();
        else hidePanel();
    });

    closeBtn.addEventListener("click", hidePanel);

    clearBtn.addEventListener("click", async () => {
        if (isStreaming && activeAbortController) activeAbortController.abort();
        try {
            await fetch("/api/ai/conversations/new", {
                method: "POST",
                credentials: "same-origin",
                headers: { "RequestVerificationToken": getCsrfToken() }
            });
        } catch { /* ignore */ }
        conversationId = null;
        messagesEl.innerHTML = "";
        refreshEmptyState();
    });

    form.addEventListener("submit", (e) => {
        e.preventDefault();
        sendMessage(input.value);
    });

    input.addEventListener("input", autoresizeInput);
    input.addEventListener("keydown", (e) => {
        if (e.key === "Enter" && !e.shiftKey) {
            e.preventDefault();
            sendMessage(input.value);
        }
    });

    document.querySelectorAll("#ai-chat-empty [data-suggestion]").forEach(btn => {
        btn.addEventListener("click", () => sendMessage(btn.textContent.trim()));
    });

    document.addEventListener("keydown", (e) => {
        if (e.key === "Escape" && !panel.classList.contains("hidden")) hidePanel();
    });

    refreshEmptyState();
})();
