using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace GenericQueue
{
    [XmlRootAttribute("Details")]
    public class FieldCollection
    {
        [XmlElement("Field")]
        public Field[] Fields { get; set; }
    }
}
