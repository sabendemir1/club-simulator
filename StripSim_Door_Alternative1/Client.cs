using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace StripSim_Door_Alternative1
{
    public class Client
    {
        public DateTime entryTime;
        public DateTime exitTime;
        public static int ClientCount { get; private set; } = 0;
        public int Id { get; set; }
        public enum GenderType
        {
            Male,
            Female
        }

        public enum RiskProfileTypeEnum
        {
            None,
            Drunk,
            Underage,
            Mob,
            UndercoverCop,
            High
        }

        public GenderType Gender { get; set; }

        public RiskProfileTypeEnum RiskProfileType { get; set; }

        public int ImageId { get; set; }

        public int RiskProfile { get; set; }

        private static readonly Random _random = new Random();

        public Client(DateTime entry)
        {
            entryTime = entry;
            Id = ++ClientCount;
            // Gender: 90% Male, 10% Female
            Gender = _random.NextDouble() < 0.9 ? GenderType.Male : GenderType.Female;

            // RiskProfile: 0-100 inclusive
            RiskProfile = _random.Next(0, 101);
            List<int> possibleImages;
            // RiskProfileType: None if below 70, else random (excluding None)
            if (RiskProfile < 70)
            {
                RiskProfileType = RiskProfileTypeEnum.None;
                possibleImages = new List<int> { 3, 5, 8, 10, 11, 12, 13, 15, 17, 21 };
            }
            else
            {
                // Get all enum values except None (start from 1)
                Array values = Enum.GetValues(typeof(RiskProfileTypeEnum));
                int randomIndex = _random.Next(1, values.Length);
                RiskProfileType = (RiskProfileTypeEnum)values.GetValue(randomIndex);
                if (RiskProfileType == RiskProfileTypeEnum.Drunk)
                {
                    possibleImages = new List<int> { 1, 2, 4, 14, 16, 18 };
                }
                else if (RiskProfileType == RiskProfileTypeEnum.Underage)
                {
                    possibleImages = new List<int> { 7 };
                }
                else if (RiskProfileType == RiskProfileTypeEnum.Mob)
                {
                    possibleImages = new List<int> { 19, 20 };
                }
                else if (RiskProfileType == RiskProfileTypeEnum.UndercoverCop)
                {
                    possibleImages = new List<int> { 6, 9 };
                }
                else // High
                {
                    possibleImages = new List<int> { 2, 16 };
                }
            }
            ImageId = possibleImages[_random.Next(possibleImages.Count)];
        }

        public void SetExitTime(DateTime now, int level)
        {
            // Mean stay: 1.5 hours (90 minutes)
            double meanMinutes = 90.0;
            string xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "club_levels_updated_entrance_fees.xml");
            if (!File.Exists(xmlPath))
                meanMinutes = 90.0;

            try
            {
                var doc = System.Xml.Linq.XDocument.Load(xmlPath);
                var clubLevelElem = doc.Descendants("ClubLevel")
                    .Skip(level)
                    .FirstOrDefault();

                if (clubLevelElem != null)
                {
                    var customersElem = clubLevelElem.Element("AvgTimeMinutes");
                    if (customersElem != null)
                    {
                        if (!double.TryParse(customersElem.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out meanMinutes))
                            meanMinutes = 90.0; ;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading XML data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                meanMinutes = 90.0;
            }
            // Standard deviation: 20 minutes (tunable)
            double sigmaMinutes = 20.0;

            // Box-Muller transform for Gaussian
            Random rand = new Random(Guid.NewGuid().GetHashCode());
            double u1 = 1.0 - rand.NextDouble();
            double u2 = 1.0 - rand.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            double stayMinutes = meanMinutes + sigmaMinutes * randStdNormal;

            // Clamp stay to minimum 30 min, max 180 min
            stayMinutes = Math.Max(30, Math.Min(180, stayMinutes));

            DateTime proposedExit = now.AddMinutes(stayMinutes);
            DateTime lastAllowedExit = DateTime.Today.AddDays(1).AddHours(4); // 04:00 next day

            if (proposedExit > lastAllowedExit)
                proposedExit = lastAllowedExit;

            exitTime = proposedExit;
        }
    }
}
           