using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace StripSim_Door_Alternative1
{
    public class DailyEarningsChart : Control
    {
        private List<double> earningsPerMinute = new List<double>(420);

        public DailyEarningsChart()
        {
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.BackColor = Color.FromArgb(28, 21, 39);
            this.Size = new Size(68, 312);
        }

        /// <summary>
        /// Set the earnings data for the chart. Expects 420 values (one per minute).
        /// </summary>
        public void SetEarnings(List<double> earnings)
        {
            if (earnings == null)
                earningsPerMinute = new List<double>(420);
            else
                earningsPerMinute = new List<double>(earnings);
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.Clear(Color.FromArgb(28, 21, 39));

            if (earningsPerMinute == null || earningsPerMinute.Count == 0)
                return;

            int width = this.ClientSize.Width;
            int height = this.ClientSize.Height;

            // Find max for scaling
            double max = 1;
            foreach (var v in earningsPerMinute)
                if (v > max) max = v;

            // Draw axes (optional, minimal for this size)
            using (Pen axisPen = new Pen(Color.White, 1))
            {
                g.DrawLine(axisPen, 0, height - 1, width, height - 1); // X axis
                g.DrawLine(axisPen, 0, 0, 0, height); // Y axis
            }

            // Draw hour labels (21 to 4)
            using (Font font = new Font("Arial", 7))
            using (Brush brush = new SolidBrush(Color.White))
            {
                int[] hourMarks = { 0, 60, 120, 180, 240, 300, 360, 419 };
                string[] hourLabels = { "21", "22", "23", "0", "1", "2", "3", "4" };
                for (int i = 0; i < hourMarks.Length; i++)
                {
                    int x = (int)((hourMarks[i] / 419.0) * (width - 1));
                    g.DrawString(hourLabels[i], font, brush, x - 8, height - 14);
                }
            }

            // Draw earnings line
            if (earningsPerMinute.Count > 1)
            {
                using (Pen pen = new Pen(Color.HotPink, 2))
                using (Pen pointPen = new Pen(Color.HotPink, 1))
                using (Brush pointBrush = new SolidBrush(Color.HotPink))
                {
                    PointF? prev = null;
                    for (int i = 0; i < earningsPerMinute.Count; i++)
                    {
                        float x = (float)i / (earningsPerMinute.Count - 1) * (width - 1);
                        if (double.IsNaN(earningsPerMinute[i])) earningsPerMinute[i] = 0;
                        float y = height - 1 - (float)(earningsPerMinute[i] / max * (height - 20));
                        if (prev != null)
                        {
                            g.DrawLine(pen, prev.Value, new PointF(x, y));
                        }
                        // Draw a circle every 30 minutes
                        if (i % 30 == 0)
                        {
                            float radius = 4f;
                            g.FillEllipse(pointBrush, x - radius, y - radius, radius * 2, radius * 2);
                            g.DrawEllipse(pointPen, x - radius, y - radius, radius * 2, radius * 2);
                        }
                        prev = new PointF(x, y);
                    }
                }
            }
        }
    }
}