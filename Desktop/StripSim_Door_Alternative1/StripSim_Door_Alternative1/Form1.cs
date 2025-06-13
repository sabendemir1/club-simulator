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
using System.Xml;
using System.Xml.Linq;

namespace StripSim_Door_Alternative1
{
    public partial class DoorSim1 : Form
    {
        public int clients = 0; // Add this public variable to the DoorSim1 class
        private DateTime lastCustomerTime; // Add this field to track the last customer creation time

        private Timer simulationTimer;
        private DateTime simulationTime;
        private readonly DateTime simulationStart = DateTime.Today.AddHours(21); // 21:00 today
        private readonly DateTime simulationEnd = DateTime.Today.AddDays(1).AddHours(4); // 04:00 next day
        private bool isSimulating = false;

        private int screeningProgress = 0;
        private int currentClientProcessTime = 0;

        private DateTime lastTickTime;

        private double simulationMinuteAccumulator = 0;

        private Queue<Client> clientQueue = new Queue<Client>();
        private List<Client> clientsList = new List<Client>();
        private List<Client> acceptedClients = new List<Client>();
        private List<Client> rejectedClients = new List<Client>();
        private double doorTolerance = 0.5;
        private int entryPrice = 30;
        private int doorRevenue = 0;
        private List<VelvetClub> velvetClubs = new List<VelvetClub>();
        private VelvetClub velvetClub = new VelvetClub();
        private VelvetClubRuntime velvetClubRuntimes = new VelvetClubRuntime();
        private int[] clientArrivals; 
        private ClientExitTimeLinkedList clientsLinkedList = new ClientExitTimeLinkedList();
        // Add these fields to DoorSim1 class (if not already present)
        private int[] multiScreening; // Progress for each bouncer (except the main one)
        private ProgressBar[] multiProgressBars; // ProgressBar references for bouncers 2-5
        private bool multiScreeningActive = false;
        private double clubMood = 0.5;


        private int dayCounter = 0;
        private int followersCount = 5;

        public DoorSim1()
        {
            InitializeComponent();
            simulationTimer = new Timer();
            simulationTimer.Interval = 1000; // 1 second
            simulationTimer.Tick += SimulationTimer_Tick;
            LoadVelvetClubs();

            numericUpDown2.ValueChanged += numericUpDown2_ValueChanged;
            checkBox1.CheckedChanged += checkBox1_CheckedChanged;
            comboBox1.SelectedIndexChanged += comboBox1_SelectedIndexChanged;

            lastTickTime = DateTime.Now;
            setDoorReadiness();
            label44.Text = expectedClientsFormula().ToString(); // Initialize expected clients formula
            clientArrivals = GenerateClientArrivals(expectedClientsFormula());
            multiScreening = new int[4]; // For bouncers 2-5 (progressBar5,6,7,8)
            multiProgressBars = new ProgressBar[] { progressBar5, progressBar6, progressBar7, progressBar8 };
        }

        private void LoadVelvetClubs()
        {
            string xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "clubs_attributes.xml");
            if (!File.Exists(xmlPath))
                return;

            var doc = new XmlDocument();
            doc.Load(xmlPath);

            velvetClubs.Clear();
            comboBox1.Items.Clear();

            var clubNodes = doc.SelectNodes("//club");
            int clubNumber = 1;
            foreach (XmlNode node in clubNodes)
            {
                var club = new VelvetClub
                {
                    Id = node.Attributes["id"]?.Value,
                    Level = int.Parse(node.Attributes["level"]?.Value ?? "0"),
                    Capacity = int.Parse(node.Attributes["capacity"]?.Value ?? "0"),
                    Poles = int.Parse(node.Attributes["poles"]?.Value ?? "0"),
                    LapDanceBooths = int.Parse(node.Attributes["lapDanceBooths"]?.Value ?? "0"),
                    VipRooms = int.Parse(node.Attributes["vipRooms"]?.Value ?? "0"),
                    ClotheRoom = bool.Parse(node.Attributes["clotheRoom"]?.Value ?? "false"),
                    DoorBouncerCapacity = int.Parse(node.Attributes["doorBouncerCapacity"]?.Value ?? "1")
                };
                velvetClubs.Add(club);
                comboBox1.Items.Add($"Velvet Club {clubNumber}");
                clubNumber++;
            }

