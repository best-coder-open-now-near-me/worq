using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace GenericQueue
{
    public class Enum
    {
        [XmlElement("Value")]
        public string Value { get; set; }

        [XmlElement("Label")]
        public string Label { get; set; }

        [XmlElement("Name")]
        public string Name { get; set; }
    }


    [XmlRootAttribute("Enums")]
    public class EnumCollection
    {
        [XmlElement("Enum")]
        public Enum[] Enums { get; set; }
    }
}
