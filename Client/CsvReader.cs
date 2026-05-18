using Common.Dispose;
using Common.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

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

        public IEnumerable<PvSample> ReadSamplesStreaming(string rejectedLogPath)
        {
            var headers = new List<string>();
            string line;
            int rowIndex = 0;

            using (var rejectedWriter = new StreamWriter(rejectedLogPath, append: true))
            {
                if ((line = _reader.ReadLine()) != null)
                {
                    var parts = line.Split(',');
                    foreach (var p in parts)
                        headers.Add(p.Trim());
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

                ValidateColumn("DAY", dayIdx);
                ValidateColumn("HOUR", hourIdx);
                ValidateColumn("ACPWRT", acPwrtIdx);
                ValidateColumn("DCVOLT", dcVoltIdx);
                ValidateColumn("TEMPER", temperIdx);
                ValidateColumn("VL1TO2", vl1to2Idx);
                ValidateColumn("VL2TO3", vl2to3Idx);
                ValidateColumn("VL3TO1", vl3to1Idx);
                ValidateColumn("ACCUR1", acCur1Idx);
                ValidateColumn("ACVLT1", acVlt1Idx);

                while ((line = _reader.ReadLine()) != null && rowIndex < _rowLimitN)
                {
                    rowIndex++;
                    var parts = line.Split(',');
                    PvSample sample = null;

                    try
                    {
                        if (!int.TryParse(parts[dayIdx].Trim(), out int day))
                        {
                            rejectedWriter.WriteLine($"{line},REASON: UNSUCCESSFUL PARSE DAY");
                            continue;
                        }

                        string hourStr = parts[hourIdx].Trim();
                        if (!TimeSpan.TryParse(hourStr, out _))
                        {
                            rejectedWriter.WriteLine($"{line},REASON: UNSUCCESSFUL PARSE HOUR");
                            continue;
                        }

                        bool hasCriticalSentinel =
                            IsSentinel(parts[acPwrtIdx].Trim()) ||
                            IsSentinel(parts[dcVoltIdx].Trim()) ||
                            IsSentinel(parts[temperIdx].Trim());

                        if (hasCriticalSentinel)
                        {
                            rejectedWriter.WriteLine($"{line},REASON: SENTINEL 32767.0 on critical field (row {rowIndex})");
                            continue;
                        }

                        double? acPwrt = ParseNullable(parts[acPwrtIdx].Trim());
                        double? dcVolt = ParseNullable(parts[dcVoltIdx].Trim());
                        double? temper = ParseNullable(parts[temperIdx].Trim());

                        if (!acPwrt.HasValue)
                        {
                            rejectedWriter.WriteLine($"{line},REASON: MISSING/INVALID value on critical field ACPWRT (row {rowIndex})");
                            continue;
                        }

                        if (!dcVolt.HasValue)
                        {
                            rejectedWriter.WriteLine($"{line},REASON: MISSING/INVALID value on critical field DCVOLT (row {rowIndex})");
                            continue;
                        }

                        if (!temper.HasValue)
                        {
                            rejectedWriter.WriteLine($"{line},REASON: MISSING/INVALID value on critical field TEMPER (row {rowIndex})");
                            continue;
                        }

                        sample = new PvSample
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
                            AcVlt1 = ParseNullable(parts[acVlt1Idx].Trim()),
                            RawLine = line
                        };
                    }
                    catch (Exception ex)
                    {
                        rejectedWriter.WriteLine($"{line},REASON: {ex.Message}");
                        continue;
                    }

                    if (sample != null)
                        yield return sample;
                }
            }
        }

        private void ValidateColumn(string columnName, int index)
        {
            if (index < 0)
                throw new InvalidOperationException($"Missing required column: {columnName}");
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