using Client;
using Common;
using Common.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Server;
using Server.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceModel;

namespace Tests
{
    [TestClass]
    public class DisposeTests
    {
        // -------------------------------------------------------
        // DISPOSE TESTOVI
        // -------------------------------------------------------

        [TestMethod]
        public void CsvReader_Dispose_ClosesStream()
        {
            string path = Path.Combine(Path.GetTempPath(), "test_csvreader.csv");
            File.WriteAllText(path, "DAY,HOUR,ACPWRT,DCVOLT,TEMPER,VL1TO2,VL2TO3,VL3TO1,ACCUR1,ACVLT1\n1,00:00,100,200,25,220,220,220,5,220\n");

            var reader = new CsvReader(path, 10);
            reader.Dispose();

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.IsTrue(fs.CanRead, "Fajl treba da bude dostupan nakon Dispose CsvReader-a.");
            }

            File.Delete(path);
        }

        [TestMethod]
        public void CsvReader_DoubleDispose_DoesNotThrow()
        {
            string path = Path.Combine(Path.GetTempPath(), "test_double_dispose.csv");
            File.WriteAllText(path, "DAY,HOUR,ACPWRT,DCVOLT,TEMPER,VL1TO2,VL2TO3,VL3TO1,ACCUR1,ACVLT1\n");

            var reader = new CsvReader(path, 10);
            reader.Dispose();

            try
            {
                reader.Dispose();
            }
            catch (Exception ex)
            {
                Assert.Fail($"Drugi Dispose bacio izuzetak: {ex.Message}");
            }

            File.Delete(path);
        }

