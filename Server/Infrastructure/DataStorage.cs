using Common.Dispose;
using Common.Models;
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Server.Infrastructure
{
    public class DataStorage : DisposableBase
    {
        private StreamWriter _sessionWriter;
        private StreamWriter _rejectWriter;
        private readonly string _sessionDirectory;
        private readonly string _sessionPath;
        private readonly string _rejectPath;

        public DataStorage(PvMeta meta)
        {
            string date = DateTime.Now.ToString("yyyy-MM-dd");
            _sessionDirectory = Path.Combine("Data", meta.PlantId, date);

            Directory.CreateDirectory(_sessionDirectory);

            _sessionPath = Path.Combine(_sessionDirectory, "session.csv");
            _rejectPath = Path.Combine(_sessionDirectory, "rejects.csv");

            bool sessionNew = !File.Exists(_sessionPath) || new FileInfo(_sessionPath).Length == 0;
            bool rejectNew = !File.Exists(_rejectPath) || new FileInfo(_rejectPath).Length == 0;

            _sessionWriter = new StreamWriter(_sessionPath, append: true, encoding: Encoding.UTF8);
            _rejectWriter = new StreamWriter(_rejectPath, append: true, encoding: Encoding.UTF8);

            if (sessionNew)
                _sessionWriter.WriteLine("RowIndex,Day,Hour,AcPwrt,DcVolt,Temper,Vl1to2,Vl2to3,Vl3to1,AcCur1,AcVlt1");

            if (rejectNew)
                _rejectWriter.WriteLine("RowIndex,Reason,RawLine");
        }

        private string F(double? value)
        {
            return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "";
        }

        public void WriteSample(PvSample sample)
        {
            _sessionWriter.WriteLine(
                $"{sample.RowIndex},{sample.Day},{sample.Hour}," +
                $"{F(sample.AcPwrt)},{F(sample.DcVolt)},{F(sample.Temper)}," +
                $"{F(sample.Vl1to2)},{F(sample.Vl2to3)},{F(sample.Vl3to1)}," +
                $"{F(sample.AcCur1)},{F(sample.AcVlt1)}");
            _sessionWriter.Flush();
        }

        public void WriteReject(PvSample sample, string reason)
        {
            string rawLine = sample.RawLine ?? "";
            _rejectWriter.WriteLine($"{sample.RowIndex},\"{reason}\",\"{rawLine}\"");
            _rejectWriter.Flush();
        }

        public void WriteTransferLog(string message)
        {
            string logPath = Path.Combine(_sessionDirectory, "transfer.log");
            File.AppendAllText(logPath,
                $"{DateTime.Now:u} {message}{Environment.NewLine}");
        }

        protected override void DisposeManaged()
        {
            _sessionWriter?.Flush();
            _sessionWriter?.Close();
            _sessionWriter?.Dispose();
            _sessionWriter = null;

            _rejectWriter?.Flush();
            _rejectWriter?.Close();
            _rejectWriter?.Dispose();
            _rejectWriter = null;
        }
    }
}