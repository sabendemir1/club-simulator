using System;
using System.IO;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Rebar;

namespace StripSim_Door_Alternative1
{
	internal class SocialEngagement
	{
        public int followers { get; set; }

        public int likes { get; set; }

        public int engagement { get; set; }

        public int change { get; set; }

        private Random rand = new Random();

        public string imagePath { get; set; }

        public SocialEngagement(int day, int currentFollowers, float dailySuccess)
        {
            likes = rand.Next(currentFollowers / 10, currentFollowers / 3);
            engagement = rand.Next(2 * likes, 5 * likes);
            followers = CalculateFollowers(day, currentFollowers, dailySuccess, out string eventLog);
            change = (100 * followers) / currentFollowers;
            int randomIndex = rand.Next(1, 21);
            imagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images/Strippers", randomIndex + ".jpeg");
        }

        public int CalculateFollowers(int day, int currentFollowers, float dailySuccess,
            out string eventLog, int targetFollowers = 100000)
        {
            float baseDailyAvg = targetFollowers / 100.0f;
            float scale = (dailySuccess - 0.5f) / 1.0f;
            int baseGrowth = (int)(baseDailyAvg * ((scale + 1) / 2.0f));
            int modifier = 0;
            eventLog = "";
            if (rand.NextDouble() < 0.1)
            {
                float lossPercent = (float)(rand.NextDouble() * 0.10 + 0.10);
                int loss = (int)(currentFollowers * lossPercent);
                modifier -= loss;
                eventLog = $"Scandal: -{loss} followers";
            }
            else if (rand.NextDouble() < 0.02)
            {
                float boostPercent = (float)(rand.NextDouble() * 0.05 + 0.20);
                int boost = (int)(currentFollowers * boostPercent);
                modifier += boost;
                eventLog = $"Celebrity Visit: +{boost} followers";
            }
            else
            {
                eventLog = $"Normal Growth: +{baseGrowth} followers";
            }
            int netChange = baseGrowth + modifier;
            int newTotal = Math.Max(currentFollowers + netChange, 0);
            return newTotal;
        }
    }
}