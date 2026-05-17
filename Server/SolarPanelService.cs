using Common;
using Common.Models;
using Server.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class SolarPanelService : ISolarPanelService, IDisposable
    {
        private DataStorage _storage;
        private AnalyticsEngine _analytics;
        private EventPublisher _publisher;
        public EventPublisher Publisher => _publisher;

        private bool _sessionActive = false;
        private int _receivedRows = 0;
        private int _rowLimitN = 0;
        private int _lastRowIndex = -1;

        public SolarPanelService()
        {
            _publisher = new EventPublisher();
            _analytics = new AnalyticsEngine(_publisher);
        }

        public void StartSession(PvMeta meta)
        {
            try
            {
                if (_sessionActive)
                    throw new FaultException<SolarFaultException>(
                        new SolarFaultException("Session is active."));

                _rowLimitN = meta.RowLimitN;
                _receivedRows = 0;
                _lastRowIndex = -1;

                _storage = new DataStorage(meta);
                _sessionActive = true;

                _publisher.RaiseTransferStarted(meta.FileName);
                Console.WriteLine($"[SERVER] Session begin: {meta.FileName}, limit: {meta.RowLimitN} rows");
            }
            catch (FaultException<SolarFaultException>)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new FaultException<SolarFaultException>(
                    new SolarFaultException($"ERROR StartSession: {ex.Message}"));
            }
        }

        public void PushSample(PvSample sample)
        {
            try
            {
                if (!_sessionActive)
                    throw new FaultException<SolarFaultException>(
                        new SolarFaultException("Not single session active."));

                if (sample.RowIndex <= _lastRowIndex)
                    throw new FaultException<SolarFaultException>(
                        new SolarFaultException($"RowIndex is not monotonous: {sample.RowIndex}"));

                _lastRowIndex = sample.RowIndex;

                if (sample.AcPwrt.HasValue && sample.AcPwrt < 0)
                {
                    _storage.WriteReject(sample, "AcPwrt negative");
                    return;
                }

                if (sample.DcVolt.HasValue && sample.DcVolt <= 0)
                {
                    _storage.WriteReject(sample, "DcVolt not positive");
                    return;
                }

                if (sample.AcVlt1.HasValue && sample.AcVlt1 <= 0)
                {
                    _storage.WriteReject(sample, "AcVlt1 not positive");
                    return;
                }

                if (sample.Vl1to2.HasValue && sample.Vl1to2 <= 0)
                {
                    _storage.WriteReject(sample, "Vl1to2 not positive");
                    return;
                }

                if (sample.Vl2to3.HasValue && sample.Vl2to3 <= 0)
                {
                    _storage.WriteReject(sample, "Vl2to3 not positive");
                    return;
                }

                if (sample.Vl3to1.HasValue && sample.Vl3to1 <= 0)
                {
                    _storage.WriteReject(sample, "Vl3to1 not positive");
                    return;
                }

                _storage.WriteSample(sample);
                _receivedRows++;

                _analytics.Analyze(sample);

                _publisher.RaiseSampleReceived(sample.RowIndex, _receivedRows, _rowLimitN);

                double pct = _rowLimitN > 0 ? (double)_receivedRows / _rowLimitN * 100 : 0;
                Console.WriteLine($"[SERVER] Transfer in progress .... row {_receivedRows}/{_rowLimitN} ({pct:F1}%)");
            }
            catch (FaultException<SolarFaultException>)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new FaultException<SolarFaultException>(
                    new SolarFaultException($"Error PushSample: {ex.Message}"));
            }
        }

        public void EndSession()
        {
            try
            {
                if (!_sessionActive)
                    throw new FaultException<SolarFaultException>(
                        new SolarFaultException("Session is not active"));

                _storage?.Dispose();
                _sessionActive = false;

                _publisher.RaiseTransferCompleted(_receivedRows);
                Console.WriteLine($"[SERVER] Transfer finished. Rows collected: {_receivedRows}");
            }
            catch (FaultException<SolarFaultException>)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new FaultException<SolarFaultException>(
                    new SolarFaultException($"Error EndSession: {ex.Message}"));
            }
        }

        public void Dispose()
        {
            _storage?.Dispose();
            _storage = null;
        }
    }
}