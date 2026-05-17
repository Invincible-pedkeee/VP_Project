using Common.Dispose;
using Common.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    public class CsvReader : DisposableBase
    {
        private StreamReader _reader;
        private readonly string _filePath;
        private readonly int _rowLimitN;

        public CsvReader(string filePath,int rowLimitN)
        {
            _filePath = filePath;
            _rowLimitN = rowLimitN;
            _reader = new StreamReader(filePath);
        }

        public List<PvSample> ReadSamples(string rejectedLogPath)
        {
            var samples = new List<PvSample>();
            var headers = new List<string>();
            string line;
            int rowIndex = 0;
            int dataRowCount = 0;

            using (var rejectedWriter = new StreamWriter(rejectedLogPath, append: true))
            {
                if ((line = _reader.ReadLine()) != null)
                {
                    var parts = line.Split(',');
                    for (int i = 0; i < parts.Length; i++)
                        headers.Add(parts[i].Trim());
                }

                while ((line = _reader.ReadLine()) != null && dataRowCount < _rowLimitN)
                {
                    rowIndex++;
                    var parts = line.Split(',');

                    try
                    {
                        int dayIdx = headers.IndexOf("DAY");
                        int hourIdx = headers.IndexOf("HOUR");
                        int acPwrtIdx = headers.IndexOf("ACPWRT");
                        int dcVoltIdx = headers.IndexOf("DCVOLT");
                        int temperIdx = headers.IndexOf("TEMPER");
                        int vl1to2Idx = headers.IndexOf("VL1TO2");
                        int vl2to3Idx = headers.IndexOf("VL2TO3");
                        int vl3to1Idx = headers.IndexOf("VL3TO1");
                        int acCur1Idx = headers.IndexOf("ACCUR1");
                        int acVlt1Idx = headers.IndexOf("ACVLT1");

                        if (!int.TryParse(parts[dayIdx].Trim(), out int day))
                        {
                            rejectedWriter.WriteLine($"{line},REASON: UNSUCCESFUL PARSE DAY");
                            continue;
                        }
                        string hourStr = parts[hourIdx].Trim();
                        if (!TimeSpan.TryParse(hourStr, out TimeSpan hourTime))
                        {
                            rejectedWriter.WriteLine($"{line},REASON: UNSUCCESFUL PARSE HOUR");
                            continue;
                        }


                        var sample = new PvSample
                        {
                            RowIndex = rowIndex,
                            Day = day,
                            Hour = hourStr,
                            AcPwrt = ParseNullable(parts[acPwrtIdx].Trim()),
                            DcVolt = ParseNullable(parts[dcVoltIdx].Trim()),
                            Temper = ParseNullable(parts[temperIdx].Trim()),
                            Vl1to2 = ParseNullable(parts[vl1to2Idx].Trim()),
                            Vl2to3 = ParseNullable(parts[vl2to3Idx].Trim()),
                            Vl3to1 = ParseNullable(parts[vl3to1Idx].Trim()),
                            AcCur1 = ParseNullable(parts[acCur1Idx].Trim()),
                            AcVlt1 = ParseNullable(parts[acVlt1Idx].Trim())
                        };
                        if (parts[acPwrtIdx].Trim() == "32767")
                            rejectedWriter.WriteLine($"{line},REASON: SENTINEL (32767) on critical field ACPWRT (row {rowIndex})");
                        if (parts[dcVoltIdx].Trim() == "32767")
                            rejectedWriter.WriteLine($"{line},REASON: SENTINEL (32767) on critical field DCVOLT (row {rowIndex})");
                        if (parts[temperIdx].Trim() == "32767")
                            rejectedWriter.WriteLine($"{line},REASON: SENTINEL (32767) on critical field TEMPER (row {rowIndex})");


                        samples.Add(sample);
                        dataRowCount++;
                    }
                    catch (Exception ex)
                    {
                        rejectedWriter.WriteLine($"{line},REASON: {ex.Message}");
                    }

                }

            }
            return samples;
        }

        private double? ParseNullable(string value)
        {
            if(double.TryParse(value,NumberStyles.Any,CultureInfo.InvariantCulture,out double result))
            {
                if (result == 32767.0) return null;

                return result;
            }
            return null;
        }

        protected override void DisposeManaged()
        {
            _reader?.Dispose();
            _reader = null;
        }
    }
}
