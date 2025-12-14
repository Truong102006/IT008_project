using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace LapTrinhTrucQuangProjectTest
{
    public partial class MainMenuForm : Form
    {
        // Biến cho hình nền và Panel chứa game
        private Bitmap _backgroundImage;
        private Panel contentPanel;

        // --- CÁC BIẾN LƯU TRỮ TRẠNG THÁI GỐC ĐỂ TÍNH TỶ LỆ ---
        private Size _originalFormSize;

        // 1. Nút New Game
        private Rectangle _orgNewGameRect;
        private float _orgNewGameFontSize;

        // 3. Nút Exit (Thoát)
        private Rectangle _orgExitRect;
        private float _orgExitFontSize;

        public MainMenuForm()
        {
            InitializeComponent();
            this.Text = "Mini Parkour Game";
            this.KeyPreview = true;

            // Kích hoạt DoubleBuffered để giảm giật hình khi resize
            this.DoubleBuffered = true;

            // Tạo Panel chứa Game (ban đầu ẩn)
            contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Visible = false
            };
            this.Controls.Add(contentPanel);

            // Gán các sự kiện chính của Form
            this.Load += MainMenuForm_Load;
            this.Disposed += MainMenuForm_Disposed;
            this.Resize += MainMenuForm_Resize;

            // Gán sự kiện Click cho các nút (nếu chưa gán trong Designer)
            // Lưu ý: Code này phòng trường hợp bạn quên gán sự kiện trong bảng Properties
            if (btnNewGame != null) btnNewGame.Click += btnNewGame_Click;
        }

        private void MainMenuForm_Load(object sender, EventArgs e)
        {
            // Tải hình nền
            try
            {
                string imagePath = @"Images\background_menu.png";
                if (File.Exists(imagePath))
                {
                    _backgroundImage = (Bitmap)Image.FromFile(imagePath);
                }
            }
            catch { }

            // --- LƯU LẠI KÍCH THƯỚC GỐC LÀM CHUẨN (MỐC 1) ---
            _originalFormSize = this.ClientSize;

            // Lưu thông số nút New Game
            if (btnNewGame != null)
            {
                _orgNewGameRect = btnNewGame.Bounds;
                _orgNewGameFontSize = btnNewGame.Font.Size;
            }

            // Lưu thông số nút Exit
            if (btnExit != null)
            {
                _orgExitRect = btnExit.Bounds;
                _orgExitFontSize = btnExit.Font.Size;
            }

            // Xóa bỏ sự kiện thừa do Designer sinh ra (nếu có)
            this.Load -= MainMenuForm_Load_1;
        }

        // --- SỰ KIỆN RESIZE: TÍNH TOÁN VÀ CO GIÃN NÚT ---
        private void MainMenuForm_Resize(object sender, EventArgs e)
        {
            this.Invalidate(); // Vẽ lại hình nền ngay lập tức

            // Nếu form chưa load xong hoặc bị thu nhỏ xuống taskbar thì không làm gì
            if (_originalFormSize.Width == 0 || _originalFormSize.Height == 0) return;

            // 1. Tính tỷ lệ thay đổi so với kích thước gốc
            float xRatio = (float)this.ClientSize.Width / _originalFormSize.Width;
            float yRatio = (float)this.ClientSize.Height / _originalFormSize.Height;

            // 2. Cập nhật kích thước cho từng nút
            UpdateControlSize(btnNewGame, _orgNewGameRect, _orgNewGameFontSize, xRatio, yRatio);
            UpdateControlSize(btnExit, _orgExitRect, _orgExitFontSize, xRatio, yRatio);
        }

        // Hàm hỗ trợ tính toán kích thước mới (Dùng chung cho cả 3 nút)
        private void UpdateControlSize(Button btn, Rectangle originalRect, float originalFontSize, float xRatio, float yRatio)
        {
            if (btn == null) return;

            // Tính vị trí và kích thước mới dựa trên tỷ lệ
            int newX = (int)(originalRect.X * xRatio);
            int newY = (int)(originalRect.Y * yRatio);
            int newWidth = (int)(originalRect.Width * xRatio);
            int newHeight = (int)(originalRect.Height * yRatio);

            // Áp dụng vào nút
            btn.Bounds = new Rectangle(newX, newY, newWidth, newHeight);
        }

        // Vẽ hình nền thủ công để tối ưu hiệu năng
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Chỉ vẽ hình nền khi các nút Menu còn hiện (Tức là chưa vào game)
            // Chúng ta kiểm tra btnNewGame như một đại diện
            if (btnNewGame != null && btnNewGame.Visible && _backgroundImage != null)
            {
                e.Graphics.DrawImage(_backgroundImage, this.ClientRectangle);
            }
        }

        // --- CÁC HÀM XỬ LÝ SỰ KIỆN CLICK ---

        private void btnNewGame_Click(object sender, EventArgs e)
        {
            StartGameLevel1();
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        // --- LOGIC VÀO GAME ---
        private void StartGameLevel1()
        {
            // 1. Tạm dừng layout
            this.SuspendLayout();

            // 2. Khởi tạo màn chơi
            MapLevel1 gameLevel = new MapLevel1
            {
                Dock = DockStyle.Fill,
                TabStop = true,
                Name = "GameLevel1"
            };

            // 3. Đưa game vào Panel
            contentPanel.Controls.Clear();
            contentPanel.Controls.Add(gameLevel);

            // 4. Ẩn TOÀN BỘ các nút Menu
            SetMenuButtonsVisible(false);

            // Xóa hình nền menu để giải phóng bộ nhớ
            _backgroundImage?.Dispose();
            _backgroundImage = null;

            // 5. Hiện Panel game lên
            contentPanel.Visible = true;
            contentPanel.BringToFront();

            // 6. Hoàn tất
            this.ResumeLayout();
            gameLevel.Focus();
        }

        // Hàm tiện ích để Ẩn/Hiện đồng loạt 3 nút
        private void SetMenuButtonsVisible(bool isVisible)
        {
            if (btnNewGame != null) btnNewGame.Visible = isVisible;
            if (btnExit != null) btnExit.Visible = isVisible;
        }

        private void MainMenuForm_Disposed(object sender, EventArgs e)
        {
            _backgroundImage?.Dispose();
        }

        // Hàm rỗng do Designer sinh ra (giữ lại để tránh lỗi)
        private void MainMenuForm_Load_1(object sender, EventArgs e) { }
        public void ReturnToMainMenu()
        {
            // 1. Ẩn Panel chứa game và xóa User Control để giải phóng RAM
            contentPanel.Visible = false;

            // Lưu ý: Lệnh Clear() này sẽ tự động gọi Dispose() của MapLevel1 
            // -> Timer trong game sẽ dừng nhờ lệnh Dispose bạn đã viết bên MapLevel1
            contentPanel.Controls.Clear();

            // 2. Hiện lại các nút Menu (New Game, Exit)
            SetMenuButtonsVisible(true);

            // 3. KHẮC PHỤC LỖI MÀN HÌNH TRẮNG: Tải lại hình nền
            // (Vì lúc vào game bạn đã dùng _backgroundImage.Dispose() nên giờ phải load lại)
            try
            {
                string imagePath = @"Images\background_menu.png";
                if (File.Exists(imagePath))
                {
                    _backgroundImage = (Bitmap)Image.FromFile(imagePath);
                }
            }
            catch { }

            // 4. Yêu cầu vẽ lại Form ngay lập tức
            this.Invalidate();
        }
    }
}