        [TestMethod]
        public void DataStorage_Dispose_FlushesAndClosesSessionFile()
        {
            var meta = new PvMeta
            {
                PlantId = "TestPlant",
                FileName = "test.csv",
                RowLimitN = 10,
                TotalRows = 1,
                SchemaVersion = "1.0"
            };

            var storage = new DataStorage(meta);
            storage.WriteSample(new PvSample
            {
                RowIndex = 1,
                Day = 1,
                Hour = "00:00",
                AcPwrt = 100,
                DcVolt = 200,
                Temper = 25
            });
            storage.Dispose();

            string sessionPath = Path.Combine("Data", "TestPlant",
                DateTime.Now.ToString("yyyy-MM-dd"), "session.csv");

            Assert.IsTrue(File.Exists(sessionPath), "session.csv mora postojati.");
            Assert.IsTrue(new FileInfo(sessionPath).Length > 0, "session.csv ne sme biti prazan.");

            using (var fs = new FileStream(sessionPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.IsTrue(fs.CanRead);
            }
        }

        [TestMethod]
        public void DataStorage_Dispose_CalledOnException_ResourcesReleased()
        {
            var meta = new PvMeta
            {
                PlantId = "TestPlantEx",
                FileName = "ex.csv",
                RowLimitN = 10,
                TotalRows = 1,
                SchemaVersion = "1.0"
            };

            string sessionPath = Path.Combine("Data", "TestPlantEx",
                DateTime.Now.ToString("yyyy-MM-dd"), "session.csv");

            try
            {
                var storage = new DataStorage(meta);
                try
                {
                    storage.WriteSample(new PvSample { RowIndex = 1, Day = 1, Hour = "00:00" });
                    throw new Exception("Simuliran prekid prenosa");
                }
                finally
                {
                    storage.Dispose();
                }
            }
            catch (Exception ex) when (ex.Message == "Simuliran prekid prenosa") { }

            using (var fs = new FileStream(sessionPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.IsTrue(fs.CanRead, "Fajl mora biti dostupan čak i posle simuliranog prekida.");
            }
        }

        // -------------------------------------------------------
        // DISPOSE TESTOVI — WCF SESIJA
        // -------------------------------------------------------

        [TestMethod]
        public void SolarPanelService_Dispose_CleansUpActiveSession()
        {
            var service = new SolarPanelService();
            service.StartSession(new PvMeta
            {
                PlantId = "TestDispose",
                FileName = "dispose.csv",
                RowLimitN = 10,
                TotalRows = 10,
                SchemaVersion = "1.0"
            });

            // Simuliramo prekid — Dispose bez EndSession
            service.Dispose();

            // Nova sesija mora moći da se pokrene (sessionActive = false)
            try
            {
                service.StartSession(new PvMeta
                {
                    PlantId = "TestDispose2",
                    FileName = "dispose2.csv",
                    RowLimitN = 10,
                    TotalRows = 10,
                    SchemaVersion = "1.0"
                });
            }
            catch (Exception ex)
            {
                Assert.Fail($"Nova sesija nije mogla da se pokrene nakon Dispose: {ex.Message}");
            }
            finally
            {
                service.Dispose();
            }
        }
        [TestMethod]
        public void SolarPanelService_Dispose_WithoutEndSession_WritesTransferLog()
        {
            var service = new SolarPanelService();
            service.StartSession(new PvMeta
            {
                PlantId = "TestInterrupt",
                FileName = "interrupt.csv",
                RowLimitN = 10,
                TotalRows = 10,
                SchemaVersion = "1.0"
            });

            service.PushSample(new PvSample { RowIndex = 1, Day = 1, Hour = "00:00", AcPwrt = 100, DcVolt = 200, Temper = 25 });
            service.PushSample(new PvSample { RowIndex = 2, Day = 1, Hour = "01:00", AcPwrt = 110, DcVolt = 200, Temper = 26 });
            service.PushSample(new PvSample { RowIndex = 3, Day = 1, Hour = "02:00", AcPwrt = 120, DcVolt = 200, Temper = 27 });

            // Simulacija prekida — Dispose BEZ EndSession
            service.Dispose();

            string logPath = Path.Combine("Data", "TestInterrupt",
                DateTime.Now.ToString("yyyy-MM-dd"), "transfer.log");

            Assert.IsTrue(File.Exists(logPath),
                "transfer.log mora biti kreiran pri prekidu sesije.");

            string logContent = File.ReadAllText(logPath);
            Assert.IsTrue(logContent.Contains("INTERRUPTED"),
                "transfer.log mora sadržati poruku o prekinutom prenosu.");

            string sessionPath = Path.Combine("Data", "TestInterrupt",
                DateTime.Now.ToString("yyyy-MM-dd"), "session.csv");

            using (var fs = new FileStream(sessionPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.IsTrue(fs.CanRead,
                    "session.csv mora biti dostupan nakon prekida.");
            }
        }
        // -------------------------------------------------------
        // ANALYTICS — FLATLINE TESTOVI
        // -------------------------------------------------------

        [TestMethod]
        public void AnalyticsEngine_Flatline_NotRaisedBeforeK()
        {
            var warnings = new List<string>();
            var publisher = new EventPublisher();
            publisher.OnWarningRaised += (type, msg) => warnings.Add(type);
            var engine = new AnalyticsEngine(publisher);

            for (int i = 1; i <= 9; i++)
                engine.Analyze(new PvSample { RowIndex = i, AcPwrt = 100.0 });

            Assert.IsFalse(warnings.Contains("PowerFlatlineWarning"),
                "Flatline warning ne sme da se okine pre K uzastopnih redova.");
        }

        [TestMethod]
        public void AnalyticsEngine_Flatline_RaisedExactlyAtK()
        {
            var warnings = new List<string>();
            var publisher = new EventPublisher();
            publisher.OnWarningRaised += (type, msg) => warnings.Add(type);
            var engine = new AnalyticsEngine(publisher);

            for (int i = 1; i <= 10; i++)
                engine.Analyze(new PvSample { RowIndex = i, AcPwrt = 100.0 });

            Assert.IsTrue(warnings.Contains("PowerFlatlineWarning"),
                "Flatline warning mora da se okine tačno na K-tom uzastopnom redu.");
        }

        [TestMethod]
        public void AnalyticsEngine_Flatline_ResetsAfterChange()
        {
            var warnings = new List<string>();
            var publisher = new EventPublisher();
            publisher.OnWarningRaised += (type, msg) => warnings.Add(type);
            var engine = new AnalyticsEngine(publisher);

            for (int i = 1; i <= 5; i++)
                engine.Analyze(new PvSample { RowIndex = i, AcPwrt = 100.0 });

            engine.Analyze(new PvSample { RowIndex = 6, AcPwrt = 200.0 });

            for (int i = 7; i <= 11; i++)
                engine.Analyze(new PvSample { RowIndex = i, AcPwrt = 200.0 });

            Assert.IsFalse(warnings.Contains("PowerFlatlineWarning"),
                "Flatline counter mora da se resetuje nakon promene vrednosti.");
        }

        // -------------------------------------------------------
        // ANALYTICS — SPIKE TESTOVI
        // -------------------------------------------------------

        [TestMethod]
        public void AnalyticsEngine_Spike_RaisedOnlyOnRise()
        {
            var warnings = new List<string>();
            var publisher = new EventPublisher();
            publisher.OnWarningRaised += (type, msg) => warnings.Add(type);
            var engine = new AnalyticsEngine(publisher);

            // Porast za 600 > threshold 500 — mora da se okine
            engine.Analyze(new PvSample { RowIndex = 1, AcPwrt = 100.0 });
            engine.Analyze(new PvSample { RowIndex = 2, AcPwrt = 700.0 });

            Assert.IsTrue(warnings.Contains("PowerSpikeWarning"),
                "Spike warning mora da se okine na naglom porastu.");
        }

        [TestMethod]
        public void AnalyticsEngine_Spike_NotRaisedOnDrop()
        {
            var warnings = new List<string>();
            var publisher = new EventPublisher();
            publisher.OnWarningRaised += (type, msg) => warnings.Add(type);
            var engine = new AnalyticsEngine(publisher);

            // Pad za 600 — NE sme da se okine (samo porast)
            engine.Analyze(new PvSample { RowIndex = 1, AcPwrt = 700.0 });
            engine.Analyze(new PvSample { RowIndex = 2, AcPwrt = 100.0 });

            Assert.IsFalse(warnings.Contains("PowerSpikeWarning"),
                "Spike warning ne sme da se okine na padu snage.");
        }

        [TestMethod]
        public void AnalyticsEngine_Spike_NotRaisedWhenDeltaBelowThreshold()
        {
            var warnings = new List<string>();
            var publisher = new EventPublisher();
            publisher.OnWarningRaised += (type, msg) => warnings.Add(type);
            var engine = new AnalyticsEngine(publisher);

            // Porast za 100 < threshold 500 — ne sme da se okine
            engine.Analyze(new PvSample { RowIndex = 1, AcPwrt = 100.0 });
            engine.Analyze(new PvSample { RowIndex = 2, AcPwrt = 200.0 });

            Assert.IsFalse(warnings.Contains("PowerSpikeWarning"),
                "Spike warning ne sme da se okine kad delta ne premaši threshold.");
        }

        // -------------------------------------------------------
        // ANALYTICS — TEMPERATURE TESTOVI
        // -------------------------------------------------------

        [TestMethod]
        public void AnalyticsEngine_OverTemp_RaisedWhenExceedsThreshold()
        {
            var warnings = new List<string>();
            var publisher = new EventPublisher();
            publisher.OnWarningRaised += (type, msg) => warnings.Add(type);
            var engine = new AnalyticsEngine(publisher);

            engine.Analyze(new PvSample { RowIndex = 1, Temper = 61.0 });

            Assert.IsTrue(warnings.Contains("OverTempWarning"),
                "OverTemp warning mora da se okine kad temperatura premaši threshold.");
        }

        [TestMethod]
        public void AnalyticsEngine_OverTemp_NotRaisedWhenBelowThreshold()
        {
            var warnings = new List<string>();
            var publisher = new EventPublisher();
            publisher.OnWarningRaised += (type, msg) => warnings.Add(type);
            var engine = new AnalyticsEngine(publisher);

            engine.Analyze(new PvSample { RowIndex = 1, Temper = 59.0 });

            Assert.IsFalse(warnings.Contains("OverTempWarning"),
                "OverTemp warning ne sme da se okine kad temperatura ne premaši threshold.");
        }

        // -------------------------------------------------------
        // ANALYTICS — VOLTAGE IMBALANCE TESTOVI
        // -------------------------------------------------------

        [TestMethod]
        public void AnalyticsEngine_VoltageImbalance_RaisedWhenImbalanced()
        {
            var warnings = new List<string>();
            var publisher = new EventPublisher();
            publisher.OnWarningRaised += (type, msg) => warnings.Add(type);
            var engine = new AnalyticsEngine(publisher);

            engine.Analyze(new PvSample
            {
                RowIndex = 1,
                Vl1to2 = 220.0,
                Vl2to3 = 220.0,
                Vl3to1 = 300.0
            });

            Assert.IsTrue(warnings.Contains("VoltageImbalanceWarning"),
                "VoltageImbalance warning mora da se okine kad je raspon prevelik.");
        }

        [TestMethod]
        public void AnalyticsEngine_VoltageImbalance_NotRaisedWhenBalanced()
        {
            var warnings = new List<string>();
            var publisher = new EventPublisher();
            publisher.OnWarningRaised += (type, msg) => warnings.Add(type);
            var engine = new AnalyticsEngine(publisher);

            engine.Analyze(new PvSample
            {
                RowIndex = 1,
                Vl1to2 = 220.0,
                Vl2to3 = 220.0,
                Vl3to1 = 220.0
            });

            Assert.IsFalse(warnings.Contains("VoltageImbalanceWarning"),
                "VoltageImbalance warning ne sme da se okine kad su naponi balansirani.");
        }

        // -------------------------------------------------------
        // ROWINDEX MONOTONOST
        // -------------------------------------------------------

        [TestMethod]
        public void SolarPanelService_PushSample_ThrowsOnNonMonotonicRowIndex()
        {
            var service = new SolarPanelService();
            service.StartSession(new PvMeta
            {
                PlantId = "TestMono",
                FileName = "mono.csv",
                RowLimitN = 10,
                TotalRows = 10,
                SchemaVersion = "1.0"
            });

            service.PushSample(new PvSample { RowIndex = 5, Day = 1, Hour = "00:00" });

            try
            {
                service.PushSample(new PvSample { RowIndex = 3, Day = 1, Hour = "00:00" });
                Assert.Fail("Trebalo je baciti FaultException za nemonotoni RowIndex.");
            }
            catch (FaultException<SolarFaultException>)
            {
                // Očekivano
            }
            finally
            {
                service.Dispose();
            }
        }
    }
}