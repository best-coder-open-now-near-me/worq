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
        [XmlAttribute("Value")]
        public int Value { get; set; }

        [XmlAttribute("Label")]
        public string Label { get; set; }
    }
}
