using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;

namespace LapTrinhTrucQuangProjectTest
{
    // Đây là Form chính (cửa sổ chạy độc lập)
    public partial class MainMenuForm : Form
    {
        private Bitmap _backgroundImage; // Biến lưu trữ ảnh nền Menu
        private Panel contentPanel;      // Panel chứa nội dung động (Menu/Game)

        public MainMenuForm()
        {
            InitializeComponent();
            this.Text = "Mini Parkour Game";

            // QUAN TRỌNG: Form chính phải bật KeyPreview
            this.KeyPreview = true;

            // Khởi tạo Panel chứa nội dung game
            contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent // QUAN TRỌNG: Phải là Transparent khi ở Menu
            };
            this.Controls.Add(contentPanel);

            // ĐẨY Panel chứa nội dung ra sau, để Form Background và các nút hiển thị
            contentPanel.SendToBack();

            // Gắn sự kiện Load đúng
            this.Load += MainMenuForm_Load;
            this.Disposed += MainMenuForm_Disposed;

            // Khởi động bằng cách hiển thị Menu chính
            ShowMainMenu();
        }

        // TẢI ẢNH NỀN TỪ Ổ ĐĨA
        private void MainMenuForm_Load(object sender, EventArgs e)
        {
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
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi nạp ảnh nền: " + ex.Message);
            }

            // XÓA SỰ KIỆN LOAD DƯ THỪA ĐƯỢC GẮN TRONG DESIGNER
            this.Load -= MainMenuForm_Load_1;
        }
        private void MainMenuForm_Disposed(object sender, EventArgs e)
        {
            _backgroundImage?.Dispose();
        }

        // --- Logic Hiển thị Menu ---
        private void ShowMainMenu()
        {
            contentPanel.Controls.Clear();

            // Đảm bảo Form hiển thị ảnh nền menu
            this.BackgroundImage = _backgroundImage;

            // Đảm bảo nút Menu (btnNewGame) hiển thị trên cùng
            if (this.btnNewGame != null)
            {
                this.btnNewGame.Visible = true;
                this.btnNewGame.BringToFront();
            }

            // Đặt Panel chứa nội dung ra sau
            contentPanel.SendToBack();
        }

        // --- Logic Hiển thị Game ---
        private void ShowGameLevel1()
        {
            // 1. Ẩn nút Menu
            if (this.btnNewGame != null)
            {
                this.btnNewGame.Visible = false;
            }

            // 2. XÓA ảnh nền Form
            this.BackgroundImage = null;

            // 3. Đổi Panel sang màu đen (hoặc màu tối) để che nền desktop và chuẩn bị chứa game
            contentPanel.BackColor = Color.Black;

            // 4. Đẩy Panel chứa game lên trước
            contentPanel.BringToFront();

            // 5. Tạo và nhúng User Control Game (MapLevel1)
            MapLevel1 gameScreen = new MapLevel1
            {
                Dock = DockStyle.Fill,
                TabStop = true,
                Name = "GameLevel1"
            };

            contentPanel.Controls.Add(gameScreen);
            gameScreen.Focus();
        }

        // --- Xử lý sự kiện khi nhấn nút ---
        private void btnNewGame_Click(object sender, EventArgs e)
        {
            ShowGameLevel1();
        }

        // HÀM LOAD DƯ THỪA ĐỂ TRỐNG (ĐƯỢC GẮN TRONG DESIGNER)
        private void MainMenuForm_Load_1(object sender, EventArgs e)
        {

        }
    }
}