using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Infrastructure
{
    public delegate void TransferStartedHandler(string fileName);
    public delegate void SampleReceivedHandler(int rowIndex, int received, int total);
    public delegate void TransferCompletedHandler(int totalReceived);
    public delegate void WarningRaisedHandler(string warningType, string message);


    public class EventPublisher
    {
        public event TransferStartedHandler OnTransferStarted;
        public event SampleReceivedHandler OnSampleReceived;
        public event TransferCompletedHandler OnTransferCompleted;
        public event WarningRaisedHandler OnWarningRaised;

        public void RaiseTransferStarted(string fileName)
        {
            if (OnTransferStarted != null)
                OnTransferStarted(fileName);
        }
        public void RaiseSampleReceived(int rowIndex,int received,int total)
        {
            if (OnSampleReceived != null)
                OnSampleReceived(rowIndex, received, total);
        }
        public void RaiseTransferCompleted(int totalReceived)
        {
            if (OnTransferCompleted != null)
                OnTransferCompleted(totalReceived);
        }
        public void RaiseWarning(string warningType,string message)
        {
            if (OnWarningRaised != null)
                OnWarningRaised(warningType, message);
        }
    }
}
