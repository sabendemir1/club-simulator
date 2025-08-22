using System.Collections.Generic;
using System.Xml.Linq;
using System.IO;

namespace StripSim_Door_Alternative1
{
    internal class Stripper
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public int DailyWage { get; set; }
        public int PerformanceSkill { get; set; }
        public int Attractiveness { get; set; }
        public int Charisma { get; set; }
        public int OverallScore { get; set; }

        public int TipPoolPercentage { get; set; } = 0;
        public bool onDuty { get; set; } = false;

        public static List<Stripper> LoadFromXml(string xmlPath)
        {
            var strippers = new List<Stripper>();
            if (!File.Exists(xmlPath))
                return strippers;

            var doc = XDocument.Load(xmlPath);
            foreach (var elem in doc.Descendants("Dancer"))
            {
                strippers.Add(new Stripper
                {
                    ID = (int?)elem.Element("ID") ?? 0,
                    Name = (string)elem.Element("Name"),
                    DailyWage = (int?)elem.Element("DailyWage") ?? 0,
                    PerformanceSkill = (int?)elem.Element("PerformanceSkill") ?? 0,
                    Attractiveness = (int?)elem.Element("Attractiveness") ?? 0,
                    Charisma = (int?)elem.Element("Charisma") ?? 0,
                    OverallScore = (int?)elem.Element("OverallScore") ?? 0,
                    TipPoolPercentage = (int?)elem.Element("TipPoolPercentage") ?? 0,
                });
            }
            return strippers;
        }
    }
}