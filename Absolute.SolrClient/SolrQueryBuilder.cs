using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using System.Globalization;
using System.Collections;
using System.Text.RegularExpressions;

namespace Absolute.SolrClient
{
    public class SolrQueryBuilder<TDoc> where TDoc : ISolrDocument
    {
        private readonly ISolrIndex<TDoc> _index;

        private SolrQueryBuilder() { }

        internal SolrQueryBuilder(ISolrIndex<TDoc> index)
        {
            _index = index;
            _parts = new LinkedList<KeyValuePair<string, object>>();
        }

        private LinkedList<KeyValuePair<string, object>> _parts;

        public string Tag { get; set; }

        public SolrQueryBuilder<TDoc> Expression(string expression)
        {
            _parts.AddLast(new KeyValuePair<string, object>("EXPRESSION", expression));
            return this;
        }

        /// <summary>
        /// Angabe des Suchfelds mit dem darin zu suchenden Wert.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="value"></param>
        /// <param name="dynamicKey"></param>
        /// <param name="boost"></param>
        /// <param name="quoteValue">
        ///     Wenn true, wird der Wert mit doppelten Anführungszeichen " umschlossen.
        ///     Bsp:
        ///         quoteValue == false
        ///             Suchbaumknoten_PC_0:(Klimaanlage{10721})
        ///             
        ///         quoteValue == true
        ///             Suchbaumknoten_PC_0:("Klimaanlage{10721}")    
        /// </param>
        /// <returns></returns>
        public SolrQueryBuilder<TDoc> Expression(
            Expression<Func<TDoc, object>> field, 
            object value, 
            string dynamicKey, 
            decimal boost, 
            bool quoteValue)
        {
            if (field == null || value == null || Stringify(value).Trim().Length == 0)
            {
                if (_parts.Count > 0 && _parts.Last.Value.Key == "OPERATOR")
                {
                    _parts.RemoveLast();
                }
                return this;
            }

            if (value is SolrRangeValue)
            {
                var range = (SolrRangeValue)value;
                value = string.Format("[{0} TO {1}]", Stringify(range.Start), Stringify(range.End));
            }
            else
            {
                value = Stringify(value);
            }

            if (quoteValue)
            {
                value = string.Format("\"{0}\"", value);
            }

            var part = string.Format("{0}:({1})", _index.GetFieldName(field, dynamicKey), value);
            if (boost > 0)
            {
                part = string.Format("{0}^{1}", part, Stringify(boost));
            }


            _parts.AddLast(new KeyValuePair<string, object>("EXPRESSION", part));
            return this;
        }

        /// <summary>
        /// Angabe des Suchfelds mit dem darin zu suchenden Wert.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="value"></param>
        /// <param name="dynamicKey"></param>
        /// <param name="boost"></param>
        /// <returns></returns>
        public SolrQueryBuilder<TDoc> Expression(Expression<Func<TDoc, object>> field, object value, string dynamicKey, decimal boost)
        {
            return Expression( field,value, dynamicKey, boost,false);
        }


        public SolrQueryBuilder<TDoc> Expression(Expression<Func<TDoc, object>> field, object value, string dynamicKey, bool quoteValue)
        {
            return Expression(field, value, dynamicKey, 0, quoteValue);
        }

        public SolrQueryBuilder<TDoc> Expression(Expression<Func<TDoc, object>> field, object value, string dynamicKey)
        {
            return Expression(field, value, dynamicKey, false);
        }


        public SolrQueryBuilder<TDoc> Expression(Expression<Func<TDoc, object>> field, object value, decimal boost, bool quoteValue)
        {
            return Expression(field, value, null, boost, quoteValue);
        }

        public SolrQueryBuilder<TDoc> Expression(Expression<Func<TDoc, object>> field, object value, decimal boost)
        {
            return Expression(field, value, boost, false);
        }


        public SolrQueryBuilder<TDoc> Expression(Expression<Func<TDoc, object>> field, object value, bool quoteValue)
        {
            return Expression(field, value, null, 0, quoteValue);
        }

        public SolrQueryBuilder<TDoc> Expression(Expression<Func<TDoc, object>> field, object value)
        {
            return Expression(field, value, false);
        }


        public SolrQueryBuilder<TDoc> AllExpression()
        {
            _parts.AddLast(new KeyValuePair<string, object>("EXPRESSION", "*:*"));
            return this;
        }

        public SolrQueryBuilder<TDoc> NegativeExpression(Expression<Func<TDoc, object>> field, object value, string dynamicKey)
        {
            if (field == null || value == null || Stringify(value).Trim().Length == 0)
            {
                if (_parts.Count > 0 && _parts.Last.Value.Key == "OPERATOR")
                {
                    _parts.RemoveLast();
                }
                return this;
            }
            if (value is SolrRangeValue)
            {
                var range = (SolrRangeValue)value;
                value = string.Format("[{0} TO {1}]", Stringify(range.Start), Stringify(range.End));
            }
            else
            {
                value = Stringify(value);
            }
            var part = string.Format("-{0}:({1})", _index.GetFieldName(field, dynamicKey), value);
            _parts.AddLast(new KeyValuePair<string, object>("EXPRESSION", part));
            return this;
        }

        public SolrQueryBuilder<TDoc> NegativeExpression(Expression<Func<TDoc, object>> field, object value)
        {
            return NegativeExpression(field, value, null);
        }


