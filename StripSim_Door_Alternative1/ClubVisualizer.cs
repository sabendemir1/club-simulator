using System.Collections;
using System.Collections.Generic;
using System.Drawing;

namespace StripSim_Door_Alternative1
{
    public class ClubVisualizer
    {   
        public DoorSim1 DoorSim1 { get; set; } // Added attribute (property) of type DoorSim1
        public void Draw(Graphics g, VelvetClub club, Rectangle bounds, Queue<Client> queue, List<Client> clients)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Club rectangle size (increased)
            int clubWidth = 320;
            int clubHeight = 160;

            // Center the club rectangle in the bounds
            int clubLeft = bounds.Left + (bounds.Width - clubWidth) / 2;
            int clubTop = bounds.Top + (bounds.Height - clubHeight) / 2;
            var clubRect = new Rectangle(clubLeft, clubTop, clubWidth, clubHeight);

            using (var clubBrush = new SolidBrush(Color.FromArgb(30, 144, 255)))
                g.FillRectangle(clubBrush, clubRect);
            using (var clubPen = new Pen(Color.Black, 2))
                g.DrawRectangle(clubPen, clubRect);

            // Draw Bar (left room)
            int barWidth = 40;
            int barHeight = clubRect.Height - 32;
            var barRect = new Rectangle(clubRect.Left + 12, clubRect.Top + 16, barWidth, barHeight);
            using (var barBrush = new SolidBrush(Color.SaddleBrown))
                g.FillRectangle(barBrush, barRect);
            using (var barPen = new Pen(Color.Black, 2))
                g.DrawRectangle(barPen, barRect);
            using (var font = new Font("Segoe UI", 9, FontStyle.Bold))
                g.DrawString("", font, Brushes.White, barRect.Left + 4, barRect.Top + 4);

            // Draw Stage (center room)
            int stageWidth = 100;
            int stageHeight = 66;
            var stageRect = new Rectangle(
                clubRect.Left + (clubRect.Width - stageWidth) / 2,
                clubRect.Top + clubRect.Height / 2 - stageHeight / 2,
                stageWidth,
                stageHeight
            );
            using (var stageBrush = new SolidBrush(Color.LightPink))
                g.FillRectangle(stageBrush, stageRect);
            using (var stagePen = new Pen(Color.DeepPink, 2))
                g.DrawRectangle(stagePen, stageRect);
            using (var font = new Font("Segoe UI", 9, FontStyle.Bold))
                g.DrawString("", font, Brushes.Black, stageRect.Left + 6, stageRect.Top + 4);

            // Draw Poles on Stage (with round tops and bottoms)
            int poleCount = club?.Poles > 0 ? club.Poles : 3;
            int poleSpacing = stageWidth / (poleCount + 1);
            int poleRadius = 7;
            for (int i = 1; i <= poleCount; i++)
            {
                int poleX = stageRect.Left + i * poleSpacing;
                int poleY1 = stageRect.Top + 8;
                int poleY2 = stageRect.Bottom - 8;

                // Draw the pole (line)
                using (var polePen = new Pen(Color.Silver, 4))
                    g.DrawLine(polePen, poleX, poleY1 + poleRadius, poleX, poleY2 - poleRadius);

                // Draw round top
                g.FillEllipse(Brushes.Silver, poleX - poleRadius, poleY1, poleRadius * 2, poleRadius * 2);
                g.DrawEllipse(Pens.Gray, poleX - poleRadius, poleY1, poleRadius * 2, poleRadius * 2);

                // Draw round bottom
                g.FillEllipse(Brushes.Silver, poleX - poleRadius, poleY2 - poleRadius * 2, poleRadius * 2, poleRadius * 2);
                g.DrawEllipse(Pens.Gray, poleX - poleRadius, poleY2 - poleRadius * 2, poleRadius * 2, poleRadius * 2);
            }

            // Door (entry) - left side, larger
            var doorRect = new Rectangle(clubRect.Left - 18, clubRect.Top + clubRect.Height / 2 - 18, 18, 36);
            using (var doorBrush = new SolidBrush(Color.ForestGreen))
                g.FillRectangle(doorBrush, doorRect);
            using (var doorPen = new Pen(Color.Black, 1))
                g.DrawRectangle(doorPen, doorRect);

