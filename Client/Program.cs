using Common;
using Common.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

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

                Console.WriteLine("[CLIENT] Reading CSV file...");
                List<PvSample> samples;

                using (var reader = new CsvReader(csvPath, rowLimitN))
                {
                    samples = reader.ReadSamples(rejectedLog);
                }
                Console.WriteLine($"[CLIENT] Read {samples.Count} valid rows.");

                factory = new ChannelFactory<ISolarPanelService>("SolarPanelEndpoint");
                proxy = factory.CreateChannel();

                var meta = new PvMeta
                {
                    FileName = csvPath,
                    TotalRows = samples.Count,
                    SchemaVersion = schemaVersion,
                    RowLimitN = rowLimitN,
                    PlantId = plantId
                };

                Console.WriteLine("[CLIENT] Started session...");
                proxy.StartSession(meta);

                foreach (var sample in samples)
                {
                    proxy.PushSample(sample);
                }

                proxy.EndSession();
                Console.WriteLine("[CLIENT] Session finished successfully.");
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