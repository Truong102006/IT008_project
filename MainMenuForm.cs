using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace LapTrinhTrucQuangProjectTest
{
    public partial class MainMenuForm : Form
    {
        // 1. Biến lưu trữ tài nguyên
        private Bitmap _backgroundImage;
        private Panel contentPanel;      // Panel dùng để chứa màn chơi (Game Container)

        public MainMenuForm()
        {
            InitializeComponent();
            this.Text = "Mini Parkour Game";

            // Cấu hình Form chính
            this.KeyPreview = true;
            this.StartPosition = FormStartPosition.CenterScreen; // Cho game hiện giữa màn hình

            // 2. Tạo Dynamic Panel để chứa Game sau này
            contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent, // Để trong suốt lúc ở Menu
                Name = "ContentPanel"
            };

            // Thêm Panel vào Form
            this.Controls.Add(contentPanel);

            // QUAN TRỌNG: Đẩy Panel ra sau cùng để nút btnNewGame (được tạo ở Designer) nổi lên trên
            contentPanel.SendToBack();

            // Gắn các sự kiện
            this.Load += MainMenuForm_Load;
            this.FormClosing += MainMenuForm_FormClosing; // Dùng FormClosing thay vì Disposed để an toàn hơn
        }

        // --- SỰ KIỆN LOAD FORM ---
        private void MainMenuForm_Load(object sender, EventArgs e)
        {
            // Nạp hình nền Menu
            try
            {
                string imagePath = @"Images\background_menu.png";
                if (File.Exists(imagePath))
                {
                    _backgroundImage = (Bitmap)Image.FromFile(imagePath);
                    this.BackgroundImage = _backgroundImage;
                    this.BackgroundImageLayout = ImageLayout.Stretch;
                }
            }
            catch { /* Bỏ qua lỗi nếu không tìm thấy ảnh */ }

            // Đảm bảo nút New Game hiện lên
            if (btnNewGame != null)
            {
                btnNewGame.Visible = true;
                btnNewGame.BringToFront();
            }
        }

        // --- SỰ KIỆN CLICK NÚT NEW GAME ---
        private void btnNewGame_Click(object sender, EventArgs e)
        {
            StartGameLevel1();
        }

        // --- LOGIC CHUYỂN CẢNH SANG GAME ---
        private void StartGameLevel1()
        {
            // 1. Ẩn giao diện Menu
            if (btnNewGame != null) btnNewGame.Visible = false;

            // Xóa hình nền Menu đi cho nhẹ và đỡ rối mắt
            this.BackgroundImage = null;

            // 2. Cấu hình Panel chứa game
            contentPanel.BackColor = Color.Black; // Đổi nền sang đen (hoặc màu của game)
            contentPanel.Controls.Clear();        // Xóa sạch các màn chơi cũ (nếu có)
            contentPanel.BringToFront();          // Đưa Panel lên lớp trên cùng

            // 3. Khởi tạo MapLevel1 (UserControl)
            MapLevel1 gameLevel = new MapLevel1
            {
                Dock = DockStyle.Fill, // Tràn viền
                TabStop = true         // Cho phép nhận Focus
            };

            // 4. Thêm Game vào Panel
            contentPanel.Controls.Add(gameLevel);

            // 5. QUAN TRỌNG: Lấy tiêu điểm bàn phím cho Game
            gameLevel.Focus();
        }

        // --- DỌN DẸP TÀI NGUYÊN ---
        private void MainMenuForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_backgroundImage != null)
            {
                _backgroundImage.Dispose();
            }
        }

        // Hàm này do Designer tạo ra, để trống hoặc xóa đi cũng được
        private void MainMenuForm_Load_1(object sender, EventArgs e)
        {
        }
    }
}