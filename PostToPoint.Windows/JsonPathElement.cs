using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PostToPoint.Windows
{
    internal class JsonPathElement
    {
        public string PropertyName { get; set; } = string.Empty;
        public bool IsList { get; set; }

        public JsonPathElement(string propertyName, bool isList = false)
        {
            ArgumentNullException.ThrowIfNull(propertyName);
            PropertyName = propertyName;
            IsList = isList;
        }
    }
}
