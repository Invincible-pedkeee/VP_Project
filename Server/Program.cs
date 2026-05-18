using Server.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    internal class Program
    {
        static void Main(string[] args)
        {
            ServiceHost host = null;

            try
            {
                SolarPanelService service = new SolarPanelService();
                EventSubscriber subscriber = new EventSubscriber(service.Publisher);

                host = new ServiceHost(service);
                host.Open();

                Console.WriteLine("[SERVER] Service started.");
                Console.WriteLine("[SERVER] Press Enter for exit....");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SERVER] Greska: {ex.Message}");
            }
            finally
            {
                try
                {
                    host?.Close();
                }
                catch
                {
                    host?.Abort();
                }
                Console.WriteLine("[SERVER] Service closed.");
            }
        }
    }
}
