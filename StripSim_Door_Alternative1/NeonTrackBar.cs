using System;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
namespace StripSim_Door_Alternative1
{
    public class NeonTrackBar : TrackBar
    {
        private bool _dragging = false;

        public NeonTrackBar()
        {
            this.SetStyle(ControlStyles.UserPaint, true);
            this.BackColor = Color.FromArgb(0, 0, 27);
            this.TickStyle = TickStyle.None;
            this.Height = 40;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Draw the neon bar
            int barHeight = 8;
            int barY = this.Height / 2 - barHeight / 2;
            Rectangle barRect = new Rectangle(10, barY, this.Width - 20, barHeight);

            using (var glowPen = new Pen(Color.Magenta, barHeight + 8))
            using (var barPen = new Pen(Color.Magenta, barHeight))
            {
                glowPen.Color = Color.FromArgb(80, Color.Magenta);
                e.Graphics.DrawLine(glowPen, barRect.Left, barRect.Top + barHeight / 2, barRect.Right, barRect.Top + barHeight / 2);
                e.Graphics.DrawLine(barPen, barRect.Left, barRect.Top + barHeight / 2, barRect.Right, barRect.Top + barHeight / 2);
            }

            // Draw the thumb (round and neon)
            float percent = (float)(this.Value - this.Minimum) / (this.Maximum - this.Minimum);
            int thumbX = (int)(barRect.Left + percent * (barRect.Width));
            int thumbRadius = 22;
            Rectangle thumbRect = new Rectangle(thumbX - thumbRadius / 2, barRect.Top + barHeight / 2 - thumbRadius / 2, thumbRadius, thumbRadius);

            using (var glowBrush = new SolidBrush(Color.FromArgb(120, Color.Magenta)))
            using (var thumbBrush = new SolidBrush(Color.Magenta))
            {
                e.Graphics.FillEllipse(glowBrush, thumbRect);
                Rectangle innerThumb = new Rectangle(thumbRect.X + 4, thumbRect.Y + 4, thumbRect.Width - 8, thumbRect.Height - 8);
                e.Graphics.FillEllipse(thumbBrush, innerThumb);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                SetValueFromMouse(e.X);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging)
            {
                SetValueFromMouse(e.X);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                _dragging = false;
            }
        }

        private void SetValueFromMouse(int mouseX)
        {
            int barLeft = 10;
            int barRight = this.Width - 10;
            int barWidth = barRight - barLeft;

            float percent = (float)(mouseX - barLeft) / barWidth;
            percent = Math.Max(0, Math.Min(1, percent));
            int newValue = this.Minimum + (int)Math.Round(percent * (this.Maximum - this.Minimum));
            if (newValue != this.Value)
            {
                this.Value = newValue;
                this.Invalidate();
                OnScroll(EventArgs.Empty);
            }
        }
    }
}