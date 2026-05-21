using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsvHelper;
using UnityEngine;
using UnityEngine.Serialization;

namespace BagReplay
{
    public class BagDataWriter : MonoBehaviour
    {
        [HideInInspector] public string sourceFilePath;
        public Transform helperTransform;
        public List<FloatRange> ranges;

        public void WriteFile(string path)
        {
            BagReader bagReader = new BagReader(sourceFilePath);

            path = PreparePath(path);
            foreach (var range in ranges)
            {
                var rowTime = 0.0f;
                using (var obsStreamWriter = new StreamWriter(path.Replace("_X", "_" + range.start + "-" + range.end)))
                using (var obsWriter = new CsvWriter(obsStreamWriter, CultureInfo.InvariantCulture))
                {
                    obsWriter.Context.RegisterClassMap<BagCsvRowMap>();
                    obsWriter.WriteHeader<BagCsvRow>();
                    obsWriter.NextRecord();

                    var startNanos = bagReader.StartNanos;
                    var currentTime = range.start * 1000000000 + startNanos;
                    var bagRow = bagReader.ReadFields(currentTime);

                    var rangeEnd = startNanos + range.end * 1000000000;
                    while (bagRow != null && currentTime < rangeEnd)
                    {
                        obsWriter.WriteRecord(bagRow.ToCsv(helperTransform, rowTime));
                        obsWriter.NextRecord();
                        
                        rowTime += Time.fixedDeltaTime;
                        currentTime += (double)Time.fixedDeltaTime * 1000000000;
                        bagRow = bagReader.ReadFields(currentTime);
                    }

              
                }
            }
        }

        private string PreparePath(string path)
        {
            if (path.Contains(".csv")) return path.Replace(".csv", "_X.csv");
            return path + "_X.csv";
        }
    }
}