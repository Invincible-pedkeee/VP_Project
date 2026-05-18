using Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

//Tacka 2 WCF serivisi i ugovori 
namespace Common
{
    [ServiceContract]
    public interface ISolarPanelService
    {
        [OperationContract]
        [FaultContract(typeof(SolarFaultException))]
        void StartSession(PvMeta meta);

        [OperationContract]
        [FaultContract(typeof(SolarFaultException))]
        void PushSample(PvSample sample);

        [OperationContract]
        [FaultContract(typeof(SolarFaultException))]
        void EndSession();
    }
}
