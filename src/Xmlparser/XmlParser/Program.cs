using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace XmlParser
{
    class Program
    {
        static void Main(string[] args)
        {
            Fix_Global(args);

            Merge_Others(args);
        }

        static void Fix_Global(string[] args)
        {
            args = new[] { @"..\..\..\..\..\data\SN185085\SN185085_DATA.XML" };
            var srcFile = args.First();

            var directory = Path.GetDirectoryName(srcFile);
            var fileName = Path.GetFileNameWithoutExtension(srcFile);
            var destFile = Path.Combine(directory, fileName + "_fixed.xml");

            if (!File.Exists(srcFile))
                throw new Exception("file not found");

            var doc = XDocument.Load(File.OpenRead(srcFile));
            var dates = doc.Descendants(XName.Get("Date"));

            foreach (var date in dates)
            {
                var desc = date.DescendantNodes().SingleOrDefault();

                var textNode = desc as XText;
                var day = textNode.Value.Substring(0, 2).ToInt();
                var month = textNode.Value.Substring(2, 2).ToInt();
                var year = textNode.Value.Substring(4, 2).ToInt();

                var dateTime = new DateTime(year + 2000, month, day);

                textNode.Value = dateTime.ToString("d");
            }

            var samples = from day in doc.Descendants(XName.Get("Day"))
                          select new Sample
                          {
                              Date = day.Descendants(XName.Get("Date")).Select(d => DateTime.Parse(d.Value)).Single(),
                              Wh = day.Descendants(XName.Get("Energy")).Select(e => e.Value.ToInt()).Single()
                          };
            samples = samples.GroupBy(g => g.Date)
                .Select(s => new Sample
                {
                    Date = s.Key,
                    Wh = s.Sum(r => r.Wh)
                })
                .ToList();

            var kwhTotali = samples.Sum(s => s.Wh);

            if (File.Exists(destFile))
                File.Delete(destFile);



            using (var stream = File.OpenWrite(destFile))
                doc.Save(stream);
        }


        static void Merge_Others(string[] args)
        {
            args = new[] { @"..\..\..\..\..\data\SN185085\" };
            var srcFolder = args.First();

            // SN185085_2013_05_01.XML
            var dataFiles = Directory.EnumerateFiles(srcFolder, "*.xml")
                .Where(f => Path.GetFileNameWithoutExtension(f).Length == 19 && !f.Contains("DATA"))
                .Select(s =>
                {
                    var name = Path.GetFileNameWithoutExtension(s);
                    var date = DateTime.Parse(name.Substring(9).Replace('_', '-'));

                    return new DataFile
                    {
                        Path = s,
                        Name = name,
                        Date = date,
                    };
                })
                .ToList();

            //var result = Merge(dataFiles).ToList();
            //var total = result.Sum(s => s.Wh);

            var result2 = Deltas(dataFiles).ToList();

            var total2 = result2.Sum(s => s.Wh);
        }

        //static IEnumerable<Sample> Merge(IEnumerable<DataFile> files)
        //{
        //    foreach (var file in files)
        //    {
        //        var samples = Test_file(file.Path, file.Date);
        //        foreach (var sample in samples)
        //            yield return sample;
        //    }
        //}

        static IEnumerable<Delta> Deltas(IEnumerable<DataFile> files)
        {
            InstantPower last = null;
            foreach (var file in files)
            {
                var samples = Test_file(file.Path, file.Date);
                foreach (var sample in samples)
                {
                    if (sample.Date < new DateTime(2013, 5, 1))
                        continue;
                    if (last != null)
                    {
                        var dT = sample.Date - last.Date;
                        yield return new Delta { From = last.Date, To = sample.Date, Wh = (sample.Watts * (int)dT.TotalSeconds) / 3600 };
                    }
                    //else
                    //    yield return new Delta { From = DateTime.MinValue, To = sample.Date, Wh = sample.Wh };
                    last = sample;
                }
            }
        }

        static IEnumerable<InstantPower> Test_file(string filePath, DateTime baseDate)
        {
            var doc = XDocument.Load(File.OpenRead(filePath));

            var samples = doc.Descendants(XName.Get("Day")).SelectMany(s => s.Descendants(XName.Get("Sample")));

            foreach (var r in samples)
            {
                var seconds = r.Attribute(XName.Get("Secs")).Value.ToInt();
                var date = baseDate + TimeSpan.FromSeconds(seconds);
                var w = r.Descendants(XName.Get("Pout")).Select(e => e.Value.ToInt()).Single();

                yield return new InstantPower { Date = date, Watts = w };
            }

            /*
             <Day>
                <Sample Secs="20204">
                <Pout>3</Pout>
                </Sample>
             </Day>
             */
        }
    }

    public static class Help
    {
        public static int ToInt(this string str)
        {
            return int.Parse(str);
        }
    }

    public class Sample
    {
        public DateTime Date;
        public int Wh;

        public override string ToString()
        {
            return string.Format("{0} : {1} Kwh", Date, Wh);
        }
    }

    public class InstantPower
    {
        public DateTime Date;
        public int Watts;

        public override string ToString()
        {
            return string.Format("{0} : {1} W", Date, Watts);
        }
    }

    public class Delta
    {
        public DateTime From;
        public DateTime To;
        public int Wh;

        public override string ToString()
        {
            var elapsed = To - From;
            return string.Format("At {0}, For {1} s : {2} Wh", From, elapsed.TotalSeconds, Wh);
        }
    }

    public class DataFile
    {
        public string Path;
        public string Name;
        public DateTime Date;

        public override string ToString()
        {
            return string.Format("{0} : {1} Kwh", Date, Name);
        }
    }
}
