using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Absolute.SolrClient
{
    public class SolrRangeValue
    {
        public SolrRangeValue(object start, object end)
        {
            Start = start ?? "*";
            End = end ?? "*" ;
        }

        public object Start { get; private set; }
        public object End { get; private set; }
    }
}