            // Exit - right side, larger
            var exitRect = new Rectangle(clubRect.Right, clubRect.Top + clubRect.Height / 2 - 18, 18, 36);
            using (var exitBrush = new SolidBrush(Color.IndianRed))
                g.FillRectangle(exitBrush, exitRect);
            using (var exitPen = new Pen(Color.Black, 1))
                g.DrawRectangle(exitPen, exitRect);

            // Three rooms inside the club, larger
            int roomWidth = (clubRect.Width - 24) / 3;
            int roomHeight = clubRect.Height - 24;
            Rectangle[] roomRects = new Rectangle[3];
            for (int i = 0; i < 3; i++)
            {
                var roomRect = new Rectangle(
                    clubRect.Left + 8 + i * (roomWidth + 4),
                    clubRect.Top + 8,
                    roomWidth,
                    roomHeight
                );
                roomRects[i] = roomRect;
                using (var roomBrush = new SolidBrush(Color.FromArgb(180, 255, 255, 255)))
                    g.FillRectangle(roomBrush, roomRect);
                using (var roomPen = new Pen(Color.DarkGray, 1))
                    g.DrawRectangle(roomPen, roomRect);

                // Room labels
                string[] roomNames = { "", "", "" };
                using (var font = new Font("Segoe UI", 11, FontStyle.Bold))
                using (var textBrush = new SolidBrush(Color.Black))
                {
                    var textSize = g.MeasureString(roomNames[i], font);
                    g.DrawString(roomNames[i], font, textBrush,
                        roomRect.Left + (roomRect.Width - textSize.Width) / 2,
                        roomRect.Top + (roomRect.Height - textSize.Height) / 2);
                }
            }

            // Draw clients inside rooms, randomly distributed
            if (clients != null && clients.Count > 0)
            {
                // Shuffle clients for randomness
                List<Client> shuffled = new List<Client>(clients);
                var rand = new System.Random();
                for (int i = shuffled.Count - 1; i > 0; i--)
                {
                    int j = rand.Next(i + 1);
                    var temp = shuffled[i];
                    shuffled[i] = shuffled[j];
                    shuffled[j] = temp;
                }

                // Divide into 3 groups
                int perRoom = (shuffled.Count + 2) / 3; // ceil
                for (int room = 0; room < 3; room++)
                {
                    int start = room * perRoom;
                    int end = System.Math.Min(start + perRoom, shuffled.Count);
                    Rectangle r = roomRects[room];
                    int n = end - start;
                    int cols = (int)System.Math.Ceiling(System.Math.Sqrt(n));
                    int rows = cols;
                    int clientSize = 14;
                    int margin = 6;
                    for (int k = 0; k < n; k++)
                    {
                        int idx = start + k;
                        int row = k / cols;
                        int col = k % cols;
                        int x = r.Left + margin + col * ((r.Width - 2 * margin - clientSize) / System.Math.Max(1, cols - 1));
                        int y = r.Top + margin + row * ((r.Height - 2 * margin - clientSize) / System.Math.Max(1, rows - 1));
                        // Add a little jitter for visual variety
                        x += rand.Next(-2, 3);
                        y += rand.Next(-2, 3);
                        g.FillEllipse(Brushes.MediumPurple, x, y, clientSize, clientSize);
                        g.DrawEllipse(Pens.Black, x, y, clientSize, clientSize);
                    }
                }
            }

            // Queue line (outside, left of door), larger
            int queueLength = queue != null ? queue.Count : 0;
            int queueSpacing = 18;
            for (int i = 0; i < queueLength; i++)
            {
                int cx = doorRect.Left - 22 - i * queueSpacing;
                int cy = doorRect.Top + doorRect.Height / 2;
                g.FillEllipse(Brushes.Gray, cx - 7, cy - 7, 14, 14);
                g.DrawEllipse(Pens.Black, cx - 7, cy - 7, 14, 14);
            }
            using (var font = new Font("Segoe UI", 10))
                g.DrawString("Queue", font, Brushes.Black, doorRect.Left - 90, doorRect.Top + doorRect.Height / 2 - 16);

            // Exit label
            using (var font = new Font("Segoe UI", 10))
                g.DrawString("Exit", font, Brushes.Black, exitRect.Left + 2, exitRect.Top + 10);
        }
    }
}