        /// <summary>
        /// Suche im Standardsuchfeld (text).
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public SolrQueryBuilder<TDoc> DefaultExpression(object value)
        {
            if (value == null || Stringify(value).Trim().Length == 0)
            {
                if (_parts.Count > 0 && _parts.Last.Value.Key == "OPERATOR")
                {
                    _parts.RemoveLast();
                }
                return this;
            }
            _parts.AddLast(new KeyValuePair<string, object>("EXPRESSION", string.Format("({0})", Stringify(value))));
            return this;
        }

        public SolrQueryBuilder<TDoc> Join(Expression<Func<TDoc, object>> field, SolrQueryOperator queryOperator, IEnumerable values, string dynamicKey = null, decimal boost = 0)
        {
            if (!values.GetType().IsGenericType && !values.GetType().IsArray)
            {
                return this;
            }
            foreach (var value in values)
            {
                Expression(field, value, dynamicKey, boost);
                Operator(queryOperator);
            }
            if (_parts.Count > 0 && _parts.Last.Value.Key == "OPERATOR")
            {
                _parts.RemoveLast();
            }
            return this;
        }

        public SolrQueryBuilder<TDoc> NegativeJoin(Expression<Func<TDoc, object>> field, SolrQueryOperator queryOperator, IEnumerable values, string dynamicKey = null)
        {
            if (!values.GetType().IsGenericType && !values.GetType().IsArray)
            {
                return this;
            }
            foreach (var value in values)
            {
                NegativeExpression(field, value, dynamicKey);
                Operator(queryOperator);
            }
            if (_parts.Count > 0 && _parts.Last.Value.Key == "OPERATOR")
            {
                _parts.RemoveLast();
            }
            return this;
        }

        /// <summary>
        /// Der Operator zwischen dem zuletzt angegeben und dem nächsten Ausdruck.
        /// </summary>
        /// <param name="queryOperator"></param>
        /// <returns></returns>
        public SolrQueryBuilder<TDoc> Operator(SolrQueryOperator queryOperator)
        {
            if (_parts.Count() > 0)
            {
                if (_parts.Last().Key == "OPERATOR")
                    _parts.RemoveLast();
                _parts.AddLast(new KeyValuePair<string, object>("OPERATOR", queryOperator.ToString().ToUpper()));
            }
            return this;
        }

        public SolrQueryBuilder<TDoc> And()
        {
            if (_parts.Count() > 0)
            {
                if (_parts.Last().Key == "OPERATOR")
                    _parts.RemoveLast();
                _parts.AddLast(new KeyValuePair<string, object>("OPERATOR", SolrQueryOperator.And.ToString().ToUpper()));
            }
            return this;
        }

        public SolrQueryBuilder<TDoc> Or()
        {
            if (_parts.Count() > 0)
            {
                if (_parts.Last().Key == "OPERATOR")
                    _parts.RemoveLast();
                _parts.AddLast(new KeyValuePair<string, object>("OPERATOR", SolrQueryOperator.Or.ToString().ToUpper()));
            }
            return this;
        }

        /// <summary>
        /// Eine in Klammern stehende Unterabfrage.
        /// </summary>
        /// <param name="subQuery"></param>
        /// <returns></returns>
        public SolrQueryBuilder<TDoc> SubQuery(SolrQueryBuilder<TDoc> subQuery)
        {
            _parts.AddLast(new KeyValuePair<string, object>("SUBQUERY", subQuery));
            return this;
        }

        public string Create()
        {
            if (_parts.Count == 0)
            {
                return null;
            }
            var result = string.Empty;
            var parts = _parts;

            if (!string.IsNullOrEmpty(Tag))
            {
                result = string.Format("{{!tag={0}}}", Tag);
            }

            if (parts.Last.Value.Key == "OPERATOR")
            {
                parts.RemoveLast();
            }

            foreach (var part in parts)
            {
                if (part.Key == "SUBQUERY")
                {
                    var subquery = (SolrQueryBuilder<TDoc>)part.Value;
                    result += string.Format("({0})", subquery.Create()) + " ";
                    continue;
                }

                string v = part.Value.ToString();
                v = v.Replace("\\\"", "\"");
                result += v + " ";
            }
            return result.Trim();
        }

        private string Stringify(object value)
        {
            var culture = new CultureInfo("en-US");
            if (value is decimal)
            {
                return ((decimal)value).ToString("0.###", culture);
            }
            else if (value is double)
            {
                return ((double)value).ToString("0.###", culture);
            }
            else if (value is DateTime)
            {
                return ((DateTime)value).ToString("u");
            }

            // Escape the following chars: " - ! ( ) { } [ ] ^ ~  ? : \
            var result = Regex.Replace(value.ToString(), "([\"\\-!\\(\\)\\{\\}\\[\\]\\^~\\?\\:\\\\]{1})", "\\$1");  //PV-2517 QueryParser.Escape() hier nicht ohne weiteres möglich, da damit z.B. das Sternchen escaped wird.

            return result;
        }

        public override string ToString()
        {
            try
            {
                if (_parts.Count == 0)
                {
                    return null;
                }
                var result = string.Empty;
                var parts = _parts;

                foreach (var part in parts)
                {
                    if (part.Key == "SUBQUERY")
                    {
                        var subquery = (SolrQueryBuilder<TDoc>)part.Value;
                        result += string.Format("({0})", subquery.Create());
                        continue;
                    }
                    result += part.Value.ToString() + " ";
                }
                return result.Trim();
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }

    public enum SolrQueryOperator
    {
        And,
        Or
    }
}
