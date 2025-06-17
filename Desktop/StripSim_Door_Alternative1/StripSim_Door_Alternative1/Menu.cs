using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StripSim_Door_Alternative1
{
    public partial class Menu : Form
    {
        List<DoorSim1> games = new List<DoorSim1>();
        DoorSim1 currentGame = null;
        public string language = "English"; 
        private Image comboBox1ClosedBg = null;
        private Image comboBoxLanguageClosedBg = null;

        public Menu()
        {
            InitializeComponent();
            // Add levels 0 to 5 to comboBox1
            for (int i = 0; i <= 5; i++)
            {
                comboBox1.Items.Add($"Level {i}");
            }
            comboBox1.DropDown += comboBox1_DropDown;
            SetupComboBox1OwnerDraw();

            comboBoxLanguage.Items.Add("English");
            comboBoxLanguage.Items.Add("Spanish");
            comboBoxLanguage.Items.Add("French");
            comboBoxLanguage.Items.Add("German");
            comboBoxLanguage.DropDown += comboBoxLanguage_DropDown;
            SetupComboBoxLanguageOwnerDraw();
        }

        private async void pictureBoxNewGame_Click(object sender, EventArgs e)
        {
            currentGame = new DoorSim1();
            // Show an image for 5 seconds before showing the game window
            using (Form imageForm = new Form())
            {
                imageForm.FormBorderStyle = FormBorderStyle.None;
                imageForm.StartPosition = FormStartPosition.CenterScreen;
                imageForm.Size = new Size(this.Width, this.Height);

                PictureBox pictureBox = new PictureBox();
                pictureBox.Dock = DockStyle.Fill;
                pictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
                // Replace with your image path or resource
                pictureBox.Image = Image.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images/LoadingScreens", language + ".jpeg"));
                imageForm.Controls.Add(pictureBox);

                imageForm.Show();
                await Task.Delay(1000);
                imageForm.Close();
                
                games.Add(currentGame);
            }
            currentGame.Show();
        }

        private void SetupComboBox1OwnerDraw()
        {
            comboBox1.DrawMode = DrawMode.OwnerDrawFixed;
            comboBox1.ItemHeight = 50; // Match the ComboBox height
            comboBox1.DrawItem -= comboBox1_DrawItemWithBackground; // Prevent double subscription
            comboBox1.DrawItem += comboBox1_DrawItemWithBackground;
        }

        private void SetupComboBoxLanguageOwnerDraw()
        {
            comboBoxLanguage.DrawMode = DrawMode.OwnerDrawFixed;
            comboBoxLanguage.ItemHeight = 50;
            comboBoxLanguage.DrawItem -= comboBoxLanguage_DrawItemWithBackground;
            comboBoxLanguage.DrawItem += comboBoxLanguage_DrawItemWithBackground;
        }

        private void comboBox1_DropDown(object sender, EventArgs e)
        {
            // Use BackgroundImage if set, otherwise use Image
            if (pictureBox3.BackgroundImage != null)
                comboBox1ClosedBg = (Image)pictureBox3.BackgroundImage.Clone();
            else if (pictureBox3.Image != null)
                comboBox1ClosedBg = (Image)pictureBox3.Image.Clone();
            else
                comboBox1ClosedBg = null;
        }

        private void comboBox1_DrawItemWithBackground(object sender, DrawItemEventArgs e)
        {
            Image bgImage = null;

            if (e.Index < 0)
            {
                // Closed state: use the stored closed background
                bgImage = comboBox1ClosedBg;
            }
            else
            {
                // Open state: use the level-specific image
                try
                {
                    bgImage = Image.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images/ClubLevel", "ClubLevel" + e.Index.ToString() + ".png"));
                }
                catch
                {
                    // If image not found, fallback to default background
                }
            }

            if (bgImage != null)
            {
                e.Graphics.DrawImage(bgImage, e.Bounds);
            }
            else
            {
                e.DrawBackground();
            }
            e.DrawFocusRectangle();
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            pictureBox3.Image = Image.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images/ClubLevel", "ClubLevel" + comboBox1.SelectedIndex.ToString() + ".png"));
        }

        private void comboBoxLanguage_DropDown(object sender, EventArgs e)
        {
            if (pictureBox4.BackgroundImage != null)
                comboBoxLanguageClosedBg = (Image)pictureBox4.BackgroundImage.Clone();
            else if (pictureBox4.Image != null)
                comboBoxLanguageClosedBg = (Image)pictureBox4.Image.Clone();
            else
                comboBoxLanguageClosedBg = null;
        }

        private void comboBoxLanguage_DrawItemWithBackground(object sender, DrawItemEventArgs e)
        {
            Image bgImage = null;

            if (e.Index < 0)
            {
                // Closed state: use the stored closed background
                bgImage = comboBoxLanguageClosedBg;
            }
            else
            {
                // Open state: use the language-specific image
                string[] languages = { "English", "Spanish", "French", "German" };
                if (e.Index >= 0 && e.Index < languages.Length)
                {
                    try
                    {
                        bgImage = Image.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images/Languages", languages[e.Index] + ".png"));
                    }
                    catch
                    {
                        // If image not found, fallback to default background
                    }
                }
            }

            if (bgImage != null)
            {
                e.Graphics.DrawImage(bgImage, e.Bounds);
            }
            else
            {
                e.DrawBackground();
            }
            e.DrawFocusRectangle();
        }

        private void comboBoxLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Update pictureBox4's image to match the selected language
            string[] languages = { "English", "Spanish", "French", "German" };
            if (comboBoxLanguage.SelectedIndex >= 0 && comboBoxLanguage.SelectedIndex < languages.Length)
            {
                pictureBox4.Image = Image.FromFile(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images/Languages", languages[comboBoxLanguage.SelectedIndex] + ".png")
                );
            }

            // Update closed background
            if (pictureBox4.BackgroundImage != null)
                comboBoxLanguageClosedBg = (Image)pictureBox4.BackgroundImage.Clone();
            else if (pictureBox4.Image != null)
                comboBoxLanguageClosedBg = (Image)pictureBox4.Image.Clone();
            else
                comboBoxLanguageClosedBg = null;

            comboBoxLanguage.Invalidate();
        }

        private void pictureBox3_Click(object sender, EventArgs e)
        {
            comboBox1.DroppedDown = true;
        }

        private void pictureBox4_Click(object sender, EventArgs e)
        {
            comboBoxLanguage.DroppedDown = true;
        }
    }
}
