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
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.VisualStyles;
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

        private SocialEngagement currentSocial;
        private Queue<SocialEngagement> previousSocialEngagements = new Queue<SocialEngagement>(4);

        private Queue<Client> clientQueue = new Queue<Client>();
        private List<Client> clientsList = new List<Client>();
        private List<Client> acceptedClients = new List<Client>();
        private List<Client> rejectedClients = new List<Client>();
        private double doorTolerance = 0.5;
        private int entryPrice = 30;
        private List<VelvetClub> velvetClubs = new List<VelvetClub>();
        private VelvetClub velvetClub = new VelvetClub();
        public VelvetClubRuntime velvetClubRuntimes = new VelvetClubRuntime();
        private int[] clientArrivals;
        private ClientExitTimeLinkedList clientsLinkedList = new ClientExitTimeLinkedList();
        private int[] multiScreening; // Progress for each bouncer (except the main one)
        private ProgressBar[] multiProgressBars; // ProgressBar references for bouncers 2-5
        private bool multiScreeningActive = false;
        private double clubMood = 0;
        private (String, int)[] usernames;
        private Queue<int> lastFiveDaysRatings = new Queue<int>(20);
        private bool firstDay = true;

        public int dayCounter = 1;
        private List<int> selectedReviewIds = new List<int>();
        private List<Bouncer> bouncerList = new List<Bouncer>();
        private List<Stripper> stripperList = Stripper.LoadFromXml(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Strippers.xml"));
        private string xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Barmens.xml");
        List<Barman> barmans;
        List<Cleaner> cleaners;
        List<Waiter> waiters;
        List<DJ> djs;
        private Dictionary<string, List<double>> clubMoodByMinute = new Dictionary<string, List<double>>();
        private ClubVisualizer clubVisualizer = new ClubVisualizer();
        private List<double> earningsPerMinute = Enumerable.Repeat(0.0, 420).ToList();

        private int selectedStripperIndex = -1; // Add this field to your form

        public DoorSim1(int language = 0, int level = 0)
        {
            InitializeComponent();
            simulationTimer = new Timer();
            simulationTimer.Interval = 1000; // 1 second
            simulationTimer.Tick += SimulationTimer_Tick;
            LoadVelvetClubs();
            LoadUsernames();

            comboBox1.SelectedIndex = level;
            velvetClub = velvetClubs[level]; // Set the velvet club based on the selected level
            LanguageSelector.SelectedIndex = language; // Set the language selection based on the passed index
            if (LanguageSelector.SelectedIndex == 1 || LanguageSelector.SelectedIndex == 2)
            {
                if (LanguageSelector.SelectedIndex == 1)
                    LanguageSelector.SelectedIndex = 2;
                else if (LanguageSelector.SelectedIndex == 2)
                    LanguageSelector.SelectedIndex = 1; // Swap Spanish and French for testing
            }

            flowLayoutPanelStrippers.AutoScroll = true;
            flowLayoutPanelStrippers.WrapContents = true; // or false for single column/row
            flowLayoutPanelStrippers.FlowDirection = FlowDirection.TopDown; // or LeftToRight
            numericUpDown2.ValueChanged += numericUpDown2_ValueChanged;
            comboBox1.SelectedIndexChanged += comboBox1_SelectedIndexChanged;
            LanguageSelector.SelectedIndexChanged += languageSelection_SelectedIndexChanged;
            this.removeStripperButton.Click += this.removeStripperButton_Click;
            this.addStripperButton.Click += this.addStripperButton_Click;
            this.trackBarDoorBouncers.Scroll += this.trackBarDoorBouncer_Scroll;
            //this.StripperSelectionBox.SelectedIndexChanged += StripperListBox_SelectedIndexChanged;
            this.barmanTrackbar.Scroll += this.FindClosestBarmanScoreToTrackBar;
            this.CleanerTrackBar.Scroll += this.FindClosestCleanerScoreToTrackBar;
            this.WaiterTrackBar.Scroll += this.FindClosestWaiterScoreToTrackBar;
            this.djTrackBar.Scroll += this.FindClosestDjScoreToTrackBar;
            this.insideBouncerTrackBar.Scroll += this.FindClosestInsideBouncerScoreToTrackBar;

            clubVisualizer.DoorSim1 = this;

            lastTickTime = DateTime.Now;
            setDoorReadiness(0);
            label44.Text = expectedClientsFormula().ToString(); // Initialize expected clients formula
            clientArrivals = GenerateClientArrivals(expectedClientsFormula());
            multiScreening = new int[4]; // For bouncers 2-5 (progressBar5,6,7,8)
            multiProgressBars = new ProgressBar[] { progressBar5, progressBar6, progressBar7, progressBar8 };
            comboBox1_SelectedIndexChanged(new Object(), new EventArgs());

            LoadBouncers();
            LoadContractedDoorBouncers();
            PopulateStripperListPanel();
            barmans = StaffLoader.LoadBarmans(xmlPath);
            cleaners = StaffLoader.LoadCleaners(xmlPath);
            waiters = StaffLoader.LoadWaiters(xmlPath);
            djs = StaffLoader.LoadDJs(xmlPath);

            clubMoodByMinute["Club Mood"] = new List<double>(420);
            clubMoodByMinute["Strippers to Clients"] = new List<double>(420);
            clubMoodByMinute["Bar and Service"] = new List<double>(420);
            clubMoodByMinute["Cleanliness"] = new List<double>(420);
            clubMoodByMinute["Strippers Attractiveness"] = new List<double>(420);
            clubMoodByMinute["Queue to Capacity"] = new List<double>(420);
            clubMoodByMinute["Problematic Clients"] = new List<double>(420);
            clubMoodByMinute["DJ"] = new List<double>(420);
            clubMoodByMinute["Inside Safety"] = new List<double>(420);
        }

        public DoorSim1(int language = 0, int level = 0, int day = 0, int balance = 0)
            : this(language, level) 
        {
            dayCounter = day;
            velvetClubRuntimes.totalBalance = balance;
            label51.Text = dayCounter.ToString(); 
            velvetClubRuntimes.newDay();
            label34.Text = velvetClubRuntimes.totalBalance.ToString("F2");
            label32.Text = velvetClubRuntimes.TotalDailyRevenue.ToString("F2");
            label42.Text = velvetClubRuntimes.EntranceRevenue.ToString("F2");
            label40.Text = velvetClubRuntimes.CloakRoomRevenue.ToString("F2");
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

                while (clientsLinkedList.Count > 0 && clientsLinkedList.PeekFirst().exitTime <= simulationTime)
                {
                    // Remove clients that have exited
                    Client exitedClient = clientsLinkedList.RemoveFirst();
                    velvetClubRuntimes.NofCustomers = Math.Max(0, velvetClubRuntimes.NofCustomers - 1);
                    label36.Text = velvetClubRuntimes.NofCustomers.ToString();
                }
                if (simulationTime.Hour >= 21 || simulationTime.Hour < 4)
                {
                    CalculateBarRevenue(velvetClub.Level, velvetClubRuntimes.NofCustomers, clubMood, simulationTime);
                    CalculateShowRevenue(velvetClub.Level, velvetClubRuntimes.NofCustomers, clubMood, simulationTime);
                    CalculateVipRoomRevenue(velvetClub.Level, velvetClubRuntimes.NofCustomers, clubMood, simulationTime);
                    CalculateLapDanceRevenue(velvetClub.Level, velvetClubRuntimes.NofCustomers, clubMood, simulationTime);
                    earningsChart.SetEarnings(earningsPerMinute);
                }

                velvetClubRuntimes.TotalDailyRevenue = velvetClubRuntimes.BarRevenue + velvetClubRuntimes.ShowRevenue + velvetClubRuntimes.VipRoomRevenue + velvetClubRuntimes.EntranceRevenue + velvetClubRuntimes.LapBoothRevenue;
                label32.Text = velvetClubRuntimes.TotalDailyRevenue.ToString("F2");

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
                    endDay();
                }

                setClubMood(); // This will calculate the club mood based on the above parameters
                simulationMinuteAccumulator -= 1.0;
                /**
                if (!tabPage7.IsDisposed)
                {
                    tabPage7_Paint(this, new PaintEventArgs(tabPage7.CreateGraphics(), tabPage7.ClientRectangle));
                }
                */
            }
        }

        // Strippers to clients ratio (1:7 is ideal); better under, exponentially worse over — 20%
        // Bar quality: enough bartenders, cleaners, servers, and drinks — 20%
        // Club cleanliness ratio — 10%
        // Hour of the night; peaks around 01:00 — 10%
        // Girls' attractiveness — 20%
        // Queue to capacity ratio; too much queue lowers mood — 10%
        // Problematic clients inside (drunk, high, mobs) — 5%
        // DJ quality — 5%
        // Inside safety (bouncers inside) — 5%
        // Club level or reputation bonus — 5%
        private void setClubMood()
        {
            double idealRatio = 1.0 / 7.0;
            double strippersToClientsRatio = (double)strippersOnDuty.Items.Count / Math.Max(1, clients);
            double dancerCoverageScore = Clamp(1 - Math.Max(0, (idealRatio - strippersToClientsRatio)) / idealRatio, 0, 1);
            double barQuality = (double)(barmanTrackbar.Value + WaiterTrackBar.Value) / 200; //TODO add drinks quantity and variety
            double cleanRatio = (double)CleanerTrackBar.Value / 100;
            double hourFactor = 0;
            int hour = simulationTime.Hour;
            if (hour < 2) hourFactor = 1.2;
            else if (hour < 4) hourFactor = 1.0;
            else if (hour < 5) hourFactor = 0.9;
            else if (hour >= 21 && hour < 22) hourFactor = 0.5;
            else if (hour >= 21 && hour < 24) hourFactor = 0.8;
            else hourFactor = 0.5;

            int counter = 0;
            double score = 0;
            foreach (string name in strippersOnDuty.Items)
            {
                Stripper stripper = stripperList.FirstOrDefault(s => s.Name == name);
                if (stripper != null)
                {
                    score += stripper.OverallScore;
                    counter++;
                }
            }
            double attractivenessFactor = counter > 0 ? (score / counter) / 100 : 0;
            double queueToCapacityRatio = 1 - (double)(clientQueue.Count / velvetClub.Capacity) * 3;
            queueToCapacityRatio = Clamp(queueToCapacityRatio, 0, 1);
            double riskCount = clientsLinkedList.Count(c => c.RiskProfile > 70);
            double problematicClientsRatio = riskCount / (double)Math.Max(1, clientsLinkedList.Count);
            double problematicClientsFactor = Clamp(1 - problematicClientsRatio * 5, 0, 1);
            problematicClientsFactor = Clamp(problematicClientsFactor, 0, 1);
            double djFactor = djTrackBar.Value / 100.0;
            double insideSafetyFactor = insideBouncerTrackBar.Value / 100.0;

            clubMoodByMinute["Club Mood"].Add(clubMood);
            clubMoodByMinute["Strippers to Clients"].Add(dancerCoverageScore);
            clubMoodByMinute["Bar and Service"].Add(barQuality);
            clubMoodByMinute["Cleanliness"].Add(cleanRatio);
            clubMoodByMinute["Strippers Attractiveness"].Add(attractivenessFactor);
            clubMoodByMinute["Queue to Capacity"].Add(queueToCapacityRatio);
            clubMoodByMinute["Problematic Clients"].Add(problematicClientsFactor);
            clubMoodByMinute["DJ"].Add(djFactor);
            clubMoodByMinute["Inside Safety"].Add(insideSafetyFactor);

            double totalScore = dancerCoverageScore * 0.2 +
                                barQuality * 0.2 +  
                                cleanRatio * 0.1 +
                                hourFactor * 0.1 +
                                attractivenessFactor * 0.2 +
                                queueToCapacityRatio * 0.1 +
                                problematicClientsFactor * 0.05 +
                                djFactor * 0.05 +
                                insideSafetyFactor * 0.05 +
                                ((double)velvetClub.Level / 5) * 0.05;
                        
            clubMood = Clamp(totalScore, 0, 1);
            clubMood = Math.Round(clubMood, 2); 
            clubMoodScroller.Value = (int)(100 * clubMood);
            label49.Text = "Club Mood : " + clubMood.ToString();
        }

        private void customerScreening()
        {
            if (listBoxAddedDoorBouncers.Items.Count == 0) {
                return;
            }
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
                        client.SetExitTime(simulationTime, this.velvetClub.Level);
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
                                client.SetExitTime(simulationTime, this.velvetClub.Level);
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
                if (!firstDay)
                {
                    dayCounter++;
                }

                if (dayCounter > 1)
                {
                    ShowRandomCommentsOnSocialMediaTab(6, true); //Call this before reset
                    feedNewRandomPost(); //Call this before reset
                    startDay(); //Reset the day
                }

                // If not the very first simulation, increment the day
                
                firstDay = false;

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
            endDay();
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

        private void feedNewRandomPost()
        {
            if (currentSocial != null)
                previousSocialEngagements.Enqueue(currentSocial);
            newPost();
            try
            {
                if (previousSocialEngagements.Count > 4)
                    previousSocialEngagements.Dequeue();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            ShowRandomPostAndImage();

        }

        private void ShowRandomPostAndImage()
        {
            try
            { 
                string xmlPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "social_media_posts","social_media_posts_" + LanguageSelector.SelectedItem + ".xml");
                
                var doc = XDocument.Load(xmlPath);
                var posts = doc.Descendants("Post").ToList();
                if (posts.Count == 0)
                {
                    textBoxSocialMediaPost.Text = "No posts found.";
                    return;
                }
                var rand = new Random();
                var randomPost = posts[rand.Next(posts.Count)];
                var text = randomPost.Element("Text")?.Value ?? "No text found.";
                textBoxSocialMediaPost.Text = text;

                string imagePath = this.currentSocial.imagePath;
                if (File.Exists(imagePath))
                    pictureBoxSocialCurrent.Image = Image.FromFile(imagePath);
                else
                    pictureBoxSocialCurrent.Image = null;
            }
            catch (Exception ex)
            {
                textBoxSocialMediaPost.Text = "Error: " + ex.Message;
                pictureBoxSocialCurrent.Image = null;
            }
            labelFollowersCount.Text = velvetClubRuntimes.followersCount.ToString();
            labelDailyReach.Text = currentSocial.engagement.ToString();
            labelLikes.Text = currentSocial.likes.ToString();
            PictureBox[] pictureBoxes = {pictureBoxPrevDay1, pictureBoxPrevDay2, pictureBoxPrevDay3, pictureBoxPrevDay4};
            Label[] dailyReach = {DailyReach1, DailyReach2, DailyReach3, DailyReach4};
            Label[] followersLabels = { FollowerCount1, FollowerCount2, FollowerCount3, FollowerCount4 };
            Label[] followerChanges = { FollowerChange1, FollowerChange2, FollowerChange3, FollowerChange4 };
            Label[] likes = { LikeCount1, LikeCount2, LikeCount3, LikeCount4 };
            for (int i = previousSocialEngagements.Count - 1; i >= 0; i--)
            {
                SocialEngagement elem = previousSocialEngagements.ElementAt(i);
                pictureBoxes[previousSocialEngagements.Count - 1 - i].Image = Image.FromFile(elem.imagePath);
                dailyReach[previousSocialEngagements.Count - 1 - i].Text = elem.engagement.ToString();
                followersLabels[previousSocialEngagements.Count - 1 - i].Text = elem.followers.ToString();
                followerChanges[previousSocialEngagements.Count - 1 - i].Text = elem.likes.ToString();
                likes[previousSocialEngagements.Count - 1 - i].Text = elem.likes.ToString();
            }
        }

        private void newPost()
        {
            var rand = new Random();
            double expectedRevenue = GetNetRevenueFromXml(velvetClub.Level);
            double revenueSuccess = Math.Max(1, (velvetClubRuntimes.BarRevenue + velvetClubRuntimes.ShowRevenue +
                           velvetClubRuntimes.VipRoomRevenue + velvetClubRuntimes.EntranceRevenue +
                           velvetClubRuntimes.LapBoothRevenue + velvetClubRuntimes.CloakRoomRevenue) / expectedRevenue);
            double dailySuccess = clubMoodByMinute["Club Mood"].Average() * 0.5 + revenueSuccess * 0.5;
            currentSocial = new SocialEngagement(dayCounter, velvetClubRuntimes.followersCount, (float)dailySuccess);
            velvetClubRuntimes.followersCount += currentSocial.followers - velvetClubRuntimes.followersCount;
        }

        private void ShowRandomCommentsOnSocialMediaTab(int count = 6, bool forceNewSelection = false)
        {
            // Map clubMoodByMinute keys to review problem types
            var moodTypeToProblem = new Dictionary<string, string>
            {
                { "Bar and Service", "bar" },
                { "Queue to Capacity", "queue" },
                { "Cleanliness", "cleanliness" },
                { "Strippers to Clients", "service" },
                { "Strippers Attractiveness", "dancers" },
                { "Problematic Clients", "problematic_clients" },
                { "DJ", "dj" },
                { "Inside Safety", "inside_safety" }
            };

            var problematicTypes = moodTypeToProblem
            .Where(kvp => clubMoodByMinute.TryGetValue(kvp.Key, out var values) && values.Count > 0 && values.Average() < 0.4)
            .Select(kvp => kvp.Value)
            .ToList();

            try
            {
                string xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "daily_reviews","daily_reviews_" + LanguageSelector.SelectedItem + ".xml");
                var doc = XDocument.Load(xmlPath);

                // Get all valid reviews for the current club level and language
                var allPosts = doc.Descendants("Review")
                    .Where(x =>
                        x.Element("ClubLevel") != null &&
                        int.TryParse(x.Element("ClubLevel").Value, out int lvl) &&
                        lvl == velvetClub.Level &&
                        x.Element("Stars") != null &&
                        int.TryParse(x.Element("Stars").Value, out int _) &&
                        x.Element("Text") != null &&
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
                    // Calculate average club mood (normalized 0-1)
                    double clubMoodAvg = clubMoodByMinute["Club Mood"].Average();

                    // Define weights for each star (1-5) based on clubMoodAvg
                    double[] starWeights = new double[5];
                    for (int s = 1; s <= 5; s++)
                    {
                        double t = (s - 1) / 4.0;
                        starWeights[s - 1] = Math.Pow(clubMoodAvg, t) * Math.Pow(1 - clubMoodAvg, 1 - t);
                    }
                    double totalWeight = starWeights.Sum();
                    for (int s = 0; s < 5; s++) starWeights[s] /= totalWeight;

                    selectedReviewIds.Clear();
                    var rand = new Random();

                    for (int i = 0; i < count; i++)
                    {
                        // Weighted random star selection (as before)
                        double r = rand.NextDouble();
                        double cumulative = 0;
                        int chosenStar = 5;
                        for (int s = 0; s < 5; s++)
                        {
                            cumulative += starWeights[s];
                            if (r < cumulative)
                            {
                                chosenStar = s + 1;
                                break;
                            }
                        }

                        List<XElement> possiblePosts;

                        if (chosenStar <= 3)
                        {
                            // If there are problematic types, try to pick a review matching one of them
                            if (problematicTypes.Count > 0)
                            {
                                // Randomly pick a problematic type for this review
                                string chosenProblem = problematicTypes[rand.Next(problematicTypes.Count)];
                                possiblePosts = allPosts
                                    .Where(x =>
                                        int.TryParse(x.Element("Stars")?.Value, out int stars) && stars == chosenStar &&
                                        string.Equals((string)x.Attribute("problem"), chosenProblem, StringComparison.OrdinalIgnoreCase)
                                    )
                                    .ToList();

                                // Fallback: if none, use any problem review with this star
                                if (possiblePosts.Count == 0)
                                {
                                    possiblePosts = allPosts
                                        .Where(x =>
                                            int.TryParse(x.Element("Stars")?.Value, out int stars) && stars == chosenStar &&
                                            !string.IsNullOrWhiteSpace((string)x.Attribute("problem")) &&
                                            !string.IsNullOrWhiteSpace(x.Attribute("problem").Value)
                                        )
                                        .ToList();
                                }
                                // Fallback: if still none, use any review with this star
                                if (possiblePosts.Count == 0)
                                {
                                    possiblePosts = allPosts
                                        .Where(x => int.TryParse(x.Element("Stars")?.Value, out int stars) && stars == chosenStar)
                                        .ToList();
                                }
                            }
                            else
                            {
                                // No problematic types, use any problem review with this star
                                possiblePosts = allPosts
                                    .Where(x =>
                                        int.TryParse(x.Element("Stars")?.Value, out int stars) && stars == chosenStar &&
                                        !string.IsNullOrWhiteSpace((string)x.Attribute("problem")) &&
                                        !string.IsNullOrWhiteSpace(x.Attribute("problem").Value)
                                    )
                                    .ToList();

                                if (possiblePosts.Count == 0)
                                {
                                    possiblePosts = allPosts
                                        .Where(x => int.TryParse(x.Element("Stars")?.Value, out int stars) && stars == chosenStar)
                                        .ToList();
                                }
                            }
                        }
                        else // For 4-5 stars, prefer reviews with empty problem attribute
                        {
                            possiblePosts = allPosts
                                .Where(x =>
                                    int.TryParse(x.Element("Stars")?.Value, out int stars) && stars == chosenStar &&
                                    (string.IsNullOrWhiteSpace((string)x.Attribute("problem")) || x.Attribute("problem").Value == "")
                                )
                                .ToList();

                            if (possiblePosts.Count == 0)
                            {
                                possiblePosts = allPosts
                                    .Where(x => int.TryParse(x.Element("Stars")?.Value, out int stars) && stars == chosenStar)
                                    .ToList();
                            }
                        }

                        // Pick a random review with this star
                        var availableIds = possiblePosts.Select(x => int.Parse(x.Element("ID").Value)).Except(selectedReviewIds).ToList();
                        if (availableIds.Count == 0)
                            availableIds = possiblePosts.Select(x => int.Parse(x.Element("ID").Value)).ToList();

                        if (availableIds.Count > 0)
                        {
                            int idx = rand.Next(availableIds.Count);
                            selectedReviewIds.Add(availableIds[idx]);
                        }
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
                    .Select(x => x.Element("Text").Value ?? "No comments available.")
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
            int customers = 0;
            // Center is double the club capacity
            string xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "club_levels_updated_entrance_fees.xml");
            if (!File.Exists(xmlPath))
                return 0;

            try
            {
                var doc = System.Xml.Linq.XDocument.Load(xmlPath);
                var clubLevelElem = doc.Descendants("ClubLevel")
                    .Skip(velvetClub.Level)
                    .FirstOrDefault();

                if (clubLevelElem != null)
                {
                    var customersElem = clubLevelElem.Element("DailyCustomers");
                    if (customersElem != null)
                    {
                        if (!int.TryParse(customersElem.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out customers))
                            return 0;
                    }
                }
            }
            catch
            {
                return 0;
            }
            int center = customers;
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
            int level = Math.Max(1, Math.Min(velvetClub.Level, 6));
            int expectedPrice = 0;
            try
            {
                var doc = System.Xml.Linq.XDocument.Load(xmlPath);
                var clubLevelElem = doc.Descendants("ClubLevel")
                    .Skip(velvetClub.Level)
                    .FirstOrDefault();

                if (clubLevelElem != null)
                {
                    var customersElem = clubLevelElem.Element("EntranceFeePerCustomer");
                    if (customersElem != null)
                    {
                        if (!int.TryParse(customersElem.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out expectedPrice))
                            return 0;
                    }
                }
            }
            catch
            {
                return 0;
            }

            double priceRatio = (double)expectedPrice / Math.Max(1, entryPrice);

            // Improved price multiplier logic for more realistic drop-off
            double priceMultiplier;
            if (priceRatio > 1.2)
                priceMultiplier = 1.4;
            else if (priceRatio < 0.8)
            {
                // For very high prices, drop off more steeply
                if (priceRatio < 0.2)
                    priceMultiplier = 0.05;
                else if (priceRatio < 0.4)
                    priceMultiplier = 0.15;
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
            tabControl1.SelectedTab = tabControl1.TabPages[2];
        }

        private void pictureBox17_Click(object sender, EventArgs e)
        {
            tabControl1.SelectedTab = tabControl1.TabPages[3];
        }

        private void pictureBox18_Click(object sender, EventArgs e)
        {
            tabControl1.SelectedTab = tabControl1.TabPages[4];
        }

        private void endDay()
        {
            for (int i = (simulationTime - simulationStart).Minutes; i < (simulationStart - simulationEnd).Minutes; i++)
            {
                ;
            }

            double dailyEarnings = velvetClubRuntimes.BarRevenue + velvetClubRuntimes.ShowRevenue +
                           velvetClubRuntimes.VipRoomRevenue + velvetClubRuntimes.EntranceRevenue +
                           velvetClubRuntimes.LapBoothRevenue + velvetClubRuntimes.CloakRoomRevenue;
            int totalVisitors = acceptedClients.Count + rejectedClients.Count;
            double totalCost = 0;
            // Parse costs from labels (already calculated in updateExpenses)
            double.TryParse(labelTotalExpenses.Text.Replace("$", "").Trim(), out totalCost);
            totalCost += (velvetClubRuntimes.VipRoomRevenue + 
                            velvetClubRuntimes.ShowRevenue +
                                velvetClubRuntimes.LapBoothRevenue)
                                * ((double)progressBarStripperTip.Value / 100);
            double netRevenue = dailyEarnings - totalCost;
            velvetClubRuntimes.totalBalance += (int)netRevenue;

            // Assign to tabPage8 labels
            labelReportDailyEarnings.Text = dailyEarnings.ToString("F2");
            labelReportVisitors.Text = totalVisitors.ToString();
            labelReportTotalCost.Text = totalCost.ToString("F2");
            labelReportNetRevenue.Text = netRevenue.ToString("F2");
            labelReportTotalCash.Text = velvetClubRuntimes.totalBalance.ToString("F2");
            labelReportCloakRoom.Text = velvetClubRuntimes.CloakRoomRevenue.ToString("F2");
            labelReportStageShow.Text = velvetClubRuntimes.ShowRevenue.ToString("F2");
            labelReportVIP.Text = velvetClubRuntimes.VipRoomRevenue.ToString("F2");
            labelReportBar.Text = velvetClubRuntimes.BarRevenue.ToString("F2");
            labelReportLapDance.Text = velvetClubRuntimes.LapBoothRevenue.ToString("F2");
            labelReportEntrance.Text = velvetClubRuntimes.EntranceRevenue.ToString("F2");
            labelCash.Text = "$ "+velvetClubRuntimes.totalBalance.ToString("F2");
            string xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "club_levels_revenue_model.xml");
            if (!File.Exists(xmlPath))
            {
                MessageBox.Show("Revenue model file not found: " + xmlPath, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            try
            {
                double expectedBarRevenue = 0;
                double expectedStageRevenue = 0;
                double expectedLapDanceRevenue = 0;
                double expectedVipRevenue = 0;
                var doc = System.Xml.Linq.XDocument.Load(xmlPath);
                var clubLevelElem = doc.Descendants("ClubLevel")
                    .Skip(velvetClub.Level)
                    .FirstOrDefault();
               if (clubLevelElem != null)
                {
                    var customerElem = clubLevelElem.Element("DailyCustomers");
                    var barElem = clubLevelElem.Element("PerMinuteSpendingBreakdown")?.Element("Bar");
                    var stageElem = clubLevelElem.Element("PerMinuteSpendingBreakdown")?.Element("StageShow");
                    var danceElem = clubLevelElem.Element("PerMinuteSpendingBreakdown")?.Element("LapDanceRoom");
                    var vipElem = clubLevelElem.Element("PerMinuteSpendingBreakdown")?.Element("VIPRoom");
                    var expectedStay = clubLevelElem.Element("AvgTimeMinutes");
                    if (customerElem != null && barElem != null && stageElem != null)
                    {
                        double.TryParse(customerElem.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double expectedCustomers);
                        double.TryParse(barElem.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out expectedBarRevenue);
                        double.TryParse(stageElem.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out expectedStageRevenue);
                        double.TryParse(expectedStay.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double expectedStayMinutes);
                        if (danceElem != null)
                        {
                            double.TryParse(danceElem.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out expectedLapDanceRevenue);
                            expectedLapDanceRevenue *= expectedCustomers * expectedStayMinutes;
                            progressBarReportLapDance.Maximum = (int)expectedLapDanceRevenue;
                            progressBarReportLapDance.Value = Math.Min((int)velvetClubRuntimes.LapBoothRevenue, progressBarReportLapDance.Maximum);
                            progressBarReportLapDance.Visible = true;
                        }
                        else
                            progressBarReportLapDance.Visible = false;
                        if (vipElem != null)
                        {
                            double.TryParse(vipElem.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out expectedVipRevenue);
                            expectedVipRevenue *= expectedCustomers * expectedStayMinutes;
                            progressBarReportVIP.Maximum = (int)expectedVipRevenue;
                            progressBarReportVIP.Value = Math.Min((int)velvetClubRuntimes.VipRoomRevenue, progressBarReportVIP.Maximum);
                            progressBarReportVIP.Visible = true;
                        }
                        else
                            progressBarReportVIP.Visible = false;

                        expectedBarRevenue *= expectedCustomers * expectedStayMinutes;
                        expectedStageRevenue *= expectedCustomers * expectedStayMinutes;
                        
                        progressBarReportBar.Maximum = (int)expectedBarRevenue;
                        progressBarReportStageShow.Maximum = (int)expectedStageRevenue;            
                        progressBarReportBar.Value = Math.Min((int)velvetClubRuntimes.BarRevenue, progressBarReportBar.Maximum);
                        progressBarReportStageShow.Value = Math.Min((int)velvetClubRuntimes.ShowRevenue, progressBarReportStageShow.Maximum);
                   
                        xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "club_levels_updated_entrance_fees.xml");
                        doc = System.Xml.Linq.XDocument.Load(xmlPath);
                        clubLevelElem = doc.Descendants("ClubLevel")
                            .Skip(velvetClub.Level)
                            .FirstOrDefault();
                        var entranceElem = clubLevelElem?.Element("EntranceFeePerCustomer");
                        var cloakElem = clubLevelElem?.Element("CloakRoomPerCustomer");
                        if (clubLevelElem != null)
                        {
                            double.TryParse(entranceElem.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double expectedEntranceFee);
                            expectedEntranceFee *= expectedCustomers;
                            progressBarReportEntrance.Maximum = (int)expectedEntranceFee;   
                            progressBarReportEntrance.Value = Math.Min((int)velvetClubRuntimes.EntranceRevenue, progressBarReportEntrance.Maximum);
                        }
                        if (cloakElem != null)
                        {
                            double.TryParse(cloakElem.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double expectedCloakRevenue);
                            expectedCloakRevenue *= expectedCustomers;
                            progressBarReportCloakRoom.Maximum = (int)expectedCloakRevenue;
                            progressBarReportCloakRoom.Value = Math.Min((int)velvetClubRuntimes.CloakRoomRevenue, progressBarReportCloakRoom.Maximum);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading XML data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void startDay()
        {
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

            earningsChart.SetEarnings(earningsPerMinute);
            earningsPerMinute.Clear();
            earningsPerMinute = Enumerable.Repeat(0.0, 420).ToList();
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
            updateExpenses();
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

            const int maxScore = 60;

            // Dynamically set how many bouncers are required to get full score
            // Level 1 requires ~1.5, Level 10 requires 5.0 bouncers
            double requiredBouncers = 1 + (clubLevel) * (3.5 / 5); // maps 1→1.5, 10→5.0

            // Calculate the score proportionally
            double proportion = Math.Min(numberOfBouncers / requiredBouncers, 1.0);

            return (int)Math.Round(maxScore * proportion) + (int)(maxConflict * 0.24) + (int)(maxScreening * 0.16);
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static double Clamp(double value, double min, double max)
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

        private void PopulateStripperListPanel()
        {
            flowLayoutPanelStrippers.Controls.Clear();

            for (int i = 0; i < stripperList.Count; i++)
            {
                var stripper = stripperList[i];
                Color cardColor;
                if (stripperList[i].OverallScore < 70)
                {
                    cardColor = Color.FromArgb(128, 128, 128);
                }
                else if (stripperList[i].OverallScore < 75)
                {
                    cardColor = Color.FromArgb(0, 200, 0);
                }
                else if (stripperList[i].OverallScore < 80)
                {
                    cardColor = Color.FromArgb(0, 0, 200);
                }
                else if (stripperList[i].OverallScore < 90)
                {
                    cardColor = Color.FromArgb(138, 43, 226);
                    cardColor = Color.FromArgb(40, 30, 50);
                }
                else
                {
                    cardColor = Color.FromArgb(207, 181, 59); 
                }

                // Create card panel
                Panel card = new Panel
                {
                    Width = 160,
                    Height = 220,
                    Margin = new Padding(8),
                    BackColor = cardColor,
                    BorderStyle = BorderStyle.FixedSingle,
                    Tag = i,
                };

                // Stripper photo
                PictureBox pic = new PictureBox
                {
                    Width = 140,
                    Height = 140,
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    Left = 10,
                    Top = 10,
                    BackColor = Color.Black
                };
                string imagesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", "Strippers");
                string imagePath = Path.Combine(imagesDir, (stripperList.IndexOf(stripper) + 1) + ".jpeg");
                if (File.Exists(imagePath))
                    pic.Image = Image.FromFile(imagePath);

                // Name label
                Label nameLabel = new Label
                {
                    Text = stripper.Name,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    AutoSize = false,
                    Width = 140,
                    Height = 24,
                    Top = 155,
                    Left = 10,
                    TextAlign = System.Drawing.ContentAlignment.MiddleCenter
                };

                // Overall label
                Label overallLabel = new Label
                {
                    Text = $"Overall: {stripper.OverallScore}",
                    ForeColor = Color.DeepSkyBlue,
                    Font = new Font("Segoe UI", 9, FontStyle.Regular),
                    AutoSize = false,
                    Width = 140,
                    Height = 20,
                    Top = 185,
                    Left = 10,
                    TextAlign = System.Drawing.ContentAlignment.MiddleCenter
                };

                card.Click += StripperCard_Click;
                pic.Click += StripperCard_Click;
                nameLabel.Click += StripperCard_Click;
                overallLabel.Click += StripperCard_Click;

                // Add controls to card
                card.Controls.Add(pic);
                card.Controls.Add(nameLabel);
                card.Controls.Add(overallLabel);

                // Add card to flow panel
                flowLayoutPanelStrippers.Controls.Add(card);
            }
        }

        private void StripperCard_Click(object sender, EventArgs e)
        {
            Control ctrl = sender as Control;
            Panel card = ctrl as Panel ?? ctrl.Parent as Panel;
            if (card != null && card.Tag is int index)
            {
                selectedStripperIndex = index;
                StripperListBox_SelectedIndexChanged(sender, EventArgs.Empty);
                HighlightSelectedStripperCard(index);
            }
        }

        private void HighlightSelectedStripperCard(int selectedIndex)
        {
            for (int i = 0; i < flowLayoutPanelStrippers.Controls.Count; i++)
            {
                var card = flowLayoutPanelStrippers.Controls[i] as Panel;
                if (card != null)
                {
                    var stripper = stripperList[i];
                    Color normalColor;
                    Color highlightColor;

                    // Define normal (less apparent) and highlight (vivid) colors
                    if (stripper.OverallScore < 70)
                    {
                        normalColor = Color.FromArgb(80, 80, 80); // dull gray
                        highlightColor = Color.FromArgb(128, 128, 128);
                    }
                    else if (stripper.OverallScore < 75)
                    {
                        normalColor = Color.FromArgb(0, 80, 0); // dull green
                        highlightColor = Color.FromArgb(0, 200, 0);
                    }
                    else if (stripper.OverallScore < 80)
                    {
                        normalColor = Color.FromArgb(0, 0, 80); // dull blue
                        highlightColor = Color.FromArgb(0, 0, 200);
                    }
                    else if (stripper.OverallScore < 90)
                    {
                        normalColor = Color.FromArgb(25, 18, 30); // dull purple/gray
                        highlightColor = Color.FromArgb(40, 30, 50);
                    }
                    else
                    {
                        normalColor = Color.FromArgb(120, 105, 40); // dull gold
                        highlightColor = Color.FromArgb(207, 181, 59);
                    }

                    card.BackColor = (i == selectedIndex) ? highlightColor : normalColor;
                    card.BorderStyle = (i == selectedIndex) ? BorderStyle.Fixed3D : BorderStyle.FixedSingle;
                }
            }
        }

        private void StripperListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (selectedStripperIndex < 0 || stripperList == null || stripperList.Count == 0)
            {
                StripperImage.Image = null;
                ShowStripperBox.Text = "Stripper Name";
                labelDailyWageStripper.Text = "Daily Wage";
                return;
            }

            // Get the selected stripper
            var selectedStripper = stripperList[selectedStripperIndex];

            // Build the expected image path (e.g., "bin/debug/images/strippers/Lola Velvet.jpg")
            string imagesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", "Strippers");

            string imagePath = Path.Combine(imagesDir, ((selectedStripperIndex) + 1).ToString() + ".jpeg");

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
            progressBarStripperTipPercentage.Value = selectedStripper.TipPoolPercentage;
            label24.Text = $"Tip Pool Percentage: {selectedStripper.TipPoolPercentage}%";
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
            if (selectedStripperIndex < 0 || stripperList == null || stripperList.Count == 0)
                return;

            var selectedStripper = stripperList[selectedStripperIndex];

            if (!strippersOnDuty.Items.Contains(selectedStripper.Name))
            {
                strippersOnDuty.Items.Add(selectedStripper.Name);
                labelStripperCount.Text = $"Strippers on Duty: {strippersOnDuty.Items.Count}";
                int cost = 0;
                int average = 0;
                foreach (var item in strippersOnDuty.Items)
                {
                    cost += stripperList.FirstOrDefault(x => x.Name == item.ToString())?.DailyWage ?? 0;
                    average += stripperList.FirstOrDefault(x => x.Name == item.ToString())?.TipPoolPercentage ?? 0;
                }
                labelStripperCost.Text = $"Total Stripper Cost: {cost} $";
                progressBarStripperReadiness.Value = 50;
                progressBarStripperTip.Value = average / Math.Max(1, strippersOnDuty.Items.Count);
                labelStripperTipPercentage.Text = $"Average Stipper Tip Pool Percentage: {progressBarStripperTip.Value}%";
            }
            updateExpenses();
            progressBarStripperReadiness.Value = calculateStripperReadiness();
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
                int average = 0;
                foreach (var item in strippersOnDuty.Items)
                {
                    cost += stripperList.FirstOrDefault(x => x.Name == item.ToString())?.DailyWage ?? 0;
                    average += stripperList.FirstOrDefault(x => x.Name == item.ToString())?.TipPoolPercentage ?? 0;
                }
                labelStripperCost.Text = $"Total Stripper Cost: {cost} $";
                progressBarStripperTip.Value = average / strippersOnDuty.Items.Count;
                labelStripperTipPercentage.Text = $"Average Stipper Tip Pool Percentage: {progressBarStripperTip.Value}%";
            }
            progressBarStripperReadiness.Value = 50;
            updateExpenses();
            progressBarStripperReadiness.Value = calculateStripperReadiness();
        }

        private void CalculateBarRevenue(int clubLevel, int customerCount, double clubMood, DateTime simTime)
        {
            if(barmanTrackbar.Value == 0)
            {
                return;
            }

            string xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "club_levels_revenue_model.xml");
            if (!File.Exists(xmlPath))
                return;

            var doc = System.Xml.Linq.XDocument.Load(xmlPath);
            var clubLevelElem = doc.Descendants("ClubLevel")
                .Skip(clubLevel) 
                .FirstOrDefault();

            double barValue = 0;
            if (clubLevelElem != null)
            {
                var barElem = clubLevelElem.Element("PerMinuteSpendingBreakdown")?.Element("Bar");
                if (barElem != null)
                    double.TryParse(barElem.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out barValue);
            }

            double mood = (clubMood + 0.5 + (barmanTrackbar.Value + WaiterTrackBar.Value) / 200.0 + 0.5) / 2;
            double multiplier;
            if (mood < 1.0)
                multiplier = Math.Pow(mood, 2);
            else
                multiplier = 1 + 0.5 * (mood - 1.0);

            earningsPerMinute[(int)(simulationTime - simulationStart).TotalMinutes] += barValue * customerCount * multiplier;
            velvetClubRuntimes.BarRevenue += barValue * customerCount * multiplier;
            label38.Text = velvetClubRuntimes.BarRevenue.ToString("F2");
        }

        private void CalculateShowRevenue(int clubLevel, int customerCount, double clubMood, DateTime simTime)
        {
            if(strippersOnDuty.Items.Count == 0)
            {
                return;
            }
            string xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "club_levels_revenue_model.xml");
            if (!File.Exists(xmlPath))
                return;

            var doc = System.Xml.Linq.XDocument.Load(xmlPath);
            var clubLevelElem = doc.Descendants("ClubLevel")
                .Skip(clubLevel)
                .FirstOrDefault();

            double showValue = 0;
            if (clubLevelElem != null)
            {
                var showElem = clubLevelElem.Element("PerMinuteSpendingBreakdown")?.Element("StageShow");
                if (showElem != null)
                    double.TryParse(showElem.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out showValue);
            }

            double mood = clubMood + 0.5;
            double multiplier;
            if (mood < 1.0)
                multiplier = 0.5 + 0.5 * mood; 
            else
                multiplier = 1 + 0.7 * (mood - 1.0);
            earningsPerMinute[(int)(simulationTime - simulationStart).TotalMinutes] += showValue * customerCount * multiplier;
            velvetClubRuntimes.ShowRevenue += showValue * customerCount * multiplier; 
            label31.Text = velvetClubRuntimes.ShowRevenue.ToString("F2");
        }

        private void CalculateLapDanceRevenue(int clubLevel, int customerCount, double clubMood, DateTime simTime)
        {
            string xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "club_levels_revenue_model.xml");
            if (!File.Exists(xmlPath))
                return;
            var doc = System.Xml.Linq.XDocument.Load(xmlPath);
            var clubLevelElem = doc.Descendants("ClubLevel")
                .Skip(clubLevel)
                .FirstOrDefault();
            double lapDanceValue = 0;
            if (clubLevelElem != null)
            {
                var lapDanceElem = clubLevelElem.Element("PerMinuteSpendingBreakdown")?.Element("LapDanceRoom");
                if (lapDanceElem != null)
                    double.TryParse(lapDanceElem.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out lapDanceValue);
            }
            double mood = clubMood + 0.5;
            double multiplier = 0.8 + 0.4 * (mood - 1.0);
            double averageGirl = 0;
            foreach (string stripperName in strippersOnDuty.Items)
            {
                var stripper = stripperList.FirstOrDefault(x => x.Name == stripperName);
                if (stripper != null)
                    averageGirl += stripper.OverallScore;
            }
            averageGirl /= strippersOnDuty.Items.Count > 0 ? strippersOnDuty.Items.Count : 1;
            averageGirl /= 10;
            double score_effect;
            if (averageGirl < 6) score_effect = 1 + 0.1 * (averageGirl - 6);
            else score_effect = 0.6 + 0.1 * averageGirl;
            multiplier = multiplier * score_effect;
            earningsPerMinute[(int)(simulationTime - simulationStart).TotalMinutes] += lapDanceValue * customerCount * multiplier;
            velvetClubRuntimes.LapBoothRevenue += lapDanceValue * customerCount * multiplier;
            label33.Text = velvetClubRuntimes.LapBoothRevenue.ToString("F2");
        }

        private void CalculateVipRoomRevenue(int clubLevel, int customerCount, double clubMood, DateTime simTime)
        {
            string xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "club_levels_revenue_model.xml");
            if (!File.Exists(xmlPath))
                return;
            var doc = System.Xml.Linq.XDocument.Load(xmlPath);
            var clubLevelElem = doc.Descendants("ClubLevel")
                .Skip(clubLevel)
                .FirstOrDefault();
            double vipRoomValue = 0;
            if (clubLevelElem != null)
            {
                var vipRoomElem = clubLevelElem.Element("PerMinuteSpendingBreakdown")?.Element("VIPRoom");
                if (vipRoomElem != null)
                    double.TryParse(vipRoomElem.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out vipRoomValue);
            }
            double mood = clubMood + 0.5;
            double multiplier = 0.8 + 0.4 * (mood - 1.0);
            double averageGirl = 0;
            int stripperCount = strippersOnDuty.Items.Count > 0 ? strippersOnDuty.Items.Count : 0;
            foreach (object item in strippersOnDuty.Items)
            {
                string name = item.ToString();
                var s = stripperList.FirstOrDefault(x => x.Name == name);
                if (s != null)
                    averageGirl += s.OverallScore;
            }
            averageGirl = stripperCount > 0 ? averageGirl / stripperCount : 0;
            averageGirl /= 10;
            double score_effect;
            if (averageGirl >= 6) score_effect = 1 + 0.2 * (averageGirl - 6);
            else score_effect = 0.4 + 0.1 * averageGirl;
            multiplier = multiplier * score_effect;
            earningsPerMinute[(int)(simulationTime - simulationStart).TotalMinutes] += vipRoomValue * customerCount * multiplier;
            velvetClubRuntimes.VipRoomRevenue += vipRoomValue * customerCount * multiplier;
            label34.Text = velvetClubRuntimes.VipRoomRevenue.ToString("F2");
            return;
        }

        private void pictureBox19_Click(object sender, EventArgs e)
        {
            tabControl1.SelectedTab = tabPage6;
        }

        private void FindClosestBarmanScoreToTrackBar(object sender, EventArgs e)
        {
            int targetScore = barmanTrackbar.Value;
            int n = barmans.Count;
            int closestScore = int.MinValue;
            int closestDiff = int.MaxValue;
            List<Barman> bestCombo = null;
            bool perfectMatch = false;
            List<int> ns = new List<int>(n);
            for (int i = 0; i < n; i++)
                ns.Add(i);

            int maxBarmens = 5;
            for (int k = 1; k <= Math.Min(n, maxBarmens); k++)
            {
                foreach (var indices in GetCombinations(ns, k))
                {
                    var combo = new List<Barman>();
                    int maxOverall = int.MinValue;
                    foreach (int i in indices)
                    {
                        var b = barmans[i];
                        combo.Add(b);
                        if (b.Overall > maxOverall) maxOverall = b.Overall;
                    }
                    int level = velvetClub.Level;
                    int score = GetBarmanScore(level, k, maxOverall);
                    int diff = Math.Abs(score - targetScore);
                    if (diff < closestDiff)
                    {
                        closestDiff = diff;
                        closestScore = score;
                        bestCombo = new List<Barman>(combo);
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
            ListBox listBoxAddedBarmens = new ListBox();
            listBoxAddedBarmens.Items.Clear();
            int totalBarmenCost = 0;
            if (bestCombo != null)
            {
                foreach (var b in bestCombo)
                {
                    listBoxAddedBarmens.Items.Add(b.Name);
                    totalBarmenCost += b.DailyWage;
                }
            }
            labelBartenderCounter.Text = listBoxAddedBarmens.Items.Count.ToString();
            labelBartenderCost.Text = totalBarmenCost.ToString();
            updateExpenses();
        }

        public int GetBarmanScore(int clubLevel, int numberOfBarmens, int maxOverall)
        {
            clubLevel = Clamp(clubLevel, 0, 5);
            numberOfBarmens = Clamp(numberOfBarmens, 0, 5);
            double requiredBarmens = 1 + clubLevel;
            double proportion = Math.Min(numberOfBarmens / requiredBarmens, 1.0);
            return (int)Math.Round(75 * proportion) + (int)(maxOverall * 0.25);
        }

        private void FindClosestCleanerScoreToTrackBar(object sender, EventArgs e)
        {
            int targetScore = CleanerTrackBar.Value;
            int n = cleaners.Count;
            int closestScore = int.MinValue;
            int closestDiff = int.MaxValue;
            List<Cleaner> bestCombo = null;
            bool perfectMatch = false;
            List<int> ns = new List<int>(n);
            for (int i = 0; i < n; i++)
                ns.Add(i);

            int maxCleaners = 5;
            for (int k = 1; k <= Math.Min(n, maxCleaners); k++)
            {
                foreach (var indices in GetCombinations(ns, k))
                {
                    var combo = new List<Cleaner>();
                    int maxOverall = int.MinValue;
                    foreach (int i in indices)
                    {
                        var b = cleaners[i];
                        combo.Add(b);
                        if (b.Overall > maxOverall) maxOverall = b.Overall;
                    }
                    int level = velvetClub.Level;
                    int score = GetCleanerScore(level, k, maxOverall);
                    int diff = Math.Abs(score - targetScore);
                    if (diff < closestDiff)
                    {
                        closestDiff = diff;
                        closestScore = score;
                        bestCombo = new List<Cleaner>(combo);
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
            ListBox listBoxAddedCleaners = new ListBox();
            listBoxAddedCleaners.Items.Clear();
            int totalCleanerCost = 0;
            if (bestCombo != null)
            {
                foreach (var b in bestCombo)
                {
                    listBoxAddedCleaners.Items.Add(b.Name);
                    totalCleanerCost += b.DailyWage;
                }
            }
            labelCleanersCounter.Text = listBoxAddedCleaners.Items.Count.ToString();
            labelCleanersCost.Text = totalCleanerCost.ToString();
            updateExpenses();
        }

        public int GetCleanerScore(int clubLevel, int numberOfCleaners, int maxOverall)
        {
            clubLevel = Clamp(clubLevel, 0, 5);
            numberOfCleaners = Clamp(numberOfCleaners, 0, 5);
            double requiredCleaners = 1 + (clubLevel / 2);
            double proportion = Math.Min(numberOfCleaners / requiredCleaners, 1.0);
            return (int)Math.Round(75 * proportion) + (int)(maxOverall * 0.25);
        }

        private void FindClosestWaiterScoreToTrackBar(object sender, EventArgs e)
        {
            int targetScore = WaiterTrackBar.Value;
            int n = waiters.Count;
            int closestScore = int.MinValue;
            int closestDiff = int.MaxValue;
            List<Waiter> bestCombo = null;
            bool perfectMatch = false;
            List<int> ns = new List<int>(n);
            for (int i = 0; i < n; i++)
                ns.Add(i);

            int maxWaiters = 5;
            for (int k = 1; k <= Math.Min(n, maxWaiters); k++)
            {
                foreach (var indices in GetCombinations(ns, k))
                {
                    var combo = new List<Waiter>();
                    int maxOverall = int.MinValue;
                    foreach (int i in indices)
                    {
                        var b = waiters[i];
                        combo.Add(b);
                        if (b.Overall > maxOverall) maxOverall = b.Overall;
                    }
                    int level = velvetClub.Level;
                    int score = GetWaiterScore(level, k, maxOverall);
                    int diff = Math.Abs(score - targetScore);
                    if (diff < closestDiff)
                    {
                        closestDiff = diff;
                        closestScore = score;
                        bestCombo = new List<Waiter>(combo);
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
            ListBox listBoxAddedWaiters = new ListBox();
            listBoxAddedWaiters.Items.Clear();
            int totalWaiterCost = 0;
            if (bestCombo != null)
            {
                foreach (var b in bestCombo)
                {
                    listBoxAddedWaiters.Items.Add(b.Name);
                    totalWaiterCost += b.DailyWage;
                }
            }
            labelWaiterCounter.Text = listBoxAddedWaiters.Items.Count.ToString();
            labelWaiterCost.Text = totalWaiterCost.ToString();
            updateExpenses(); 
        }

        public int GetWaiterScore(int clubLevel, int numberOfWaiters, int maxOverall)
        {
            clubLevel = Clamp(clubLevel, 0, 5);
            numberOfWaiters = Clamp(numberOfWaiters, 0, 5);
            double requiredBarmens = 1 + clubLevel;
            double proportion = Math.Min(numberOfWaiters / requiredBarmens, 1.0);
            return (int)Math.Round(75 * proportion) + (int)(maxOverall * 0.25);
        }

        private void FindClosestDjScoreToTrackBar(object sender, EventArgs e)
        {
            int targetScore = djTrackBar.Value;
            int closestScore = int.MinValue;
            int closestDiff = int.MaxValue;
            DJ selectedDJ = null;
            foreach (DJ dj in djs)
            {
                if (Math.Abs(dj.Overall - targetScore) < closestDiff)
                {
                    selectedDJ = dj;
                    closestScore = dj.Overall;
                    closestDiff = Math.Abs(dj.Overall - targetScore);
                }
            }
            labelDjCost.Text = selectedDJ?.DailyWage.ToString() ?? "0";
            labelDjCounter.Text = selectedDJ != null ? "1" : "0";
            updateExpenses();
        }

        private void FindClosestInsideBouncerScoreToTrackBar(object sender, EventArgs e)
        {
            int targetScore = insideBouncerTrackBar.Value;
            int closestScore = int.MinValue;
            int closestDiff = int.MaxValue;
            List<Bouncer> insideBouncers = new List<Bouncer>(
                bouncerList.Where(b => !listBoxAddedDoorBouncers.Items.Contains(b.Name))
            ); //Only consider bouncers not already added to the door
            List<Bouncer> bestCombo = null;
            bool perfectMatch = false;
            List<int> ns = new List<int>(insideBouncers.Count);
            for (int i = 0; i < insideBouncers.Count; i++)
                ns.Add(i);
            int maxBouncers = 5;
            for (int k = 1; k <= Math.Min(insideBouncers.Count, maxBouncers); k++)
            {
                foreach (var indices in GetCombinations(ns, k))
                {
                    var combo = new List<Bouncer>();
                    int maxOverall = int.MinValue;
                    foreach (int i in indices)
                    {
                        var b = insideBouncers[i];
                        combo.Add(b);
                        if (b.Overall > maxOverall) maxOverall = b.Overall;
                    }
                    int level = velvetClub.Level;
                    int score = GetInsideBouncerScore(level, k, maxOverall);
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
            ListBox listBoxAddedInsideBouncers = new ListBox();
            listBoxAddedInsideBouncers.Items.Clear();
            int totalInsideBouncerCost = 0;
            if (bestCombo != null)
            {
                foreach (var b in bestCombo)
                {
                    listBoxAddedInsideBouncers.Items.Add(b.Name);
                    totalInsideBouncerCost += b.DailyWage;
                }
            }
            labelInsideBouncerCount.Text = listBoxAddedInsideBouncers.Items.Count.ToString();
            labelInsideBouncerCost.Text = totalInsideBouncerCost.ToString();
            updateExpenses();
        }

        private int GetInsideBouncerScore(int clubLevel, int numberOfBouncers, int maxOverall)
        {
            clubLevel = Clamp(clubLevel, 0, 5);
            numberOfBouncers = Clamp(numberOfBouncers, 0, 5);
            double requiredBouncers = 1 + (clubLevel);
            double proportion = Math.Min(numberOfBouncers / requiredBouncers, 1.0);
            return (int)Math.Round(75 * proportion) + (int)(maxOverall * 0.25);
        }

        private void updateExpenses()
        {
            int total = 0; 
            int doorBouncerCost = 0;
            foreach (String name in listBoxAddedDoorBouncers.Items)
            {
                Bouncer bouncer = bouncerList.FirstOrDefault(b => b.Name == name);
                doorBouncerCost += bouncer?.DailyWage ?? 0;
            }
            int.TryParse(labelInsideBouncerCost.Text, out int insideBouncerCost);
            int.TryParse(labelBartenderCost.Text, out int barmanCost);
            int.TryParse(labelWaiterCost.Text, out int waiterCost);
            int.TryParse(labelCleanersCost.Text, out int cleanerCost);
            int.TryParse(labelDjCost.Text, out int djCost);
            string stripperCostText = labelStripperCost.Text.Replace("Total Stripper Cost: ", "").Replace(" $", "");
            int.TryParse(stripperCostText, out int stripperCost);
            total = doorBouncerCost + insideBouncerCost + barmanCost + waiterCost + cleanerCost + djCost + stripperCost;
            labelTotalExpenses.Text = "$ " + total.ToString();
        }

        private void tabPage7_Paint(object sender, PaintEventArgs e)
        {
            //clubVisualizer.Draw(e.Graphics, velvetClub, tabPage7.ClientRectangle, clientQueue, clientsList);
        }
        private double GetNetRevenueFromXml(int clubLevel)
        {
            string xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "club_levels_revenue_model.xml");
            if (!File.Exists(xmlPath))
                return 0.0;

            try
            {
                var doc = System.Xml.Linq.XDocument.Load(xmlPath);
                // ClubLevel elements are 0-based in Skip, so clubLevel 1 means Skip(0)
                var clubLevelElem = doc.Descendants("ClubLevel")
                    .Skip(clubLevel)
                    .FirstOrDefault();

                if (clubLevelElem != null)
                {
                    var netRevenueElem = clubLevelElem.Element("NetRevenue");
                    if (netRevenueElem != null)
                    {
                        double netRevenue;
                        if (double.TryParse(netRevenueElem.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out netRevenue))
                            return netRevenue;
                    }
                }
            }
            catch
            {
                // Ignore errors, return 0
            }
            return 0.0;
        }

        private int calculateStripperReadiness()
        {
            int readiness = 0;
            int customers = 0;

            string xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "club_levels_revenue_model.xml");
            if (!File.Exists(xmlPath))
                return 0;

            try
            {
                var doc = System.Xml.Linq.XDocument.Load(xmlPath);
                var clubLevelElem = doc.Descendants("ClubLevel")
                    .Skip(velvetClub.Level)
                    .FirstOrDefault();

                if (clubLevelElem != null)
                {
                    var customersElem = clubLevelElem.Element("DailyCustomers");
                    if (customersElem != null)
                    {
                        if (!int.TryParse(customersElem.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out customers))
                            return 0;
                    }
                }
            }
            catch
            {
                return 0;
            }
            double average = 0;
            foreach (string name in strippersOnDuty.Items)
            {
                average += stripperList.FirstOrDefault(x => x.Name == name)?.OverallScore ?? 0;
            }
            average /= strippersOnDuty.Items.Count > 0 ? strippersOnDuty.Items.Count : 1;
            readiness = (int)average / 2 + (int)Math.Min(100, 100 * ((double)strippersOnDuty.Items.Count / customers) * 7) / 2;
            return readiness;
        }

        private void pictureBoxReport_Click(object sender, EventArgs e)
        {
            tabControl1.SelectedTab = tabPage8;
        }

        private void labelLowStripper_Click(object sender, EventArgs e)
        {

        }

        private void StripperPoolPercentage_Click(object sender, EventArgs e)
        {

        }
    }
}
