using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace LapTrinhTrucQuangProjectTest
{
    // THAY ĐỔI LỚP CƠ SỞ: Từ Form sang UserControl
    public partial class MapLevel1 : UserControl
    {
        // ===== GAME VARIABLES (KHÔNG THAY ĐỔI) =====
        Bitmap platformImg;
        Bitmap backgroundImg; // biến chứa ảnh nền (GIF hoặc PNG đều được)
        Bitmap portal;
        Bitmap gameOverImg;
        bool goLeft, goRight, jumping, onGround, levelTransitioning;
        int jumpSpeed = 0, force = 20, gravity = 8, playerSpeed = 3, currentLevel = 1;
        int score = 0;
        int maxHealth = 100;
        int currentHealth = 100;
        bool isGameOver = false;
        bool isSubmerged = false;
        bool isCollected = false;
        int startX;
        int startY;

        private Size _originalFormSize;
        private Rectangle _orgMenuRect;
        private float _orgMenuFontSize;
        private Rectangle _orgHomeRect;
        private float _orgHomeFontSize;
        private Rectangle _orgContinueRect;
        private float _orgContinueFontSize;
        private Rectangle _orgResetRect;
        private float _orgResetFontSize;
        private bool isPaused = false;

        Rectangle player, door;
        List<Rectangle> platforms = new List<Rectangle>();
        List<Rectangle> coin1 = new List<Rectangle>();
        List<Rectangle> coin2 = new List<Rectangle>();
        List<Rectangle> coin3 = new List<Rectangle>();
        List<Tile> tiles = new List<Tile>();
        List<Enemy> enemies = new List<Enemy>();
        Dictionary<string, Bitmap> tileAssets = new Dictionary<string, Bitmap>();
        Timer gameTimer = new Timer();

        readonly int baseWidth = 800, baseHeight = 500;
        float scaleX = 1f, scaleY = 1f;

        int? groundedIndex = null;

        const int HB_TOP = 11, HB_BOTTOM = 1;
        const int HB_BACK = 11, HB_FRONT = 11;

        enum AnimState { Idle, Run, Jump }

        // Khuôn mẫu cho ô gạch/địa hình mới:
        class Tile
        {
            public Rectangle Rect;
            public string Type;

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
            public bool FacingRight = false;
            public bool IsDead = false;
            public int MaxHP = 1;
            public int CurrentHP = 1;
            public bool IsBoss = false;
            public Enemy(int x, int y, int w, int h)
            {
                Rect = new Rectangle(x, y, w, h);
            }
        }

        private IEnumerable<Rectangle> Solid
        {
            get
            {
                foreach (var p in platforms) yield return p;

                foreach (var t in tiles)
                {
                    if (!t.Type.StartsWith("water_") && !t.Type.StartsWith("deco_") && !t.Type.StartsWith("trap_"))
                    {
                        yield return t.Rect;
                    }
                }
            }
        }
        class SpriteAnim
        {
            public Bitmap Sheet;
            public List<Rectangle> Frames = new List<Rectangle>();
            public int FrameIndex;
            public double FPS = 10;
            public bool Loop = true;
            double _accum;
            public int DrawOffsetX = 0; //độ lệch vẽ (Mặc định 0)

            public void Reset() { _accum = 0; FrameIndex = 0; }

            public void Update(double dt)
            {
                if (Sheet == null || Frames.Count == 0 || FPS <= 0) return;
                _accum += dt;
                double spf = 1.0 / FPS;
                while (_accum >= spf)
                {
                    _accum -= spf;
                    int next = FrameIndex + 1;
                    FrameIndex = (next >= Frames.Count) ? (Loop ? 0 : Frames.Count - 1) : next;
                }
            }

            public void Draw(Graphics g, Rectangle hitbox, bool facingRight)
            {
                if (Sheet == null || Frames.Count == 0) return;
                Rectangle src = Frames[FrameIndex];
                // Truyền DrawOffsetX vào hàm vẽ
                // Lưu ý: Khi quay trái (facingRight = false), ta có thể cần đảo dấu offset
                // Thử nghiệm: Nếu quay phải lệch phải, quay trái lệch trái -> OK
                int finalOffset = facingRight ? DrawOffsetX : -DrawOffsetX;

                DrawUtil.DrawRegion(g, Sheet, src, hitbox, facingRight, finalOffset);
            }
        }


        static class DrawUtil
        {
            public static void DrawRegion(Graphics g, Bitmap sheet, Rectangle src, Rectangle hitbox, bool facingRight, int xOffset = 0)
            {
                float sx = (float)hitbox.Width / src.Width;
                float sy = (float)hitbox.Height / src.Height;
                float scale = Math.Min(sx, sy);

                int w = (int)(src.Width * scale);
                int h = (int)(src.Height * scale);
                int x = hitbox.X + (hitbox.Width - w) / 2 + xOffset;
                int y = hitbox.Bottom - h;

                var st = g.Save();
    
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

        private void AddTileImage(string tag, string fileName)
        {
            try
            {
                string path = @"Images\" + fileName;
                if (File.Exists(path))
                {
                    Bitmap img = (Bitmap)Image.FromFile(path);
                    if (ImageAnimator.CanAnimate(img))
                    {
                        ImageAnimator.Animate(img, (s, e) => { });
                    }
                    if (tileAssets.ContainsKey(tag))
                        tileAssets[tag] = img;
                    else
                        tileAssets.Add(tag, img);
                }
            }
            catch { }
        }

        private readonly string RunPath = @"Images\Punk_run.png";
        private readonly string JumpPath = @"Images\Punk_jump.png";
        private readonly string IdlePath = @"Images\Punk_idle.png";
        private readonly string DoorPath = @"Images\flag.png";
        private readonly string CoinPath1 = @"Images\coin_1.png";
        private readonly string CoinPath2 = @"Images\coin_2.png";
        private readonly string CoinPath3 = @"Images\coin_3.png";
        private readonly string DecoPath1 = @"Images\deco_arrow.png";
        private readonly string DecoPath2 = @"Images\deco_7.png";
        private readonly string EnemyPath = @"Images\enemy.png";
        private readonly string BossPath = @"Images\boss.png";
        private readonly string TrapPath1 = @"Images\trap_3.png";
        private readonly string TrapPath2 = @"Images\trap_4.png";

        SpriteAnim runAnim = new SpriteAnim { FPS = 10, Loop = true };
        SpriteAnim jumpAnim = new SpriteAnim { FPS = 10, Loop = true };
        SpriteAnim idleAnim = new SpriteAnim { FPS = 6, Loop = true };
        SpriteAnim doorAnim = new SpriteAnim { FPS = 10, Loop = true };
        SpriteAnim coinAnim1 = new SpriteAnim { FPS = 9, Loop = true };
        SpriteAnim coinAnim2 = new SpriteAnim { FPS = 9, Loop = true };
        SpriteAnim coinAnim3 = new SpriteAnim { FPS = 9, Loop = true };
        SpriteAnim decoAnim1 = new SpriteAnim { FPS = 10, Loop = true };
        SpriteAnim decoAnim2 = new SpriteAnim { FPS = 10, Loop = true };
        SpriteAnim enemyAnim = new SpriteAnim { FPS = 8, Loop = true };
        SpriteAnim bossAnim = new SpriteAnim { FPS = 7, Loop = true };
        SpriteAnim trapAnim1 = new SpriteAnim { FPS = 20, Loop = true };
        SpriteAnim trapAnim2 = new SpriteAnim { FPS = 6, Loop = true };

        AnimState currentState = AnimState.Idle;
        SpriteAnim currentAnim;
        bool facingRight = true;
        readonly Stopwatch sw = new Stopwatch();

        // THAY ĐỔI CONSTRUCTOR: (Giữ nguyên)
        public MapLevel1()
        {
            InitializeComponent();
            CreateLevel1();
            // Gắn sự kiện Load vào hàm khởi tạo game
            this.Load += MapLevel1_Load;
            // Gắn sự kiện Resize của UserControl
            this.Resize += MapLevel1_Resize;
        }

        // THAY ĐỔI TÊN HÀM: Form1_Load -> MapLevel1_Load
        private void MapLevel1_Load(object sender, EventArgs e)
        {
            // XÓA: this.KeyPreview = true; (Chỉ Form có)
            // THAY THẾ: this.Deactivate -> this.LostFocus
            // Ghi chú: Cần set TabStop=true cho UserControl để nó nhận được focus
            this.LostFocus += (s, e2) => { goLeft = false; goRight = false; };

            DoubleBuffered = true;
            // XÓA: Text = ... (UserControl không có thanh tiêu đề)
            _originalFormSize = this.ClientSize;

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
            LoadAnimationEven(DecoPath1, decoAnim1, 7, alphaThreshold: 16, tightenEdges: true);
            LoadAnimationEven(DecoPath2, decoAnim2, 13, alphaThreshold: 16, tightenEdges: true);
            LoadAnimationEven(EnemyPath, enemyAnim, 12, alphaThreshold: 16, tightenEdges: true);
            LoadAnimationEven(BossPath, bossAnim, 6, alphaThreshold: 16, tightenEdges: true);
            LoadAnimationEven(TrapPath1, trapAnim1, 8, alphaThreshold: 16, tightenEdges: true);
            LoadAnimationEven(TrapPath2, trapAnim2, 4, alphaThreshold: 16, tightenEdges: true);

            currentAnim = idleAnim; currentAnim.Reset();
            gameTimer.Interval = 16;
            gameTimer.Tick += GameLoop;
            gameTimer.Start();
          

            KeyDown += KeyIsDown;
            KeyUp += KeyIsUp;

            sw.Start();

            // ==== FIX: scale ngay khi khởi động ====
            UpdateScale();
            // XÓA: this.Shown += ... (Chỉ Form có)
            // this.Shown += (s, _) => UpdateScale();
            for (int i = 1; i <= 60; i++)
            {
                AddTileImage("tile_" + i, "tile_" + i + ".png");
            }
            for (int i = 1; i <= 17; i++)
            {
                if (i != 7)
                {
                    AddTileImage("deco_" + i, "deco_" + i + ".png");
                }
            }
            for (int i = 1; i <= 4; i++)
            {
                AddTileImage("deco_bui" + i, "deco_bui" + i + ".png");
            }
            for (int i = 1; i <= 3; i++)
            {
                AddTileImage("deco_cay" + i, "deco_cay" + i + ".png");
            }
            for (int i = 1; i <= 2; i++)
            {
                AddTileImage("deco_da" + i, "deco_da" + i + ".png");
            }
            for (int i = 1; i <= 9; i++)
            {
                AddTileImage("deco_arm" + i, "deco_arm" + i + ".png");
            }
            AddTileImage("tile_invisible", "tile_invisible.png");
            AddTileImage("deco_bui5.1", "deco_bui5.1.png");
            AddTileImage("deco_bui5.2", "deco_bui5.2.png");
            AddTileImage("deco_bui5.3", "deco_bui5.3.png");                  
            AddTileImage("trap_1", "trap_1.png");
            AddTileImage("water_2", "water4.gif");
            AddTileImage("water_3", "water4-rotated.gif");
            try
            {
                if (File.Exists(@"Images\platform.png"))
                    platformImg = (Bitmap)Image.FromFile(@"Images\platform.png");
                if (File.Exists(@"Images\GameOverFont_2.png"))
                    gameOverImg = (Bitmap)Image.FromFile(@"Images\GameOverFont_2.png");
            }
            catch { }
            LoadPortal(@"Images\portal.gif");
        }
        // ===== LOAD HELPERS (KHÔNG THAY ĐỔI) =====

        private void LoadAnimationEven(string path, SpriteAnim anim, int frameCount, int alphaThreshold, bool tightenEdges)
        {
            try
            {
                if (!File.Exists(path)) throw new FileNotFoundException("Không tìm thấy file", path);
                anim.Sheet = (Bitmap)Image.FromFile(path);

                var frames = SplitEven(anim.Sheet, frameCount);

                var tb = GetGlobalVerticalBounds(anim.Sheet, alphaThreshold);
                int top = tb.Item1, bottom = tb.Item2;
                int height = (bottom > top) ? (bottom - top + 1) : anim.Sheet.Height;

                if (tightenEdges)
                    frames = TightenHorizontal(anim.Sheet, frames, alphaThreshold);

                for (int i = 0; i < frames.Count; i++)
                    frames[i] = new Rectangle(frames[i].Left, top, frames[i].Width, height);

                anim.Frames = frames;
                anim.Reset();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi nạp animation: " + ex.Message, "Sprite",
                                 MessageBoxButtons.OK, MessageBoxIcon.Warning);
                anim.Sheet = null; anim.Frames = new List<Rectangle>();
            }
        }

        private List<Rectangle> SplitEven(Bitmap bmp, int frameCount)
        {
            var frames = new List<Rectangle>();
            frameCount = Math.Max(1, frameCount);
            int sliceW = bmp.Width / frameCount;
            for (int i = 0; i < frameCount; i++)
            {
                int sx = i * sliceW;
                int width = (i == frameCount - 1) ? bmp.Width - sx : sliceW;
                frames.Add(new Rectangle(sx, 0, width, bmp.Height));
            }
            return frames;
        }

        private Tuple<int, int> GetGlobalVerticalBounds(Bitmap bmp, int alphaThreshold)
        {
            int top = int.MaxValue, bottom = -1;
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                    if (bmp.GetPixel(x, y).A > alphaThreshold)
                    {
                        if (y < top) top = y;
                        if (y > bottom) bottom = y;
                    }
            if (bottom < top) { top = 0; bottom = bmp.Height - 1; }
            return Tuple.Create(top, bottom);
        }

        private List<Rectangle> TightenHorizontal(Bitmap bmp, List<Rectangle> slices, int alphaThreshold)
        {
            var result = new List<Rectangle>(slices.Count);
            foreach (var s in slices)
            {
                int minX = int.MaxValue, maxX = -1;
                for (int x = s.Left; x < s.Right; x++)
                    for (int y = s.Top; y < s.Bottom; y++)
                        if (bmp.GetPixel(x, y).A > alphaThreshold)
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                        }
                if (maxX < minX) result.Add(s);
                else result.Add(Rectangle.FromLTRB(minX, s.Top, maxX + 1, s.Bottom));
            }
            return result;
        }

        private void LoadBackground(string path)
        {
            try
            {
                if (backgroundImg != null)
                {
                    ImageAnimator.StopAnimate(backgroundImg, (s, e) => { });
                    backgroundImg.Dispose();
                    backgroundImg = null;
                }

                if (File.Exists(path))
                {
                    backgroundImg = (Bitmap)Image.FromFile(path);

                    if (ImageAnimator.CanAnimate(backgroundImg))
                    {
                        ImageAnimator.Animate(backgroundImg, (s, e) => { });
                    }
                }
            }
            catch { }
        }
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
        // ===== WINDOW & GAME LOOP (ĐÃ SỬA ĐỔI) =====
        private void UpdateScale()
        {
            int w = Math.Max(1, this.ClientSize.Width);
            int h = Math.Max(1, this.ClientSize.Height);
            scaleX = (float)w / baseWidth;
            scaleY = (float)h / baseHeight;
            Invalidate();
        }

        // THAY ĐỔI TÊN HÀM: Form1_Resize -> MapLevel1_Resize
        private void MapLevel1_Resize(object sender, EventArgs e)
        {
            UpdateScale();
            float xRatio = (float)this.ClientSize.Width / _originalFormSize.Width;
            float yRatio = (float)this.ClientSize.Height / _originalFormSize.Height;
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
                case 5: CreateLevel5(); break;
                case 6: CreateLevel6(); break;
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


        private Rectangle GetCollRect(Rectangle r, bool faceRight)
        {
            int leftInset = faceRight ? HB_BACK : HB_FRONT;
            int rightInset = faceRight ? HB_FRONT : HB_BACK;

            int x = r.X + leftInset;
            int y = r.Y + HB_TOP;

            int w = Math.Max(1, r.Width - leftInset - rightInset);
            int h = Math.Max(1, r.Height - HB_TOP - HB_BOTTOM);
            return new Rectangle(x, y, w, h);
        }
        private void GameLoop(object sender, EventArgs e)
        {
            double dt = sw.Elapsed.TotalSeconds; sw.Restart();
            if (dt <= 0 || dt > 0.05) dt = 1.0 / 60.0;

            int prevX = player.X;
            int prevY = player.Y;
            bool prevFace = facingRight;
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

                Rectangle wallSensor = new Rectangle(en.FacingRight ? en.Rect.Right : en.Rect.Left - 2, en.Rect.Y, 2, en.Rect.Height - 5);

                Rectangle groundSensor = new Rectangle(en.FacingRight ? en.Rect.Right : en.Rect.Left - lookAhead, en.Rect.Bottom, lookAhead, 4);

                bool hitWall = false;
                bool hasGround = false;
                foreach (var p in Solid)
                {
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
                    en.FacingRight = !en.FacingRight;
                }

                // C. Tương tác với Player
                if (playerHitbox.IntersectsWith(en.Rect))
                {
                    bool isFallingAttack = !onGround && jumpSpeed <= 0;
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
                            score += (en.IsBoss ? 100 : 5);

                            if (en.IsBoss)
                            {
                                MessageBox.Show("YOU WIN! Đã tiêu diệt Boss!");
                            }
                        }
                    }
                    else
                    {
                        // ==> PLAYER BỊ ĐÁNH (Thua)

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
                                currentHealth -= (en.IsBoss ? 30 : 20);

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
            // Xử lí tương tác với trap và water:
            bool hitTrap = false;
            isSubmerged = false;
            isCollected = false;

            foreach (var t in tiles)
            {
                if (t.Type.StartsWith("water_") || t.Type.StartsWith("deco_arm"))
                {
                    if (playerHitbox.IntersectsWith(t.Rect))
                    {
                        isSubmerged = true;
                    }
                }
                if (t.Type.StartsWith("trap_"))
                {
                    if (playerHitbox.IntersectsWith(t.Rect))
                    {
                        if (!hitTrap)
                        {
                            currentHealth -= 10;
                            hitTrap = true;
                        }
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
            int oldX = prevX;
            Rectangle afterX = GetCollRect(player, facingRight);

            foreach (var p in Solid)
            {
                if (!afterX.IntersectsWith(p)) continue;

                bool underCeilingNow = afterX.Top < p.Bottom;
                bool underCeilingPrev = prevColl.Top < p.Bottom;

                if (underCeilingNow && underCeilingPrev)
                {
                    player.X = oldX;
                    afterX = GetCollRect(player, facingRight);
                    break;
                }
            }

            if (isSubmerged)
            {
                if (jumping && jumpSpeed < 0)
                {
                    jumping = false;
                    jumpSpeed = 0;
                }
            }

            // Nhảy + trọng lực
            if (jumping) { player.Y -= jumpSpeed; jumpSpeed -= 1; }
            if (!onGround)
            {
                int currentGravity = isSubmerged ? 2 : gravity;
                player.Y += currentGravity;
            }
            Rectangle coll = GetCollRect(player, facingRight);

            if (player.Y < prevY) // chỉ xét nếu nhân vật đang nhảy lên
            {
                foreach (var p in Solid)
                {
                    bool overlapX = coll.Right > p.Left && coll.Left < p.Right;
                    bool crossedBottom = (coll.Top <= p.Bottom) && (prevColl.Top >= p.Bottom);

                    if (overlapX && crossedBottom)
                    {
                        player.Y = p.Bottom - HB_TOP;
                        coll = GetCollRect(player, facingRight);
                        jumping = false; jumpSpeed = 0;
                        onGround = false;
                        break;
                    }
                }
            }

            // ---- BẮT ĐẤT ỔN ĐỊNH ----
            bool wasGrounded = onGround;
            onGround = false;

            int prevBottom = prevColl.Bottom;
            int bottom = coll.Bottom;

            int footSpan = Math.Max(12, coll.Width * 2 / 3);
            int edgeGrace = 4;
            int keepTol = 5;
            int minSupportCross = 2;
            int minSupportStick = Math.Max(10, coll.Width / 2);

            int footLeft = coll.Left + coll.Width / 2 - footSpan / 2;
            int footRight = footLeft + footSpan;

            bool falling = player.Y > prevY;
            bool rising = player.Y < prevY;

            var Objects = new List<Rectangle>();
            foreach (var p in platforms) Objects.Add(p);
            foreach (var t in tiles)
            {
                if (!t.Type.StartsWith("water_") && !t.Type.StartsWith("trap_") && !t.Type.StartsWith("deco_"))
                {
                    Objects.Add(t.Rect);
                }
            }

            for (int i = 0; i < Objects.Count; i++)
            {
                var p = Objects[i];

                int pLeft = p.Left - edgeGrace;
                int pRight = p.Right + edgeGrace;

                int overlap = Math.Min(footRight, pRight) - Math.Max(footLeft, pLeft);

                if (overlap <= 0) continue;

                bool crossedTop = (falling &&
                                     (prevBottom <= p.Top + 2) &&
                                     (bottom >= p.Top - 2) &&
                                     (overlap >= minSupportCross));

                int needStickOverlap = wasGrounded ? 1 : minSupportStick;

                bool keepStick =
                    wasGrounded && !rising &&
                    Math.Abs(prevBottom - p.Top) <= keepTol &&
                    Math.Abs(bottom - p.Top) <= keepTol &&
                    (overlap >= needStickOverlap);

                if (crossedTop || keepStick)
                {
                    player.Y = p.Top - (coll.Height + HB_TOP);

                    coll = GetCollRect(player, facingRight);

                    onGround = true;
                    groundedIndex = i;
                    jumping = false;
                    jumpSpeed = 0;
                    break;
                }
            }

            if (!onGround) groundedIndex = null;


            //GIỚI HẠN BIÊN

            // 1. Tính toán lề thụt vào hiện tại (Logic giống hệt GetCollRect)
            // Để biết hitbox nhân vật cách mép ảnh bao xa
            int currentLeftInset = facingRight ? HB_BACK : HB_FRONT;
            int currentRightInset = facingRight ? HB_FRONT : HB_BACK;

            // 2. Chặn bên TRÁI
            // Thay vì chặn ở 0, ta cho phép X âm một đoạn đúng bằng lề trái
            // Để Hitbox chạm đúng vào điểm 0
            if (player.X < -currentLeftInset)
            {
                player.X = -currentLeftInset;
            }

            // 3. Chặn bên PHẢI
            // Cho phép ảnh lấn ra ngoài màn hình một đoạn đúng bằng lề phải
            // Để Hitbox chạm đúng vào điểm baseWidth
            int rightLimit = baseWidth - player.Width + currentRightInset;
            if (player.X > rightLimit)
            {
                player.X = rightLimit;
            }
            if (player.Y > baseHeight)
            {
                currentHealth -= 100;

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
                NextLevel(); 
                goLeft = false;
                goRight = false;
                jumping = false;
                jumpSpeed = 0;
            }

            // Coin:
            for (int i = coin1.Count - 1; i >= 0; i--)
            {
                if (coll.IntersectsWith(coin1[i]) && isCollected == false)
                {
                    isCollected = true;                   
                    score += 1;
                    coin1.RemoveAt(i);
                }
            }
            for (int i = coin2.Count - 1; i >= 0; i--)
            {
                if (coll.IntersectsWith(coin2[i]) && isCollected == false)
                {
                    isCollected = true;
                    score += 5;
                    coin2.RemoveAt(i);
                }
            }
            for (int i = coin3.Count - 1; i >= 0; i--)
            {
                if (coll.IntersectsWith(coin3[i]) && isCollected == false)
                {
                    isCollected = true;
                    score += 10;
                    coin3.RemoveAt(i);
                }
            }
            // === Chọn state theo DI CHUYỂN THỰC TẾ ===

            bool inAir = jumping || !onGround;
            bool movingNow = (player.X != prevX);

            AnimState desired = inAir ? AnimState.Jump : (movingNow ? AnimState.Run : AnimState.Idle);

            if (desired != currentState) SwitchState(desired);

            currentAnim?.Update(dt);
            doorAnim.Update(dt);
            coinAnim1.Update(dt);
            coinAnim2.Update(dt);
            coinAnim3.Update(dt);
            decoAnim1.Update(dt);
            decoAnim2.Update(dt);
            trapAnim1.Update(dt);
            trapAnim2.Update(dt);
            enemyAnim.Update(dt);
            bossAnim.Update(dt);

            Invalidate();
        }

        private void SwitchState(AnimState s)
        {
            currentState = s;
            switch (s)
            {
                case AnimState.Idle: currentAnim = idleAnim; break;
                case AnimState.Run: currentAnim = runAnim; break;
                case AnimState.Jump: currentAnim = jumpAnim; break;
            }
            currentAnim?.Reset();
        }


        // ===== INPUT (KHÔNG THAY ĐỔI) =====
        private void KeyIsDown(object sender, KeyEventArgs e)
        {
            if (isGameOver)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    isGameOver = false;
                    currentHealth = maxHealth;
                    CreateLevel1();
                    currentLevel = 1;
                    gameTimer.Start();
                }
                return;
            }

            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.A) goLeft = true;
            if (e.KeyCode == Keys.Right || e.KeyCode == Keys.D) goRight = true;

            if ((e.KeyCode == Keys.Space || e.KeyCode == Keys.Up || e.KeyCode == Keys.W) && !jumping)
            {
                if (onGround || IsTouchingPlatformBelow())
                {
                    jumping = true;
                    jumpSpeed = force;
                    onGround = false;
                }
            }
            if (e.KeyCode == Keys.D6)
            {
                currentLevel = 6;
                CreateLevel6();
                currentHealth = maxHealth;
                jumping = false;
                onGround = false;
                gameTimer.Start();
            }
        }

        private void MapLevel1_Load_1(object sender, EventArgs e)
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
            foreach (var p in Solid) if (feet.IntersectsWith(p)) return true;
            return false;
        }

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
            if (currentLevel > 6) { gameTimer.Stop(); MessageBox.Show("🎉 Bạn đã hoàn thành tất cả các màn!"); ; return; }
            platforms.Clear();
            switch (currentLevel)
            {
                case 2: CreateLevel2(); break;
                case 3: CreateLevel3(); break;
                case 4: CreateLevel4(); break;
                case 5: CreateLevel5(); break;
                case 6: CreateLevel6(); break;
            }
            levelTransitioning = false;
        }

        private void LoadMapFromContainer(Control container)
        {
            platforms.Clear();
            tiles.Clear();
            enemies.Clear();
            coin1.Clear();
            coin2.Clear();
            coin3.Clear(); 
            // SỬ DỤNG container.Controls
            foreach (Control c in container.Controls)
            {
                string tag = c.Tag?.ToString();

                if (tag == "platform")
                {
                    platforms.Add(new Rectangle(c.Left, c.Top, c.Width, c.Height));
                    c.Visible = false;
                }
                if (tag != null && (tag.StartsWith("tile_") || tag.StartsWith("trap_") || tag.StartsWith("water_") || tag.StartsWith("deco_")))
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
                if (tag == "player")
                {
                    player.X = c.Left;
                    player.Y = c.Top;
                    player = new Rectangle(c.Left, c.Top, 40, 40);
                    c.Visible = false;
                }
                if (tag == "enemy")
                {
                    enemies.Add(new Enemy(c.Left, c.Top, 40, 40));
                    c.Visible = false;
                }
                if (tag == "boss")
                {
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
            MapLevel2 map = new MapLevel2();
            LoadMapFromContainer(map);
            LoadBackground(@"Images\background3.gif");
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
            base.OnPaint(e);
            e.Graphics.ScaleTransform(scaleX, scaleY);

            // vẽ background:
            if (backgroundImg != null)
            {
                ImageAnimator.UpdateFrames(backgroundImg);
                e.Graphics.DrawImage(backgroundImg, 0, 0, baseWidth, baseHeight);
            }
            else
            {
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

            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

            foreach (var p in platforms)
            {
                if (platformImg != null)
                {
                    e.Graphics.DrawImage(platformImg, p);
                }
                else
                {
                    e.Graphics.FillRectangle(Brushes.SaddleBrown, p);
                }
            }

            foreach (var t in tiles) 
            {               
                if (t.Type == "trap_3") { trapAnim1.Draw(e.Graphics, t.Rect, false); continue; }
                if (t.Type == "trap_4") { trapAnim2.Draw(e.Graphics, t.Rect, false); continue; }
                if (t.Type == "deco_arrow") { decoAnim1.Draw(e.Graphics, t.Rect, false); continue; }
                if (t.Type == "deco_7") { decoAnim2.Draw(e.Graphics, t.Rect, false); continue; }

                if (tileAssets.ContainsKey(t.Type) && tileAssets[t.Type] != null)
                {
                    Bitmap img = tileAssets[t.Type];
                    ImageAnimator.UpdateFrames(img);
                    e.Graphics.DrawImage(img, t.Rect);
                }
                else
                {
                    e.Graphics.FillRectangle(Brushes.Gray, t.Rect);
                }
            }

            bool moving = (goLeft || goRight);
            bool inAir = jumping || !onGround;

            if (currentAnim != null && currentAnim.Sheet != null && currentAnim.Frames.Count > 0)
                currentAnim.Draw(e.Graphics, player, facingRight);
            else
                e.Graphics.FillRectangle(Brushes.Red, player);            

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
            foreach (var en in enemies)
            {
                if (en.IsDead) continue;

                if (en.IsBoss && bossAnim.Sheet != null)
                {
                    bossAnim.Draw(e.Graphics, en.Rect, en.FacingRight);
                }
                else if (!en.IsBoss && enemyAnim.Sheet != null)
                {
                    enemyAnim.Draw(e.Graphics, en.Rect, !en.FacingRight);
                }
                else
                {
                    e.Graphics.FillRectangle(en.IsBoss ? Brushes.DarkRed : Brushes.Purple, en.Rect);
                }

                if (en.CurrentHP < en.MaxHP)
                {
                    int hpBarW = en.Rect.Width;
                    int hpBarH = 5;
                    int hpBarX = en.Rect.X;
                    int hpBarY = en.Rect.Top - 10;

                    e.Graphics.FillRectangle(Brushes.Red, hpBarX, hpBarY, hpBarW, hpBarH);
                    float hpPercent = (float)en.CurrentHP / en.MaxHP;
                    e.Graphics.FillRectangle(Brushes.LimeGreen, hpBarX, hpBarY, (int)(hpBarW * hpPercent), hpBarH);
                }
            }

            using (var pen = new Pen(Color.Lime, 2))
            {
                e.Graphics.DrawRectangle(pen, GetCollRect(player, facingRight));
                foreach (var en in enemies)
                {
                    if (!en.IsDead) e.Graphics.DrawRectangle(Pens.Red, en.Rect);
                }
            }

            // ===== VẼ GIAO DIỆN (UI) - THANH MÁU =====
            int barX = 50;
            int barY = 20;
            int barW = 130;
            int barH = 13;

            string hp = "HP";
            using (Font hpFont = new Font("Arial", 12, FontStyle.Bold))
            {
                e.Graphics.DrawString(hp, hpFont, Brushes.Black, barX - 40, barY);
                e.Graphics.DrawString(hp, hpFont, Brushes.Red, barX - 42, barY - 2);
            }

            float percentage = (float)currentHealth / maxHealth;
            if (percentage <= 0) percentage = 0;
            int fillW = (int)(barW * percentage);

            e.Graphics.FillRectangle(Brushes.Gray, barX, barY, barW, barH);
            e.Graphics.FillRectangle(Brushes.Red, barX, barY, fillW, barH);
   
            string scoreText = "SCORE: " + score.ToString();

            using (Font scoreFont = new Font("Arial", 14, FontStyle.Bold))
            {
                float scoreX = barX - 43; 
                float scoreY = barY + 450;

                e.Graphics.DrawString(scoreText, scoreFont, Brushes.Black, scoreX + 2, scoreY + 2);
                e.Graphics.DrawString(scoreText, scoreFont, Brushes.Gold, scoreX, scoreY);
            }

            // === VẼ MÀN HÌNH PAUSE ===
            if (isPaused)
            {
                // Phủ lớp đen mờ
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(200, 0, 0, 0)))
                {
                    e.Graphics.FillRectangle(brush, 0, 0, baseWidth, baseHeight);
                }
            }
            // ======================================================

            // ===== VẼ MÀN HÌNH GAME OVER =====
            if (isGameOver)
            {
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
                {
                    e.Graphics.FillRectangle(brush, 0, 0, baseWidth, baseHeight);
                }

                if (gameOverImg != null)
                {
                    int imgW = gameOverImg.Width;
                    int imgH = gameOverImg.Height;

                    if (imgW > 400) { imgW = 400; imgH = (400 * gameOverImg.Height) / gameOverImg.Width; }

                    int x = (baseWidth - imgW) / 2;
                    int y = (baseHeight - imgH) / 2 - 50;

                    e.Graphics.DrawImage(gameOverImg, x, y, imgW, imgH);
                }
                else
                {
                    e.Graphics.DrawString("GAME OVER", this.Font, Brushes.Red, 100, 100);
                }

                string text2 = "Nhấn ENTER để chơi lại";
                using (Font font2 = new Font("Arial", 16, FontStyle.Regular))
                {
                    SizeF size2 = e.Graphics.MeasureString(text2, font2);
                    float x2 = (baseWidth - size2.Width) / 2;
                    float y2 = (baseHeight / 2) + 70;

                    e.Graphics.DrawString(text2, font2, Brushes.Red, x2, y2);
                }
            }
        }

        // THAY ĐỔI CUỐI CÙNG: Phương thức Dispose để giải phóng tài nguyên
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                gameTimer?.Stop();
                gameTimer?.Dispose();
                runAnim.Sheet?.Dispose();
                jumpAnim.Sheet?.Dispose();
                idleAnim.Sheet?.Dispose();
                platformImg?.Dispose();
                backgroundImg?.Dispose();
                gameOverImg?.Dispose();

                foreach (var img in tileAssets.Values)
                {
                    img?.Dispose();
                }
                tileAssets.Clear();
            }
            base.Dispose(disposing);
        }
        
    }
}