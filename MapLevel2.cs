using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LapTrinhTrucQuangProjectTest
{
    public partial class MapLevel2 : UserControl
    {
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
        public MapLevel2()
        {
            InitializeComponent();
        }

        private void MapLevel2_Load(object sender, EventArgs e)
        {
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
        }
        private void MapLevel2_Resize(object sender, EventArgs e)
        {
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

        }
        private void btnHome_Click(object sender, EventArgs e)
        {

        }
        private void btnContinue_Click(object sender, EventArgs e)
        {

        }
        private void btnReset_Click(object sender, EventArgs e)
        {

        }
        protected override void OnPaint(PaintEventArgs e)
        {

        }

        private void panel10_Paint(object sender, PaintEventArgs e)
        {

        }

        private void panel12_Paint(object sender, PaintEventArgs e)
        {

        }

        private void panel11_Paint(object sender, PaintEventArgs e)
        {

        }

        private void panel2_Paint(object sender, PaintEventArgs e)
        {

        }

        private void panel121_Paint(object sender, PaintEventArgs e)
        {

        }

        private void panel186_Paint(object sender, PaintEventArgs e)
        {

        }
    }
}
