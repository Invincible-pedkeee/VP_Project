using Common.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Infrastructure
{
    public class AnalyticsEngine
    {
        private readonly EventPublisher _publisher;

        private readonly double _overTempThreshold;
        private readonly double _voltageImbalancePct;
        private readonly int _powerFlatlineWindow;
        private readonly double _powerSpikeThreshold;
        private readonly double _flatlineEpsilon;

        private int _flatlineCount = 0;
        private double? _lastAcPwrt = null;

        public AnalyticsEngine(EventPublisher publisher)
        {
            _publisher = publisher;
            _overTempThreshold = double.Parse(ConfigurationManager.AppSettings["OverTempThreshold"], CultureInfo.InvariantCulture);
            _voltageImbalancePct = double.Parse(ConfigurationManager.AppSettings["VoltageImbalancePct"], CultureInfo.InvariantCulture);
            _powerFlatlineWindow = int.Parse(ConfigurationManager.AppSettings["PowerFlatlineWindow"], CultureInfo.InvariantCulture);
            _powerSpikeThreshold = double.Parse(ConfigurationManager.AppSettings["PowerSpikeThreshold"], CultureInfo.InvariantCulture);
            _flatlineEpsilon = double.Parse(ConfigurationManager.AppSettings["FlatlineEpsilon"], CultureInfo.InvariantCulture);
        }

        public void Analyze(PvSample sample)
        {
            CheckTemperature(sample);
            CheckVoltageImbalance(sample);
            CheckPowerFlatline(sample);
            CheckPowerSpike(sample);

             if (sample.AcPwrt.HasValue)
                _lastAcPwrt = sample.AcPwrt.Value;
        }

        private void CheckTemperature(PvSample sample)
        {
            if (sample.Temper.HasValue && sample.Temper > _overTempThreshold)
            {
                _publisher.RaiseWarning("OverTempWarning",
                    $"Temperature {sample.Temper}°C crosses the threshold {_overTempThreshold}°C " +
                    $"(Row {sample.RowIndex})");
            }
        }

        private void CheckVoltageImbalance(PvSample sample)
        {
            if (!sample.Vl1to2.HasValue || !sample.Vl2to3.HasValue || !sample.Vl3to1.HasValue)
                return;

            double v1 = sample.Vl1to2.Value;
            double v2 = sample.Vl2to3.Value;
            double v3 = sample.Vl3to1.Value;

            double max = Math.Max(v1, Math.Max(v2, v3));
            double min = Math.Min(v1, Math.Min(v2, v3));
            double avg = (v1 + v2 + v3) / 3.0;
            double range = max - min;

            if (avg > 0 && range > _voltageImbalancePct * avg)
            {
                _publisher.RaiseWarning("VoltageImbalanceWarning",
                    $"Voltage disbalance {range:F2}V crossing over {_voltageImbalancePct * 100}% " +
                    $"average {avg:F2}V (row {sample.RowIndex})");
            }
        }

        private void CheckPowerFlatline(PvSample sample)
        {
            if (!sample.AcPwrt.HasValue)
            {
                _flatlineCount = 0;
                _lastAcPwrt = null;
                return;
            }

            if (_lastAcPwrt.HasValue)
            {
                double delta = Math.Abs(sample.AcPwrt.Value - _lastAcPwrt.Value);
                if (delta < _flatlineEpsilon)
                {
                    _flatlineCount++;
                    if (_flatlineCount == _powerFlatlineWindow)
                    {
                        _publisher.RaiseWarning("PowerFlatlineWarning",
                            $"ACPWRT is not changed for {_flatlineCount} in a row " +
                            $"(row {sample.RowIndex})");
                    }
                }
                else
                {
                    _flatlineCount = 0;
                }
            }
        }

        private void CheckPowerSpike(PvSample sample)
        {
            if (!sample.AcPwrt.HasValue || !_lastAcPwrt.HasValue)
                return;

            double delta = Math.Abs(sample.AcPwrt.Value - _lastAcPwrt.Value);
            if (delta > _powerSpikeThreshold)
            {
                _publisher.RaiseWarning("PowerSpikeWarning",
                    $"ACPWRT jump of {delta:F2}W exceeds the threshold {_powerSpikeThreshold}W " +
                    $"(row {sample.RowIndex})");
            }
        }
    }
}