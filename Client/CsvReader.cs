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

        public CsvReader(string filePath, int rowLimitN)
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

            using (var rejectedWriter = new StreamWriter(rejectedLogPath, append: true))
            {
                if ((line = _reader.ReadLine()) != null)
                {
                    var parts = line.Split(',');
                    for (int i = 0; i < parts.Length; i++)
                        headers.Add(parts[i].Trim());
                }

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

                // Broji SVE pročitane data redove, ne samo validne
                while ((line = _reader.ReadLine()) != null && rowIndex < _rowLimitN)
                {
                    rowIndex++;
                    var parts = line.Split(',');

                    try
                    {
                        if (!int.TryParse(parts[dayIdx].Trim(), out int day))
                        {
                            rejectedWriter.WriteLine($"{line},REASON: UNSUCCESSFUL PARSE DAY");
                            continue;
                        }

                        string hourStr = parts[hourIdx].Trim();
                        if (!TimeSpan.TryParse(hourStr, out TimeSpan hourTime))
                        {
                            rejectedWriter.WriteLine($"{line},REASON: UNSUCCESSFUL PARSE HOUR");
                            continue;
                        }

                        // Parsiraj kritična polja
                        double? acPwrt = ParseNullable(parts[acPwrtIdx].Trim());
                        double? dcVolt = ParseNullable(parts[dcVoltIdx].Trim());
                        double? temper = ParseNullable(parts[temperIdx].Trim());

                        // Log sentinel po parsiranoj vrijednosti (hvata i "32767" i "32767.0")
                        if (IsSentinel(parts[acPwrtIdx].Trim()))
                            rejectedWriter.WriteLine($"{line},REASON: SENTINEL (32767.0) on critical field ACPWRT (row {rowIndex})");
                        if (IsSentinel(parts[dcVoltIdx].Trim()))
                            rejectedWriter.WriteLine($"{line},REASON: SENTINEL (32767.0) on critical field DCVOLT (row {rowIndex})");
                        if (IsSentinel(parts[temperIdx].Trim()))
                            rejectedWriter.WriteLine($"{line},REASON: SENTINEL (32767.0) on critical field TEMPER (row {rowIndex})");

                        // Validacija kritičnih numeričkih polja — ako nije parsibilno i nije sentinel, reject
                        if (!acPwrt.HasValue && !IsSentinel(parts[acPwrtIdx].Trim()) && !string.IsNullOrWhiteSpace(parts[acPwrtIdx].Trim()))
                        {
                            rejectedWriter.WriteLine($"{line},REASON: INVALID value on critical field ACPWRT (row {rowIndex})");
                            continue;
                        }
                        if (!dcVolt.HasValue && !IsSentinel(parts[dcVoltIdx].Trim()) && !string.IsNullOrWhiteSpace(parts[dcVoltIdx].Trim()))
                        {
                            rejectedWriter.WriteLine($"{line},REASON: INVALID value on critical field DCVOLT (row {rowIndex})");
                            continue;
                        }
                        if (!temper.HasValue && !IsSentinel(parts[temperIdx].Trim()) && !string.IsNullOrWhiteSpace(parts[temperIdx].Trim()))
                        {
                            rejectedWriter.WriteLine($"{line},REASON: INVALID value on critical field TEMPER (row {rowIndex})");
                            continue;
                        }

                        var sample = new PvSample
                        {
                            RowIndex = rowIndex,
                            Day = day,
                            Hour = hourStr,
                            AcPwrt = acPwrt,
                            DcVolt = dcVolt,
                            Temper = temper,
                            Vl1to2 = ParseNullable(parts[vl1to2Idx].Trim()),
                            Vl2to3 = ParseNullable(parts[vl2to3Idx].Trim()),
                            Vl3to1 = ParseNullable(parts[vl3to1Idx].Trim()),
                            AcCur1 = ParseNullable(parts[acCur1Idx].Trim()),
                            AcVlt1 = ParseNullable(parts[acVlt1Idx].Trim())
                        };

                        samples.Add(sample);
                    }
                    catch (Exception ex)
                    {
                        rejectedWriter.WriteLine($"{line},REASON: {ex.Message}");
                    }
                }
            }
            return samples;
        }

        private bool IsSentinel(string value)
        {
            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
                return result == 32767.0;
            return false;
        }

        private double? ParseNullable(string value)
        {
            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
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