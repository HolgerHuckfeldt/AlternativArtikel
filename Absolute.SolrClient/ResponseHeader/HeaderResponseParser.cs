using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Xml;

namespace Absolute.SolrClient.ResponseHeader
{
    /// <summary>
    /// Parses header (status, QTime, etc) from a query response
    /// </summary>
    public class HeaderResponseParser
    {
        /// <summary>
        /// Parses response header
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private static ResponseHeader ParseHeader(XmlNode node)
        {
            var r = new ResponseHeader();
            r.Status = int.Parse(node.SelectSingleNode("int[@name='status']").InnerText, CultureInfo.InvariantCulture.NumberFormat);
            r.QTime = int.Parse(node.SelectSingleNode("int[@name='QTime']").InnerText, CultureInfo.InvariantCulture.NumberFormat);
            r.Params = new Dictionary<string, string>();
            var paramNodes = node.SelectNodes("lst[@name='params']/str");
            foreach (XmlNode n in paramNodes)
            {
                if(n.Attributes.Count == 0) continue;
                r.Params[n.Attributes[0].Value] = n.InnerText;
            }
            return r;
        }

        public static ResponseHeader Parse(XmlDocument response)
        {
            var responseHeaderNode = response.SelectSingleNode("response/lst[@name='responseHeader']");
            if (responseHeaderNode != null)
                return ParseHeader(responseHeaderNode);
            return null;
        }
    }
}
