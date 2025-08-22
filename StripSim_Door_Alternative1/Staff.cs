using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace StripSim_Door_Alternative1
{
    public abstract class Staff
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Overall { get; set; }
        public int DailyWage { get; set; }
    }

    public class Barman : Staff { }

    public class Cleaner : Staff { }

    public class Waiter : Staff { }

    public class DJ : Staff { }

    public static class StaffLoader
    {
        public static List<Barman> LoadBarmans(string xmlPath)
        {
            var list = new List<Barman>();
            if (!File.Exists(xmlPath)) return list;
            var doc = XDocument.Load(xmlPath);
            foreach (var x in doc.Descendants("Barman"))
            {
                list.Add(new Barman
                {
                    Id = (int)x.Attribute("id"),
                    Name = (string)x.Element("name"),
                    Overall = (int)x.Element("overall"),
                    DailyWage = (int)x.Element("daily_wage")
                });
            }
            return list;
        }

        public static List<Cleaner> LoadCleaners(string xmlPath)
        {
            var list = new List<Cleaner>();
            if (!File.Exists(xmlPath)) return list;
            var doc = XDocument.Load(xmlPath);
            foreach (var x in doc.Descendants("Cleaner"))
            {
                list.Add(new Cleaner
                {
                    Id = (int)x.Attribute("id"),
                    Name = (string)x.Element("name"),
                    Overall = (int)x.Element("overall"),
                    DailyWage = (int)x.Element("daily_wage")
                });
            }
            return list;
        }

        public static List<Waiter> LoadWaiters(string xmlPath)
        {
            var list = new List<Waiter>();
            if (!File.Exists(xmlPath)) return list;
            var doc = XDocument.Load(xmlPath);
            foreach (var x in doc.Descendants("Server"))
            {
                list.Add(new Waiter
                {
                    Id = (int)x.Attribute("id"),
                    Name = (string)x.Element("name"),
                    Overall = (int)x.Element("overall"),
                    DailyWage = (int)x.Element("daily_wage")
                });
            }
            return list;
        }

        public static List<DJ> LoadDJs(string xmlPath)
        {
            var list = new List<DJ>();
            if (!File.Exists(xmlPath)) return list;
            var doc = XDocument.Load(xmlPath);
            foreach (var x in doc.Descendants("DJ"))
            {
                list.Add(new DJ
                {
                    Id = (int)x.Attribute("id"),
                    Name = (string)x.Element("name"),
                    Overall = (int)x.Element("fame"),
                    DailyWage = (int)x.Element("daily_wage")
                });
            }
            return list;
        }
    }
}