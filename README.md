# Gateway - WebSocket Gateway Server
**Notes: Cấu hình mặc định chạy ở localhost nhé các bạn**

Gateway là một máy chủ WebSocket trung gian cho phép kết nối giữa Admin (giao diện web) và Agent (máy khách).
1. Websocket Gateway(Node.js):
+ Một máy chủ Node.js đơn giản, chạy ở một địa chỉ công khai (hoặc **localhost** để test)
+ Nó sẽ lắng nghe kết nối Websocket.
+ Nó không hiểu nội dung tin nhắn, chỉ làm nhiệm vụ "chuyển tiếp"
2. Ứng dụng C# (Windows App):
+ Dù là "server" về logic, nó sẽ hoạt động như một **client** kết nối đến Gateway
+ Nó tự "đăng ký" với Gateway: "Tôi là C# Server"
3. Ứng dụng Web (Browser)
+ Hoạt động như một **client** kết nối đến Gateway.
+ Nó tự "đăng ký" với Gateway: "Tôi là Web Client"

### Cách chạy mà không cần mở Visual Studio (Còn không follow full ở dưới)
1. File Agent.exe đã push lên Release (bên phải) rồi unzip ra xong chạy
2. Vẫn cài Node.js
3. Vào folder Gateway trong cùng **(Chuột phải -> Open in Terminal)**
4. Chạy 2 lệnh sau để cài:
```bash
npm init -y
npm install ws
```
5. Cài xong thì chạy
```bash
node app.js
```

## Cần cài đặt
- Visual Studio 2022 (Không hỗ trợ bản 2026) **(Chỉ cần cài nếu Build file)**
- Node.js 
- npm (thường đi kèm với Node.js)

## Cài đặt Node.js và npm

### Bước 1: Tải và cài đặt Node.js

1. Truy cập và tải trên: https://nodejs.org/en/download
2. Chạy file cài đặt (.msi cho Windows)

## Cài đặt thư viện npm cho Gateway

### Bước 1: Di chuyển vào thư mục Gateway

```bash
cd Gateway (Subfolder Gateway)
```

### Bước 2: Cài đặt các package cần thiết

```bash
npm init -y
npm install ws
```
Lệnh này sẽ đọc file `package.json` và tự động cài đặt tất cả các thư viện cần thiết, bao gồm:
- `ws`: Thư viện WebSocket server cho Node.js

- Sau khi cài đặt sẽ xuất hiện folder **nodes_modules**

### Khởi động Gateway

Từ thư mục `Gateway`, chạy lệnh:

```bash
node app.js
```
Khi chạy sẽ có dòng
```
Gateway đang chạy tại ws://localhost:8080
```

- Nếu tắt server thì **Ctrl + C"** hoặc tắt Terminal

## Cách kết nối

### Kết nối từ Admin (Web Interface)

1. Chạy server trước (Guide ở trên)

2. **Mở file `index.html`** 

3. **Kiểm tra kết nối:**
   - Ở góc dưới bên trái, biểu thị trạng thái "Connected" (Xanh) nếu kết nối thành công
   - Nếu hiển thị "Disconnected" (màu đỏ), kiểm tra lại Gateway server có đang chạy không

4. **Chọn Agent:**
   - Sau khi kết nối, danh sách Agent sẽ tự động hiển thị trong dropdown "Target Agent"
   - Chọn một Agent để bắt đầu điều khiển

### Kết nối từ Agent (C# Application)
1. **Build và chạy Agent:**
2. **Kiểm tra kết nối:**
   - Agent sẽ tự động kết nối đến Gateway khi khởi động
   - Nếu kết nối thành công, Agent sẽ xuất hiện trong danh sách trên Admin interface
   - Agent sẽ tự động kết nối lại nếu bị ngắt kết nối

### Nếu gặp bug
- **`Port 8080 is already in use`**: Kiểm tra có server đang chạy ko, nếu ko thì tắt trong Task Manager hoặc 
- **`Cannot find module 'ws'`**: Kiểm tra lại đã cài **node_modules** chưa

## Structure

```
Gateway/
├── Gateway/          # Server
│   ├── app.js        
│   ├── package.json  
│   └── node_modules/ # Xuất hiện sau khi cài
├── Agent/            # C# Agent Application
│   ├── AgentForm.cs  # Tính năng chính
│   └── Keylogger.cs
└── index.html        # Web Admin Interface
```


### Log trò chuyện
1. Trò chuyện chính cách làm và cấu trúc: https://aistudio.google.com/app/prompts?state=%7B%22ids%22:%5B%221Rhm36jViLMwsmU3XTFu-ClueBMzE2wfP%22%5D,%22action%22:%22open%22,%22userId%22:%22104803856014257514213%22,%22resourceKeys%22:%7B%7D%7D&usp=sharing
2. Fix vài lỗi khi chạy: https://gemini.google.com/share/1ecb9f136b03
