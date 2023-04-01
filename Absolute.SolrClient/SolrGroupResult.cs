using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Absolute.SolrClient
{
    public class SolrGroupResult<TDoc> where TDoc : ISolrDocument
    {
        public SolrGroupResult()
        {
            Result = new LinkedList<TDoc>();
        }

        public string Name { get; internal set; }
        public int NumFound { get; internal set; }
        public int StartIndex { get; internal set; }
        public ICollection<TDoc> Result { get; internal set; }

    }
}
