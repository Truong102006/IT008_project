using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace LapTrinhTrucQuangProjectTest
{
    // THAY ĐỔI LỚP CƠ SỞ: Từ Form sang UserControl để nhúng vào Form chính
    public partial class MapLevel1 : UserControl
    {
        // ===== GAME VARIABLES =====
        Bitmap platformImg;
        Bitmap backgroundImg; // biến chứa ảnh nền (GIF hoặc PNG đều được)
        Bitmap portal;        // Cổng dịch chuyển
        Bitmap gameOverImg;
        bool isLoading = true; // Mặc định là true khi vừa mở game
        Bitmap loadingBg;      // Biến chứa ảnh nền màn hình chờ
        bool goLeft, goRight,goUp,goDown, jumping, onGround, levelTransitioning;
        int jumpSpeed = 0, force = 20, gravity = 8, playerSpeed = 3, currentLevel = 1;
        int score = 0;
        int maxHealth = 100;     // Máu tối đa
        int currentHealth = 100; // Máu hiện tại
        bool isGameOver = false;
        bool isSubmerged = false;
        bool isCollected = false;
        bool isClimbed = false;
        bool secretSpawned = false; // Biến kiểm tra xem đã hiện coin bí mật chưa
        int startX;
        int startY;

        // Các biến dùng cho việc Resize và Scale giao diện UserControl
        private Size _originalFormSize;
        private Rectangle _orgMenuRect;
        private float _orgMenuFontSize;
        private Rectangle _orgHomeRect;
        private float _orgHomeFontSize;
        private Rectangle _orgContinueRect;
        private float _orgContinueFontSize;
        private Rectangle _orgResetRect;
        private float _orgResetFontSize;
        private bool isPaused = false; // Trạng thái tạm dừng game

        Rectangle player, door;
        List<Rectangle> platforms = new List<Rectangle>();
        // Phân loại coin để có điểm số khác nhau
        List<Rectangle> coin1 = new List<Rectangle>();
        List<Rectangle> coin2 = new List<Rectangle>();
        List<Rectangle> coin3 = new List<Rectangle>();
        List<Rectangle> coin4 = new List<Rectangle>();

        // List quản lý các chữ bay (hiệu ứng sát thương/ăn điểm)
        List<FloatingText> floatingTexts = new List<FloatingText>();

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
        const int HB_BACK = 11, HB_FRONT = 11;

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
            // Thêm chỉ số cho Boss 
            public int MaxHP = 1;      // Máu tối đa (Quái thường = 1, Boss = 5)
            public int CurrentHP = 1; // Máu hiện tại
            public bool IsBoss = false; // Đánh dấu đây có phải Boss không
            public Enemy(int x, int y, int w, int h)
            {
                Rect = new Rectangle(x, y, w, h);
            }
        }

        // Khuôn mẫu cho chữ bay lên với hiệu ứng dần nhạt đi tại điểm ăn tiền hoặc mất máu
        class FloatingText
        {
            public string Text; // chữ muốn hiện
            public float X, Y; // tọa độ hiện chữ
            public int Alpha; // độ trong suốt (255 = rõ, 0 = tàng hình)
            public Color TextColor; // màu chữ
            public Brush TextBrush; // brush để vẽ

            public FloatingText(string text, int x, int y, Color color) // viết chữ text tại vị trí (x,y) với màu color
            {
                Text = text;
                X = x;
                Y = y;
                Alpha = 255; // lúc mới xuất hiện thì rõ nhất (255) và giảm dần về 0
                TextColor = color;
                TextBrush = new SolidBrush(color);
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
                    // Chỉ tính là vật thể rắn nếu tag không bắt đầu bằng "water_","deco_","trap_","stair_"
                    if (!t.Type.StartsWith("water_") && !t.Type.StartsWith("deco_") && !t.Type.StartsWith("trap_") && !t.Type.StartsWith("stair_"))
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
            public int DrawOffsetX = 0; //độ lệch vẽ (Mặc định 0) dùng để căn chỉnh hình ảnh khớp với hitbox

            public void Reset() { _accum = 0; FrameIndex = 0; }

            public void Update(double dt) // dt là thời gian mà mỗi lần update máy đã chạy được
            {
                if (Sheet == null || Frames.Count == 0 || FPS <= 0) return;
                _accum += dt; // mỗi lần update thì cộng số thời gian đã chạy bởi máy vào _accum
                double spf = 1.0 / FPS; // thời gian để chiếu 1 khung hình ( 1.0 / 10FPS = 0.1s-> mỗi 0.1s chiếu 1 khung hình nếu muốn 10FPS )
                while (_accum >= spf) // nếu máy đã update _accum lên đủ thời gian để hiện ra hình animation tiếp theo ( _accum = 0.1s thì đủ chạy 1 khung hình )
                {
                    _accum -= spf; // nếu đủ hoặc dư thì quay _accum lại khoảng bắt đầu và giữ phần thời gian đã được update dư ra để tính cho lần nạp hình animation tiếp theo
                    int next = FrameIndex + 1;
                    FrameIndex = (next >= Frames.Count) ? (Loop ? 0 : Frames.Count - 1) : next; // xử lý vòng lặp frame
                }
            }

            public void Draw(Graphics g, Rectangle hitbox, bool facingRight)
            {
                if (Sheet == null || Frames.Count == 0) return;
                Rectangle src = Frames[FrameIndex]; // lấy thông tin tọa độ của frame có số thứ tự [FrameIndex] để lưu vào src

                // Truyền DrawOffsetX vào hàm vẽ
                // Lưu ý: Khi quay trái (facingRight = false), ta có thể cần đảo dấu offset để hình không bị lệch
                int finalOffset = facingRight ? DrawOffsetX : -DrawOffsetX;

                DrawUtil.DrawRegion(g, Sheet, src, hitbox, facingRight, finalOffset); // dùng DrawRegion của class DrawUtil ở dưới để vẽ
            }
        }


        static class DrawUtil
        {
            public static void DrawRegion(Graphics g, Bitmap sheet, Rectangle src, Rectangle hitbox, bool facingRight, int xOffset = 0) // cắt hình src từ sheet và vẽ nó vào khu vực hitbox
            {
                float sx = (float)hitbox.Width / src.Width;
                float sy = (float)hitbox.Height / src.Height;
                float scale = Math.Min(sx, sy); // tính tỉ lệ xem khung hitbox to gấp bao nhiêu lần khung ảnh src

                int w = (int)(src.Width * scale); // dùng tỉ lệ đã tính đó để tính kích thước dài, rộng thật của hình
                int h = (int)(src.Height * scale);
                int x = hitbox.X + (hitbox.Width - w) / 2 + xOffset; // cộng thêm xOffset để căn chỉnh thủ công nếu cần
                int y = hitbox.Bottom - h; // dùng kích thước w,h của hình src để căn cho nhân vật nằm giữa hitbox

                var st = g.Save(); // 1. lưu trạng thái bình thường của game trước khi lật

                // Xử lý Lật hình (Flip) nếu nhân vật quay sang trái
                if (!facingRight)
                {
                    // Để lật hình tại chỗ mà không bị nhảy vị trí, ta phải xoay quanh TÂM của bức ảnh sẽ vẽ
                    float centerX = x + w / 2.0f;

                    // B1: Dời gốc toạ độ của bàn vẽ về đúng tâm bức ảnh
                    g.TranslateTransform(centerX, 0);

                    // B2: Lật ngược trục X (như soi gương)
                    g.ScaleTransform(-1, 1);

                    // B3: Dời gốc toạ độ ngược lại về vị trí cũ để không ảnh hưởng các hình khác
                    g.TranslateTransform(-centerX, 0);
                }

                // 4. Vẽ ảnh lên màn hình
                // Lưu ý: Lúc này toạ độ vẽ vẫn là (x, y, w, h), phép lật (nếu có) đã được xử lý bởi g.Transform ở trên
                g.DrawImage(sheet, new Rectangle(x, y, w, h), src, GraphicsUnit.Pixel);

                // 5. Khôi phục trạng thái đồ hoạ (Xóa bỏ phép xoay/lật vừa làm)
                g.Restore(st);
            }
        }

        // Hàm AddTileImage() dùng để thêm ảnh vào cho các ô tilesets để thiết kế map
        private void AddTileImage(string tag, string fileName)
        {
            try
            {
                string path = @"Images\" + fileName;
                if (File.Exists(path))
                {
                    // 1.nạp ảnh lên ram
                    Bitmap img = (Bitmap)Image.FromFile(path);
                    // 2. kích hoạt animation nếu là gif:
                    if (ImageAnimator.CanAnimate(img))
                    {
                        ImageAnimator.Animate(img, (s, e) => { });
                    }
                    // Nếu dictionary đã chứa tag thì cập nhật, chưa có thì thêm mới
                    if (tileAssets.ContainsKey(tag))
                        tileAssets[tag] = img;
                    else
                        tileAssets.Add(tag, img);
                }
            }
            catch { }
        }

        // File ảnh assets
        private readonly string RunPath = @"Images\Punk_run.png";
        private readonly string JumpPath = @"Images\Punk_jump.png";
        private readonly string IdlePath = @"Images\Punk_idle.png";
        private readonly string DoorPath = @"Images\flag.png";
        private readonly string CoinPath1 = @"Images\coin_1.png";
        private readonly string CoinPath2 = @"Images\coin_2.png";
        private readonly string CoinPath3 = @"Images\coin_3.png";
        private readonly string CoinPath4 = @"Images\coin_4.png";
        private readonly string DecoPath1 = @"Images\deco_arrow.png";
        private readonly string DecoPath2 = @"Images\deco_7.png";
        private readonly string EnemyPath = @"Images\enemy.png";
        private readonly string BossPath = @"Images\boss.png";
        private readonly string TrapPath1 = @"Images\trap_3.png";
        private readonly string TrapPath2 = @"Images\trap_4.png";
        private readonly string TrapPath3 = @"Images\trap_5.png";
        private readonly string TrapPath4 = @"Images\trap_6.png";
        private readonly string TrapPath5 = @"Images\trap_7.png";

        // Anim: tạo các đối tượng class SpriteAnim để quản lý hoạt ảnh
        SpriteAnim runAnim = new SpriteAnim { FPS = 10, Loop = true };
        SpriteAnim jumpAnim = new SpriteAnim { FPS = 10, Loop = true };
        SpriteAnim idleAnim = new SpriteAnim { FPS = 6, Loop = true };
        SpriteAnim doorAnim = new SpriteAnim { FPS = 10, Loop = true };
        SpriteAnim coinAnim1 = new SpriteAnim { FPS = 9, Loop = true };
        SpriteAnim coinAnim2 = new SpriteAnim { FPS = 9, Loop = true };
        SpriteAnim coinAnim3 = new SpriteAnim { FPS = 9, Loop = true };
        SpriteAnim coinAnim4 = new SpriteAnim { FPS = 7, Loop = true };
        SpriteAnim decoAnim1 = new SpriteAnim { FPS = 10, Loop = true };
        SpriteAnim decoAnim2 = new SpriteAnim { FPS = 10, Loop = true };
        SpriteAnim enemyAnim = new SpriteAnim { FPS = 8, Loop = true };
        SpriteAnim bossAnim = new SpriteAnim { FPS = 7, Loop = true };
        SpriteAnim trapAnim1 = new SpriteAnim { FPS = 20, Loop = true };
        SpriteAnim trapAnim2 = new SpriteAnim { FPS = 6, Loop = true };
        SpriteAnim trapAnim3 = new SpriteAnim { FPS = 6, Loop = true };
        SpriteAnim trapAnim4 = new SpriteAnim { FPS = 6, Loop = true };
        SpriteAnim trapAnim5 = new SpriteAnim { FPS = 6, Loop = true };

        AnimState currentState = AnimState.Idle;
        SpriteAnim currentAnim;
        bool facingRight = true;
        readonly Stopwatch sw = new Stopwatch(); // dùng để đo dt chính xác

        // THAY ĐỔI CONSTRUCTOR: Khởi tạo UserControl thay vì Form
        public MapLevel1()
        {
            InitializeComponent();
            // Nạp ảnh chờ NGAY LẬP TỨC trước khi làm các việc khác
            try
            {
                if (File.Exists(@"Images\loading_screen.png"))
                    loadingBg = (Bitmap)Image.FromFile(@"Images\loading_screen.png");
            }
            catch { /* Xử lý nếu file lỗi */ }
            CreateLevel1(); // nạp màn 1
            // Gắn sự kiện Load vào hàm khởi tạo game
            this.Load += MapLevel1_Load;
            // Gắn sự kiện Resize của UserControl
            this.Resize += MapLevel1_Resize;
        }

        // THAY ĐỔI TÊN HÀM: Form1_Load -> MapLevel1_Load
        private void MapLevel1_Load(object sender, EventArgs e)
        {
            // THAY THẾ: this.Deactivate -> this.LostFocus (vì UserControl dùng LostFocus)
            // Ghi chú: Cần set TabStop=true cho UserControl ở Designer để nó nhận được focus
            this.LostFocus += (s, e2) => { goLeft = false; goRight = false; };

            DoubleBuffered = true; // Bật bộ đệm đôi: chống giật hình

            _originalFormSize = this.ClientSize; // Lưu kích thước gốc để tính tỉ lệ resize UI

            // Lưu trữ vị trí gốc của các nút bấm menu để resize sau này
            if (btnMenu != null)
            {
                _orgMenuRect = btnMenu.Bounds;
                _orgMenuFontSize = btnMenu.Font.Size;
            }

            if (btnHome != null)
            {
                _orgHomeRect = btnHome.Bounds;
                _orgHomeFontSize = btnHome.Font.Size;
            }

            if (btnContinue != null)
            {
                _orgContinueRect = btnContinue.Bounds;
                _orgContinueFontSize = btnContinue.Font.Size;
            }

            if (btnReset != null)
            {
                _orgResetRect = btnReset.Bounds;
                _orgResetFontSize = btnReset.Font.Size;
            }


            currentAnim = idleAnim; currentAnim.Reset();
            gameTimer.Interval = 16;
            gameTimer.Tick += GameLoop;


            KeyDown += KeyIsDown;
            KeyUp += KeyIsUp;

            sw.Start();

            // ==== FIX: scale ngay khi khởi động ====
            UpdateScale();
            this.Refresh(); // Sử dụng Refresh() thay vì Invalidate() để ép WinForms vẽ ngay lập tức
            // XÓA: this.Shown += ... (Chỉ Form có)
            // this.Shown += (s, _) => UpdateScale();
            // TẠO ĐỘ TRỄ ĐỂ HIỆN ẢNH LOADING TRƯỚC
            Timer delayLoader = new Timer();
            delayLoader.Interval = 100; // Đợi 0.1 giây
            delayLoader.Tick += (s, ev) => {
                delayLoader.Stop();
                FinishLoadingAssets(); // Gọi hàm nạp ảnh nặng ở trên
                delayLoader.Dispose(); // Giải phóng timer tạm này sau khi dùng xong
            };
            delayLoader.Start();
        }

        private void FinishLoadingAssets()
        {
            // CHUYỂN TOÀN BỘ CODE NẠP ẢNH TỪ LOAD VÀO ĐÂY
            // Tinh chỉnh độ lệch vẽ cho nhân vật chính để chân khớp với hitbox
            runAnim.DrawOffsetX = 8;
            idleAnim.DrawOffsetX = 8;
            jumpAnim.DrawOffsetX = 8;
            // nếu cần đẩy sang trái thì dùng số âm
            // runAnim.DrawOffsetX = -4;


            LoadAnimationEven(RunPath, runAnim, 6, alphaThreshold: 16, tightenEdges: false);
            LoadAnimationEven(JumpPath, jumpAnim, 4, alphaThreshold: 16, tightenEdges: false);
            LoadAnimationEven(IdlePath, idleAnim, 4, alphaThreshold: 16, tightenEdges: false);
            LoadAnimationEven(DoorPath, doorAnim, 7, alphaThreshold: 16, tightenEdges: true);
            LoadAnimationEven(CoinPath1, coinAnim1, 7, alphaThreshold: 16, tightenEdges: true);
            LoadAnimationEven(CoinPath2, coinAnim2, 7, alphaThreshold: 16, tightenEdges: true);
            LoadAnimationEven(CoinPath3, coinAnim3, 7, alphaThreshold: 16, tightenEdges: true);
            LoadAnimationEven(CoinPath4, coinAnim4, 4, alphaThreshold: 16, tightenEdges: true);
            LoadAnimationEven(DecoPath1, decoAnim1, 7, alphaThreshold: 16, tightenEdges: true);
            LoadAnimationEven(DecoPath2, decoAnim2, 13, alphaThreshold: 16, tightenEdges: true);
            LoadAnimationEven(EnemyPath, enemyAnim, 12, alphaThreshold: 16, tightenEdges: true);
            LoadAnimationEven(BossPath, bossAnim, 6, alphaThreshold: 16, tightenEdges: true);
            LoadAnimationEven(TrapPath1, trapAnim1, 8, alphaThreshold: 16, tightenEdges: true);
            LoadAnimationEven(TrapPath2, trapAnim2, 4, alphaThreshold: 16, tightenEdges: true);
            LoadAnimationEven(TrapPath3, trapAnim3, 6, alphaThreshold: 16, tightenEdges: true);
            LoadAnimationEven(TrapPath4, trapAnim4, 6, alphaThreshold: 16, tightenEdges: true);
            LoadAnimationEven(TrapPath5, trapAnim5, 6, alphaThreshold: 16, tightenEdges: true);

            // NẠP ẢNH CHO PANEL (Vòng lặp for)
            for (int i = 1; i <= 86; i++) { AddTileImage("tile_" + i, "tile_" + i + ".png"); }
            for (int i = 1; i <= 68; i++) { if (i != 7 && i != 53 && i != 65) AddTileImage("deco_" + i, "deco_" + i + ".png"); }
            for (int i = 1; i <= 4; i++) { AddTileImage("deco_bui" + i, "deco_bui" + i + ".png"); }
            for (int i = 1; i <= 3; i++) { AddTileImage("deco_cay" + i, "deco_cay" + i + ".png"); }
            for (int i = 1; i <= 2; i++) { AddTileImage("deco_da" + i, "deco_da" + i + ".png"); }
            for (int i = 1; i <= 9; i++) { AddTileImage("deco_arm" + i, "deco_arm" + i + ".png"); }
            for (int i = 1; i <= 3; i++) { AddTileImage("stair_" + i, "stair_" + i + ".png"); }

            AddTileImage("tile_invisible", "tile_invisible.png");
            AddTileImage("deco_53", "deco_53.gif");
            AddTileImage("deco_65", "deco_65.gif");
            AddTileImage("deco_bui5.1", "deco_bui5.1.png");
            AddTileImage("deco_bui5.2", "deco_bui5.2.png");
            AddTileImage("deco_bui5.3", "deco_bui5.3.png");
            AddTileImage("trap_1", "trap_1.png");
            AddTileImage("water_2", "water4.gif");
            AddTileImage("water_3", "water5.gif");
            AddTileImage("water_4", "water6.gif");
            AddTileImage("water_5", "water7.gif");

            try
            {
                if (File.Exists(@"Images\platform.png")) platformImg = (Bitmap)Image.FromFile(@"Images\platform.png");
                if (File.Exists(@"Images\GameOverFont_2.png")) gameOverImg = (Bitmap)Image.FromFile(@"Images\GameOverFont_2.png");
            }
            catch { }

            LoadPortal(@"Images\portal.gif");

            // KẾT THÚC LOADING
            isLoading = false;
            gameTimer.Start(); // Bắt đầu chạy game sau khi nạp xong
            Invalidate();      // Vẽ lại để hiện Map
        }
        // LoadAnimationEven: Hàm helper nạp và cắt ảnh sprite sheet
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
                // B3: nếu tightenEdges = true thì gọi hàm cắt ngang để cắt bớt phần thừa 2 bên hông của từng khung hình

                for (int i = 0; i < frames.Count; i++)
                    frames[i] = new Rectangle(frames[i].Left, top, frames[i].Width, height);
                // B4: với mỗi frame ta thay thế chúng với một rectangle mới đã được căn chỉnh

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
            // đảm bảo số lượng frame ít nhất là 1 để tránh chia cho 0
            int sliceW = bmp.Width / frameCount;
            // tính chiều rộng mỗi frame = tổng chiều rộng / số lượng
            for (int i = 0; i < frameCount; i++)
            {
                int sx = i * sliceW;
                // tính tọa độ x bắt đầu cắt
                int width = (i == frameCount - 1) ? bmp.Width - sx : sliceW;
                // tính chiều rộng của frame này, nếu là frame cuối thì lấy hết phần còn lại
                frames.Add(new Rectangle(sx, 0, width, bmp.Height));
            }
            return frames;
        }

        // hàm đồng bộ chiều cao GetGlobalVerticalBounds, tìm đỉnh đầu cao nhất và điểm dưới chân thấp nhất:
        private Tuple<int, int> GetGlobalVerticalBounds(Bitmap bmp, int alphaThreshold)
        {
            int top = int.MaxValue, bottom = -1;
            // đặt cột mốc ban đầu là giá trị cực đại và cực tiểu để làm điểm tựa
            for (int y = 0; y < bmp.Height; y++) // quét từng dòng (Y) từ trên xuống
                for (int x = 0; x < bmp.Width; x++) // quét từng điểm (X) từ trái sang phải
                    if (bmp.GetPixel(x, y).A > alphaThreshold)
                    // kiểm tra điểm có nhìn thấy được không (độ đục A > ngưỡng)
                    {
                        if (y < top) top = y; // cập nhật đỉnh cao nhất
                        if (y > bottom) bottom = y; // cập nhật đáy thấp nhất
                    }
            // nếu ảnh không có điểm nào thấy được thì lấy chiều cao gốc của ảnh
            if (bottom < top) { top = 0; bottom = bmp.Height - 1; }
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
                    for (int y = s.Top; y < s.Bottom; y++)
                        if (bmp.GetPixel(x, y).A > alphaThreshold) // kiểm tra pixel có màu hay không
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            // tìm mép trái nhất và mép phải nhất có chứa frame
                        }
                if (maxX < minX) result.Add(s);
                // nếu không tìm thấy hình -> giữ nguyên như cũ
                else result.Add(Rectangle.FromLTRB(minX, s.Top, maxX + 1, s.Bottom));
                // nếu tìm thấy, tạo khung hình mới gọn gàng hơn
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
                        ImageAnimator.Animate(backgroundImg, (s, e) => { });
                    }
                }
            }
            catch { }
        }

        // Hàm nạp ảnh cổng dịch chuyển (Portal) - tương tự LoadBackground
        private void LoadPortal(string path)
        {
            try
            {
                if (portal != null)
                {
                    ImageAnimator.StopAnimate(portal, (s, e) => { });
                    portal.Dispose();
                    portal = null;
                }

                if (File.Exists(path))
                {
                    portal = (Bitmap)Image.FromFile(path);

                    if (ImageAnimator.CanAnimate(portal))
                    {
                        ImageAnimator.Animate(portal, (s, e) => { });
                    }
                }
            }
            catch { }
        }

        // hàm tạo chữ bay:
        private void AddFloatingText(string text, int x, int y, Color color)
        {
            // tạo chữ mới ngay tại vị trí x, y truyền vào
            // trừ bớt Y đi 10px và cộng thêm X 10px để chữ hiện ngay phía trên bên phải vị trí mất máu hoặc ăn tiền
            floatingTexts.Add(new FloatingText(text, x + 10, y - 10, color));
        }

        // ===== WINDOW & GAME LOOP (ĐÃ SỬA ĐỔI) =====
        private void UpdateScale()
        {
            int w = Math.Max(1, this.ClientSize.Width);
            int h = Math.Max(1, this.ClientSize.Height);
            // lấy chiều rộng w và chiều dài h hiện tại của cửa sổ game
            scaleX = (float)w / baseWidth;
            scaleY = (float)h / baseHeight;
            // tính tỉ lệ cần thiết bằng cách chia kích thước hiện tại với kích thước gốc
            Invalidate();
            // Invalidate() để báo cáo máy chủ thực hiện vẽ lại màn hình theo tỷ lệ mới
        }

        // THAY ĐỔI TÊN HÀM: Form1_Resize -> MapLevel1_Resize
        private void MapLevel1_Resize(object sender, EventArgs e)
        {
            UpdateScale(); // dùng helper chung để scale đồ họa game
            // Tính toán tỉ lệ thay đổi kích thước của UserControl so với kích thước gốc
            float xRatio = (float)this.ClientSize.Width / _originalFormSize.Width;
            float yRatio = (float)this.ClientSize.Height / _originalFormSize.Height;
            // Cập nhật vị trí và kích thước của các nút bấm UI dựa theo tỉ lệ mới
            UpdateControlSize(btnMenu, _orgMenuRect, _orgMenuFontSize, xRatio, yRatio);
            UpdateControlSize(btnHome, _orgHomeRect, _orgHomeFontSize, xRatio, yRatio);
            UpdateControlSize(btnContinue, _orgContinueRect, _orgContinueFontSize, xRatio, yRatio);
            UpdateControlSize(btnReset, _orgResetRect, _orgResetFontSize, xRatio, yRatio);
        }
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
        private void btnMenu_Click(object sender, EventArgs e)
        {
            if (isGameOver) return;

            // 1. Dừng game
            isPaused = true;
            gameTimer.Stop();

            // 2. Hiện nút Continue và Reset
            if (btnContinue != null)
            {
                btnContinue.Visible = true;
                btnContinue.BringToFront(); // Đảm bảo nút nổi lên trên cùng
            }
            if (btnReset != null)
            {
                btnReset.Visible = true;
                btnReset.BringToFront();
            }

            // 3. Vẽ lại màn hình (để hiện lớp đen mờ ở bước sau)
            this.Invalidate();
        }
        private void btnHome_Click(object sender, EventArgs e)
        {
            // 1. Dừng game ngay lập tức để tránh lỗi chạy ngầm
            gameTimer.Stop();

            // 2. Tìm Form cha và gọi hàm quay về
            var mainForm = this.TopLevelControl as LapTrinhTrucQuangProjectTest.MainMenuForm;

            if (mainForm != null)
            {
                mainForm.ReturnToMainMenu();
            }
        }
        private void btnContinue_Click(object sender, EventArgs e)
        {
            // 1. Ẩn nút và bỏ trạng thái Pause
            isPaused = false;
            if (btnContinue != null) btnContinue.Visible = false;
            if (btnReset != null) btnReset.Visible = false;

            // 2. Trả lại tiêu điểm cho UserControl để nhận phím di chuyển
            this.Focus();

            // 3. Chạy lại game
            gameTimer.Start();
            this.Invalidate();
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            // 1. Ẩn nút và bỏ trạng thái Pause
            isPaused = false;
            if (btnContinue != null) btnContinue.Visible = false;
            if (btnReset != null) btnReset.Visible = false;

            // 2. Reset máu
            currentHealth = maxHealth;

            // 3. Load lại màn chơi hiện tại
            // (Hàm này sẽ xóa hết quái/vàng cũ và tạo lại mới)
            switch (currentLevel)
            {
                case 1: CreateLevel1(); break;
                case 2: CreateLevel2(); break;
                case 3: CreateLevel3(); break;
                case 4: CreateLevel4(); break;
                //case 5: CreateLevel5(); break;
                //case 6: CreateLevel6(); break;
                default: CreateLevel1(); break;
            }

            // 4. Reset nhân vật
            jumping = false;
            onGround = false;
            jumpSpeed = 0;
            goLeft = false; goRight = false;
            isGameOver = false;

            // 5. Bắt đầu lại
            this.Focus();
            gameTimer.Start();
            this.Invalidate();
        }

        // hàm giải quyết va chạm GetCollRect() dùng để xác định chính xác hitbox của nhân vật
        private Rectangle GetCollRect(Rectangle r, bool faceRight)
        {
            // xác định lề thụt vào (Inset) tùy theo hướng mặt
            // nếu quay phải: Lưng là bên trái (HB_BACK), Mặt là bên phải (HB_FRONT)
            int leftInset = faceRight ? HB_BACK : HB_FRONT;
            int rightInset = faceRight ? HB_FRONT : HB_BACK;

            int x = r.X + leftInset; // dịch vào từ trái
            int y = r.Y + HB_TOP; // dịch xuống từ đầu

            int w = Math.Max(1, r.Width - leftInset - rightInset);
            // tính chiều rộng mới của hitbox bằng cách trừ cho thụt lề trái và thụt lề phải
            int h = Math.Max(1, r.Height - HB_TOP - HB_BOTTOM);
            // tính chiều cao mới tương tự
            return new Rectangle(x, y, w, h);
        }
        private void GameLoop(object sender, EventArgs e)
        {
            double dt = sw.Elapsed.TotalSeconds; sw.Restart();
            if (dt <= 0 || dt > 0.05) dt = 1.0 / 60.0;
            // đo thời gian đã trôi qua dt kể từ lần chạy trước, nếu máy quá lag thì ép thời gian về 1/60s

            int prevX = player.X;
            int prevY = player.Y;
            bool prevFace = facingRight; // lưu hướng quay mặt cũ lại
            Rectangle prevColl = GetCollRect(new Rectangle(prevX, prevY, player.Width, player.Height), prevFace);
            Rectangle playerHitbox = GetCollRect(player, facingRight);
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                Enemy en = enemies[i];
                if (en.IsDead) continue;

                // A. Di chuyển
                int moveStep = en.FacingRight ? en.Speed : -en.Speed;
                en.Rect.X += moveStep;
                en.Rect.Y += 4;
                // B. AI: Quay đầu khi gặp tường hoặc hết đường
                int lookAhead = 10;
                int sensorX = en.FacingRight ? (en.Rect.Right + 2) : (en.Rect.Left - 2 - lookAhead);

                // Sensor tường (Check ngay trước mặt)
                Rectangle wallSensor = new Rectangle(en.FacingRight ? en.Rect.Right : en.Rect.Left - 2, en.Rect.Y, 2, en.Rect.Height - 5);
                // Sensor đất (Check dưới chân + phía trước 1 khoảng)
                Rectangle groundSensor = new Rectangle(en.FacingRight ? en.Rect.Right : en.Rect.Left - lookAhead, en.Rect.Bottom, lookAhead, 4);

                bool hitWall = false;
                bool hasGround = false;
                foreach (var p in Solid)
                {
                    // Nếu Boss rơi trúng platform thì đẩy ngược lên cho đứng vừa khít
                    if (en.Rect.IntersectsWith(p))
                    {
                        if (en.Rect.Bottom > p.Top && en.Rect.Top < p.Top)
                        {
                            en.Rect.Y = p.Top - en.Rect.Height;
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
                    bool isAbove = prevColl.Bottom <= en.Rect.Top + (en.Rect.Height / 2);

                    if (isFallingAttack && isAbove)
                    {
                        // nhảy vào đầu enemy => PLAYER ĐẠP TRÚNG ĐẦU

                        // 1. Trừ máu enemy
                        en.CurrentHP--;
                        // Hiện chữ sát thương (Critical hit) màu cam
                        AddFloatingText("CRIT!! +5", player.X, player.Y, Color.OrangeRed);

                        // 2. Nảy người chơi lên
                        jumping = true;
                        onGround = false;
                        jumpSpeed = en.IsBoss ? 30 : 15; // Nảy cao hơn nếu đạp Boss

                        // 3. Kiểm tra chết
                        if (en.CurrentHP <= 0)
                        {
                            en.IsDead = true;
                            score += (en.IsBoss ? 100 : 5);

                            if (en.IsBoss)
                            {
                                MessageBox.Show("YOU WIN! Đã tiêu diệt Boss!");
                            }
                        }
                    }
                    else
                    {
                        // xử lí va chạm enemy => PLAYER BỊ ĐÁNH

                        // Tạo vùng sát thương nhỏ hơn nằm bên trong con quái ("LÕI")
                        Rectangle damageZone = new Rectangle(
                            en.Rect.X + 10,
                            en.Rect.Y + 20,
                            Math.Max(1, en.Rect.Width - 20),
                            Math.Max(1, en.Rect.Height - 20)
                        );

                        if (playerHitbox.IntersectsWith(damageZone))
                        {
                            if (currentHealth > 0)
                            {
                                currentHealth -= (en.IsBoss ? 30 : 10);
                                // Hiện chữ mất máu bay lên đầu nhân vật
                                if (en.IsBoss) { AddFloatingText("-30", player.X, player.Y, Color.Red); }
                                else { AddFloatingText("-10", player.X, player.Y, Color.Red); }

                                // Hiệu ứng bị đẩy lùi (Knockback)
                                if (player.X < en.Rect.X) player.X -= 50;
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
            // Xử lí tương tác với trap,stair và water:
            bool hitTrap = false;
            isSubmerged = false; // Reset trạng thái nước mỗi khung hình
            isCollected = false; // Reset trạng thái nhặt coin mỗi khung hình
            isClimbed = false; // Reset trạng thái leo cầu thang mỗi khung hình

            foreach (var t in tiles)
            {
                if (t.Type.StartsWith("water_") || t.Type.StartsWith("deco_arm"))
                {
                    if (playerHitbox.IntersectsWith(t.Rect))
                    {
                        isSubmerged = true; // Bật trạng thái: Đang ở trong nước
                    }
                }
                if (t.Type.StartsWith("stair_"))
                {
                    if (playerHitbox.IntersectsWith(t.Rect))
                    {
                        // Nếu đáy hitbox nhân vật chạm đỉnh thang (cách 10px) -> Tự động nảy lên
                        if (player.Bottom <= t.Rect.Top + 10)
                        {
                            // Quan trọng: Tắt trạng thái leo để trọng lực hoạt động lại
                            isClimbed = false;

                            jumping = true;
                            onGround = false;
                            jumpSpeed = 15;

                            
                        }
                        else
                        {
                            // Nếu chưa tới đỉnh -> Bật trạng thái leo
                            isClimbed = true;
                        }
                    }
                }
                if (t.Type.StartsWith("trap_"))
                {
                    if (playerHitbox.IntersectsWith(t.Rect))
                    {
                        if (!hitTrap)
                        {
                            currentHealth -= 10;
                            AddFloatingText("-10", player.X, player.Y, Color.Red);
                            hitTrap = true; // Đánh dấu đã dính, không trừ liên tục
                        }
                        // Hất tung nhân vật lên
                        jumping = true;
                        onGround = false;
                        jumpSpeed = 20;

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

            if (isClimbed)
            {                
                // Reset các trạng thái nhảy/rơi tự do để nhân vật bám dính vào cầu thang
                jumping = false;
                jumpSpeed = 0;
                // Kéo lên trên 
                if (goUp) player.Y -= 3;
                if (goDown) player.Y += 3;
            }

            // Nhảy + trọng lực: giải quyết chạm trần
            if (jumping) { player.Y -= jumpSpeed; jumpSpeed -= 1; }
            if (!onGround && !isClimbed)
            {
                int currentGravity = isSubmerged ? 2 : gravity; // Ở dưới nước rơi chậm hơn
                player.Y += currentGravity;
            }
            Rectangle coll = GetCollRect(player, facingRight); // tính toán hitbox mới sau khi Y thay đổi

            if (player.Y < prevY) // chỉ xét nếu nhân vật đang nhảy lên
            {
                foreach (var p in Solid)
                {
                    bool overlapX = coll.Right > p.Left && coll.Left < p.Right;
                    bool crossedBottom = (coll.Top <= p.Bottom) && (prevColl.Top >= p.Bottom);

                    if (overlapX && crossedBottom) // nếu nhảy đụng trần platform
                    {
                        player.Y = p.Bottom - HB_TOP;
                        coll = GetCollRect(player, facingRight);
                        jumping = false; jumpSpeed = 0; // rớt xuống ngay
                        onGround = false;
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



            //GIỚI HẠN BIÊN
            // 1. Tính toán lề thụt vào hiện tại (Logic giống hệt GetCollRect)
            int currentLeftInset = facingRight ? HB_BACK : HB_FRONT;
            int currentRightInset = facingRight ? HB_FRONT : HB_BACK;

            // 2. Chặn bên TRÁI: Cho phép X âm một đoạn đúng bằng lề trái để Hitbox chạm đúng điểm 0
            if (player.X < -currentLeftInset)
            {
                player.X = -currentLeftInset;
            }

            // 3. Chặn bên PHẢI: Cho phép lấn ra ngoài để Hitbox chạm đúng điểm baseWidth
            int rightLimit = baseWidth - player.Width + currentRightInset;
            if (player.X > rightLimit)
            {
                player.X = rightLimit;
            }
            if (player.Y > baseHeight) // rớt xuống vực
            {
                currentHealth -= 100;

                // Hồi sinh về vị trí cũ
                player.X = startX;
                player.Y = startY;

                jumping = false;
                jumpSpeed = 0;
                goLeft = false;
                goRight = false;

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


            // Cửa:
            if (coll.IntersectsWith(door) && !levelTransitioning)
            {
                levelTransitioning = true;
                NextLevel(); // Chuyển màn
                goLeft = false;
                goRight = false;
                jumping = false;
                jumpSpeed = 0;
            }

            // Coin: (Xử lý cho 4 loại coin khác nhau)
            for (int i = coin1.Count - 1; i >= 0; i--)
            {
                if (coll.IntersectsWith(coin1[i]) && isCollected == false)
                {
                    isCollected = true;
                    score += 1;
                    AddFloatingText("+1", player.X, player.Y, Color.Gold); // Hiện chữ +1 vàng
                    coin1.RemoveAt(i);
                }
            }
            for (int i = coin2.Count - 1; i >= 0; i--)
            {
                if (coll.IntersectsWith(coin2[i]) && isCollected == false)
                {
                    isCollected = true;
                    score += 5;
                    AddFloatingText("+5", coin2[i].X, coin2[i].Y, Color.Pink); // Hiện chữ +5 hồng
                    coin2.RemoveAt(i);
                }
            }
            for (int i = coin3.Count - 1; i >= 0; i--)
            {
                if (coll.IntersectsWith(coin3[i]) && isCollected == false)
                {
                    isCollected = true;
                    score += 10;
                    AddFloatingText("SECRET!! +10", coin3[i].X, coin3[i].Y, Color.Crimson); // Hiện chữ bí mật
                    coin3.RemoveAt(i);
                }
            }
            for (int i = coin4.Count - 1; i >= 0; i--)
            {
                if (coll.IntersectsWith(coin4[i]) && isCollected == false)
                {
                    isCollected = true;
                    jumping = true;
                    onGround = false;
                    jumpSpeed = 23;
                    AddFloatingText("UP!!!", coin4[i].X, coin4[i].Y, Color.Pink); // Hiện chữ UP
                    coin4.RemoveAt(i);
                    if (coin4.Count == 1 && secretSpawned == false) // khi đã ăn hết coin4 thì chạy đoạn code sau:
                    {
                        secretSpawned = true;
                        int spawnX = 0; int spawnY = 0; // vị trí xuất hiện coin4 secret
                        if (currentLevel == 3)
                        {
                            spawnX = 320;
                            spawnY = 290;
                        }
                        coin4.Add(new Rectangle(spawnX, spawnY, 32, 32)); // tạo coin4 tại tọa độ cần thiết để qua màn
                        AddFloatingText("SECRET APPEARED!!", spawnX, spawnY, Color.Pink);
                        coin4.Add(new Rectangle(500, 420, 32, 32)); // tạo thêm coin4 khác tại tọa đô nếu cần
                        AddFloatingText("SECRET APPEARED!!", 530, 420, Color.Pink);
                    }
                }
            }

            // XỬ LÝ CHỮ BAY (FADING TEXT):
            for (int i = floatingTexts.Count - 1; i >= 0; i--)
            {
                var ft = floatingTexts[i];

                // 1. bay lên trên
                ft.Y -= 2; // tốc độ bay lên (càng lớn càng nhanh)

                // 2. mờ dần
                ft.Alpha -= 10; // tốc độ mờ (càng lớn càng nhanh mờ)

                // 3. nếu đã mờ hết cỡ (Alpha <= 0) thì xóa chữ luôn
                if (ft.Alpha <= 0)
                {
                    ft.TextBrush.Dispose(); // Giải phóng 
                    floatingTexts.RemoveAt(i);
                }
                else
                {
                    // cập nhật màu với độ trong suốt mới để tạo hiệu ứng mờ dần
                    ft.TextBrush = new SolidBrush(Color.FromArgb(ft.Alpha, ft.TextColor));
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
            // gọi hàm tính toán thời gian cho bộ animation hiện tại để chuyển khung hình
            doorAnim.Update(dt);
            coinAnim1.Update(dt);
            coinAnim2.Update(dt);
            coinAnim3.Update(dt);
            coinAnim4.Update(dt);
            // gọi hàm tương tự cho animation cửa qua màn và các loại coin trong game
            decoAnim1.Update(dt);
            decoAnim2.Update(dt);
            trapAnim1.Update(dt);
            trapAnim2.Update(dt);
            trapAnim3.Update(dt);
            trapAnim4.Update(dt);
            trapAnim5.Update(dt);
            enemyAnim.Update(dt);
            bossAnim.Update(dt);

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


        // ===== INPUT (KHÔNG THAY ĐỔI) =====
        private void KeyIsDown(object sender, KeyEventArgs e)
        {
            if (isGameOver)
            {
                if (e.KeyCode == Keys.R)
                {
                    // Reset game khi bấm R
                    isGameOver = false;
                    currentHealth = maxHealth;
                    CreateLevel1();
                    currentLevel = 1; // Reset về màn 1
                    gameTimer.Start();
                }
                return; // Không làm gì khác khi đang Game Over
            }

            if (e.KeyCode == Keys.A) goLeft = true;
            if (e.KeyCode == Keys.D) goRight = true;

            if (e.KeyCode == Keys.W)
            {
                if (isClimbed)
                {
                    // Nếu đang leo thang -> W là đi lên
                    goUp = true;
                }
                else if (!jumping) // !jumping đảm bảo chỉ cho phép nhảy khi nhân vật không đang trong trạng thái nhảy sẵn
                {
                    // Nếu KHÔNG leo thang -> W là nhảy 
                    if (onGround || IsTouchingPlatformBelow()) // khi nhân vật đang onGround hoặc IsTouchingPLatFormBelow() là true tức là chân nhân vật vừa chạm một platform -> cho phép nhảy ngay sau khi chạm đất
                    {
                        jumping = true;
                        jumpSpeed = force;
                        onGround = false;
                    }
                }
            }
            // Thêm nút S để đi xuống
            if (e.KeyCode == Keys.S) goDown = true;

            // bấm nút i thì sẽ chuyển tới màn thứ i ( khi xong game sẽ xóa tính năng này )
            for (int i = 1; i <= 4; i++)
            {
                Keys key = (Keys)((int)Keys.D0 + i);
                if (e.KeyCode == key)
                {
                    currentLevel = i;
                    LoadLevel(i);
                }
            }
        }

        private void MapLevel1_Load_1(object sender, EventArgs e)
        {

        }

        private void KeyIsUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.A) goLeft = false;
            if (e.KeyCode == Keys.D) goRight = false;
            if (e.KeyCode == Keys.W) goUp = false;
            if (e.KeyCode == Keys.S) goDown = false;

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


        // ===== LEVELS (THAY ĐỔI LoadMapFromContainer) =====
        private void NextLevel()
        {
            currentLevel++;
            if (currentLevel > 4) { gameTimer.Stop(); MessageBox.Show("🎉 Bạn đã hoàn thành tất cả các màn!"); ; return; }
            platforms.Clear();
            // xóa hết dữ liệu platform của màn chơi cũ khỏi bộ nhớ
            switch (currentLevel)
            {
                case 2: CreateLevel2(); break;
                case 3: CreateLevel3(); break;
                case 4: CreateLevel4(); break;
                //case 5: CreateLevel5(); break;
                //case 6: CreateLevel6(); break;
            }
            levelTransitioning = false;
            // tắt flag chuyển màn để chuẩn bị cho lần chạm cửa tiếp theo
        }

        // --- Hàm HÚT dữ liệu từ giao diện thiết kế (Toolbox) ---
        private void LoadMapFromContainer(Control container)
        {
            platforms.Clear(); // Xóa dữ liệu cũ
            tiles.Clear();
            enemies.Clear();
            coin1.Clear();
            coin2.Clear();
            coin3.Clear();
            coin4.Clear();
            // SỬ DỤNG container.Controls
            foreach (Control c in container.Controls)
            {
                string tag = c.Tag?.ToString(); // Lấy tag an toàn

                if (tag == "platform")
                {
                    platforms.Add(new Rectangle(c.Left, c.Top, c.Width, c.Height));
                    c.Visible = false;
                }
                if (tag != null && (tag.StartsWith("tile_") || tag.StartsWith("trap_") || tag.StartsWith("water_") || tag.StartsWith("deco_") || tag.StartsWith("stair_")))
                {
                    tiles.Add(new Tile(c.Left, c.Top, c.Width, c.Height, tag));
                    c.Visible = false;
                }
                if (tag == "door")
                {
                    door = new Rectangle(c.Left, c.Top, c.Width, c.Height);
                    c.Visible = false;
                }
                if (tag != null && tag.StartsWith("coin_1"))
                {
                    coin1.Add(new Rectangle(c.Left, c.Top, c.Width, c.Height));
                    c.Visible = false;
                }
                if (tag != null && tag.StartsWith("coin_2"))
                {
                    coin2.Add(new Rectangle(c.Left, c.Top, c.Width, c.Height));
                    c.Visible = false;
                }
                if (tag != null && tag.StartsWith("coin_3"))
                {
                    coin3.Add(new Rectangle(c.Left, c.Top, c.Width, c.Height));
                    c.Visible = false;
                }
                if (tag != null && tag.StartsWith("coin_4"))
                {
                    coin4.Add(new Rectangle(c.Left, c.Top, c.Width, c.Height));
                    c.Visible = false;
                }
                if (tag == "player")
                {
                    player.X = c.Left;
                    player.Y = c.Top;
                    player = new Rectangle(c.Left, c.Top, 40, 40);
                    c.Visible = false;
                }
                if (tag == "enemy")
                {
                    // Tạo quái tại vị trí Panel, kích thước chuẩn 40x40
                    enemies.Add(new Enemy(c.Left, c.Top, 40, 40));
                    c.Visible = false;
                }
                if (tag == "boss")
                {
                    // Boss to hơn và trâu hơn
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
            // 'this' bây giờ là MapLevel1 (User Control)
            LoadMapFromContainer(this);
            LoadBackground(@"Images\background1.gif");
        }
        private void CreateLevel2()
        {
            // Các class MapLevelX (ví dụ MapLevel2) phải được chuyển thành UserControl
            // Tạo ra tờ bản đồ Level 2 ảo
            MapLevel2 map = new MapLevel2();
            // Hút dữ liệu từ tờ bản đồ đó vào game
            LoadMapFromContainer(map);
            LoadBackground(@"Images\background3.gif");
            // Hút xong thì xóa tờ bản đồ đó đi cho nhẹ máy
            map.Dispose();
        }
        private void CreateLevel3()
        {
            MapLevel3 map = new MapLevel3();
            LoadMapFromContainer(map);
            LoadBackground(@"Images\background4.gif");
            map.Dispose();
        }
        private void CreateLevel4()
        {
            MapLevel4 map = new MapLevel4();
            LoadMapFromContainer(map);
            map.Dispose();
        }
        //private void CreateLevel5()
        //{
        //    MapLevel4 map = new MapLevel4();
        //    LoadMapFromContainer(map);
        //    map.Dispose();
        //}
        //k dung den man 6
        //private void CreateLevel6()
        //{
        //    MapLevel5 map = new MapLevel5();
        //    LoadMapFromContainer(map);
        //    map.Dispose();
        //}

        // Hàm load màn chơi: dùng để reset lại toàn bộ dữ liệu khi cheat hoặc chọn màn
        private void LoadLevel(int level)
        {
            // 1. Dọn dẹp màn cũ 
            platforms.Clear();
            tiles.Clear();
            enemies.Clear();
            coin1.Clear(); coin2.Clear(); coin3.Clear(); coin4.Clear();
            secretSpawned = false;
            // clear các list khác nếu có 

            // 2. Gọi hàm tạo màn tương ứng
            switch (level)
            {
                case 1: CreateLevel1(); break;
                case 2: CreateLevel2(); break;
                case 3: CreateLevel3(); break;
                case 4: CreateLevel4(); break;
                //case 5: CreateLevel5(); break;
                //case 6: CreateLevel6(); break;
                default: return; // Nếu số không hợp lệ thì thoát
            }

            // 3. Reset trạng thái nhân vật
            currentLevel = level;
            currentHealth = maxHealth;
            jumping = false;
            onGround = false;
            jumpSpeed = 0;
            isGameOver = false;
            goLeft = false;
            goRight = false;

            // 4. Khởi động lại game
            gameTimer.Start();
            Invalidate();
        }
        // ===== DRAW =====
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); // gọi hàm vẽ của Windows
            e.Graphics.ScaleTransform(scaleX, scaleY); // áp dụng tỷ lệ scale của hàm UpdateScale cho form, để các nét vẽ đều được scale theo tỷ lệ form

            if (isLoading)
            {
                // Xóa sạch màu trắng cũ bằng màu đen
                e.Graphics.Clear(Color.Black);

                if (loadingBg != null)
                {
                    e.Graphics.DrawImage(loadingBg, 0, 0, baseWidth, baseHeight);
                }

                // Hiện thêm chữ Loading... cho chắc chắn
                e.Graphics.DrawString("LOADING...", new Font("Arial", 12), Brushes.White, 10, 10);

                return;
            }

            // vẽ background:
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

            // vẽ cổng qua màn:
            if (portal != null)
            {
                // Cập nhật khung hình tiếp theo cho GIF (để nó chuyển động)
                ImageAnimator.UpdateFrames(portal);

                // Vẽ Portal vào vị trí của Door
                e.Graphics.DrawImage(portal, door);
            }
            else
            {
                // Vẽ dự phòng nếu ảnh bị lỗi hoặc chưa load xong
                e.Graphics.FillRectangle(Brushes.Gold, door);
            }

            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor; // chế độ phóng to/thu nhỏ hình ảnh, giữ cho pixel vuông vắn
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half; // giúp căn chỉnh pixel chính xác hơn

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
            }

            foreach (var t in tiles)
            {
                // Các hiệu ứng đặc biệt cho trap và deco cụ thể
                if (t.Type == "trap_3") { trapAnim1.Draw(e.Graphics, t.Rect, false); continue; }
                if (t.Type == "trap_4") { trapAnim2.Draw(e.Graphics, t.Rect, false); continue; }
                if (t.Type == "trap_5") { trapAnim3.Draw(e.Graphics, t.Rect, false); continue; }
                if (t.Type == "trap_6") { trapAnim4.Draw(e.Graphics, t.Rect, false); continue; }
                if (t.Type == "trap_7") { trapAnim5.Draw(e.Graphics, t.Rect, false); continue; }
                if (t.Type == "deco_arrow") { decoAnim1.Draw(e.Graphics, t.Rect, false); continue; }
                if (t.Type == "deco_7") { decoAnim2.Draw(e.Graphics, t.Rect, false); continue; }

                // Tìm ảnh trong kho dựa theo Tag (t.Type)
                if (tileAssets.ContainsKey(t.Type) && tileAssets[t.Type] != null)
                {
                    Bitmap img = tileAssets[t.Type];
                    ImageAnimator.UpdateFrames(img); // Dòng này dùng để cập nhật frame cho file gif
                    e.Graphics.DrawImage(img, t.Rect);
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

            // Vẽ các loại coin khác nhau
            foreach (var c in coin1)
            {
                if (coinAnim1.Sheet != null)
                {
                    coinAnim1.Draw(e.Graphics, c, true);
                }
                else
                {
                    e.Graphics.FillRectangle(Brushes.Gold, c);
                }
            }
            foreach (var c in coin2)
            {
                if (coinAnim2.Sheet != null)
                {
                    coinAnim2.Draw(e.Graphics, c, true);
                }
                else
                {
                    e.Graphics.FillRectangle(Brushes.Gold, c);
                }
            }
            foreach (var c in coin3)
            {
                if (coinAnim3.Sheet != null)
                {
                    coinAnim3.Draw(e.Graphics, c, true);
                }
                else
                {
                    e.Graphics.FillRectangle(Brushes.Gold, c);
                }
            }
            foreach (var c in coin4)
            {
                if (coinAnim4.Sheet != null)
                {
                    coinAnim4.Draw(e.Graphics, c, true);
                }
                else
                {
                    e.Graphics.FillRectangle(Brushes.Gold, c);
                }
            }
            foreach (var en in enemies)
            {
                if (en.IsDead) continue;

                // 1. VẼ HÌNH ẢNH
                if (en.IsBoss && bossAnim.Sheet != null)
                {
                    bossAnim.Draw(e.Graphics, en.Rect, en.FacingRight);
                }
                else if (!en.IsBoss && enemyAnim.Sheet != null)
                {
                    enemyAnim.Draw(e.Graphics, en.Rect, !en.FacingRight); // Quái thường có thể cần lật ngược lại tùy sprite
                }
                else
                {
                    e.Graphics.FillRectangle(en.IsBoss ? Brushes.DarkRed : Brushes.Purple, en.Rect);
                }

                // 2. VẼ THANH MÁU TRÊN ĐẦU (Nếu quái mất máu)
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
            using (var pen = new Pen(Color.Lime, 2))
            {
                // Vẽ khung xanh lá cho Player
                e.Graphics.DrawRectangle(pen, GetCollRect(player, facingRight));

                // Vẽ khung đỏ cho Enemy/Boss
                foreach (var en in enemies)
                {
                    // Nếu kẻ địch chưa chết thì vẽ khung hitbox để kiểm tra va chạm
                    if (!en.IsDead) e.Graphics.DrawRectangle(Pens.Red, en.Rect);
                }
            }

            // ===== VẼ GIAO DIỆN (UI) - THANH MÁU =====
            // 1. Cấu hình vị trí
            int barX = 50;  // Dịch sang phải một chút để nhường chỗ cho chữ HP
            int barY = 20;  // Cách mép trên 20px
            int barW = 130; // Chiều dài thanh máu
            int barH = 13;  // Chiều cao thanh máu

            // --- VẼ CHỮ "HP" ---
            // Tạo font chữ: Arial, cỡ 12, in đậm
            string hp = "HP";
            using (Font hpFont = new Font("Arial", 12, FontStyle.Bold))
            {
                // Vẽ bóng đen cho chữ nổi bật
                e.Graphics.DrawString(hp, hpFont, Brushes.Black, barX - 40, barY);
                // Vẽ chữ "HP" màu đỏ, nằm bên trái thanh máu
                e.Graphics.DrawString(hp, hpFont, Brushes.Red, barX - 42, barY - 2);
            }

            // 2. Tính toán độ dài phần máu còn lại
            float percentage = (float)currentHealth / maxHealth;
            // Chặn không cho âm (nếu máu <= 0 thì phần trăm là 0)
            if (percentage <= 0) percentage = 0;
            int fillW = (int)(barW * percentage);

            // 3. Vẽ nền thanh máu (Màu xám - phần máu đã mất)
            e.Graphics.FillRectangle(Brushes.Gray, barX, barY, barW, barH);

            // 4. Vẽ lượng máu hiện tại (Màu đỏ tươi)
            // Nếu máu > 0 thì mới vẽ màu đỏ, nếu = 0 thì fillW = 0 nên không vẽ gì
            e.Graphics.FillRectangle(Brushes.Red, barX, barY, fillW, barH);

            // VẼ ĐIỂM SỐ (ở góc dưới màn hình)
            string scoreText = "SCORE: " + score.ToString(); //"SCORE: 5"

            using (Font scoreFont = new Font("Arial", 14, FontStyle.Bold))
            {
                float scoreX = barX - 43; // canh sao cho ngang hàng với chữ HP
                float scoreY = barY + 450; // Đặt ở phía dưới cùng màn hình

                // Vẽ bóng đen cho chữ nổi bật
                e.Graphics.DrawString(scoreText, scoreFont, Brushes.Black, scoreX + 2, scoreY + 2);
                // Vẽ chữ màu vàng
                e.Graphics.DrawString(scoreText, scoreFont, Brushes.Gold, scoreX, scoreY);
            }

            // === VẼ MÀN HÌNH PAUSE ===
            if (isPaused)
            {
                // Phủ lớp đen mờ (Làm tối màn hình khi tạm dừng)
                // Color.FromArgb(200, 0, 0, 0): 200 là độ trong suốt (Alpha)
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(200, 0, 0, 0)))
                {
                    e.Graphics.FillRectangle(brush, 0, 0, baseWidth, baseHeight);
                }
            }
            // ======================================================

            // VẼ CHỮ BAY (Hiệu ứng khi ăn tiền hoặc mất máu):
            using (Font fontFx = new Font("Arial Black", 12, FontStyle.Bold))
            {
                foreach (var ft in floatingTexts)
                {
                    // Vẽ từng đoạn chữ đang bay với màu và độ trong suốt tương ứng
                    e.Graphics.DrawString(ft.Text, fontFx, ft.TextBrush, ft.X, ft.Y);
                }
            }

            // ===== VẼ MÀN HÌNH GAME OVER =====
            if (isGameOver)
            {
                // 1. Làm tối màn hình (Vẽ một lớp màu đen trong suốt đè lên game)
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

                    // Nếu ảnh to quá thì thu nhỏ lại để cân đối
                    if (imgW > 400) { imgW = 400; imgH = (400 * gameOverImg.Height) / gameOverImg.Width; }

                    int x = (baseWidth - imgW) / 2;
                    int y = (baseHeight - imgH) / 2 - 50; // Dịch lên trên một chút cho cân đối

                    e.Graphics.DrawImage(gameOverImg, x, y, imgW, imgH);
                }
                else
                {
                    // Dự phòng: Nếu ảnh lỗi thì vẽ chữ tạm
                    e.Graphics.DrawString("GAME OVER", this.Font, Brushes.Red, 100, 100);
                }

                // 3. Vẽ dòng hướng dẫn "Press R"
                string text2 = "Nhấn R để chơi lại";
                using (Font font2 = new Font("Arial", 16, FontStyle.Regular))
                {
                    SizeF size2 = e.Graphics.MeasureString(text2, font2);
                    float x2 = (baseWidth - size2.Width) / 2;
                    float y2 = (baseHeight / 2) + 70; // Nằm phía dưới ảnh Game Over

                    e.Graphics.DrawString(text2, font2, Brushes.Red, x2, y2);
                }
            }
        }

        // THAY ĐỔI CUỐI CÙNG: Phương thức Dispose để giải phóng tài nguyên
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 1. Dừng bộ đếm thời gian
                gameTimer?.Stop();
                gameTimer?.Dispose();

                // 2. Giải phóng các tấm ảnh Animation của nhân vật
                runAnim.Sheet?.Dispose();
                jumpAnim.Sheet?.Dispose();
                idleAnim.Sheet?.Dispose();

                // 3. Giải phóng ảnh hệ thống
                platformImg?.Dispose();
                backgroundImg?.Dispose();
                gameOverImg?.Dispose();

                // 4. Duyệt qua kho chứa tileAssets để giải phóng toàn bộ ảnh các ô gạch/trang trí
                foreach (var img in tileAssets.Values)
                {
                    img?.Dispose();
                }
                tileAssets.Clear(); // Xóa sạch danh sách sau khi đã giải phóng
            }
            base.Dispose(disposing); // Gọi hàm Dispose gốc của hệ thống
        }
    }
}