            if (comboBox1.Items.Count > 0)
                comboBox1.SelectedIndex = 0;

            velvetClub = velvetClubs.First();
        }

        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            numericUpDown2.Maximum = velvetClub.DoorBouncerCapacity;
            setDoorReadiness();
            multiScreening = new int[4];
            for (int i = 0; i < multiProgressBars.Length; i++)
            {
                multiProgressBars[i].Visible = (i < numericUpDown2.Value - 1);
                multiProgressBars[i].Value = 0;
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            setDoorReadiness();
        }

        private void setDoorReadiness()
        {
            // Get selected club
            int idx = comboBox1.SelectedIndex;
            if (idx < 0 || idx >= velvetClubs.Count)
            {
                progressBar1.Value = 1;
                return;
            }

            var club = velvetClubs[idx];
            decimal bouncerCapacity = club.DoorBouncerCapacity;
            
            decimal bouncersOnDuty = numericUpDown2.Value;
            // Avoid division by zero
            decimal ratio = bouncerCapacity > 0 ? (bouncersOnDuty / bouncerCapacity) : 0;
            decimal readiness = ratio * 70m;

            if (checkBox1.Checked)
                readiness += 20m;

            // Add random value (0-10)
            Random rnd = new Random(Guid.NewGuid().GetHashCode());
            readiness += rnd.Next(0, 11);

            // Clamp between 1 and 100
            int readinessValue = (int)Math.Max(1, Math.Min(100, Math.Round(readiness)));

            progressBar1.Value = readinessValue;
            progressBar4.Value = readinessValue;
            if (readinessValue < 40)
            {
                progressBar4.ForeColor = Color.Red;
                progressBar1.ForeColor = Color.Red;
            }
            else if (readinessValue <= 65)
            {
                progressBar4.ForeColor = Color.Orange;
                progressBar1.ForeColor = Color.Orange;
            }
            else
            {
                progressBar4.ForeColor = Color.Green;
                progressBar1.ForeColor = Color.Green;
            }
        }

