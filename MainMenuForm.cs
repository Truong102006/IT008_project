using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace LapTrinhTrucQuangProjectTest
{
    public partial class MainMenuForm : Form
    {
        private Bitmap _backgroundImage;
        private Panel contentPanel;
        // các biến dùng cho bật full màn hình:
        private bool isFullscreen = false;
        private FormBorderStyle oldStyle = FormBorderStyle.Sizable;
        private FormWindowState oldState = FormWindowState.Normal;

        public MainMenuForm()
        {
            InitializeComponent();
            this.Text = "Mini Parkour Game";
            this.KeyPreview = true;

            // 1. Kích hoạt DoubleBuffered để giảm giật cho toàn Form
            this.DoubleBuffered = true;

            // 2. Tạo Panel chứa Game
            contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                // QUAN TRỌNG: Không dùng Transparent nữa, dùng màu mặc định
                BackColor = Color.Transparent,
                // QUAN TRỌNG NHẤT: Ẩn nó đi ngay từ đầu
                Visible = false
            };

            this.Controls.Add(contentPanel);

            // Các sự kiện
            this.Load += MainMenuForm_Load;
            this.Disposed += MainMenuForm_Disposed;
            this.Resize += MainMenuForm_Resize;
        }

        private void MainMenuForm_Load(object sender, EventArgs e)
        {
            try
            {
                string imagePath = @"Images\background_menu.png";
                if (File.Exists(imagePath))
                {
                    _backgroundImage = (Bitmap)Image.FromFile(imagePath);
                }
            }
            catch { }

            // Xóa sự kiện dư thừa
            this.Load -= MainMenuForm_Load_1;
        }
        private void ToggleFullscreen()
        {
            isFullscreen = !isFullscreen;

            if (isFullscreen)
            {
                // Lưu trạng thái cũ để lát quay lại
                oldStyle = this.FormBorderStyle;
                oldState = this.WindowState;

                // Chuyển sang Fullscreen Borderless
                this.FormBorderStyle = FormBorderStyle.None;
                this.WindowState = FormWindowState.Maximized;
            }
            else
            {
                // Quay về cửa sổ bình thường
                this.FormBorderStyle = oldStyle;
                this.WindowState = oldState;
            }
        }
        // Tự vẽ hình nền để mượt hơn (thay vì dùng BackgroundImage)
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Chỉ vẽ hình nền khi đang ở Menu (tức là nút New Game còn hiện)
            if (this.btnNewGame != null && this.btnNewGame.Visible && _backgroundImage != null)
            {
                e.Graphics.DrawImage(_backgroundImage, this.ClientRectangle);
            }
        }

        // Logic co giãn nút New Game (Code cũ của bạn vẫn tốt)
        // Trong MainMenuForm.cs

        private void MainMenuForm_Resize(object sender, EventArgs e)
        {
            this.Invalidate();
            if (btnNewGame != null && btnNewGame.Visible)
            {
                // 1. Giữ nguyên công thức chiều rộng
                int newWidth = this.ClientSize.Width / 8;
                int newHeight = newWidth / 3;

                // Giới hạn nhỏ nhất
                if (newWidth < 120) { newWidth = 120; newHeight = 35; }

                btnNewGame.Size = new Size(newWidth, newHeight);

                // 2. Căn giữa theo chiều ngang (Giữ nguyên)
                btnNewGame.Left = (this.ClientSize.Width - btnNewGame.Width) / 2;

                //3. SỬA LẠI VỊ TRÍ DỌC (TOP)

                // Cũ (Bị thấp): 
                // btnNewGame.Top = (this.ClientSize.Height - btnNewGame.Height) / 2;

                // Mới (Cao hơn, đúng thiết kế):
                // Đặt nút ở vị trí 35% chiều cao màn hình (0.35)
                // Nếu muốn cao hơn nữa thì giảm xuống 0.3, muốn thấp hơn thì tăng lên 0.4
                btnNewGame.Top = (int)(this.ClientSize.Height * 0.27);

                // 4. Chỉnh cỡ chữ (Giữ nguyên)
                float fontSize = newHeight / 3.2f;
                if (fontSize > 6)
                    btnNewGame.Font = new Font(btnNewGame.Font.FontFamily, fontSize, FontStyle.Bold);
            }
        }

        private void MainMenuForm_Disposed(object sender, EventArgs e)
        {
            _backgroundImage?.Dispose();
        }

        // --- HÀM START GAME ĐÃ ĐƯỢC SỬA ---
        private void StartGameLevel1()
        {
            // 1. Tạm dừng vẽ giao diện
            this.SuspendLayout();

            // 2. Khởi tạo Game (Lúc này contentPanel vẫn đang Visible = false nên người dùng không thấy màn hình đen/trắng)
            MapLevel1 gameLevel = new MapLevel1
            {
                Dock = DockStyle.Fill,
                TabStop = true,
                Name = "GameLevel1"
            };

            // 3. Dọn dẹp và Thêm Game vào Panel
            contentPanel.Controls.Clear();
            contentPanel.Controls.Add(gameLevel);

            // 4. Ẩn nút New Game & Xóa hình nền Menu
            if (btnNewGame != null)
            {
                btnNewGame.Visible = false;
            }
            // Xóa hình nền để giải phóng RAM và tránh vẽ đè bên dưới
            _backgroundImage?.Dispose();
            _backgroundImage = null;

            // 5. BƯỚC QUAN TRỌNG NHẤT: Bật Panel hiện lên
            // Lúc này Game đã load xong rồi, nên nó hiện ra ngay lập tức
            contentPanel.Visible = true;
            contentPanel.BringToFront();

            // 6. Cho phép vẽ lại
            this.ResumeLayout();

            // 7. Focus vào game
            gameLevel.Focus();
        }

        private void btnNewGame_Click(object sender, EventArgs e)
        {
            StartGameLevel1();
        }

        private void MainMenuForm_Load_1(object sender, EventArgs e) { }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // 1. Nhấn phím F -> Bật/Tắt toàn màn hình
            if (keyData == Keys.F)
            {
                ToggleFullscreen();
                return true; // Báo hiệu đã xử lý xong phím này
            }

            // 2. Nhấn phím ESC -> Thoát toàn màn hình (chỉ hoạt động nếu đang Fullscreen)
            if (keyData == Keys.Escape && isFullscreen)
            {
                ToggleFullscreen();
                return true;
            }

            // Trả về xử lý mặc định cho các phím khác (di chuyển, nhảy...)
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}