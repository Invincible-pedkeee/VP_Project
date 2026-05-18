using Common;
using Common.Models;
using System;
using System.Configuration;
using System.ServiceModel;

namespace Client
{
    internal class Program
    {
        static void Main(string[] args)
        {
            ChannelFactory<ISolarPanelService> factory = null;
            ISolarPanelService proxy = null;

            try
            {
                string csvPath = ConfigurationManager.AppSettings["CsvFilePath"];
                string rejectedLog = ConfigurationManager.AppSettings["RejectedLogPath"];
                string plantId = ConfigurationManager.AppSettings["PlantId"];
                string schemaVersion = ConfigurationManager.AppSettings["SchemaVersion"];
                int rowLimitN = int.Parse(ConfigurationManager.AppSettings["RowLimitN"]);

                factory = new ChannelFactory<ISolarPanelService>("SolarPanelEndpoint");
                proxy = factory.CreateChannel();

                var meta = new PvMeta
                {
                    FileName = csvPath,
                    TotalRows = rowLimitN,
                    SchemaVersion = schemaVersion,
                    RowLimitN = rowLimitN,
                    PlantId = plantId
                };

                Console.WriteLine("[CLIENT] Starting session...");
                proxy.StartSession(meta);

                int sent = 0;

                using (var reader = new CsvReader(csvPath, rowLimitN))
                {
                    foreach (var sample in reader.ReadSamplesStreaming(rejectedLog))
                    {
                        proxy.PushSample(sample);
                        sent++;
                    }
                }

                proxy.EndSession();
                Console.WriteLine($"[CLIENT] Session finished successfully. Sent {sent} rows.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CLIENT] Error: {ex.Message}");
            }
            finally
            {
                try
                {
                    if (proxy != null)
                        ((IClientChannel)proxy).Close();
                    factory?.Close();
                }
                catch
                {
                    ((IClientChannel)proxy)?.Abort();
                    factory?.Abort();
                }

                Console.WriteLine("[CLIENT] Connection closed.");
                Console.ReadLine();
            }
        }
    }
}