        private void SimulationTimer_Tick(object sender, EventArgs e)
        {
            // Add the current speed multiplier to the accumulator
            simulationMinuteAccumulator += (double)numericUpDown1.Value;

            // For each full simulation minute elapsed, update simulation and accumulators
            while (simulationMinuteAccumulator >= 1.0)
            {
                simulationTime = simulationTime.AddMinutes(1);
                label1.Text = simulationTime.ToString("HH:mm");

                // Generate clients according to clientArrivals for the current simulation minute
                int minuteIndex = (int)((simulationTime - simulationStart).TotalMinutes);
                if (clientArrivals != null && minuteIndex >= 0 && minuteIndex < clientArrivals.Length)
                {
                    int clientsToCreate = clientArrivals[minuteIndex];
                    for (int i = 0; i < clientsToCreate; i++)
                    {
                        createCustomer();
                    }
                }

                if (simulationTime.Minute % 15 == 0)
                {
                    // Update virtual revenue for each club every 15 minutes
                    Random revenueRandom = new Random();

                    // Interpolate multiplier: 1.0 for first, 2.0 for last, linear in between
                    double multiplier = 1.0 + (velvetClubRuntimes.level * 0.2);
                    // Show revenue: random 5-15 USD per customer, times multiplier
                    int perCustomerShow = revenueRandom.Next(5, 16);
                    velvetClubRuntimes.ShowRevenue += perCustomerShow * multiplier * Math.Max(1, velvetClubRuntimes.NofCustomers);
                    label31.Text = (velvetClubRuntimes.ShowRevenue).ToString();
                    // Update bar revenue every 15 minutes
                    Random barRandom = new Random();
                    int barRevenue = barRandom.Next(1, 5); // Random bar revenue between 10 and 50
                    velvetClubRuntimes.BarRevenue += barRevenue * multiplier * Math.Max(1, velvetClubRuntimes.NofCustomers);
                    label38.Text = velvetClubRuntimes.BarRevenue.ToString();
                    if (velvetClub.LapDanceBooths > 0)
                    {
                        // Lap dance revenue: random 20-40 USD per booth, times multiplier, half are empty though
                        int lapDanceRevenue = revenueRandom.Next(20, 40);
                        velvetClubRuntimes.LapBoothRevenue += lapDanceRevenue * multiplier * (double)(velvetClub.LapDanceBooths / 2);
                        label33.Text = velvetClubRuntimes.LapBoothRevenue.ToString();
                    }
                    if (velvetClub.VipRooms > 0)
                    {
                        // VIP room revenue: random 40-60 USD per customer, times multiplier
                        int vipRoomRevenue = revenueRandom.Next(40, 60);
                        velvetClubRuntimes.VipRoomRevenue += vipRoomRevenue * multiplier * velvetClub.VipRooms;
                        label34.Text = velvetClubRuntimes.VipRoomRevenue.ToString();
                    }
                }
                velvetClubRuntimes.TotalDailyRevenue = velvetClubRuntimes.BarRevenue + velvetClubRuntimes.CloakRoomRevenue +
                    velvetClubRuntimes.ShowRevenue + velvetClubRuntimes.LapBoothRevenue + velvetClubRuntimes.VipRoomRevenue +
                    velvetClubRuntimes.EntranceRevenue;
                label32.Text = velvetClubRuntimes.TotalDailyRevenue.ToString("F2");

                while (clientsLinkedList.Count > 0 && clientsLinkedList.PeekFirst().exitTime <= simulationTime)
                {
                    // Remove clients that have exited
                    Client exitedClient = clientsLinkedList.RemoveFirst();
                    velvetClubRuntimes.NofCustomers = Math.Max(0, velvetClubRuntimes.NofCustomers - 1);
                    label36.Text = velvetClubRuntimes.NofCustomers.ToString();
                }

                // Call customerScreening if there are customers in the queue
                if (clients > 0)
                {
                    customerScreening();
                }
                else
                {
                    // If no customers, reset progress bar
                    progressBar2.Value = 0;
                    screeningProgress = 0;
                }

                if (simulationTime.TimeOfDay >= simulationEnd.TimeOfDay && simulationTime.Date >= simulationEnd.Date)
                {
                    simulationTimer.Stop();
                    isSimulating = false;
                    ShowRandomCommentsOnSocialMediaTab();
                }
                //Club Mood will be a combination of multiple parameters
                //-Clients to Capacity ratio (peak at 0.75) linear function
                //-Strippers to Clients ratio (1/7) is good, it gets slightly better under and exponentially worse over
                //-Are there enough barmens / cleaners / servers / drinks 
                //-Hour is important, peaks around 01:00 
                clubMood = 0.5; 
                simulationMinuteAccumulator -= 1.0;
            }
        }

