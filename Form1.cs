using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace LapTrinhTrucQuangProjectTest
{
    public partial class Form1 : Form
    {
        // ===== GAME =====
        Bitmap platformImg;
        Bitmap backgroundImg; // biến chứa ảnh nền (GIF hoặc PNG đều được)
        Bitmap gameOverImg;
        bool goLeft, goRight, jumping, onGround, levelTransitioning;
        int jumpSpeed = 0, force = 20, gravity = 8, playerSpeed = 3, currentLevel = 1;
        int score = 0;
        int maxHealth = 100;     // Máu tối đa
        int currentHealth = 100; // Máu hiện tại
        bool isGameOver = false;
        bool isSubmerged = false;
        bool isCollected = false;
        int startX;
        int startY;
        Rectangle player, door;
        List<Rectangle> platforms = new List<Rectangle>();
        List<Rectangle> coin = new List<Rectangle>();
        List<Tile> tiles = new List<Tile>();
        List<Enemy> enemies = new List<Enemy>();
        Dictionary<string, Bitmap> tileAssets = new Dictionary<string, Bitmap>(); // Khai báo Kho chứa (Dictionary) chứa các ô tilesets, Key (string) là Tag của ô, Value (Bitmap) là hình ảnh của ô        
        Timer gameTimer = new Timer();

        readonly int baseWidth = 800, baseHeight = 500;
        float scaleX = 1f, scaleY = 1f;

        // ghi nhớ đang đứng trên bệ nào (chỉ số trong list platforms). null = đang trên không
        int? groundedIndex = null;


        // Tinh chỉnh hitbox (px): co bớt trên/dưới + trước/sau (mặt trước co nhiều hơn)
        const int HB_TOP = 11, HB_BOTTOM = 1;
        const int HB_BACK = 2, HB_FRONT = 18;      

        // ===== ANIM ===== (chỉ Run & Jump)
        enum AnimState { Idle, Run, Jump }        

        // Khuôn mẫu cho ô gạch/địa hình mới:
        class Tile
        {
            public Rectangle Rect; // Vị trí và kích thước
            public string Type;    // Loại (tag), ví dụ: "tile_1", "tile_2"

            public Tile(int x, int y, int w, int h, string type)
            {
                Rect = new Rectangle(x, y, w, h);
                Type = type;
            }
        }
        class Enemy
        {
            public Rectangle Rect;
            public int Speed = 1;
            public bool FacingRight = false; // Mặc định đi sang trái
            public bool IsDead = false;      // Trạng thái sống/chết
            //Thêm chỉ số cho Boss 
            public int MaxHP = 1;     // Máu tối đa (Quái thường = 1, Boss = 5)
            public int CurrentHP = 1; // Máu hiện tại
            public bool IsBoss = false; // Đánh dấu đây có phải Boss không
            public Enemy(int x, int y, int w, int h)
            {
                Rect = new Rectangle(x, y, w, h);
            }
        }
        // Thuộc tính ảo: Tự động nối 2 danh sách lại mỗi khi được gọi, dùng thuộc tính AllSolids này để gộp các thuộc tính của tiles và platforms lại với nhau để xử lí va chạm vì chúng có chung tính chất đều là vật thể rắn trong game
        private IEnumerable<Rectangle> Solid
        {
            get
            {
                foreach (var p in platforms) yield return p;

                foreach (var t in tiles)
                {
                    // Chỉ tính là vật thể rắn nếu tag không bắt đầu bằng "water_","deco_","trap_"
                    if (!t.Type.StartsWith("water_") && !t.Type.StartsWith("deco_") && !t.Type.StartsWith("trap_"))
                    {
                        yield return t.Rect;
                    }
                }
            }
        }
        class SpriteAnim
        {
            public Bitmap Sheet; // hình tổng thể của toàn bộ các khung hình cần cho animation
            public List<Rectangle> Frames = new List<Rectangle>(); // hình của mỗi khung hình được cắt ra từ Sheet
            public int FrameIndex;
            public double FPS = 10;
            public bool Loop = true;
            double _accum; //lưu trữ khoảng thời gian mà máy tính đã chạy được để đủ thời gian sang khung hình tiếp theo

            public void Reset() { _accum = 0; FrameIndex = 0; }

            public void Update(double dt) // dt là thời gian mà mỗi lần update máy đã chạy được
            {
                if (Sheet == null || Frames.Count == 0 || FPS <= 0) return;
                _accum += dt; // mỗi lần update thì cộng số thời gian đã chạy bởi máy vào _accum
                double spf = 1.0 / FPS; // thời gian để chiếu 1 khung hình ( 1.0 / 10FPS = 0.1s-> mỗi 0.1s chiếu 1 khung hình nếu muốn 10FPS )
                while (_accum >= spf) // nếu máy đã update _accum lên đủ thời gian để hiện ra hình animation tiếp theo ( _accum = 0.1s thì đủ chạy 1 khung hình )
                {
                    _accum -= spf; // nếu đủ hoặc dư thì quay _accum lại khoảng bắt đầu và giữ phần thời gian đã được update dư ra để tính cho lần nạp hình animation tiếp theo (_accum = 0.12s thì đủ chạy 1 khung hình và quay về 0.02s)
                    int next = FrameIndex + 1;
                    FrameIndex = (next >= Frames.Count) ? (Loop ? 0 : Frames.Count - 1) : next; // nếu chưa hết tổng số frame trong 1 lần lặp thì chạy frame tiếp theo ( next ) còn nếu đã hết tổng số frame thì nếu loop bằng true thì quay về frame 0 ( chiếu lại từ đầu ) còn nếu loop bằng false thì đứng yên ở frame cuối cùng
                }
            }

            public void Draw(Graphics g, Rectangle hitbox, bool facingRight)
            {
                if (Sheet == null || Frames.Count == 0) return;
                Rectangle src = Frames[FrameIndex]; // lấy thông tin tọa độ của frame có số thứ tự [FrameIndex] để lưu vào src. src chứa tọa độ của nhân vật ở trạng thái hiện tại.
                DrawUtil.DrawRegion(g, Sheet, src, hitbox, facingRight); // dùng DrawRegion của class DrawUtil ở dưới để lấy Sheet, cắt vùng src, vẽ src đó vào vị trí hitbox bằng cây cọ g, lật hình nếu facingRight = false.
            }
        }


        static class DrawUtil
        {
            public static void DrawRegion(Graphics g, Bitmap sheet, Rectangle src, Rectangle hitbox, bool facingRight) // cắt hình src từ sheet và vẽ nó vào khu vực hitbox
            {
                float sx = (float)hitbox.Width / src.Width;
                float sy = (float)hitbox.Height / src.Height;
                float scale = Math.Min(sx, sy); // tính tỉ lệ xem khung hitbox to gấp bao nhiêu lần khung ảnh src. sau đó chọn tỉ lệ chiều dài hay chiều rộng bé hơn để đảm bảo hình ảnh src nằm trọn trong hitbox mà không bị méo.

                int w = (int)(src.Width * scale); // dùng tỉ lệ đã tính đó để tính kích thước dài, rộng thật của hình sau khi đã phóng to hoặc thu nhỏ theo tỉ lệ scale
                int h = (int)(src.Height * scale);
                int x = hitbox.X + (hitbox.Width - w) / 2;
                int y = hitbox.Bottom - h; // dùng kích thước w,h của hình src để căn cho nhân vật nằm giữa hitbox bằng cách tính vị trí x,y trong hitbox để đặt ảnh nhân vật cho vừa khung hình. 

                // nếu nhân vật nhìn sang trái thì lật hình sang trái:
                var st = g.Save(); // 1. lưu trạng thái bình thường của game trước khi lật
                if (!facingRight) // 2. Nếu đang nhìn sang trái
                {
                    g.TranslateTransform(x + w / 2f, 0); // Dời tâm vẽ về giữa nhân vật
                    g.ScaleTransform(-1, 1); // Lật ngược trục X (soi gương)
                    g.TranslateTransform(-(x + w / 2f), 0); // Dời tâm về chỗ cũ
                }

                g.DrawImage(sheet, new Rectangle(x, y, w, h), src, GraphicsUnit.Pixel); // cắt phần hình src từ sheet cần thiết và vẽ hình đó với chiều rộng w bắt đầu từ x và chiều dài h bắt đầu từ y:
                g.Restore(st); // dòng này để khôi phục lại trạng thái bình thường nếu đang vẽ quay sang trái để vẽ cái khác không bị ngược.
            }
        }

        // Hàm AddTileImage() dùng để thêm ảnh vào cho các ô tilesets để thiết kế map: lấy hình ảnh từ đường dẫn Images\fileName (path) sau đó gán hình ảnh đó vào tag ta muốn.
        private void AddTileImage(string tag, string fileName)
        {
            try
            {
                string path = @"Images\" + fileName;
                if (File.Exists(path))
                {
                    // 1.nạp ảnh lên ram
                    Bitmap img = (Bitmap)Image.FromFile(path);
                    // 2. kích hoạt animation:
                    if (ImageAnimator.CanAnimate(img)) // kiểm tra xem img có chuyển động được không
                    {
                        // Nếu có, bắt đầu bộ đếm thời gian cho nó.
                        // Hàm callback (s, e) => {} để trống vì ta không cần xử lý sự kiện từng khung hình,
                        // ta chỉ cần nó bắt đầu chạy ngầm là được.
                        ImageAnimator.Animate(img, (s, e) => { });
                    }

                    // Nếu dictionary đã chứa tag thì cập nhật, chưa có thì thêm mới
                    if (tileAssets.ContainsKey(tag))
                        tileAssets[tag] = img;
                    else
                        tileAssets.Add(tag,img);
                }
            }
            catch { }
        }

        // File ảnh
        private readonly string RunPath = @"Images\Punk_run.png";
        private readonly string JumpPath = @"Images\Punk_jump.png";
        private readonly string IdlePath = @"Images\Punk_idle.png";
        private readonly string DoorPath = @"Images\flag.png";
        private readonly string CoinPath = @"Images\coin_1.png";
        private readonly string EnemyPath = @"Images\enemy.png";
        private readonly string BossPath = @"Images\boss.png";

        // Anim: tạo 3 đối tượng class SpriteAnim để quản lý hoạt ảnh nhân vật cho 3 hành động khác nhau
        SpriteAnim runAnim = new SpriteAnim { FPS = 10, Loop = true }; // chạy chậm lại
        SpriteAnim jumpAnim = new SpriteAnim { FPS = 10, Loop = true };
        SpriteAnim idleAnim = new SpriteAnim { FPS = 6, Loop = true };
        SpriteAnim doorAnim = new SpriteAnim { FPS = 10, Loop = true };
        SpriteAnim coinAnim = new SpriteAnim { FPS = 9, Loop = true };
        SpriteAnim enemyAnim = new SpriteAnim { FPS = 8, Loop = true }; // Animation cho quái
        SpriteAnim bossAnim = new SpriteAnim { FPS = 6, Loop = true };

        AnimState currentState = AnimState.Idle;
        SpriteAnim currentAnim;
        bool facingRight = true;
        readonly Stopwatch sw = new Stopwatch(); // dùng để do dt chính xác

        public Form1()
        {
            InitializeComponent();

            // 1. Cấu hình hiển thị Menu
            pnlMenu.Visible = true;
            pnlMenu.BringToFront(); // Đưa Menu lên lớp trên cùng
            pnlGame.Visible = false;

        }
        private void btnNewGame_Click(object sender, EventArgs e)
        {
            // 1. Ẩn Menu đi để lộ game bên dưới
            pnlMenu.Visible = false;

            // 2. Bắt đầu tính toán game
            gameTimer.Start();

            // 3. Quan trọng: Lấy lại sự tập trung cho bàn phím
            this.Focus();
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }


        private void Form1_Load(object sender, EventArgs e)
        {
            this.KeyPreview = true;
            // Reset trạng thái phím khi mất focus để tránh kẹt phím
            this.Deactivate += (s, e2) => { goLeft = false; goRight = false; };
            // Nếu người chơi chuyển sang của sổ khác thì tự động dừng di chuyển
            DoubleBuffered = true;
            // Bật bộ đệm đôi: chống giật hình, thay vì vẽ trực tiếp lên màn hình làm cho bị nhấp nháy khi chơi thì máy tính vẽ lên một buffer ẩn rồi up buffer đó lên màn hình.
            Text = "Mini Parkour Game — Run + Jump + Still";
            player = new Rectangle(0, 440 - (40 - HB_BOTTOM), 40, 40);

            CreateLevel1(); // nạp màn 1, quét tất cả panel trên Form và biến chúng thành các đối tượng trong game

            // Chia đều & đồng bộ top/bottom; KHÔNG tighten ngang để chiều rộng các frame đều nhau
            LoadAnimationEven(RunPath, runAnim, 6, alphaThreshold: 16, tightenEdges: false);
            LoadAnimationEven(JumpPath, jumpAnim, 4, alphaThreshold: 16, tightenEdges: false);
            LoadAnimationEven(IdlePath, idleAnim, 4, alphaThreshold: 16, tightenEdges: false);
            LoadAnimationEven(DoorPath, doorAnim, 7, alphaThreshold: 0, tightenEdges: true);
            LoadAnimationEven(CoinPath, coinAnim, 7, alphaThreshold: 0, tightenEdges: true);
            LoadAnimationEven(EnemyPath, enemyAnim, 6, alphaThreshold: 16, tightenEdges: true);
            LoadAnimationEven(BossPath, bossAnim, 8, alphaThreshold: 16, tightenEdges: true);

            currentAnim = idleAnim; currentAnim.Reset();
            // Khởi động nhân vật, đặt trạng thái của nhân vật về Idle và tua về khung hình đầu tiên

            gameTimer.Interval = 16; // gọi hàm GameLoop mỗi 16ms
            gameTimer.Tick += GameLoop;  // kết nối đồng hồ với hàm xử lý GameLoop, mỗi tích tắc hàm sẽ chạy để tính toán vật lý và vẽ hình
            gameTimer.Start(); // bắt đầu đếm giờ, khởi động game

            KeyDown += KeyIsDown; // ấn phím xuống thì chạy
            KeyUp += KeyIsUp; // thả phím ra thì dừng
            Resize += Form1_Resize; // khi cửa sổ thay đổi kích thước thì gọi hàm scale

            sw.Start(); // bấm nút bắt đầu tính giờ để tính thời gian thực tế trôi qua và cho vào dt

            // ==== FIX: scale ngay khi khởi động ====
            UpdateScale();
            // tính toán tỷ lệ scale cho lần vẽ hình đầu tiên
            this.Shown += (s, _) => UpdateScale();
            // gọi lại updatescale để tính toán lại những sai lệch nhỏ nếu có ở lần 1 sau khi show
            AddTileImage("tile_1", "tile_1.png");
            AddTileImage("tile_2", "tile_2.png");
            AddTileImage("tile_3", "tile_3.png");
            AddTileImage("tile_4", "tile_4.png");
            AddTileImage("tile_5", "tile_5.png");
            AddTileImage("tile_6", "tile_6.png");
            AddTileImage("tile_7", "tile_7.png");
            AddTileImage("tile_8", "tile_8.png");
            AddTileImage("tile_9", "tile_9.png");
            AddTileImage("tile_10", "tile_10.png");
            AddTileImage("tile_11", "tile_11.png");
            AddTileImage("tile_12", "tile_12.png");
            AddTileImage("tile_13", "tile_13.png");
            AddTileImage("tile_14", "tile_14.png");
            AddTileImage("tile_15", "tile_15.png");
            AddTileImage("tile_16", "tile_16.png");
            AddTileImage("tile_17", "tile_17.png");
            AddTileImage("tile_18", "tile_18.png");
            AddTileImage("tile_19", "tile_19.png");
            AddTileImage("tile_20", "tile_20.png");
            AddTileImage("tile_21", "tile_21.png");
            AddTileImage("tile_22", "tile_22.png");
            AddTileImage("tile_23", "tile_23.png");
            AddTileImage("tile_24", "tile_24.png");
            AddTileImage("tile_25", "tile_25.png");
            AddTileImage("tile_26", "tile_26.png");
            AddTileImage("tile_27", "tile_27.png");
            AddTileImage("tile_28", "tile_28.png");
            AddTileImage("tile_29", "tile_29.png");
            AddTileImage("tile_30", "tile_30.png");
            AddTileImage("tile_31", "tile_31.png");
            AddTileImage("tile_32", "tile_32.png");
            AddTileImage("tile_33", "tile_33.png");           
            AddTileImage("deco_bui1", "deco_bui1.png");
            AddTileImage("deco_bui2", "deco_bui2.png");
            AddTileImage("deco_bui3", "deco_bui3.png");
            AddTileImage("deco_bui4", "deco_bui4.png");
            AddTileImage("deco_bui5.1", "deco_bui5.1.png");
            AddTileImage("deco_bui5.2", "deco_bui5.2.png");
            AddTileImage("deco_bui5.3", "deco_bui5.3.png");
            AddTileImage("deco_cay1", "deco_cay1.png");
            AddTileImage("deco_cay2", "deco_cay2.png");
            AddTileImage("deco_cay3", "deco_cay3.png");
            AddTileImage("deco_da1", "deco_da1.png");
            AddTileImage("deco_da2", "deco_da2.png");
            AddTileImage("trap_1", "trap_1.png");
            AddTileImage("water_2", "water4-resized.gif");
            AddTileImage("water_3", "water4-rotated.gif");
            // nạp hình các tiles vào với các tag tương ứng cho panel
            try
            {
                if (File.Exists(@"Images\platform.png"))
                    platformImg = (Bitmap)Image.FromFile(@"Images\platform.png");
                if (File.Exists(@"Images\GameOverFont_2.png"))
                    gameOverImg = (Bitmap)Image.FromFile(@"Images\GameOverFont_2.png");
            }
            catch { }            
            // nạp ảnh vào biến platformImg và biến gameOverImg, nếu không nạp được ảnh thì sẽ vẽ màu nâu thay thế
        }
        // ===== LOAD HELPERS =====

        // hàm chính để quản lí các hàm điều chỉnh frame khác tạo thành một hoạt ảnh hoàn chỉnh:
        private void LoadAnimationEven(string path, SpriteAnim anim, int frameCount, int alphaThreshold, bool tightenEdges)
        {
            try
            {
                if (!File.Exists(path)) throw new FileNotFoundException("Không tìm thấy file", path);
                anim.Sheet = (Bitmap)Image.FromFile(path);
                // đọc file ảnh từ ổ cứng lên RAM

                var frames = SplitEven(anim.Sheet, frameCount);
                // B1: dùng hàm SplitEven() để bắt đầu chia đều tấm ảnh gốc thành các frame bằng nhau theo chiều dọc

                var tb = GetGlobalVerticalBounds(anim.Sheet, alphaThreshold);
                int top = tb.Item1, bottom = tb.Item2;
                int height = (bottom > top) ? (bottom - top + 1) : anim.Sheet.Height;
                // B2: dùng hàm GGVB() để tính chiều cao thực tế của nhân vật và lưu vào height

                if (tightenEdges)
                    frames = TightenHorizontal(anim.Sheet, frames, alphaThreshold);
                // B3: nếu tightenEdges = true thì gọi hàm cắt ngang để cắt bớt phần thừa 2 bên hông của từng khung hình, còn nếu không muốn cắt bỏ thì lúc nạp hình để tightenEdges là false

                for (int i = 0; i < frames.Count; i++)
                    frames[i] = new Rectangle(frames[i].Left, top, frames[i].Width, height);
                // B4: với mỗi frame ta thay thế chúng với một rectangle mới với các thông số sau:
                // frames[i].Left : tọa độ mép bên trái giữ nguyên của khung hình cũ vì đã được cắt ở bước SplitEven()
                // top : dùng top được tính từ hàm GGVB(), mọi khung hình đều được vẽ từ đỉnh cao nhất của nhân vật
                // frames[i].Width : giữ nguyên chiều rộng của khung hình cũ vì nó đã được cắt xén bởi hàm TightenHorizontal()
                // height : dùng height được tính từ hàm GGVB(), mọi khung hình đều có cùng chiều cao

                anim.Frames = frames;
                // gán danh sách khung hình đã xử lí xong vào đối tượng anim
                anim.Reset();
                // chạy hàm Reset để chạy animation từ đầu
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi nạp animation: " + ex.Message, "Sprite",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                // có lỗi ở bất kì bước nào ở trong try{} thì sẽ thông báo lỗi
                anim.Sheet = null; anim.Frames = new List<Rectangle>();
                // gán sheet = null và danh sách frames rỗng để game không vẽ ảnh lỗi
            }
        }

        // hàm cắt các frame ra từ hình tổng hợp:
        private List<Rectangle> SplitEven(Bitmap bmp, int frameCount)
        {
            var frames = new List<Rectangle>();
            // tạo một danh sách lưu trữ các frame cắt ra từ ảnh gốc
            frameCount = Math.Max(1, frameCount);
            // đảm bảo số lượng frame ít nhất là 1 để tránh chia cho 0 trong chương trình
            int sliceW = bmp.Width / frameCount;
            // tính chiều rộng mỗi frame = tổng chiều rộng / số lượng
            for (int i = 0; i < frameCount; i++) // bắt đầu cắt frame
            {
                int sx = i * sliceW;
                // tính tọa độ x bắt đầu cắt ( mỗi frame có chiều rộng sliceW nên mỗi frame sẽ cách nhau một khoảng cách bằng sliceW nên tọa độ x của mỗi frame thứ i sẽ bắt đầu từ i * sliceW
                int width = (i == frameCount - 1) ? bmp.Width - sx : sliceW;
                // tính chiều rộng của frame này, nếu là frame cuối thì lấy hết phần còn lại của hình, nếu không thì lấy đúng sliceW
                frames.Add(new Rectangle(sx, 0, width, bmp.Height));
                // tạo một rectangle với chiều rộng là width bắt đầu từ sx và chiều cao là chiều cao của ảnh gốc bmp.Height bắt đầu từ 0. Sau đó add vào danh sách các frame
            }
            return frames;
        }

        // hàm đồng bộ chiều cao GetGlobalVerticalBounds, tìm đỉnh đầu cao nhất và điểm dưới chân thấp nhất của nhân vật để đồng bộ để tránh nhân vật bị giật lên giật xuống lúc chạy:
        private Tuple<int, int> GetGlobalVerticalBounds(Bitmap bmp, int alphaThreshold)
        {
            int top = int.MaxValue, bottom = -1;
            // đặt cột mốc ban đầu là giá trị cực đại và cực tiểu để làm điểm tựa
            for (int y = 0; y < bmp.Height; y++) // quét từng dòng (Y) từ trên xuống
                for (int x = 0; x < bmp.Width; x++) // quét từng điểm (X) từ trái sang phải
                    if (bmp.GetPixel(x, y).A > alphaThreshold)
                    // kiểm tra điểm có nhìn thấy được không: nếu A (độ đục của điểm) > ngưỡng quy định -> là điểm ảnh có màu
                    {
                        // nếu dòng này cao hơn top thì cập nhật top hiện tại
                        if (y < top) top = y;
                        // nếu dòng này thấp hơn bottom thì cập nhật bottom
                        if (y > bottom) bottom = y;
                    }
            // nếu ảnh không có điểm nào thấy được thì lấy chiều cao gốc của ảnh
            if (bottom < top) { top = 0; bottom = bmp.Height - 1; }
            // trả về cặp số (đỉnh,đáy)
            return Tuple.Create(top, bottom);
        }
        // hàm cắt bỏ khoảng trống thừa ở bên trái và bên phải của từng frame 
        private List<Rectangle> TightenHorizontal(Bitmap bmp, List<Rectangle> slices, int alphaThreshold)
        {
            var result = new List<Rectangle>(slices.Count);
            // danh sách kết quả mới để lưu vào
            foreach (var s in slices) // duyệt qua từng frame đã được cắt ở hàm SplitEven
            {
                int minX = int.MaxValue, maxX = -1;
                for (int x = s.Left; x < s.Right; x++)
                    for (int y = s.Top; y < s.Bottom; y++) // quét trong phạm vi frame
                        if (bmp.GetPixel(x, y).A > alphaThreshold) // kiểm tra pixel có màu hay không
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            // tìm mép trái nhất và mép phải nhất có chứa frame
                        }
                if (maxX < minX) result.Add(s);
                // nếu không tìm thấy hình -> giữ nguyên như cũ và thêm vào result
                else result.Add(Rectangle.FromLTRB(minX, s.Top, maxX + 1, s.Bottom));
                // nếu tìm thấy, tạo khung hình mới từ minX đến maxX
            }
            return result;
        }
        // Hàm nạp background (Hỗ trợ cả GIF động)
        private void LoadBackground(string path)
        {
            try
            {
                // 1. Xóa ảnh nền cũ nếu có (để tiết kiệm RAM khi qua màn)
                if (backgroundImg != null)
                {
                    ImageAnimator.StopAnimate(backgroundImg, (s, e) => { }); // Dừng chạy hình cũ
                    backgroundImg.Dispose();
                    backgroundImg = null;
                }

                // 2. Nạp ảnh mới
                if (File.Exists(path))
                {
                    backgroundImg = (Bitmap)Image.FromFile(path);

                    // 3. QUAN TRỌNG: Kích hoạt chế độ hoạt hình cho GIF
                    if (ImageAnimator.CanAnimate(backgroundImg))
                    {
                        // Hàm này bảo: "Mỗi khi đến lúc đổi khung hình, không cần làm gì cả"
                        // (Vì GameLoop của chúng ta đã tự vẽ lại 60 lần/giây rồi)
                        ImageAnimator.Animate(backgroundImg, (s, e) => { });
                    }
                }
            }
            catch { }
        }
        // ===== WINDOW & GAME LOOP =====
        private void UpdateScale()
        {
            int w = Math.Max(1, pnlGame.ClientSize.Width);
            int h = Math.Max(1, pnlGame.ClientSize.Height);
            // lấy chiều rộng w và chiều dài h hiện tại của cửa sổ game với kích thước tối thiểu là 1
            scaleX = (float)w / baseWidth;
            scaleY = (float)h / baseHeight;
            // tính tỉ lệ cần thiết bằng cách chia kích thước hiện tại với kích thước gốc
            Invalidate();
            // Invalidate() được dùng để báo cáo máy chủ thực hiện lệnhh OnPaint() e.Graphics.ScaleTransform(scaleX, scaleY); để scale cửa sổ game
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            UpdateScale(); // dùng helper chung
        }

        // hàm giải quyết va chạm GetCollRect() dùng để xác định chính xác hitbox của nhân vật và thêm cơ chế hitbox linh hoạt theo hướng nhìn của nhân vật.
        private Rectangle GetCollRect(Rectangle r, bool faceRight)
        {
            int leftInset = faceRight ? HB_BACK : HB_FRONT;
            int rightInset = faceRight ? HB_FRONT : HB_BACK;
            // xác định lề thụt vào (Inset) tùy theo hướng mặt
            // nếu quay phải: Lưng là bên trái (HB_BACK), Mặt là bên phải (HB_FRONT)
            // nếu quay trái: Lưng là bên phải, Mặt là bên trái.
            // ở phía trước thụt vào nhiều hơn còn phía sau phần lưng thường phẳng nên thụt vào ít hơn

            int x = r.X + leftInset; // dịch vào từ trái
            int y = r.Y + HB_TOP; // dịch xuống từ đầu
            // tính toán tọa độ bắt đầu vẽ của hitbox mới nhỏ hơn hình gốc

            int w = Math.Max(1, r.Width - leftInset - rightInset);
            // tính chiều rộng mới của hitbox bằng cách trừ cho thụt lề trái và thụt lề phải
            int h = Math.Max(1, r.Height - HB_TOP - HB_BOTTOM);
            // tính chiều cao mới tương tự
            return new Rectangle(x, y, w, h);
            // vẽ lại hitbox với chỉ số mới
        }
        private void GameLoop(object sender, EventArgs e)
        {
            double dt = sw.Elapsed.TotalSeconds; sw.Restart();
            if (dt <= 0 || dt > 0.05) dt = 1.0 / 60.0;
            // đo thời gian đã trôi qua dt kể từ lần chạy trước, nếu máy quá lag (lâu hơn 0.05s) thì ép thời gian về 1/60s để tránh việc nhân vật bị xuyên tường do di chuyển quá xa trong 1 bước

            int prevX = player.X;
            int prevY = player.Y;
            bool prevFace = facingRight;// lưu hướng quay mặt cũ lại
            Rectangle prevColl = GetCollRect(new Rectangle(prevX, prevY, player.Width, player.Height), prevFace);
            // prevColl có tác dụng lưu lại vị trí cũ trước khi di chuyển của nhân vật để phòng các trường hợp nhân vật di chuyển bị bug
            Rectangle playerHitbox = GetCollRect(player, facingRight);
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                Enemy en = enemies[i];
                if (en.IsDead) continue;

                // A. Di chuyển
                int moveStep = en.FacingRight ? en.Speed : -en.Speed;
                // nếu đang quay sang phải thì tốc độ là dương, nếu quay sang trái thì tốc độ là âm
                en.Rect.X += moveStep;
                // với tốc độ dương thì X của nhân vật sẽ di chuyển sang phải (đi thẳng), còn tốc độ âm thì sang trái (đi hướng ngược lại).
                en.Rect.Y += 4;
                // B. AI: Quay đầu khi gặp tường hoặc hết đường
                // Tạo cảm biến: 1 cái check tường (ngang), 1 cái check đất (dưới chân)
                int sensorX = en.FacingRight ? (en.Rect.Right + 2) : (en.Rect.Left - 2);
                Rectangle wallSensor = new Rectangle(sensorX, en.Rect.Y, 2, en.Rect.Height - 5);               
                Rectangle groundSensor = new Rectangle(sensorX, en.Rect.Bottom, 2, 2);

                bool hitWall = false;
                bool hasGround = false;
                foreach (var p in Solid)
                {
                    // Nếu Boss rơi trúng platform
                    if (en.Rect.IntersectsWith(p))
                    {
                        // Nếu đang ở trên platform thì đẩy ngược lên cho đứng vừa khít
                        if (en.Rect.Bottom > p.Top && en.Rect.Top < p.Top) // bottom của enemy hitbox nếu lớn hơn top của p tức là enemy đang nhảy từ trên xuống platform còn top của enemy bé hơn top của p tức là đầu của enemy đã xuyên qua platform
                        {
                            en.Rect.Y = p.Top - en.Rect.Height; // nếu enemy đang nhảy từ trên xuống và top đã xuyên qua platform thì cho nhân vật đứng ngay ở trên platform bằng cách cho đáy của hitbox enemy = top của platform đi lên một khoảng bằng chiều cao của hitbox enemy
                        }
                    }
                }
                foreach (var p in Solid)
                {
                    if (wallSensor.IntersectsWith(p)) hitWall = true;
                    if (groundSensor.IntersectsWith(p)) hasGround = true;

                }

                if (hitWall || !hasGround)
                {
                    en.FacingRight = !en.FacingRight; // Đổi hướng
                }

                // C. Tương tác với Player
                if (playerHitbox.IntersectsWith(en.Rect))
                {
                    bool isFallingAttack = !onGround && jumpSpeed <= 0;
                    // Nếu là Boss thì hitbox to hơn nên cần nới lỏng điều kiện isAbove một chút (+20)
                    bool isAbove = prevColl.Bottom <= en.Rect.Top + (en.Rect.Height / 2);

                    if (isFallingAttack && isAbove)
                    {
                        // ==> PLAYER ĐẠP TRÚNG ĐẦU

                        // 1. Trừ máu quái
                        en.CurrentHP--;

                        // 2. Nảy người chơi lên
                        jumping = true;
                        onGround = false;
                        jumpSpeed = 15;

                        // 3. Kiểm tra chết
                        if (en.CurrentHP <= 0)
                        {
                            en.IsDead = true;
                            score += (en.IsBoss ? 100 : 5); // Boss cho nhiều điểm

                            // Nếu Boss chết -> Thắng game luôn (hoặc hiện cửa)
                            if (en.IsBoss)
                            {
                                MessageBox.Show("YOU WIN! Đã tiêu diệt Boss!");
                                // Có thể gọi NextLevel() hoặc EndGame() ở đây
                            }
                        }
                    }
                    else
                    {
                        // ==> PLAYER BỊ ĐÁNH (Thua)

                        // --- SỬA ĐỔI Ở ĐÂY: TẠO VÙNG SÁT THƯƠNG NHỎ HƠN ---
                        // Tạo một hình chữ nhật nhỏ hơn nằm bên trong con quái
                        // X + 10: Thụt vào bên trái 10px
                        // Y + 20: Thụt xuống từ đỉnh đầu 20px (để phần đầu an toàn hơn)
                        // Width - 20: Tổng thu hẹp chiều ngang (10 trái + 10 phải)
                        // Height - 20: Tổng thu hẹp chiều dọc
                        Rectangle damageZone = new Rectangle(
                            en.Rect.X + 10,
                            en.Rect.Y + 20,
                            Math.Max(1, en.Rect.Width - 20),
                            Math.Max(1, en.Rect.Height - 20)
                        );

                        // Chỉ khi Player chạm vào "LÕI" (damageZone) này thì mới bị mất máu
                        // Còn nếu chỉ chạm vào rìa ngoài (10px) thì không sao cả
                        if (playerHitbox.IntersectsWith(damageZone))
                        {
                            if (currentHealth > 0)
                            {
                                // Boss đánh đau hơn (30 máu), quái thường (20 máu)
                                currentHealth -= (en.IsBoss ? 30 : 20);

                                // Hiệu ứng bị đẩy lùi (Knockback)
                                if (player.X < en.Rect.X) player.X -= 50; // Đẩy mạnh hơn
                                else player.X += 50;

                                if (currentHealth <= 0)
                                {
                                    currentHealth = 0; score = 0; isGameOver = true;
                                    gameTimer.Stop(); Invalidate(); return;
                                }
                            }
                        }
                    }
                }
            }
            // Xử lí tương tác với trap và water:
            bool hitTrap = false; // Frame này đã dính bẫy chưa?
            isSubmerged = false;  // Reset trạng thái nước mỗi khung hình
            isCollected = false; // Reset trạng thái nhặt coin mỗi khung hình

            foreach (var t in tiles)
            {
                if (t.Type.StartsWith("water_"))
                {
                    // Nếu hitbox nhân vật chạm vào nước
                    if (playerHitbox.IntersectsWith(t.Rect))
                    {
                        isSubmerged = true; // Bật trạng thái: Đang ở trong nước
                    }
                }
                if (t.Type.StartsWith("trap_"))
                {
                    // Kiểm tra va chạm giữa Nhân vật và Bẫy
                    if (playerHitbox.IntersectsWith(t.Rect))
                    {
                        // Logic chạm bẫy:
                        // Chỉ bị thương khi chạm từ trên xuống (đạp vào gai) hoặc chạm trực tiếp

                        // 1.Trừ máu
                        if (!hitTrap)
                        {
                            currentHealth -= 25;
                            hitTrap = true; // Đánh dấu đã dính, các bẫy sau sẽ không trừ máu nữa
                        }
                        // 2. Hất tung nhân vật lên
                        jumping = true;      // Kích hoạt trạng thái nhảy
                        onGround = false;    // Rời khỏi mặt đất
                        jumpSpeed = 25;      // Lực nảy lên (nhỏ hơn lực nhảy thường một chút)    

                        // 3. Kiểm tra Chết
                        if (currentHealth <= 0)
                        {
                            currentHealth = 0;
                            score = 0;
                            isGameOver = true;
                            gameTimer.Stop();
                            Invalidate();
                            return;
                        }                        
                    }
                }
            }
            // Điều khiển trái/phải + hướng vẽ
            if (goLeft) { player.X -= playerSpeed; facingRight = false; }
            if (goRight) { player.X += playerSpeed; facingRight = true; }

            // --- SAU khi cập nhật X từ phím trái/phải ---
            int oldX = prevX; // vị trí X trước khi bước
            Rectangle afterX = GetCollRect(player, facingRight); // đây là hitbox thử nghiệm được máy tạo ra để thử việc va chạm với các platforms xem có bị dính vào tường hay không 

            foreach (var p in Solid) // lấy hitbox thử nghiệm afterX đó cho va chạm với tất cả platforms
            {
                if (!afterX.IntersectsWith(p)) continue; // nếu không chạm vào platform đó thì continue
                                                         // nếu có chạm ( nghĩa là bước đi đã khiến hitbox dính vào tường hoặc vào đất thì sẽ xét như bên dưới )

                // Chỉ chặn khi ta đang ở "dưới" bệ (đỉnh hitbox cao hơn/touch đáy bệ)
                bool underCeilingNow = afterX.Top < p.Bottom; // kiểm tra đầu của hitbox sau khi di chuyển có đang nằm thấp hơn đáy cục gạch không
                // underCeilingNow nếu đúng thì nghĩa là thân nhân vật đang nằm ngang với cục gạch ( đụng tường )
                // underCeilingNow nếu sai thì nhân vật đang ở trên nóc cục gạch ( đứng trên đất )
                bool underCeilingPrev = prevColl.Top < p.Bottom; // kiểm tra tương tự với hitbox lúc trước khi di chuyển  

                if (underCeilingNow && underCeilingPrev)
                // nếu lúc trước khi di chuyển và sau khi di chuyển mà thân nhân vật đều đang đụng tường thì làm như sau:
                {
                    player.X = oldX; // phát hiện đụng tường => quay lại oldX ngay lập tức
                    afterX = GetCollRect(player, facingRight); // cập nhật lại hitbox thử nghiệm cho các lần thử tiếp theo
                    break; // đụng tường thì quay lại rồi break, không cần kiểm tra các cục gạch khác
                }
                // *TÓM TẮT: đoạn code trên dùng để tránh trường hợp nhân vật va chạm vào tường theo bề ngang và hitbox của nhân vật đi thẳng luôn vào tường, thay vào đó dùng hitbox thử nghiệm đi vào cục gạch trước để xem có phải là va chạm ngang hay không, nếu phải thì cho lùi lại hitbox oldX còn nếu là va chạm từ trên xuống tức là nhân vật đang nằm ở trên cục gạch thì cho phép
                // *QUAN TRỌNG: nếu không có thì nhân vật sẽ kẹt vĩnh viễn trong tường
            }

            // đoạn code if này để cho nhân vật đang nhảy và rơi xuống nếu chạm nước thì tắt chế độ nhảy ngay lập tức và chuyển sang trạng thái ngập nước, vì nếu đang nhảy và rớt xuống mà gặp nước không ngừng trạng thái nhảy thì trọng lực sẽ không bị thay đổi mà nhân vật bị quán tính kéo thẳng xuống vực
            // dễ hiểu thì đoạn code này mô phỏng khi nhảy xuống nước thì lực kéo xuống khi nhảy bị triệt tiêu dần dần
            if (isSubmerged)
            {
                // Nếu đang ở dưới nước và đang bị kéo rơi xuống (do hết lực nhảy)
                if (jumping && jumpSpeed < 0)
                {
                    jumping = false; // Tắt chế độ nhảy ngay lập tức
                    jumpSpeed = 0;   // Xóa bỏ quán tính rơi nhanh
                }
            }

            // Nhảy + trọng lực: đoạn code dưới này dùng để giải quyết chạm trần, khi nhân vật nhảy lên mà chạm trần thì cho rớt xuống ngay chứ không nhảy xuyên qua
            if (jumping) { player.Y -= jumpSpeed; jumpSpeed -= 1; }
            // nhân vật nhảy lên, jumpSpeed giảm dần mô phỏng trọng lực
            if (!onGround)
            {
                // Nếu đang chìm -> Rơi cực chậm (2). Nếu không -> Rơi thường (8)
                int currentGravity = isSubmerged ? 2 : gravity;
                player.Y += currentGravity;
            }
            Rectangle coll = GetCollRect(player, facingRight);
            // tính toán một hitbox mới cho nhân vật sau khi Y được cập nhật
            // hitbox đó được dùng để xét va chạm trong đoạn dưới đây

            if (player.Y < prevY) // chỉ xét nếu nhân vật đang nhảy lên
            {
                foreach (var p in Solid)
                {
                    bool overlapX = coll.Right > p.Left && coll.Left < p.Right;
                    // overlapX được dùng để kiểm tra nhân vật và platform có va chạm nhau hay chồng lên nhau hay không
                    // nếu một trong hai điều kiện của overlapX sai thì tức là hai vật thể đang cách xa nhau, không va chạm
                    // nếu overlapX này true thì có nghĩa là hai vật thể đang ngang hàng với nhau

                    bool crossedBottom = (coll.Top <= p.Bottom) && (prevColl.Top >= p.Bottom);
                    // crossedBottom dùng để kiểm tra đầu nhân vật có xuyên qua platform không, tức là đang nhảy từ dưới lên xuyên qua platform

                    if (overlapX && crossedBottom) // nếu nhân vật đang ngang hàng với platform và nhân vật vừa mới nhảy xuyên qua đáy
                    {
                        player.Y = p.Bottom - HB_TOP;
                        // cho đầu nhân vật ghim vào ngay dưới đáy platform
                        coll = GetCollRect(player, facingRight);
                        // tạo hitbox mới cho nhân vật
                        jumping = false; jumpSpeed = 0;
                        // triệt tiêu lực nhảy, cho đầu nhân vật đụng đáy platform và rớt xuống ngay lập tức
                        onGround = false;
                        // vì vừa chạm đáy platform nên nhân vật phải ở trạng thái lơ lửng
                        break;
                    }
                }
            }

            // ---- BẮT ĐẤT ỔN ĐỊNH: chỉ đáp khi cắt qua mặt trên; keepStick chỉ cho "cùng bệ" ----
            // Logic vật lý tiếp đất, giúp nhân vật không bị trượt, không bị rơi xuyên sàn và đứng được trên mép vực

            bool wasGrounded = onGround;
            // lưu trạng thái tiếp đất của nhân vật
            onGround = false;

            int prevBottom = prevColl.Bottom;
            // lưu vị trí chân cũ
            int bottom = coll.Bottom;
            // vị trí chân hiện tại

            // những chỉ số dưới đây giúp game dễ nhảy hơn và bỏ qua lỗi sai của người chơi nhiều hơn, kéo dài thêm các chỉ số hitbox một tí để người chơi không bị rớt vì lệch một tí pixel:
            int footSpan = Math.Max(12, coll.Width * 2 / 3);
            // chiều rộng bàn chân của nhân vật nhỏ hơn chiều rộng nhân vật thực tế

            int edgeGrace = 4;
            //mở rộng hitbox của platform ra thêm 4px ở hai bên, giúp nhân vật dễ dàng chạm vào mép platform

            int keepTol = 5;
            // 5 pixel là khoảng cách tối đa mà chân được phép hở khỏi mặt đất mà vẫn không rớt

            int minSupportCross = 2;
            // mức độ chồng lên của pixel nhân vật đối với platform tối thiểu để nhân vật được phép tiếp đất lên platform, giá trị này nhỏ chỉ 2 pixel để việc tiếp đất dễ dàng và mượt mà hơn

            int minSupportStick = Math.Max(10, coll.Width / 2);
            // khi đã tiếp đất và đứng vững thì yêu cầu phải chồng lên bệ đất nhiều hơn (10pixel), để nhân vật không có hiệu ứng đã đứng ngoài rìa platform nhưng vẫn không rớt

            int footLeft = coll.Left + coll.Width / 2 - footSpan / 2;
            // vị trí tọa độ bên trái của chân nhân vật
            int footRight = footLeft + footSpan;
            // vị trí tọa độ bên phải

            bool falling = player.Y > prevY;
            bool rising = player.Y < prevY;
            // hai biến này dùng để phân biệt đang nhảy hay đang rớt xuống dùng để phân biệt va chạm trần với va chạm đất

            // Tạo một list tạm để chứa tất cả vật thể rắn:
            var Objects = new List<Rectangle>();

            // Thêm platform cũ vào trước (giữ nguyên thứ tự để groundedIndex không bị loạn):
            foreach (var p in platforms) Objects.Add(p);

            // Thêm tiles mới vào sau (CÓ CHỌN LỌC):
            foreach (var t in tiles)
            {
                // Chỉ tính là tile nếu không phải là nước, bẫy, hoặc đồ trang trí
                if (!t.Type.StartsWith("water_") && !t.Type.StartsWith("trap_") && !t.Type.StartsWith("deco_"))
                {
                    Objects.Add(t.Rect);
                }
            }

            // Hàm tiếp đất dành cho platforms và tiles:
            for (int i = 0; i < Objects.Count; i++)
            {
                var p = Objects[i];

                int pLeft = p.Left - edgeGrace;
                int pRight = p.Right + edgeGrace;
                // kéo rộng platform sang 2 bên

                int overlap = Math.Min(footRight, pRight) - Math.Max(footLeft, pLeft);
                // overlap dùng để tính xem bàn chân của nhân vật có đang nằm trên platform không và nằm trên bao nhiêu pixel
                // nếu overlap lớn hơn 0 thì chân đang chồng lên platform ít nhất 1 pixel

                if (overlap <= 0) continue;
                // nếu overlap = 0 thì chân chỉ chạm mép platform còn < 0 thì nằm ở ngoài nên bỏ qua

                // Đáp: chỉ khi đang rơi và đáy cắt qua mặt trên
                bool crossedTop = (falling && // đang rơi xuống
                                  (prevBottom <= p.Top + 2) && // lúc trước chân phải nằm cách xa ở phía trên mặt đất tối thiểu là 2 pixel
                                  (bottom >= p.Top - 2) && // lúc hiện tại chân đã ở dưới mặt gạch tối thiểu 2 pixel
                                  (overlap >= minSupportCross)); // phải có độ chồng lên tối thiểu là 2 pixel

                // Nếu trước đó đang đứng trên đất (wasGrounded), thì cho phép chuyển sang bất kỳ cục gạch nào khác
                // miễn là có chồng lên nhau dù chỉ 1px.
                int needStickOverlap = wasGrounded ? 1 : minSupportStick;

                bool keepStick =
                    wasGrounded && !rising && // nếu đang đứng vững và đang không nhảy lên
                    Math.Abs(prevBottom - p.Top) <= keepTol &&
                    Math.Abs(bottom - p.Top) <= keepTol && // khoảng cách chân lúc trước và lúc sau đều phải nhỏ hơn dung sai tối đa để đứng trên platform
                    (overlap >= needStickOverlap); // chân nhân vật phải chồng lên platform ít nhất bằng diện tích tối thiểu cần thiết để đứng vững

                if (crossedTop || keepStick) // nếu nhân vật đang đáp đất hoặc đang dính vào đất
                {
                    // ghim sao cho coll.Bottom = p.Top:

                    player.Y = p.Top - (coll.Height + HB_TOP);
                    // ta có: player.Y = coll.Bottom - (coll.Height + HB_TOP) nghĩa là đỉnh nhân vật = đáy hitbox đi lên một khoảng bằng chiều cao tổng thể
                    // nên muốn vị trí coll.Bottom = p.Top, ta phải làm cho đỉnh của nhân vật, tức là player.Y = p.Top - (coll.Height + HB_TOP)
                    // tức là đỉnh của nhân vật = đỉnh của mặt phẳng đi lên một khoảng bằng chiều cao tổng thể
                    // từ đó ta có được coll.Bottom hay chân của hitbox sẽ ngang hàng với đỉnh của mặt phẳng hay p.Top

                    coll = GetCollRect(player, facingRight);
                    //tính toán lại hitbox lần cuối, sử dụng vị trí player.Y mới

                    onGround = true; // đã chạm đất vì đang đứng trên platform
                    groundedIndex = i; // ghi lại vị trí bệ đang đứng dùng cho keepStick cho frame tiếp theo
                    jumping = false;
                    jumpSpeed = 0;
                    // triệt tiêu toàn bộ lực nhảy và trạng thái nhảy vì đã tiếp đất thành công
                    break;
                    // không kiểm tra các platform khác nữa vì đã tìm thấy platform tiếp đất thành công
                }
            }

            // nếu đã kiểm tra hết platform mà onGround vẫn là false thì nhân vật không đứng trên platform nào
            if (!onGround) groundedIndex = null;


            // Giới hạn biên
            if (player.X < 0) player.X = 0; // chặn ở mép trái
            if (player.Right > baseWidth) player.X = baseWidth - player.Width; // chặn ở mép phải
            if (player.Y > baseHeight) // rớt xuống vực
            {
                // 1. Trừ máu
                currentHealth -= 100; // Trừ thẳng 100 máu 

                // 2. Hồi sinh về vị trí cũ
                player.X = startX;
                player.Y = startY;

                // 3. Reset vật lý
                jumping = false;
                jumpSpeed = 0;
                goLeft = false;
                goRight = false;

                // 4. Kiểm tra Chết (Hết máu)
                if (currentHealth <= 0)
                {
                    currentHealth = 0; // Chốt máu về 0
                    score = 0; // reset điểm
                    isGameOver = true; // Bật chế độ Game Over
                    gameTimer.Stop();  // Dừng game lại
                    Invalidate();      // Vẽ lại màn hình lần cuối để hiện chữ Game Over
                    return;            // Thoát hàm luôn
                }
            }


            // Cửa:
            if (coll.IntersectsWith(door) && !levelTransitioning)
            {
                levelTransitioning = true;
                NextLevel();
                goLeft = false;
                goRight = false;
                jumping = false;
                jumpSpeed = 0;
            }
            // levelTransitioning là một flag để NextLevel() được gọi một lần duy nhất chứ không gọi 60 lần/s khi nhân vật đứng trong cửa

            // Coin:
            for (int i = coin.Count - 1; i >= 0; i--)
            {
                if (coll.IntersectsWith(coin[i]) && isCollected == false)
                {
                    isCollected = true;
                    score += 1;
                    coin.RemoveAt(i);
                }
            }
            // === Chọn state theo DI CHUYỂN THỰC TẾ ===

            bool inAir = jumping || !onGround;
            // nếu đang nhảy hoặc không ở trên mặt đất thì 
            bool movingNow = (player.X != prevX);
            // chỉ coi là chạy khi X thực sự đổi

            AnimState desired = inAir ? AnimState.Jump : (movingNow ? AnimState.Run : AnimState.Idle);
            // lựa chọn anim phù hợp cho mỗi desired state

            if (desired != currentState) SwitchState(desired);
            // nếu trạng thái mới khác trạng thái hiện tại thì gọi hàm chuyển trạng thái

            currentAnim?.Update(dt);
            // gọi hàm tính toán thời gian cho bột animation hiện tại để chuyển khung hình
            doorAnim.Update(dt);
            coinAnim.Update(dt);
            // gọi hàm tương tự cho animation cửa qua màn và coin trong game
            enemyAnim.Update(dt);
            bossAnim.Update(dt);
            // DEBUG: nhìn state/moving trực tiếp ở title
            //pnlGame.Text = $"State={currentState}  movingNow={(player.X != prevX)}  goL={goLeft} goR={goRight}  onGround={onGround}";


            Invalidate();
            // gửi yêu cầu vẽ lại toàn bộ Form, kích hoạt hàm OnPaint() để hiển thị mọi thay đổi về vị trí, hình ảnh và trạng thái 
        }

        private void SwitchState(AnimState s)
        {
            currentState = s; // lưu trạng thái ban đầu
            switch (s)
            {
                case AnimState.Idle: currentAnim = idleAnim; break;
                case AnimState.Run: currentAnim = runAnim; break;
                case AnimState.Jump: currentAnim = jumpAnim; break;
            }
            currentAnim?.Reset(); // reset khung hình sau khi chuyển trạng thái
        }


        // ===== INPUT =====
        private void KeyIsDown(object sender, KeyEventArgs e)
        {
            if (isGameOver)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    // Khi Enter được nhấn, reset lại trạng thái game
                    isGameOver = false;           // Đánh dấu game không còn ở trạng thái Game Over
                    currentHealth = maxHealth;    // Reset máu về giá trị tối đa
                    score = 0;                    // Reset điểm số về 0
                    currentLevel = 1;             // Đặt lại level về màn đầu tiên

                    // Reset trạng thái nhảy và tiếp đất
                    jumping = false;
                    onGround = false;

                    // Đặt lại vị trí nhân vật về điểm spawn ban đầu
                    player.X = startX;
                    player.Y = startY;

                    // Ẩn menu và hiển thị game
                    pnlMenu.Visible = false;      // Ẩn menu
                    pnlGame.Visible = true;       // Hiển thị game

                    // Đảm bảo game luôn ở trên cùng
                    pnlGame.BringToFront();

                    // Tạo lại level 1 và nền
                    CreateLevel1();

                    // Bắt đầu lại gameTimer (nếu chưa bắt đầu hoặc đã dừng lại)
                    gameTimer.Start();

                }
                return; // Không làm gì khác khi đang Game Over
            }

            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.A) goLeft = true;
            if (e.KeyCode == Keys.Right || e.KeyCode == Keys.D) goRight = true;

            if ((e.KeyCode == Keys.Space || e.KeyCode == Keys.Up || e.KeyCode == Keys.W) && !jumping) // !jumping đảm bảo chỉ cho phép nhảy khi nhân vật không đang trong trạng thái nhảy sẵn
            {
                if (onGround || IsTouchingPlatformBelow()) // khi nhân vật đang onGround hoặc IsTouchingPLatFormBelow() là true tức là chân nhân vật vừa chạm một platform -> cho phép nhảy ngay sau khi chạm đất
                {
                    jumping = true;
                    jumpSpeed = force;
                    onGround = false;
                }
            }
            // CHEAT CODE: Bấm phím số 6 để bay tới màn Boss
            if (e.KeyCode == Keys.D6)
            {
                currentLevel = 6;
                CreateLevel6();

                // Reset lại máu và vị trí cho an toàn
                currentHealth = maxHealth;
                jumping = false;
                onGround = false;

                // Reset lại nhạc hoặc các thứ khác nếu cần
                gameTimer.Start();
            }
        }

        private void pnlMenu_Paint(object sender, PaintEventArgs e)
        {

        }

        private void pnlGame_Paint(object sender, PaintEventArgs e)
        {

        }

        private void KeyIsUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.A) goLeft = false;
            if (e.KeyCode == Keys.Right || e.KeyCode == Keys.D) goRight = false;
        }

        private bool IsTouchingPlatformBelow()
        {
            Rectangle c = GetCollRect(player, facingRight);
            Rectangle feet = new Rectangle(c.X, c.Bottom, c.Width, 2);
            // tạo một hitbox giả định của chân chỉ cao 2 pixel, còn lại giữ chỉ số của hitbox chính
            foreach (var p in Solid) if (feet.IntersectsWith(p)) return true;
            // nếu hitbox chân đó chạm bất kì platform nào thì trả về true
            return false;
        }

        // hàm IsCeilingBLocked dùng để kiểm tra có platform nào ngay trên đầu hay không, tránh nhảy đụng đầu
        // hàm IsCeilingBlocked không cần thiết vì có thể cho phép nhảy lên platform ngay trên đầu và bật xuống ngay lập tức không gây ra vấn đề gì
        // trong đoạn code hiện tại không dùng hàm này
        private bool IsCeilingBlocked(int clearPx = 10, int sideGrace = 8)
        {
            Rectangle c = GetCollRect(player, facingRight);
            int headCenter = c.X + c.Width / 2;
            foreach (var p in Solid)
            {
                if (headCenter <= p.Left + sideGrace || headCenter >= p.Right - sideGrace) continue;
                int gap = p.Bottom - c.Top;
                if (gap >= 0 && gap <= clearPx) return true;
            }
            return false;
        }


        // ===== LEVELS =====
        private void NextLevel()
        {
            currentLevel++;
            if (currentLevel > 6) { gameTimer.Stop(); MessageBox.Show("🎉 Bạn đã hoàn thành tất cả các màn!"); ; return; }
            platforms.Clear();
            // xóa hết dữ liệu platform của màn chơi cũ khỏi bộ nhớ
            switch (currentLevel)
            { case 2: CreateLevel2(); break; 
              case 3: CreateLevel3(); break;
              case 4: CreateLevel4(); break; 
              case 5: CreateLevel5(); break;
              case 6: CreateLevel6(); break;
            }
            levelTransitioning = false;
            // tắt flag chuyển màn để chuẩn bị cho lần chạm cửa tiếp theo
        }

        // --- Hàm HÚT dữ liệu từ giao diện thiết kế (Toolbox) ---
        // Sửa tên hàm và thêm tham số 'container'
        private void LoadMapFromContainer(Control container)
        {
            platforms.Clear(); // Xóa dữ liệu cũ
            tiles.Clear();
            enemies.Clear();
            coin.Clear();
            // Thay 'pnlGame.Controls' bằng 'container.Controls'
            foreach (Control c in container.Controls)
            {
                string tag = c.Tag?.ToString(); // Lấy tag an toàn

                if (tag == "platform")
                {
                    platforms.Add(new Rectangle(c.Left, c.Top, c.Width, c.Height));
                    c.Visible = false;
                }
                if (tag != null && (tag.StartsWith("tile_") || tag.StartsWith("trap_") || tag.StartsWith("water_") || tag.StartsWith("deco_")))
                {
                    tiles.Add(new Tile(c.Left,c.Top,c.Width,c.Height, tag));
                    c.Visible = false;
                }
                if (tag == "door")
                {
                    door = new Rectangle(c.Left, c.Top, c.Width, c.Height);
                    c.Visible = false;
                }
                if (tag != null && tag.StartsWith("coin_"))
                {
                    coin.Add(new Rectangle(c.Left, c.Top, c.Width, c.Height));
                    c.Visible = false;
                }
                if (tag == "player")
                {
                    player.X = c.Left;
                    player.Y = c.Top;
                    c.Visible = false;
                }
                if (tag == "enemy")
                {
                    // Tạo quái tại vị trí Panel, kích thước chuẩn 40x40 (hoặc lấy c.Width, c.Height tùy bạn)
                    enemies.Add(new Enemy(c.Left, c.Top, 40, 40));
                    c.Visible = false;
                }
                if (tag == "boss")
                {
                    // Boss to hơn (80x80) và trâu hơn
                    // Lấy kích thước thật từ Panel
                    Enemy boss = new Enemy(c.Left, c.Top, c.Width, c.Height);
                    boss.IsBoss = true;
                    boss.MaxHP = 5; 
                    boss.CurrentHP = 5;
                    boss.Speed = 3;  
                    enemies.Add(boss);
                    c.Visible = false;
                }
            }
            startX = player.X;
            startY = player.Y;
            // lưu điểm hồi sinh (spawnpoint) của nhân vật
        }
        private void CreateLevel1()
        {
            // 'pnlGame' nghĩa là lấy map ngay trên Form1 hiện tại
            LoadMapFromContainer(pnlGame);
            LoadBackground(@"Images\background1.gif");
        }
        private void CreateLevel2()
        {
            // Tạo ra tờ bản đồ Level 2 ảo
            MapLevel2 map = new MapLevel2();

            // Hút dữ liệu từ tờ bản đồ đó vào game
            LoadMapFromContainer(map);


            // Hút xong thì xóa tờ bản đồ đó đi cho nhẹ máy
            map.Dispose();
        }
        private void CreateLevel3()
        {
            MapLevel3 map = new MapLevel3();
            LoadMapFromContainer(map);
            map.Dispose();
        }
        private void CreateLevel4()
        {
            MapLevel4 map = new MapLevel4();
            LoadMapFromContainer(map);
            map.Dispose();
        }
        private void CreateLevel5()
        {
            MapLevel5 map = new MapLevel5();
            LoadMapFromContainer(map);
            map.Dispose();
        }

        private void CreateLevel6()
        {
            MapLevel6 map = new MapLevel6();
            LoadMapFromContainer(map);
            map.Dispose();
        }
        // ===== DRAW =====
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); // gọi hàm vẽ của Windows
            e.Graphics.ScaleTransform(scaleX, scaleY); // áp dụng tỷ lệ scale của hàm UpdateScale cho form, để các nét vẽ đều được scale theo tỷ lệ form
                                                       // 1. Vẽ nền
            if (backgroundImg != null)
            {
                // Cập nhật khung hình GIF dựa trên thời gian thực
                ImageAnimator.UpdateFrames(backgroundImg);

                // Vẽ ảnh trải đầy màn hình game (baseWidth, baseHeight)
                e.Graphics.DrawImage(backgroundImg, 0, 0, baseWidth, baseHeight);
            }
            else
            {
                // Nếu không có ảnh thì dùng màu xám như cũ
                e.Graphics.Clear(Color.LightGray);
            }
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor; // chế độ phóng to/thu nhỏ hình ảnh, giữ cho pixel vuông vắn, không bị làm mờ khi phóng to
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half; // giúp căn chỉnh pixel chính xác hơn, tránh lỗi lệch nửa pixel gây ra hình ảnh bị rung

            //e.Graphics.FillRectangle(Brushes.SaddleBrown, 0, 440, baseWidth, 60);
            
            foreach (var p in platforms)
            {
                if (platformImg != null)
                {
                    e.Graphics.DrawImage(platformImg, p); // scale ảnh theo rectangle
                }
                else
                {
                    e.Graphics.FillRectangle(Brushes.SaddleBrown, p); // nếu file ảnh bị lỗi thì vẽ một hình màu nâu thay thế
                }
                // DEBUG: xem hitbox của platform
                //using (Pen debugPen = new Pen(Color.Red, 2))
                //{
                //    e.Graphics.DrawRectangle(debugPen, p);
                //}
            }            

            foreach (var t in tiles)
            {
                // Tìm ảnh trong kho dựa theo Tag (t.Type)
                if (tileAssets.ContainsKey(t.Type) && tileAssets[t.Type] != null)
                {
                    Bitmap img = tileAssets[t.Type];
                    ImageAnimator.UpdateFrames(img);
                    // Dòng này dùng để cập nhật frame cho file gif img dựa trên thời gian hiện tại của game, chọn frame đúng cho file này.
                    e.Graphics.DrawImage(img, t.Rect);
                    // Vẽ ảnh co giãn theo kích thước Panel thiết kế
                }
                else
                {
                    // Nếu quên nạp ảnh hoặc sai tag -> Vẽ màu xám báo lỗi
                    e.Graphics.FillRectangle(Brushes.Gray, t.Rect);
                }
            }

            // nếu đứng yên -> vẽ frame tĩnh; còn lại -> vẽ anim hiện tại
            bool moving = (goLeft || goRight);
            bool inAir = jumping || !onGround;

            if (currentAnim != null && currentAnim.Sheet != null && currentAnim.Frames.Count > 0)
                currentAnim.Draw(e.Graphics, player, facingRight); // gọi hàm vẽ từ class SpriteAnim để cắt frame và gán vào vị trí hitbox
            else
                e.Graphics.FillRectangle(Brushes.Red, player); // nếu file nhân vật bị lỗi sẽ vẽ hình chữ nhật đỏ thay thế

            // Vẽ cửa:
            if (doorAnim.Sheet != null)
            {
                // Vẽ animation cờ vào vị trí ô chữ nhật 'door'
                // facingRight = true (Cờ luôn bay về một hướng, không cần lật)
                doorAnim.Draw(e.Graphics, door, true);
            }
            else
            {
                // Nếu ảnh lỗi thì vẫn vẽ màu vàng
                e.Graphics.FillRectangle(Brushes.Gold, door);
            }

            // Vẽ coin
            foreach (var c in coin)
            {
                if (coinAnim.Sheet != null)
                {
                    coinAnim.Draw(e.Graphics, c, true);
                }
                else
                {
                    // Nếu ảnh lỗi thì vẫn vẽ màu vàng
                    e.Graphics.FillRectangle(Brushes.Gold, c);
                }
            }

            foreach (var en in enemies)
            {
                if (en.IsDead) continue;

                if (en.IsBoss && bossAnim.Sheet != null)
                {
                    // Vẽ Boss
                    bossAnim.Draw(e.Graphics, en.Rect, en.FacingRight);
                }
                else if (!en.IsBoss && enemyAnim.Sheet != null)
                {
                    // Vẽ Quái thường
                    enemyAnim.Draw(e.Graphics, en.Rect, en.FacingRight);
                }
                else
                {
                    // Vẽ hộp dự phòng
                    e.Graphics.FillRectangle(en.IsBoss ? Brushes.DarkRed : Brushes.Purple, en.Rect);
                }

                // 2. VẼ THANH MÁU TRÊN ĐẦU (Chỉ vẽ cho Boss hoặc nếu quái thường mất máu)
                if (en.CurrentHP < en.MaxHP)
                {
                    int hpBarW = en.Rect.Width;
                    int hpBarH = 5;
                    int hpBarX = en.Rect.X;
                    int hpBarY = en.Rect.Top - 10;

                    // Vẽ nền đỏ (máu mất)
                    e.Graphics.FillRectangle(Brushes.Red, hpBarX, hpBarY, hpBarW, hpBarH);
                    // Vẽ máu xanh (máu còn)
                    float hpPercent = (float)en.CurrentHP / en.MaxHP;
                    e.Graphics.FillRectangle(Brushes.LimeGreen, hpBarX, hpBarY, (int)(hpBarW * hpPercent), hpBarH);
                }
            }
            // DEBUG: xem hitbox va chạm
            //var dbg = GetCollRect(player, facingRight);
            using (var pen = new Pen(Color.Lime, 2))
            {
                // Vẽ khung xanh lá cho Player
                //e.Graphics.DrawRectangle(pen, GetCollRect(player, facingRight));

                // Vẽ khung đỏ cho Enemy/Boss
                //foreach (var en in enemies)
                //{
                //    if (!en.IsDead) e.Graphics.DrawRectangle(Pens.Red, en.Rect);
                //}
            }

            // ===== VẼ GIAO DIỆN (UI) - THANH MÁU =====
            // 1. Cấu hình vị trí
            int barX = 50;  // Dịch sang phải một chút để nhường chỗ cho chữ HP
            int barY = 20;  // Cách mép trên 20px
            int barW = 200; // Chiều dài thanh máu
            int barH = 20;  // Chiều cao thanh máu

            // --- MỚI: VẼ CHỮ "HP" ---
            // Tạo font chữ: Arial, cỡ 12, in đậm
            string hp = "HP";
            using (Font hpFont = new Font("Arial", 14, FontStyle.Bold))            
            {
                // Vẽ bóng đen cho chữ nổi bật
                e.Graphics.DrawString(hp, hpFont, Brushes.Black, barX - 40 + 2, barY + 2);
                // Vẽ chữ "HP" màu đỏ, nằm bên trái thanh máu (barX - 40)
                e.Graphics.DrawString(hp, hpFont, Brushes.Red, barX - 40, barY);
            }
            // ------------------------

            // 2. Tính toán độ dài phần máu còn lại
            float percentage = (float)currentHealth / maxHealth;
            // Chặn không cho âm (nếu máu <= 0 thì phần trăm là 0)
            if (percentage <= 0) percentage = 0;
            int fillW = (int)(barW * percentage);

            // 3. Vẽ nền thanh máu (Màu xám - phần máu đã mất)
            e.Graphics.FillRectangle(Brushes.Gray, barX, barY, barW, barH);

            // 4. Vẽ lượng máu hiện tại (Màu đỏ tươi)
            // Nếu máu > 0 thì mới vẽ màu đỏ, nếu = 0 thì fillW = 0 nên không vẽ gì (hiện nền đen)
            e.Graphics.FillRectangle(Brushes.Red, barX, barY, fillW, barH);

            // 5. Vẽ khung viền cho thanh máu nổi bật
            using (Pen borderPen = new Pen(Color.White, 2)) // Đổi viền màu trắng cho nổi trên nền đen
            {
                e.Graphics.DrawRectangle(borderPen, barX, barY, barW, barH);
            }

            // VẼ ĐIỂM SỐ ( ngay dưới thanh máu )
            string scoreText = "SCORE: " + score.ToString(); //"SCORE: 5"

            using (Font scoreFont = new Font("Arial", 14, FontStyle.Bold))
            {
                float scoreX = barX - 43; // canh sao cho ngang hàng với chữ HP
                float scoreY = barY + 450; // bên dưới thanh máu 450px

                // Vẽ bóng đen cho chữ nổi bật
                e.Graphics.DrawString(scoreText, scoreFont, Brushes.Black, scoreX + 2, scoreY + 2);
                // Vẽ chữ màu vàng/trắng
                e.Graphics.DrawString(scoreText, scoreFont, Brushes.Gold, scoreX, scoreY);
            }

            // ===== VẼ MÀN HÌNH GAME OVER =====
            if (isGameOver)
            {
                // 1. Làm tối màn hình (Vẽ một lớp màu đen trong suốt đè lên game)
                // Color.FromArgb(150, 0, 0, 0): 150 là độ trong suốt (Alpha)
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
                {
                    e.Graphics.FillRectangle(brush, 0, 0, baseWidth, baseHeight);
                }

                // 2. VẼ ẢNH GAME OVER 
                if (gameOverImg != null)
                {
                    // Tính toán để ảnh nằm chính giữa màn hình
                    int imgW = gameOverImg.Width;
                    int imgH = gameOverImg.Height;

                    // Nếu ảnh to quá thì thu nhỏ lại (Optional)
                    // Ví dụ: Nếu ảnh rộng hơn màn hình thì scale về 400px
                    if (imgW > 400) { imgW = 400; imgH = (400 * gameOverImg.Height) / gameOverImg.Width; }

                    int x = (baseWidth - imgW) / 2;
                    int y = (baseHeight - imgH) / 2 - 50; // Dịch lên trên một chút cho cân đối

                    e.Graphics.DrawImage(gameOverImg, x, y, imgW, imgH);
                }
                else
                {
                    // Dự phòng: Nếu ảnh lỗi thì vẽ chữ tạm
                    e.Graphics.DrawString("GAME OVER", pnlGame.Font, Brushes.Red, 100, 100);
                }

                // 3. Vẽ dòng hướng dẫn "Press Enter"
                string text2 = "Nhấn ENTER để chơi lại";
                using (Font font2 = new Font("Arial", 16, FontStyle.Regular))
                {
                    SizeF size2 = e.Graphics.MeasureString(text2, font2);
                    float x2 = (baseWidth - size2.Width) / 2;
                    float y2 = (baseHeight / 2) + 70; // Nằm phía dưới ảnh một chút

                    e.Graphics.DrawString(text2, font2, Brushes.Red, x2, y2);
                }
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            runAnim.Sheet?.Dispose();
            jumpAnim.Sheet?.Dispose();
            idleAnim.Sheet?.Dispose();
            // xóa tất cả sheet sau khi tắt game
        }
    }
}
