using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Absolute.SolrClient
{
    internal interface ISolrIndexWithFieldInfos<TDoc> : ISolrIndex<TDoc> where TDoc : ISolrDocument
    {
        bool IsStoredField(PropertyInfo prop);
        IDictionary<string, PropertyInfo> DocFields { get; }
    }
}
