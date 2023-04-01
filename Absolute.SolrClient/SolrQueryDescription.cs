using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Linq.Expressions;

namespace Absolute.SolrClient
{
    public class SolrQueryDescription<TDoc> where TDoc : ISolrDocument
    {
        public SolrQueryDescription(ISolrIndex<TDoc> index)
        {
            Index = index;
            DynamicFieldKeys = new LinkedList<KeyValuePair<string, string>>();
            FacetFields = new LinkedList<SolrFacetField>();
            FacetQueries = new LinkedList<SolrFacetQuery>();
            Highlighting = new SolrHighlightingQuery();
            FieldNames = new LinkedList<string>();
            SortFields = new LinkedList<string>();
            FilterQueries = new LinkedList<string>();
            QueryFields = new Dictionary<string, double>();
            SolrParameters = new SolrEscapedDictonary();
            StatsFields = new LinkedList<string>();
            GroupQueries = new LinkedList<string>();

            var indexWithInfos = index as ISolrIndexWithFieldInfos<TDoc>;
            if (indexWithInfos != null)
            {
                foreach (var name in indexWithInfos.DocFields.Where(x => indexWithInfos.IsStoredField(x.Value) && !x.Key.Contains("*")).Select(x => x.Key))
                {
                    FieldNames.Add(name);
                }
            }
        }

        public ISolrIndex<TDoc> Index { get; private set; }
        public string Query { get; set; }
        public ICollection<string> FilterQueries { get; set; }
        public int? StartIndex { get; set; }
        public int? RowCount { get; set; }
        public ICollection<KeyValuePair<string, string>> DynamicFieldKeys { get; set; }
        public ICollection<SolrFacetField> FacetFields { get; set; }
        public ICollection<SolrFacetQuery> FacetQueries { get; set; }
        public SolrHighlightingQuery Highlighting { get; set; }
        public ICollection<string> FieldNames { get; set; }
        public ICollection<string> SortFields { get; set; }
        public ICollection<string> StatsFields { get; set; }
        public Dictionary<string, double> QueryFields { get; set; }
        public ISolrEscapedDictonary SolrParameters { get; set; }
        public ICollection<string> GroupQueries { get; set; }

        public void AddQueryField(Expression<Func<TDoc, object>> field, double boost = 1, string dynamicFieldKey = null)
        {
            var fieldname = Index.GetFieldName(field, dynamicFieldKey);
            if (QueryFields.ContainsKey(fieldname))
            {
                QueryFields.Remove(fieldname);
            }
            QueryFields.Add(fieldname, boost);
        }

        public void AddSortField(Expression<Func<TDoc, object>> field, SolrSortDirection direction)
        {
            var fieldName = Index.GetFieldName(field);
            if (fieldName != null)
            {
                SortFields.Add(string.Format("{0} {1}", fieldName, direction.ToString().ToLower()));
            }
        }

        public void AddSortField(string fieldName, SolrSortDirection direction)
        {
            if (!string.IsNullOrEmpty(fieldName))
            {
                SortFields.Add(string.Format("{0} {1}", fieldName, direction.ToString().ToLower()));
            }
        }

        public void AddSortField(Expression<Func<TDoc, object>> field, string dynamicKey, SolrSortDirection direction)
        {
            var fieldName = Index.GetFieldName(field, dynamicKey);
            if (fieldName != null)
            {
                SortFields.Add(string.Format("{0} {1}", fieldName, direction.ToString().ToLower()));
            }
        }

        public void AddDynamicFieldKey(Expression<Func<TDoc, object>> field, object key)
        {
            var keyString = key != null ? key.ToString() : null;
            if (string.IsNullOrEmpty(keyString))
            {
                return;
            }
            var fieldName = Index.GetFieldName(field);
            if (string.IsNullOrEmpty(fieldName))
            {
                return;
            }
            DynamicFieldKeys.Add(new KeyValuePair<string, string>(fieldName, fieldName.Replace("*", keyString)));
            FieldNames.Add(fieldName.Replace("*", keyString));
        }

        public void AddHighlightingField(Expression<Func<TDoc, object>> field)
        {
            var expression = field.Body as MemberExpression;
            if (expression == null)
            {
                return;
            }
            Highlighting.Fields.Add(expression.Member.Name);
        }

        public void AddHighlightingField(Expression<Func<TDoc, object>> field, string dynamicKey)
        {
            var expression = field.Body as MemberExpression;
            if (expression == null)
            {
                return;
            }
            Highlighting.Fields.Add(expression.Member.Name.Replace("*", dynamicKey));
        }

        public SolrQueryDescription<TDoc> Clone()
        {
            var result = new SolrQueryDescription<TDoc>(Index);
            result.DynamicFieldKeys = this.DynamicFieldKeys.ToList();
            result.FacetFields = this.FacetFields.ToList();
            result.FacetQueries = this.FacetQueries.ToList();
            result.FieldNames = this.FieldNames.ToList();
            result.FilterQueries = this.FilterQueries.ToList();
            foreach (var v in this.Highlighting.Fields)
            {
                result.Highlighting.Fields.Add(v);
            }
            result.Highlighting.RequireFieldMatch = this.Highlighting.RequireFieldMatch;
            result.Query = this.Query;
            foreach (var kv in this.QueryFields)
            {
                result.QueryFields.Add(kv.Key, kv.Value);
            }
            result.RowCount = this.RowCount;
            result.SortFields = this.SortFields.ToList();
            result.StatsFields = this.StatsFields.ToList();
            result.StartIndex = this.StartIndex;
            return result;
        }

    }
}
