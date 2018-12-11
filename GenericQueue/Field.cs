using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GenericQueue
{
    public class Field
    {
        public string Name { get; set; }
        public string Label { get; set; }
        public string DataType { get; set; }
        public string Value { get; set; }
        public double Order { get; set; }
        public bool ReadOnly { get; set; }
        public int ID { get; set; }
        public string Color { get; set; }
    }
}