        private void customerScreening()
        {
            int processTime = 3;

            // Set progressBar2 maximum if changed
            if (currentClientProcessTime != processTime)
            {
                progressBar2.Maximum = processTime;
                currentClientProcessTime = processTime;
            }

            // Increment screening progress
            if (progressBar2.Value < progressBar2.Maximum)
            {
                screeningProgress = screeningProgress + (1);
                if (screeningProgress > progressBar2.Maximum)
                {
                    screeningProgress = progressBar2.Maximum;
                }
                progressBar2.Value = screeningProgress;
            }

            // If screening is complete
            if (progressBar2.Value >= progressBar2.Maximum)
            {
                progressBar2.Value = progressBar2.Maximum;
                System.Threading.Thread.Sleep(100);
                clients = Math.Max(0, clients - 1);
                label9.Text = clients.ToString();
                progressBar2.Value = 0;
                screeningProgress = 0;

                if (clientQueue.Count > 0)
                {
                    var client = clientQueue.Peek();
                    int doorReadiness = progressBar1.Value; // or progressBar4.Value, whichever is your readiness bar

                    bool accepted = false;

                    if (client.RiskProfile < 50)
                    {
                        accepted = true;
                        label12.BackColor = Color.Green;
                    }
                    else
                    {
                        Random _random = new Random();
                        if (doorTolerance >= 0.99)
                        {
                            // 99% chance to accept, 1% to reject for randomness
                            accepted = _random.NextDouble() > 0.01;
                            label12.BackColor = accepted ? Color.Green : Color.Red;
                        }
                        else if (doorTolerance <= 0.01)
                        {
                            // 0.1% chance to accept, 99.9% to reject for strictness
                            accepted = _random.NextDouble() < 0.001;
                            label12.BackColor = accepted ? Color.Green : Color.Red;
                        }
                        else
                        {
                            // Normal distribution logic for in-between tolerance
                            double mean = client.RiskProfile;
                            double baseSigma = 10 + (100 - doorReadiness) / 2.0;
                            double sigma = baseSigma * (doorTolerance / 0.5);

                            double u1 = 1.0 - _random.NextDouble();
                            double u2 = 1.0 - _random.NextDouble();
                            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
                            double sampledValue = mean + sigma * randStdNormal;

                            accepted = sampledValue < 70;
                            label12.BackColor = accepted ? Color.Green : Color.Red;
                        }
                    }

                    if (accepted)
                    {
                        acceptedClients.Add(client);
                        clientsLinkedList.Insert(client);
                        client.SetExitTime(simulationTime);
                        label20.Text = acceptedClients.Count.ToString();
                        doorRevenue += entryPrice;
                        label21.Text = doorRevenue.ToString();
                        velvetClubRuntimes.NofCustomers += 1;
                        label36.Text = (velvetClubRuntimes.NofCustomers).ToString();
                        double multiplier = 1.0 + (velvetClubRuntimes.level * 0.2);
                        if (velvetClub.ClotheRoom)
                        {
                            Random random = new Random();
                            velvetClubRuntimes.CloakRoomRevenue += (2 + velvetClub.Level * 2) / 2; //Divide by 2 assuming half of clients use cloakroom
                            label40.Text = velvetClubRuntimes.CloakRoomRevenue.ToString();
                        }
                        velvetClubRuntimes.EntranceRevenue += 30 * multiplier;
                        label42.Text = velvetClubRuntimes.EntranceRevenue.ToString();
                    }
                    else
                    {
                        rejectedClients.Add(client);
                        label15.Text = rejectedClients.Count.ToString();
                    }

                    clientQueue.Dequeue();
                    if (clientQueue.Count > 0)
                    {
                        var nextClient = clientQueue.Peek();
                        pictureBox6.Image = Image.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images/Clients", $"{nextClient.ImageId}.jpeg"));
                        UpdateRiskProgressBar(nextClient.RiskProfile);
                        label16.Text = "Risk:" + nextClient.RiskProfileType.ToString();
                    }
                    else
                    {
                        pictureBox6.Image = null;
                        UpdateRiskProgressBar(0);
                    }
                }
                else
                {
                    progressBar3.Value = 0;
                    progressBar3.ForeColor = Color.Green;
                    progressBar3.Refresh();
                }

                int val;
                if (int.TryParse(label10.Text, out val))
                {
                    label10.Text = (val + 1).ToString();
                }
                else
                {
                    label10.Text = "0"; // Default value if conversion fails
                }
            }

            if (!multiScreeningActive && clientQueue.Count > (int)(velvetClub.Capacity * 0.15))
            {
                multiScreeningActive = true;
            }

            if (multiScreeningActive)
            {
                int bouncers = (int)numericUpDown2.Value;
                int parallel = Math.Min(bouncers - 1, 4); // Only 4 extra progress bars

                for (int i = 0; i < multiProgressBars.Length; i++)
                {
                    multiProgressBars[i].Visible = (i < parallel);
                    if (i >= parallel)
                    {
                        multiProgressBars[i].Value = 0;
                        multiScreening[i] = 0;
                    }
                }

                int processTime2 = 3;
                for (int i = 0; i < parallel; i++)
                {
                    // If not already processing, start new client if available
                    if (multiScreening[i] == 0 && clientQueue.Count > 0)
                    {
                        multiScreening[i] = 1;
                        multiProgressBars[i].Maximum = processTime2;
                        multiProgressBars[i].Value = 1;
                    }
                    else if (multiScreening[i] > 0 && multiScreening[i] < processTime2)
                    {
                        multiScreening[i]++;
                        multiProgressBars[i].Value = multiScreening[i];
                    }
                    else if (multiScreening[i] >= processTime2)
                    {
                        // Screening done for this bouncer
                        multiProgressBars[i].Value = processTime2;
                        multiScreening[i] = 0;

                        if (clientQueue.Count > 0)
                        {
                            var client = clientQueue.Peek();
                            int doorReadiness = progressBar1.Value;
                            bool accepted = false;

                            if (client.RiskProfile < 50)
                            {
                                accepted = true;
                            }
                            else
                            {
                                Random _random = new Random();
                                if (doorTolerance >= 0.99)
                                {
                                    accepted = _random.NextDouble() > 0.01;
                                }
                                else if (doorTolerance <= 0.01)
                                {
                                    accepted = _random.NextDouble() < 0.001;
                                }
                                else
                                {
                                    double mean = client.RiskProfile;
                                    double baseSigma = 10 + (100 - doorReadiness) / 2.0;
                                    double sigma = baseSigma * (doorTolerance / 0.5);
                                    double u1 = 1.0 - _random.NextDouble();
                                    double u2 = 1.0 - _random.NextDouble();
                                    double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
                                    double sampledValue = mean + sigma * randStdNormal;
                                    accepted = sampledValue < 70;
                                }
                            }

                            if (accepted)
                            {
                                acceptedClients.Add(client);
                                clientsLinkedList.Insert(client);
                                client.SetExitTime(simulationTime);
                                label20.Text = acceptedClients.Count.ToString();
                                doorRevenue += entryPrice;
                                label21.Text = doorRevenue.ToString();
                                velvetClubRuntimes.NofCustomers += 1;
                                label36.Text = (velvetClubRuntimes.NofCustomers).ToString();
                                double multiplier = 1.0 + (velvetClubRuntimes.level * 0.2);
                                if (velvetClub.ClotheRoom)
                                {
                                    Random random = new Random();
                                    velvetClubRuntimes.CloakRoomRevenue += (2 + velvetClub.Level * 2) / 2;
                                    label40.Text = velvetClubRuntimes.CloakRoomRevenue.ToString();
                                }
                                velvetClubRuntimes.EntranceRevenue += 30 * multiplier;
                                label42.Text = velvetClubRuntimes.EntranceRevenue.ToString();
                            }
                            else
                            {
                                rejectedClients.Add(client);
                                label15.Text = rejectedClients.Count.ToString();
                            }

                            clientQueue.Dequeue();
                            clients = Math.Max(0, clients - 1);
                            label9.Text = clients.ToString();
                        }
                    }
                }
            }
            if (multiScreeningActive
                && clientQueue.Count <= (int)(velvetClub.Capacity * 0.07) // 7% threshold
                && multiScreening.All(x => x == 0))
            {
                multiScreeningActive = false;
                // Optionally hide progress bars and reset their values
                for (int i = 0; i < multiProgressBars.Length; i++)
                {
                    multiProgressBars[i].Visible = false;
                    multiProgressBars[i].Value = 0;
                    multiScreening[i] = 0;
                }
            }
        }

