using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;

namespace Absolute.SolrClient
{
    public class SolrHighlightingQuery
    {
        internal SolrHighlightingQuery()
        {
            Fields = new LinkedList<string>();
        }

        public ICollection<string> Fields { get; private set; }
        public bool RequireFieldMatch { get; set; }

    }
}
