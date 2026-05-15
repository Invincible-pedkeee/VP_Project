using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Infrastructure
{
    public class EventSubscriber
    {
        public EventSubscriber(EventPublisher publisher)
        {
            publisher.OnTransferStarted += OnTransferStarted;
            publisher.OnSampleReceived += OnSampleReceived;
            publisher.OnTransferCompleted += OnTransferCompleted;
            publisher.OnWarningRaised += OnWarningRaised;
        }


        private void OnTransferStarted(string fileName)
        {
            Console.WriteLine($"[EVENT] Prenos zapocet: {fileName}");
        }
        private void OnSampleReceived(int rowIndex, int received, int total)
        {
            Console.WriteLine($"[EVENT] Primljen red {rowIndex} ({received}/{total})");
        }

        private void OnTransferCompleted(int totalReceived)
        {
            Console.WriteLine($"[EVENT] Prenos zavrsen! Ukupno: {totalReceived} redova");
        }

        private void OnWarningRaised(string warningType, string message)
        {
            Console.WriteLine($"[WARNING] {warningType}: {message}");
        }
    }
}