        private void startSimulation(object sender, EventArgs e)
        {
            if (!isSimulating)
            {
                simulationTime = simulationStart;
                label1.Text = simulationTime.ToString("HH:mm");
                simulationTimer.Start();
                isSimulating = true;
                clients = 0;
                label9.Text = clients.ToString();
                lastCustomerTime = simulationTime;
                velvetClubRuntimes = new VelvetClubRuntime();
                clientArrivals = GenerateClientArrivals(expectedClientsFormula());
                label44.Text = clientArrivals.Sum().ToString(); 
            }
        }

        private void pauseSimulation(object sender, EventArgs e)
        {
            // Pause the simulation
            if (simulationTimer.Enabled)
            {
                simulationTimer.Stop();
                isSimulating = false;
            }
            else
            {
                simulationTimer.Start();
                isSimulating = true;
            }
        }

        private void stopSimulation(object sender, EventArgs e)
        {
            // Stop and reset the simulation
            simulationTimer.Stop();
            label1.Text = simulationTime.ToString("HH:mm");
            isSimulating = false;
            dayCounter ++;
            buttonRandomPost_Click(sender, e);
        }

        private void createCustomer()
        {
            clients++;
            label9.Text = clients.ToString();
            Client client = new Client(this.simulationTime);
            clientQueue.Enqueue(client);
            if (clientQueue.Count == 1)
            {
                pictureBox6.Image = Image.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images/Clients", $"{client.ImageId}.jpeg"));
                UpdateRiskProgressBar(client.RiskProfile);
                label16.Text = "Risk:" + client.RiskProfileType.ToString();
            }
            clientsList.Add(client);
        }

