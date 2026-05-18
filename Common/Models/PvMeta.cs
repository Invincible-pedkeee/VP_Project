using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
//Tacka 2
namespace Common.Models
{
    [DataContract]
    public class PvMeta
    {
        [DataMember]
        public string FileName { get; set; }
        [DataMember]
        public int TotalRows { get; set; }
        [DataMember]
        public string SchemaVersion { get; set; }
        [DataMember]
        public int RowLimitN { get; set; }
        [DataMember]
        public string PlantId { get; set; }
    }
}