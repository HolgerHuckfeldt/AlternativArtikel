using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Absolute.SolrClient
{
    public interface ISolrEscapedDictonary : IDictionary<string,string>
    {
        void AddUnescaped(string key, string value);
    }
}