        private void UpdateRiskProgressBar(int risk)
        {
            progressBar3.Value = risk;
            if (risk < 60)
                progressBar3.ForeColor = Color.Green;
            else if (risk <= 80)
                progressBar3.ForeColor = Color.Orange;
            else
                progressBar3.ForeColor = Color.Red;
            progressBar3.Refresh();
        }

        private void testButton_Click(object sender, EventArgs e)
        {
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "abc.html");
            StringBuilder html = new StringBuilder();

            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head><meta charset=\"UTF-8\"><title>testreport</title></head>");
            html.AppendLine("<body>");
            html.AppendLine("<h1>testreport</h1>");
            html.AppendLine("<ol>");


            for (int i = 0; i < acceptedClients.Count; i++)
            {
                html.AppendLine($"<li>Client {i + 1}: Id = {acceptedClients[i].Id} Accepted</li>");
            }

            for (int i = 0; i < rejectedClients.Count; i++)
            {
                html.AppendLine($"<li>Client {i + 1}: Id = {rejectedClients[i].Id} Rejected</li>");
            }

            html.AppendLine("</ol>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            File.WriteAllText(filePath, html.ToString());
            MessageBox.Show("Report generated: " + filePath, "Report", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            doorTolerance = (double)trackBar1.Value / 100;
        }

        private void numericUpDown3_ValueChanged(object sender, EventArgs e)
        {
            entryPrice = (int)numericUpDown3.Value;
        }

        private void DoorSim1_Load(object sender, EventArgs e)
        {

        }

        private void pictureBox10_Click(object sender, EventArgs e)
        {
            tabControl1.SelectedTab = tabPage2;
        }

        private void pictureBox9_Click(object sender, EventArgs e)
        {
            tabControl1.SelectedTab = tabPage3;
        }

        private void buttonRandomPost_Click(object sender, EventArgs e)
        {
            label25.Text = $"Day Counter : {dayCounter}";
            label24.Text = $"Number of Followers : {followersCount}";
            ShowRandomPostAndImage();
            AddFollowers();
            incrementDay();
        }

        private void ShowRandomPostAndImage()
        {
            try
            {
                string xmlPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "social_media_posts.xml");
                var doc = XDocument.Load(xmlPath);
                var posts = doc.Descendants("Post").ToList();
                if (posts.Count == 0)
                {
                    textBox1.Text = "No posts found.";
                    return;
                }
                var rand = new Random();
                var randomPost = posts[rand.Next(posts.Count)];
                var text = randomPost.Element("Text")?.Value ?? "No text found.";
                textBox1.Text = text;

                int randomIndex = rand.Next(1, 21);
                string imagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images/Strippers", randomIndex + ".jpeg");
                if (File.Exists(imagePath))
                    pictureBox12.Image = Image.FromFile(imagePath);
                else
                    pictureBox12.Image = null;
            }
            catch (Exception ex)
            {
                textBox1.Text = "Error: " + ex.Message;
                pictureBox12.Image = null;
            }
        }

        private void incrementDay()
        {
            ShowRandomPostAndImage();
            dayCounter++;
            label25.Text = $"Day Counter : {dayCounter}";
            AddFollowers();
        }

        private void AddFollowers()
        {
            var rand = new Random();

            // If scandal, decrease followers by 5%
            if (checkBox3.Checked)
            {
                int decrease = (int)Math.Floor(followersCount * 0.05);
                followersCount = Math.Max(0, followersCount - decrease);
                label24.Text = $"Number of Followers : {followersCount}";
                return;
            }

            // Base new followers: 5-10 random
            int newFollowers = rand.Next(5, 11);

            // Add 1-3% of current followers
            int percentFollowers = (int)Math.Floor(followersCount * (rand.Next(1, 4) / 100.0));
            newFollowers += percentFollowers;

            // Trackbar multipliers
            if (trackBar2.Value == trackBar2.Maximum)
            {
                newFollowers = (int)Math.Floor(newFollowers * 1.5);
            }
            else if (trackBar2.Value == trackBar2.Minimum)
            {
                newFollowers = (int)Math.Floor(newFollowers * 1.4);
            }

            // VIP Event
            if (checkBox4.Checked)
                newFollowers *= 3;

            // Special Party
            if (checkBox2.Checked)
                newFollowers *= 2;

            followersCount += newFollowers;
            label24.Text = $"Number of Followers : {followersCount}";
        }

        private void ShowRandomCommentsOnSocialMediaTab(int count = 4)
        {
            try
            {
                string xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "daily_reviews_game_exact600_final.xml");
                var doc = XDocument.Load(xmlPath);
                var posts = doc.Descendants("Review")
                    .Where(x => x.Element("ClubLevel") != null &&
                                int.TryParse(x.Element("ClubLevel").Value, out int lvl) &&
                                lvl == velvetClub.Level &&
                                x.Element("Stars") != null &&
                                int.TryParse(x.Element("Stars").Value, out int _))
                    .ToList();
                if (posts.Count == 0)
                {
                    return;
                }

                var goodPosts = posts.Where(x => int.Parse(x.Element("Stars").Value) >= 3).ToList();
                var badPosts = posts.Where(x => int.Parse(x.Element("Stars").Value) <= 2).ToList();

                var rand = new Random();
                var selectedComments = new List<string>();

                for (int i = 0; i < count; i++)
                {
                    bool pickGood = rand.NextDouble() < clubMood;
                    List<XElement> sourceList = pickGood ? goodPosts : badPosts;

                    if (sourceList.Count == 0)
                        sourceList = pickGood ? badPosts : goodPosts; // fallback if one list is empty

                    if (sourceList.Count == 0)
                    {
                        selectedComments.Add("No comments available.");
                    }
                    else
                    {
                        var comment = sourceList[rand.Next(sourceList.Count)];
                        selectedComments.Add(comment.Element("Text")?.Value ?? "");
                        // Remove to avoid duplicates
                        sourceList.Remove(comment);
                    }
                }

                if (selectedComments.Count > 0) textBox2.Text = selectedComments[0];
                if (selectedComments.Count > 1) textBox3.Text = selectedComments[1];
                if (selectedComments.Count > 2) textBox4.Text = selectedComments[2];
                if (selectedComments.Count > 3) textBox5.Text = selectedComments[3];
            }
            catch (Exception ex)
            {
                textBox1.Text = "Error loading comments: " + ex.Message;
                if (textBox2 != null) textBox2.Text = "";
                if (textBox3 != null) textBox3.Text = "";
                if (textBox4 != null) textBox4.Text = "";
                if (textBox5 != null) textBox5.Text = "";
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int idx = comboBox1.SelectedIndex;
            if (idx < 0 || idx >= velvetClubs.Count)
                return;

            // Set image
            string imagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", "logos", $"{idx}.jpeg");
            if (File.Exists(imagePath))
                pictureBox14.Image = Image.FromFile(imagePath);
            else
                pictureBox14.Image = null;

            // Set attributes in listBox1
            var club = velvetClubs[idx];
            listBox1.Items.Clear();
            listBox1.Items.Add($"Id: {club.Id}");
            listBox1.Items.Add($"Level: {club.Level}");
            listBox1.Items.Add($"Capacity: {club.Capacity}");
            listBox1.Items.Add($"Poles: {club.Poles}");
            listBox1.Items.Add($"LapDanceBooths: {club.LapDanceBooths}");
            listBox1.Items.Add($"VipRooms: {club.VipRooms}");
            listBox1.Items.Add($"ClotheRoom: {club.ClotheRoom}");
            velvetClubRuntimes.level = club.Level;
            velvetClub = club;
            label44.Text = expectedClientsFormula().ToString();
            clientArrivals = GenerateClientArrivals(expectedClientsFormula());
        }

        public static int[] GenerateClientArrivals(int totalClients)
        {
            int totalMinutes = 420;     // From 21:00 to 04:00
            int stopMinute = 360;       // No clients after 03:00
            int peakMinute = 210;       // Peak at 00:30
            double sigma = 105.0;        // Wider spread for more balanced arrival
            double[] expectedLambdas = new double[totalMinutes];

            // Step 1: Generate Gaussian-based expected λ(t) values
            double totalWeight = 0.0;
            for (int i = 0; i < totalMinutes; i++)
            {
                if (i >= stopMinute)
                {
                    expectedLambdas[i] = 0.0;
                }
                else
                {
                    double exponent = -Math.Pow(i - peakMinute, 2) / (2.0 * sigma * sigma);
                    expectedLambdas[i] = Math.Exp(exponent);
                    totalWeight += expectedLambdas[i];
                }
            }

            // Step 2: Scale weights to match totalClients
            for (int i = 0; i < stopMinute; i++)
            {
                expectedLambdas[i] = expectedLambdas[i] / totalWeight * totalClients;
            }

            // Step 3: Poisson-sample the arrivals per minute
            int[] clientsPerMinute = new int[totalMinutes];
            Random rand = new Random();
            for (int i = 0; i < stopMinute; i++)
            {
                double lambda = expectedLambdas[i];
                clientsPerMinute[i] = SamplePoisson(lambda, rand);
            }

            // Step 4: Adjust total to exactly match totalClients
            int assignedClients = clientsPerMinute.Take(stopMinute).Sum();
            int diff = totalClients - assignedClients;
            while (diff != 0)
            {
                int minute = rand.Next(0, stopMinute);
                if (diff > 0)
                {
                    clientsPerMinute[minute]++;
                    diff--;
                }
                else if (clientsPerMinute[minute] > 0)
                {
                    clientsPerMinute[minute]--;
                    diff++;
                }
            }

            return clientsPerMinute;
        }

        // Poisson sampling using Knuth's algorithm
        private static int SamplePoisson(double lambda, Random rand)
        {
            double L = Math.Exp(-lambda);
            double p = 1.0;
            int k = 0;

            do
            {
                k++;
                p *= rand.NextDouble();
            } while (p > L);

            return k - 1;
        }

        public int expectedClientsFormula()
        {
            // Center is double the club capacity
            int center = velvetClub.Capacity * 2;
            // Sigma is 10-15% of center, pick a random value in that range
            Random rand = new Random();
            double percent = rand.NextDouble() * 0.05 + 0.10; // 0.10 to 0.15
            double sigma = center * percent;

            // Box-Muller transform for Gaussian
            double u1 = 1.0 - rand.NextDouble();
            double u2 = 1.0 - rand.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            double value = center + sigma * randStdNormal;

            // Entry price adjustment: expected is 20 for level 1, 60 for level 6
            int minPrice = 20;
            int maxPrice = 60;
            int level = Math.Max(1, Math.Min(velvetClub.Level, 6));
            double expectedPrice = minPrice + (maxPrice - minPrice) * (level - 1) / 5.0;
            double priceRatio = expectedPrice / Math.Max(1, entryPrice);

            // Improved price multiplier logic for more realistic drop-off
            double priceMultiplier;
                if (priceRatio > 1.2)
                priceMultiplier = 1.4;
            else if (priceRatio < 0.8)
            {
                // For very high prices, drop off more steeply
                if (priceRatio < 0.5)
                    priceMultiplier = 0.05;
                else if (priceRatio < 0.65)
                    priceMultiplier = 0.35;
                else
                    priceMultiplier = 0.8;
            }
            else
                priceMultiplier = priceRatio;

            // Clamp to minimum 0, round to int
            return Math.Max(0, (int)Math.Round(value * priceMultiplier));
        }

        private void pictureBox16_Click(object sender, EventArgs e)
        {
            tabControl1.SelectedTab = tabControl1.TabPages[3];
        }

        private void pictureBox17_Click(object sender, EventArgs e)
        {
            tabControl1.SelectedTab = tabControl1.TabPages[4];
        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void label28_Click(object sender, EventArgs e)
        {

        }

        private void label35_Click(object sender, EventArgs e)
        {

        }
    }
}
