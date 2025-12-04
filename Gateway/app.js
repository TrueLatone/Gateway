// server.js - WebSocket Gateway
const WebSocket = require('ws');

// Cho phép gói tin lớn (50MB) để truyền ảnh
const wss = new WebSocket.Server({ port: 8080, maxPayload: 50 * 1024 * 1024 });

const clients = new Map(); // Lưu: ID -> { ws, role }

console.log("Gateway đang chạy tại ws://localhost:8080");

wss.on('connection', (ws) => {
    let myId = null;
    let myRole = null;

    ws.on('message', (message) => {
        try {
            const packet = JSON.parse(message);

            // 1. ĐĂNG KÝ (REGISTER)
            if (packet.type === 'REGISTER') {
                myId = packet.id;
                myRole = packet.role; // "AGENT" hoặc "ADMIN"

                // Lưu client
                clients.set(myId, { ws, role: myRole });
                console.log(`[+] ${myRole} connected: ${myId}`);

                // Nếu là Admin mới vào, gửi danh sách Agent ngay
                if (myRole === 'ADMIN') sendAgentListTo(ws);
                // Nếu là Agent mới vào, báo cho tất cả Admin cập nhật danh sách
                if (myRole === 'AGENT') broadcastAgentList();
                return;
            }

            // 2. CHUYỂN TIẾP (ROUTE)
            // Cấu trúc packet: { targetId: "...", payload: { cmd: "...", data: "..." } }
            if (packet.targetId && clients.has(packet.targetId)) {
                const target = clients.get(packet.targetId);
                if (target.ws.readyState === WebSocket.OPEN) {
                    // Gói tin gửi đi sẽ kèm thêm "from" để bên nhận biết ai gửi
                    const forwardPacket = {
                        from: myId,
                        payload: packet.payload
                    };
                    target.ws.send(JSON.stringify(forwardPacket));
                }
            }
        } catch (e) { console.error("Error:", e.message); }
    });

    ws.on('close', () => {
        if (myId) {
            console.log(`[-] Disconnected: ${myId}`);
            clients.delete(myId);
            if (myRole === 'AGENT') broadcastAgentList();
        }
    });
});

function getAgentList() {
    const agents = [];
    clients.forEach((val, key) => {
        if (val.role === 'AGENT') agents.push(key);
    });
    return agents;
}

function sendAgentListTo(ws) {
    if (ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify({ type: 'AGENT_LIST', data: getAgentList() }));
    }
}

function broadcastAgentList() {
    const list = getAgentList();
    const packet = JSON.stringify({ type: 'AGENT_LIST', data: list });
    clients.forEach((val) => {
        if (val.role === 'ADMIN' && val.ws.readyState === WebSocket.OPEN) {
            val.ws.send(packet);
        }
    });
}