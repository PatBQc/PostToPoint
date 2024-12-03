using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PostToPoint.Windows
{
    internal class JsonPathElement
    {
        public string PropertyName { get; set; }
        public bool IsList { get; set; }

        public JsonPathElement(string propertyName, bool isList = false)
        {
            PropertyName = propertyName;
            IsList = isList;
        }
    }
}
