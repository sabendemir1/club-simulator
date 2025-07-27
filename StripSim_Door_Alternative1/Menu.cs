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
using System.Xml.Linq;

namespace StripSim_Door_Alternative1
{
    public partial class Menu : Form
    {
        List<DoorSim1> games = new List<DoorSim1>();
        DoorSim1 currentGame = null;
        public string language = "English"; 
        private Image comboBox1ClosedBg = null;
        private Image comboBoxLanguageClosedBg = null;

        private static readonly string[] languages = {
            "English", "Spanish", "French", "German", "Italian",
            "Japanese", "Portuguese", "Korean", "Russian", "Chinese", "Indian", "Turkish"
        };

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

            comboBox1.SelectedIndex = 0;

            // Add languages in the correct order
            comboBoxLanguage.Items.Add("English");
            comboBoxLanguage.Items.Add("Spanish");
            comboBoxLanguage.Items.Add("French");
            comboBoxLanguage.Items.Add("German");
            comboBoxLanguage.Items.Add("Italian");
            comboBoxLanguage.Items.Add("Japanese");
            comboBoxLanguage.Items.Add("Portuguese");
            comboBoxLanguage.Items.Add("Korean");
            comboBoxLanguage.Items.Add("Russian");
            comboBoxLanguage.Items.Add("Chinese");
            comboBoxLanguage.Items.Add("Indian");
            comboBoxLanguage.Items.Add("Turkish");

            comboBoxLanguage.DropDown += comboBoxLanguage_DropDown;
            SetupComboBoxLanguageOwnerDraw();
            comboBoxLanguage.SelectedIndex = 0;
            this.listBoxSavedGames.Visible = false;
            this.listBoxSavedGames.SelectedIndexChanged += listBoxSavedGames_SelectedIndexChanged;
            this.Click += Menu_Click;
            foreach (Control ctrl in this.Controls)
            {
                if (ctrl != listBoxSavedGames)
                    ctrl.Click += Menu_Click;
            }
        }

        private async void pictureBoxNewGame_Click(object sender, EventArgs e)
        {
            currentGame = new DoorSim1(comboBoxLanguage.SelectedIndex, comboBox1.SelectedIndex);
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

        public void Save(DoorSim1 currentGame)
        {
            string savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "savegame.xml");
            string language = this.language;
            int balance = currentGame.velvetClubRuntimes.totalBalance;
            int day = currentGame.dayCounter;

            XDocument doc;
            if (File.Exists(savePath))
            {
                doc = XDocument.Load(savePath);
            }
            else
            {
                doc = new XDocument(new XElement("clubs"));
            }

            // Find existing save for this language
            var existing = doc.Root.Elements("club")
                .FirstOrDefault(x => (string)x.Element("language") == language);

            if (existing != null)
            {
                existing.Element("balance").Value = balance.ToString();
                existing.Element("day").Value = day.ToString();
            }
            else
            {
                doc.Root.Add(new XElement("club",
                    new XElement("language", language),
                    new XElement("balance", balance),
                    new XElement("day", day)
                ));
            }

            doc.Save(savePath);
        }


        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (currentGame != null)
            {
                Save(currentGame);
            }
        }

        private void pictureBoxLoadGame_Click(object sender, EventArgs e)
        {
            string savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "savegame.xml");
            listBoxSavedGames.Items.Clear();

            if (!File.Exists(savePath))
            {
                MessageBox.Show("No saved games found.");
                return;
            }

            var doc = XDocument.Load(savePath);
            var clubs = doc.Root.Elements("club").ToList();

            if (clubs.Count == 0)
            {
                MessageBox.Show("No saved games found.");
                return;
            }

            foreach (var club in clubs)
            {
                string language = club.Element("language")?.Value ?? "Unknown";
                string balance = club.Element("balance")?.Value ?? "0";
                string day = club.Element("day")?.Value ?? "0";
                listBoxSavedGames.Items.Add($"Language: {language}, Balance: {balance}, Day: {day}");
            }
            listBoxSavedGames.Visible = true;
        }

        private void listBoxSavedGames_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxSavedGames.SelectedIndex == -1)
                return;

            string savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "savegame.xml");
            var doc = XDocument.Load(savePath);
            var clubs = doc.Root.Elements("club").ToList();

            // Parse the selected item
            string selected = listBoxSavedGames.SelectedItem.ToString();
            // Expected format: "Language: English, Balance: 12345, Day: 3"
            var parts = selected.Split(',');
            string language = parts[0].Split(':')[1].Trim();
            int balance = int.Parse(parts[1].Split(':')[1].Trim());
            int day = int.Parse(parts[2].Split(':')[1].Trim());

            // Find the language index for DoorSim1 constructor
            int languageIndex = Array.IndexOf(languages, language);
            if (languageIndex == -1) languageIndex = 0;

            // Use the currently selected club level (or default to 0)
            int level = comboBox1.SelectedIndex;

            // Create and show the game
            currentGame = new DoorSim1(languageIndex, level, day, balance);
            currentGame.Show();
            games.Add(currentGame);

            listBoxSavedGames.Visible = false;
        }

        private void Menu_Click(object sender, EventArgs e)
        {
            if (listBoxSavedGames.Visible)
                listBoxSavedGames.Visible = false;
        }
    }
}
