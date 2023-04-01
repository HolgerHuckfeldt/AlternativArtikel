using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;

namespace Absolute.SolrClient
{
    public interface ISolrSerializer
    {
        void Serialize(ISolrDocument document, StreamWriter target);
    }
}
