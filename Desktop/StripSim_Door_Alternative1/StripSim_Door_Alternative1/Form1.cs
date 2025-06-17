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
        private List<VelvetClub> velvetClubs = new List<VelvetClub>();
        private VelvetClub velvetClub = new VelvetClub();
        private VelvetClubRuntime velvetClubRuntimes = new VelvetClubRuntime();
        private int[] clientArrivals;
        private ClientExitTimeLinkedList clientsLinkedList = new ClientExitTimeLinkedList();
        private int[] multiScreening; // Progress for each bouncer (except the main one)
        private ProgressBar[] multiProgressBars; // ProgressBar references for bouncers 2-5
        private bool multiScreeningActive = false;
        private double clubMood = 0.5;
        private (String, int)[] usernames;
        private Queue<int> lastFiveDaysRatings = new Queue<int>(20);

        private int dayCounter = 1;
        private List<int> selectedReviewIds = new List<int>();
        private List<Bouncer> bouncerList = new List<Bouncer>();
        private List<Stripper> stripperList = Stripper.LoadFromXml(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Strippers.xml"));

        public DoorSim1()
        {
            InitializeComponent();
            simulationTimer = new Timer();
            simulationTimer.Interval = 1000; // 1 second
            simulationTimer.Tick += SimulationTimer_Tick;
            LoadVelvetClubs();
            LoadUsernames();
            LoadLanguages(); // Load languages for the LanguageSelector ComboBox

            numericUpDown2.ValueChanged += numericUpDown2_ValueChanged;
            comboBox1.SelectedIndexChanged += comboBox1_SelectedIndexChanged;
            LanguageSelector.SelectedIndexChanged += languageSelection_SelectedIndexChanged;
            this.removeStripperButton.Click += this.removeStripperButton_Click;
            this.addStripperButton.Click += this.addStripperButton_Click;
            this.trackBarDoorBouncers.Scroll += this.trackBarDoorBouncer_Scroll;
            this.StripperSelectionBox.SelectedIndexChanged += StripperListBox_SelectedIndexChanged;

            lastTickTime = DateTime.Now;
            setDoorReadiness(0);
            label44.Text = expectedClientsFormula().ToString(); // Initialize expected clients formula
            clientArrivals = GenerateClientArrivals(expectedClientsFormula());
            multiScreening = new int[4]; // For bouncers 2-5 (progressBar5,6,7,8)
            multiProgressBars = new ProgressBar[] { progressBar5, progressBar6, progressBar7, progressBar8 };

            LoadBouncers();
            LoadContractedDoorBouncers();
            PopulateStripperListBox();
        }

        private void LoadBouncers()
        {
            bouncerList.Clear();
            string xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DoorBouncers.xml");
            if (!File.Exists(xmlPath))
                return;

            var doc = XDocument.Load(xmlPath);
            foreach (var x in doc.Descendants("bouncer"))
            {
                bouncerList.Add(new Bouncer
                {
                    Name = (string)x.Attribute("name") ?? (string)x.Element("name"),
                    ConflictControl = (int?)x.Element("conflict_control") ?? 0,
                    ClientScreening = (int?)x.Element("client_screening") ?? 0,
                    DailyWage = (int?)x.Element("daily_wage") ?? 0,
                    Overall = int.TryParse((string)x.Element("overall"), out var overallInt)
                        ? overallInt
                        : (float.TryParse((string)x.Element("overall"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var overallFloat)
                            ? (int)Math.Round(overallFloat)
                            : 0)
                });
            }
        }

        private void LoadContractedDoorBouncers()
        {
            contractedBouncers.Items.Clear();
            string xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DoorBouncers.xml");
            if (!File.Exists(xmlPath))
                return;

            var doc = XDocument.Load(xmlPath);
            var bouncerNames = doc.Descendants("bouncer")
                                  .Select(x => (string)x.Attribute("name"))
                                  .Where(name => !string.IsNullOrEmpty(name))
                                  .ToList();

            foreach (var name in bouncerNames)
                contractedBouncers.Items.Add(name);
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

        private void LoadUsernames()
        {
            string xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "club_user_real_names_300_Spaced.xml");
            if (!File.Exists(xmlPath))
            {
                usernames = new (string, int)[0];
                MessageBox.Show("Usernames file not found: " + xmlPath, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                var doc = XDocument.Load(xmlPath);
                usernames = doc.Descendants("User")
                               .Select(x =>
                               {
                                   var username = (string)x.Element("Username");
                                   var clubLevelAttr = x.Element("ClubLevel");
                                   int clubLevel = 0;
                                   if (clubLevelAttr != null)
                                       int.TryParse(clubLevelAttr.Value, out clubLevel);
                                   return (username, clubLevel);
                               })
                               .Where(u => !string.IsNullOrWhiteSpace(u.username))
                               .ToArray();
            }
            catch
            {
                usernames = new (string, int)[0];
            }
        }

        private void LoadLanguages()
        {
            string xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "daily_reviews_clean_distributed.xml");
            if (!File.Exists(xmlPath))
                return;

            try
            {
                var doc = XDocument.Load(xmlPath);
                var firstReview = doc.Descendants("Review")
                                     .Select(r => r.Element("text") ?? r.Element("Text"))
                                     .FirstOrDefault();

                if (firstReview != null)
                {
                    var languages = firstReview.Elements().Select(e => e.Name.LocalName).ToList();
                    LanguageSelector.DataSource = languages;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading languages: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            multiScreening = new int[4];
            for (int i = 0; i < multiProgressBars.Length; i++)
            {
                multiProgressBars[i].Visible = (i < numericUpDown2.Value - 1);
                multiProgressBars[i].Value = 0;
            }
        }

        private void setDoorReadiness(int readinessValue)
        {
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
                    endDay(); // This will generate new comments for the new day
                }
                //Club Mood will be a combination of multiple parameters
                //-Clients to Capacity ratio (peak at 0.75) linear function 5%
                //-Strippers to Clients ratio (1/7) is good, it gets slightly better under and exponentially worse over 30%
                //-Are there enough barmens / cleaners / servers / drinks 5%
                //-Hour is important, peaks around 01:00 5%
                //Girls Attractiveness 30%
                //Is there too much queue 10% (Outside bouncers)
                //Mobs/Drunk/High (problematic clients) inside 5%
                //DJ 2.5%
                //Inside safety (Inside bouncers) 2.5%
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
                        label21.Text = velvetClubRuntimes.EntranceRevenue.ToString();
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
                                label21.Text = velvetClubRuntimes.EntranceRevenue.ToString();
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
                if (dayCounter > 1)
                    endDay(); // Reset stats and labels for the new day

                // If not the very first simulation, increment the day
                if (simulationTime != default(DateTime) && simulationTime != simulationStart)
                {
                    dayCounter++;
                    buttonRandomPost_Click(sender, e);
                }

                // Always start at 21:00 for the current day
                simulationTime = simulationStart;
                label1.Text = simulationTime.ToString("HH:mm");
                simulationTimer.Start();
                isSimulating = true;
                clients = 0;
                label9.Text = clients.ToString();
                lastCustomerTime = simulationTime;
                velvetClubRuntimes.newDay();
                clientArrivals = GenerateClientArrivals(expectedClientsFormula());
                label44.Text = clientArrivals.Sum().ToString();
                label51.Text = dayCounter.ToString(); // Update day label if you have one

                selectedReviewIds.Clear();
                if (dayCounter > 1)
                    ShowRandomCommentsOnSocialMediaTab(6, true);
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
            simulationTimer.Stop();
            label1.Text = simulationTime.ToString("HH:mm");
            isSimulating = false;
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
            label24.Text = $"Number of Followers : {velvetClubRuntimes.followersCount}";
            ShowRandomPostAndImage();
            AddFollowers();
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
            selectedReviewIds.Clear(); // <-- Add this line
            ShowRandomCommentsOnSocialMediaTab(6, true); // Force new selection for the new day
        }

        private void AddFollowers()
        {
            var rand = new Random();

            // If scandal, decrease followers by 5%
            if (checkBox3.Checked)
            {
                int decrease = (int)Math.Floor(velvetClubRuntimes.followersCount * 0.05);
                velvetClubRuntimes.followersCount = Math.Max(0, velvetClubRuntimes.followersCount - decrease);
                label24.Text = $"Number of Followers : {velvetClubRuntimes.followersCount}";
                return;
            }

            // Base new followers: 5-10 random
            int newFollowers = rand.Next(5, 11);

            // Add 1-3% of current followers
            int percentFollowers = (int)Math.Floor(velvetClubRuntimes.followersCount * (rand.Next(1, 4) / 100.0));
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

            velvetClubRuntimes.followersCount += newFollowers;
            label24.Text = $"Number of Followers : {velvetClubRuntimes.followersCount}";
        }

        private void ShowRandomCommentsOnSocialMediaTab(int count = 6, bool forceNewSelection = false)
        {
            try
            {
                string xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "daily_reviews_clean_distributed.xml");
                var doc = XDocument.Load(xmlPath);

                string selectedLanguage = LanguageSelector.SelectedItem?.ToString() ?? "en";

                // Get all valid reviews for the current club level and language
                var allPosts = doc.Descendants("Review")
                    .Where(x =>
                        x.Element("ClubLevel") != null &&
                        int.TryParse(x.Element("ClubLevel").Value, out int lvl) &&
                        lvl == velvetClub.Level &&
                        x.Element("Stars") != null &&
                        int.TryParse(x.Element("Stars").Value, out int _) &&
                        x.Element("text") != null &&
                        x.Element("text").Element(selectedLanguage) != null &&
                        !string.IsNullOrWhiteSpace(x.Element("text").Element(selectedLanguage).Value) &&
                        x.Element("ID") != null &&
                        int.TryParse(x.Element("ID").Value, out int _)
                    )
                    .ToList();

                if (allPosts.Count == 0)
                {
                    foreach (var tb in new[] { textBoxReview1, textBoxReview2, textBoxReview3, textBoxReview4, textBoxReview5, textBoxReview6 })
                        tb.Text = "No comments available for this language.";
                    AverageRatingLabel.Text = "Last 5 days rating: N/A";
                    selectedReviewIds.Clear();
                    return;
                }

                // If we need to select new comments (e.g., after simulation or button click)
                if (forceNewSelection || selectedReviewIds.Count == 0)
                {
                    selectedReviewIds.Clear();
                    var rand = new Random();
                    var availableIds = allPosts.Select(x => int.Parse(x.Element("ID").Value)).ToList();
                    for (int i = 0; i < count && availableIds.Count > 0; i++)
                    {
                        int idx = rand.Next(availableIds.Count);
                        selectedReviewIds.Add(availableIds[idx]);
                        availableIds.RemoveAt(idx);
                    }
                }

                // Now, for the current language, fetch the reviews by ID
                var selectedPosts = allPosts
                    .Where(x => selectedReviewIds.Contains(int.Parse(x.Element("ID").Value)))
                    .ToList();

                // Sort by the order in selectedReviewIds
                selectedPosts = selectedReviewIds
                    .Select(id => selectedPosts.FirstOrDefault(x => int.Parse(x.Element("ID").Value) == id))
                    .Where(x => x != null)
                    .ToList();

                var selectedComments = selectedPosts
                    .Select(x => x.Element("text").Element(selectedLanguage)?.Value ?? "No comments available.")
                    .ToList();

                var selectedStars = selectedPosts
                    .Select(x => int.TryParse(x.Element("Stars")?.Value, out int stars) ? stars : 0)
                    .ToList();

                // Fill up to count with empty if not enough
                while (selectedComments.Count < count)
                {
                    selectedComments.Add("No comments available.");
                    selectedStars.Add(0);
                }

                TextBox[] textBoxes = { textBoxReview1, textBoxReview2, textBoxReview3, textBoxReview4, textBoxReview5, textBoxReview6 };
                PictureBox[] starPictures = { pictureBoxUser1, pictureBoxUser2, pictureBoxUser3, pictureBoxUser4, pictureBoxUser5, pictureBoxUser6 };
                GroupBox[] groupBoxes = { groupBoxUser1, groupBoxUser2, groupBoxUser3, groupBoxUser4, groupBoxUser5, groupBoxUser6 };

                Dictionary<int, PictureBox[]> reviewStarPictureBoxes = new Dictionary<int, PictureBox[]>
                {
                    { 1, new PictureBox[] { pictureBoxStar11, pictureBoxStar12, pictureBoxStar13, pictureBoxStar14, pictureBoxStar15 } },
                    { 2, new PictureBox[] { pictureBoxStar21, pictureBoxStar22, pictureBoxStar23, pictureBoxStar24, pictureBoxStar25 } },
                    { 3, new PictureBox[] { pictureBoxStar31, pictureBoxStar32, pictureBoxStar33, pictureBoxStar34, pictureBoxStar35 } },
                    { 4, new PictureBox[] { pictureBoxStar41, pictureBoxStar42, pictureBoxStar43, pictureBoxStar44, pictureBoxStar45 } },
                    { 5, new PictureBox[] { pictureBoxStar51, pictureBoxStar52, pictureBoxStar53, pictureBoxStar54, pictureBoxStar55 } },
                    { 6, new PictureBox[] { pictureBoxStar61, pictureBoxStar62, pictureBoxStar63, pictureBoxStar64, pictureBoxStar65 } }
                };

                for (int i = 1; i <= selectedComments.Count; i++)
                {
                    var textBox = textBoxes[i - 1];
                    if (textBox != null)
                    {
                        groupBoxes[i - 1].Text = usernames.Length > 0 ? usernames[new Random().Next(usernames.Length)].Item1 : "";
                        textBox.Text = selectedComments[i - 1];
                        textBox.Visible = true;
                        starPictures[i - 1].Visible = true;
                        starPictures[i - 1].Image = Image.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", "Customer_Reviews_Icons", "Customer1.png"));
                        for (int j = 0; j < selectedStars[i - 1]; j++)
                        {
                            if (reviewStarPictureBoxes.ContainsKey(i) && j < reviewStarPictureBoxes[i].Length)
                            {
                                reviewStarPictureBoxes[i][j].Visible = true;
                            }
                        }
                        for (int j = selectedStars[i - 1]; j < 5; j++)
                        {
                            if (reviewStarPictureBoxes.ContainsKey(i) && j < reviewStarPictureBoxes[i].Length)
                            {
                                reviewStarPictureBoxes[i][j].Visible = false;
                            }
                        }
                    }
                }

                foreach (var stars in selectedStars)
                {
                    lastFiveDaysRatings.Enqueue(stars);
                    if (lastFiveDaysRatings.Count > 20)
                        lastFiveDaysRatings.Dequeue();
                }

                if (lastFiveDaysRatings.Count > 0)
                {
                    double avg = lastFiveDaysRatings.Average();
                    switch (Math.Max(1, (int)Math.Round(avg, MidpointRounding.AwayFromZero)))
                    {
                        case 1:
                            AverageRatingLabel.Text = "Overwhelmingly Negative";
                            break;
                        case 2:
                            AverageRatingLabel.Text = "Mostly Negative";
                            break;
                        case 3:
                            AverageRatingLabel.Text = "Mixed or Neutral";
                            break;
                        case 4:
                            AverageRatingLabel.Text = "Mostly Positive";
                            break;
                        case 5:
                            AverageRatingLabel.Text = "Overwhelmingly Positive";
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    AverageRatingLabel.Text = "Last 5 days rating: N/A";
                }
            }
            catch (Exception ex)
            {
                textBoxReview1.Text = "Error loading comments: " + ex.Message;
                AverageRatingLabel.Text = "N/A";
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
            {
                pictureBox14.Image = Image.FromFile(imagePath);
                pictureBoxClubLogo.Image = Image.FromFile(imagePath);
            }
            else
            {
                pictureBox14.Image = null;
                pictureBoxClubLogo.Image = null;
            }
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

        private void pictureBox18_Click(object sender, EventArgs e)
        {
            tabControl1.SelectedTab = tabControl1.TabPages[0];
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

        private void endDay()
        {
            velvetClubRuntimes.totalBalance += (int)(velvetClubRuntimes.EntranceRevenue + velvetClubRuntimes.CloakRoomRevenue +
                velvetClubRuntimes.ShowRevenue + velvetClubRuntimes.BarRevenue + velvetClubRuntimes.LapBoothRevenue +
                velvetClubRuntimes.VipRoomRevenue);
            // Reset internal stats
            acceptedClients.Clear();
            rejectedClients.Clear();

            clientsList.Clear();
            clients = 0;
            velvetClubRuntimes.newDay();

            // Reset labels (adjust label names as needed)
            label21.Text = "0"; // Door revenue
            label20.Text = "0"; // Accepted clients
            label15.Text = "0"; // Rejected clients
            label10.Text = "0"; // Total clients processed
            label9.Text = "0";  // Current clients
            label31.Text = "0"; // Show revenue
            label38.Text = "0"; // Bar revenue
            label33.Text = "0"; // Lap booth revenue
            label34.Text = "0"; // VIP room revenue
            label40.Text = "0"; // Cloakroom revenue
            label42.Text = "0"; // Entrance revenue
            label32.Text = "0"; // Total daily revenue
            label36.Text = "0"; // Number of customers

            // Reset any other relevant UI elements or variables here
            selectedReviewIds.Clear();
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void languageSelection_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (dayCounter == 1) return;
            ShowRandomCommentsOnSocialMediaTab(); // Only update language, don't clear or re-pick comments
        }

        private void label25_Click(object sender, EventArgs e)
        {

        }

        private void label24_Click(object sender, EventArgs e)
        {

        }

        private void label23_Click(object sender, EventArgs e)
        {

        }

        private void buttonShowEventDescription_Click(object sender, EventArgs e)
        {

        }

        private void trackBarDoorBouncer_Scroll(object sender, EventArgs e)
        {
            FindClosestBouncerScoreToTrackBar();
            int cost = 0;
            foreach (var item in listBoxAddedDoorBouncers.Items)
            {
                string bouncerName = item.ToString();
                foreach (Bouncer bouncer in bouncerList)
                {
                    if (bouncer.Name == bouncerName)
                    {
                        cost += bouncer.DailyWage;
                    }
                }
            }
            labelDoorBouncersCost.Text = "Door Cost: " + cost.ToString();
            labelBouncerCount.Text = "Bouncer Count: " + listBoxAddedDoorBouncers.Items.Count.ToString();
        }

        // Pseudocode plan:
        // 1. For all possible combinations of bouncers (from 1 up to all), calculate GetBouncerScore(1, count, maxConflict, minConflict).
        // 2. For each combination, compare the score to trackBar1.Value, keep the closest (min absolute difference).
        // 3. At the end, add the names of the bouncers in the closest combination to listBox1.
        void FindClosestBouncerScoreToTrackBar()
        {
            int targetScore = trackBarDoorBouncers.Value;
            int n = bouncerList.Count;
            int closestScore = int.MinValue;
            int closestDiff = int.MaxValue;
            List<Bouncer> bestCombo = null;
            bool perfectMatch = false;
            List<int> ns = new List<int>(n);
            for (int i = 0; i < n; i++)
            {
                ns.Add(i);
            }
            for (int k = 1; k <= Math.Min(n, velvetClub.DoorBouncerCapacity); k++)
            {
                foreach (var indices in GetCombinations(ns, k))
                {
                    var combo = new List<Bouncer>();
                    int maxConflict = int.MinValue;
                    int maxScreening = int.MinValue;
                    foreach (int i in indices)
                    {
                        var b = bouncerList[i];
                        combo.Add(b);
                        if (b.ConflictControl > maxConflict) maxConflict = b.ConflictControl;
                        if (b.ClientScreening > maxScreening) maxScreening = b.ClientScreening;
                    }
                    int level = velvetClubRuntimes.level;
                    int score = GetBouncerScore(level, k, maxConflict, maxScreening);
                    int diff = Math.Abs(score - targetScore);
                    if (diff < closestDiff)
                    {
                        closestDiff = diff;
                        closestScore = score;
                        bestCombo = new List<Bouncer>(combo);
                    }
                    if (diff == 0)
                    {
                        closestScore = score;
                        perfectMatch = true;
                        break;
                    }
                }
                if (perfectMatch) break;
            }
            listBoxAddedDoorBouncers.Items.Clear();
            labelDoorReadiness.Text = $"Closest Score: {closestScore} (Target: {targetScore})";
            if (bestCombo != null)
            {
                foreach (var b in bestCombo)
                    listBoxAddedDoorBouncers.Items.Add(b.Name);
            }
            setDoorReadiness(closestScore);
            numericUpDown2.Value = listBoxAddedDoorBouncers.Items.Count;
        }

        // Helper: generate all k-combinations of n elements (returns list of index arrays)
        public static List<List<int>> GetCombinations(List<int> input, int k)
        {
            List<List<int>> result = new List<List<int>>();
            GenerateCombinations(input, k, 0, new List<int>(), result);
            return result;
        }

        // Recursive helper
        private static void GenerateCombinations(List<int> input, int k, int start, List<int> current, List<List<int>> result)
        {
            if (current.Count == k)
            {
                result.Add(new List<int>(current));
                return;
            }

            for (int i = start; i < input.Count; i++)
            {
                current.Add(input[i]);
                GenerateCombinations(input, k, i + 1, current, result);
                current.RemoveAt(current.Count - 1); // backtrack
            }
        }

        public int GetBouncerScore(int clubLevel, int numberOfBouncers, int maxConflict, int maxScreening)
        {
            // Clamp inputs
            clubLevel = Clamp(clubLevel, 0, 5);
            numberOfBouncers = Clamp(numberOfBouncers, 0, 5);

            const int maxScore = 40;

            // Dynamically set how many bouncers are required to get full score
            // Level 1 requires ~1.5, Level 10 requires 5.0 bouncers
            double requiredBouncers = 1 + (clubLevel) * (3.5 / 5); // maps 1→1.5, 10→5.0

            double equipmentWeight = 0; //GetEquipmentScore(door.OwnedEquipments); //Max 20 because 20% of the score

            // Calculate the score proportionally
            double proportion = Math.Min(numberOfBouncers / requiredBouncers, 1.0);

            return (int)Math.Round(maxScore * proportion) + (int)equipmentWeight + (int)(maxConflict * 0.24) + (int)(maxScreening * 0.16);
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public int GetEquipmentScore()//(List<Equipment> ownedEquipments)
        {
            return 0;
        }

        private void contractedBouncers_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (contractedBouncers.SelectedItem == null)
                return;

            string selectedName = contractedBouncers.SelectedItem.ToString();

            groupBoxBouncerDescription.Text = $"Bouncer: {selectedName}";
            string xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DoorBouncers.xml");
            if (!File.Exists(xmlPath))
                return;

            var doc = XDocument.Load(xmlPath);
            var bouncer = doc.Descendants("bouncer")
                .FirstOrDefault(x => (string)x.Attribute("name") == selectedName);

            if (bouncer == null)
                return;

            // Get ID and overall
            string idStr = (string)bouncer.Attribute("id");
            string overallStr = (string)bouncer.Element("overall");
            string conflictStr = (string)bouncer.Element("conflict_control");
            string screeningStr = (string)bouncer.Element("client_screening");
            string wageStr = (string)bouncer.Element("daily_wage");

            int minWage = int.MaxValue, maxWage = int.MinValue;
            string xmlPathWage = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DoorBouncers.xml");
            if (File.Exists(xmlPathWage))
            {
                var docWage = XDocument.Load(xmlPathWage);
                foreach (var item in contractedBouncers.Items)
                {
                    string bouncerName = item.ToString();
                    var bouncerNode = docWage.Descendants("bouncer")
                        .FirstOrDefault(x => (string)x.Attribute("name") == bouncerName);
                    if (bouncerNode != null)
                    {
                        int w = 0;
                        int.TryParse((string)bouncerNode.Element("daily_wage"), out w);
                        if (w < minWage) minWage = w;
                        if (w > maxWage) maxWage = w;
                    }
                }
                minWage -= 10;
                maxWage += 10;
            }
            int normalized = 0;
            if (maxWage > minWage)
                normalized = ((int)bouncer.Element("daily_wage") - minWage) * 100 / (maxWage - minWage);

            progressBarDailyWage.Value = normalized;
            if (normalized < 40)
            {
                progressBarDailyWage.ForeColor = Color.Green;
            }
            else if (normalized < 60)
            {
                progressBarDailyWage.ForeColor = Color.Orange;
            }
            else
            {
                progressBarDailyWage.ForeColor = Color.Red;
            }

            // Update PictureBox
            if (int.TryParse(idStr, out int id))
            {
                string imgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", "DoorBouncers", $"{id}.jpeg");
                if (File.Exists(imgPath))
                {
                    if (bouncerPictureBox.Image != null)
                    {
                        var oldImg = bouncerPictureBox.Image;
                        bouncerPictureBox.Image = null;
                        oldImg.Dispose();
                    }
                    bouncerPictureBox.Image = Image.FromFile(imgPath);
                }
                else
                {
                    bouncerPictureBox.Image = null;
                }
            }

            // Update ProgressBars
            if (float.TryParse(overallStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float overall))
            {
                int value = (int)Math.Round(overall);
                value = Math.Max(overallDoorBouncer.Minimum, Math.Min(overallDoorBouncer.Maximum, value));
                overallDoorBouncer.Value = value;
            }
            if (float.TryParse(conflictStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float conflictControl))
            {
                int value = (int)Math.Round(conflictControl);
                progressBarClientScreeningBouncer.Value = value;
            }
            if (float.TryParse(screeningStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float clientScreening))
            {
                int value = (int)Math.Round(clientScreening);
                progressBarConflictControl.Value = value;
            }

            // Print daily wage cost in GroupBox4 (e.g. in the group box title or a label)
            groupBoxBouncerDescription.Text = $"Bouncer: {selectedName} | Daily Wage: {wageStr}";

            if (DailyWageBouncerText != null)
                DailyWageBouncerText.Text = $"Daily Wage: {wageStr}";
        }

        private void buttonShowEventDescription_Click_1(object sender, EventArgs e)
        {
            MessageBox.Show("You forgot the XML");
        }

        private void PopulateStripperListBox()
        {
            StripperSelectionBox.Items.Clear();
            foreach (var stripper in stripperList)
            {
                // Display name and optionally other info
                StripperSelectionBox.Items.Add($"{stripper.Name} | Wage: {stripper.DailyWage} | Perf: {stripper.PerformanceSkill} | Attr: {stripper.Attractiveness} | Charisma: {stripper.Charisma} | Overall: {stripper.OverallScore}");
            }
        }

        private void StripperListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (StripperSelectionBox.SelectedIndex < 0 || stripperList == null || stripperList.Count == 0)
            {
                StripperImage.Image = null;
                ShowStripperBox.Text = "Stripper Name";
                labelDailyWageStripper.Text = "Daily Wage";
                return;
            }

            // Get the selected stripper
            var selectedStripper = stripperList[StripperSelectionBox.SelectedIndex];

            // Build the expected image path (e.g., "bin/debug/images/strippers/Lola Velvet.jpg")
            string imagesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", "Strippers");

            string imagePath = Path.Combine(imagesDir, ((StripperSelectionBox.SelectedIndex) + 1).ToString() + ".jpeg");

            if (imagePath != null)
            {
                // Dispose previous image to avoid memory leaks
                if (StripperImage.Image != null)
                {
                    var oldImg = StripperImage.Image;
                    StripperImage.Image = null;
                    oldImg.Dispose();
                }
                StripperImage.Image = Image.FromFile(imagePath);
            }
            else
            {
                StripperImage.Image = null; // Or set a default image
            }
            progressBarOverallStripper.Value = selectedStripper.OverallScore;
            progressBarAttractiveness.Value = selectedStripper.Attractiveness;
            progressBarPerformanceSkill.Value = selectedStripper.PerformanceSkill;
            progressBarCharisma.Value = selectedStripper.Charisma;
            int minWage = int.MaxValue;
            int maxWage = int.MinValue;

            foreach (var item in stripperList)
            {
                int w = item.DailyWage;
                if (w < minWage) minWage = w;
                if (w > maxWage) maxWage = w;
            }
            minWage -= 25;
            maxWage += 25;
            int normalized = 0;
            if (maxWage > minWage)
                normalized = ((int)selectedStripper.DailyWage - minWage) * 100 / (maxWage - minWage);

            if (normalized < 40)
            {
                progressBarDailyWageStripper.ForeColor = Color.Green;
            }
            else if (normalized < 60)
            {
                progressBarDailyWageStripper.ForeColor = Color.Orange;
            }
            else
            {
                progressBarDailyWageStripper.ForeColor = Color.Red;
            }
            progressBarDailyWageStripper.Value = normalized;
            ShowStripperBox.Text = "Stripper Name: " + selectedStripper.Name;
            labelDailyWageStripper.Text = "Daily Wage: " + selectedStripper.DailyWage.ToString();
        }

        private void addStripperButton_Click(object sender, EventArgs e)
        {
            if (StripperSelectionBox.SelectedIndex < 0 || stripperList == null || stripperList.Count == 0)
                return;

            var selectedStripper = stripperList[StripperSelectionBox.SelectedIndex];

            if (!strippersOnDuty.Items.Contains(selectedStripper.Name))
            {
                strippersOnDuty.Items.Add(selectedStripper.Name);
                labelStripperCount.Text = $"Strippers on Duty: {strippersOnDuty.Items.Count}";
                int cost = 0;
                foreach (var item in strippersOnDuty.Items)
                {
                    cost += stripperList.FirstOrDefault(x => x.Name == item.ToString())?.DailyWage ?? 0;
                }
                labelStripperCost.Text = $"Total Stripper Cost: {cost} $";
                progressBarStripperReadiness.Value = 50;
            }
        }

        private void removeStripperButton_Click(object sender, EventArgs e)
        {
            if (strippersOnDuty.SelectedIndex < 0 || stripperList == null || stripperList.Count == 0)
                return;

            string selectedName = strippersOnDuty.SelectedItem.ToString();
            var selectedStripper = stripperList.FirstOrDefault(x => x.Name == selectedName);
            if (selectedStripper == null)
                return;

            if (strippersOnDuty.Items.Contains(selectedStripper.Name))
            {
                strippersOnDuty.Items.Remove(selectedStripper.Name);
                labelStripperCount.Text = $"Strippers on Duty: {strippersOnDuty.Items.Count}";
                int cost = 0;
                foreach (var item in strippersOnDuty.Items)
                {
                    cost += stripperList.FirstOrDefault(x => x.Name == item.ToString())?.DailyWage ?? 0;
                }
                labelStripperCost.Text = $"Total Stripper Cost: {cost} $";
            }
            progressBarStripperReadiness.Value = 50;
        }
    }
}
