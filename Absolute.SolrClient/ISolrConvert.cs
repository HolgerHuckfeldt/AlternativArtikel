using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.XPath;

namespace Absolute.SolrClient
{
    public interface ISolrConvert
    {
        object ConvertXml(XPathNodeIterator o, Dictionary<string, string> dynamicFieldMap);
    }
}
