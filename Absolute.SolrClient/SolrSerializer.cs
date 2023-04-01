using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Absolute.SolrClient
{
    public class SolrSerializer<TDoc> : ISolrSerializer where TDoc : ISolrDocument
    {
        private readonly IDictionary<string, PropertyInfo> _documentFields;

        public static void RegisterTypeKonverter(Type t, ISolrConvert converter)
        {
            if (!KnownTypes.ContainsKey(t))
            {
                KnownTypes.Add(t, converter);
            }
        }

        private static readonly Dictionary<Type, ISolrConvert> KnownTypes = new Dictionary<Type, ISolrConvert>();

        internal SolrSerializer(IDictionary<string, PropertyInfo> documentFields)
        {
            _documentFields = documentFields;
        }

        public void Serialize(ISolrDocument document, StreamWriter target)
        {
            if (!(document is TDoc))
            {
                throw new Exception(string.Format("Das übergebene Dokument ist nicht vom Typ '{0}'.", typeof(TDoc)));
            }

            target.WriteLine("\t<doc>");
            foreach (var field in _documentFields)
            {
                var value = field.Value.GetValue(document, null);
                if (value == null)
                {
                    continue;
                }
                AppendSerializedField(target, field.Key, field.Value.PropertyType, value);
            }
            target.WriteLine("\t</doc>");
        }

        public SolrQueryResult<TDoc> Deserialize(Stream stream, SolrQueryDescription<TDoc> queryDesc)
        {
            var docType = typeof(TDoc);
            var result = new SolrQueryResult<TDoc>(queryDesc);

            var xPathDoc = new XPathDocument(stream);
            var xMainNav = xPathDoc.CreateNavigator();
            xMainNav.MoveToFirstChild(); // response
            if (xMainNav.Name != "response")
            {
                return result;
            }

            // Infos
            result.QueryTime = xMainNav.SelectSingleNode("lst[@name='responseHeader']/int[@name='QTime']").ValueAsInt;

            if (xMainNav.SelectSingleNode("result") != null)
            {
                xMainNav = xMainNav.SelectSingleNode("result");

                result.NumFound = xMainNav.SelectSingleNode("@numFound").ValueAsInt;
                result.StartIndex = xMainNav.SelectSingleNode("@start").ValueAsInt;

                // Dokumente
                var xSolrDoc = xMainNav.Select("doc");
                while (xSolrDoc.MoveNext())
                {
                    var doc = MapDocResult(xSolrDoc.Current, queryDesc);
                    result.Documents.Add(doc);
                }

            }
            else if (xMainNav.SelectSingleNode("lst[@name='grouped']") != null)
            {
                xMainNav = xMainNav.SelectSingleNode("lst[@name='grouped']");

                var xGroupedNodes = xMainNav.Select("lst");
                while (xGroupedNodes.MoveNext())
                {
                    var group = new SolrGroupResult<TDoc>();
                    var xGroupedResult = xGroupedNodes.Current.SelectSingleNode("result[@name='doclist']");
                    group.Name = xGroupedNodes.Current.SelectSingleNode("@name").Value;
                    group.NumFound = xGroupedResult.SelectSingleNode("@numFound").ValueAsInt;
                    group.StartIndex = xGroupedResult.SelectSingleNode("@start").ValueAsInt;
                    var xGroupedDoc = xGroupedResult.Select("doc");
                    while (xGroupedDoc.MoveNext())
                    {
                        var doc = MapDocResult(xGroupedDoc.Current, queryDesc);
                        group.Result.Add(doc);
                    }
                    result.Groups.Add(group);
                }
            }
            xMainNav.MoveToNext();


            if (xMainNav.SelectSingleNode("@name").Value == "facet_counts")
            {
                // FacetQueries
                var xFacetQueryNodes = xMainNav.Select("lst[@name='facet_queries']/int");
                while (xFacetQueryNodes.MoveNext())
                {
                    var facetNode = xFacetQueryNodes.Current;
                    var facetQuery = facetNode.SelectSingleNode("@name").Value;
                    result.FacetQueries.Add(facetQuery, facetNode.ValueAsInt);
                }

                // FacetFields
                var xFacetFieldNodes = xMainNav.Select("lst[@name='facet_fields']/lst");
                while (xFacetFieldNodes.MoveNext())
                {
                    var facetNode = xFacetFieldNodes.Current;
                    var fieldName = facetNode.SelectSingleNode("@name").Value;
                    var facetItem = queryDesc.FacetFields.SingleOrDefault(x => x.FieldName == fieldName);
                    if (facetItem == null)
                    {
                        continue;
                    }
                    var facetResult = new SolrFacetFieldResult(facetItem);
                    var xCountNodes = facetNode.SelectChildren(XPathNodeType.Element);
                    while (xCountNodes.MoveNext())
                    {
                        var countNode = xCountNodes.Current;
                        facetResult.Counts.Add(countNode.SelectSingleNode("@name").Value, countNode.ValueAsInt);
                    }
                    result.FacetFields.Add(facetResult);
                }
                xMainNav.MoveToNext();
            }


            // Highlights
            if (xMainNav.SelectSingleNode("@name").Value == "highlighting")
            {
                var xHighlighNodes = xMainNav.Select("lst");
                while (xHighlighNodes.MoveNext())
                {
                    var idNode = xHighlighNodes.Current;
                    var id = idNode.SelectSingleNode("@name").Value;
                    var fieldList = new Dictionary<string, ICollection<string>>();
                    result.Highlights.Add(id, fieldList);
                    var xFieldNodes = idNode.Select("arr");
                    while (xFieldNodes.MoveNext())
                    {
                        var fieldNode = xFieldNodes.Current;
                        var fieldName = fieldNode.SelectSingleNode("@name").Value;
                        var valueList = new LinkedList<string>();
                        fieldList.Add(fieldName, valueList);
                        var xValueNodes = fieldNode.Select("str");
                        while (xValueNodes.MoveNext())
                        {
                            var valueNode = xValueNodes.Current;
                            var value = valueNode.Value;
                            valueList.AddLast(value);
                        }
                    }
                }
                xMainNav.MoveToNext();
            }

            // Statistics
            if (xMainNav.SelectSingleNode("@name").Value == "stats")
            {
                // StatsFields
                var xFacetFieldNodes = xMainNav.Select("lst[@name='stats_fields']/lst");
                while (xFacetFieldNodes.MoveNext())
                {
                    var facetNode = xFacetFieldNodes.Current;
                    var fieldName = facetNode.SelectSingleNode("@name").Value;
                    var statsItem = queryDesc.StatsFields.SingleOrDefault(x => x == fieldName);
                    if (statsItem == null)
                    {
                        continue;
                    }
                    var statsResult = new SolrStatsResult(statsItem);
                    var xCountNodes = facetNode.SelectChildren(XPathNodeType.Element);
                    while (xCountNodes.MoveNext())
                    {
                        var statsNode = xCountNodes.Current;
                        switch (statsNode.SelectSingleNode("@name").Value)
                        {
                            case "min":
                                if (statsNode.Name == "double")
                                {
                                    statsResult.MinDecimal = Convert.ToDecimal(statsNode.Value, new CultureInfo("en-US"));
                                }
                                else
                                {
                                    statsResult.MinString = statsNode.Value;
                                }
                                break;
                            case "max":
                                if (statsNode.Name == "double")
                                {
                                    statsResult.MaxDecimal = Convert.ToDecimal(statsNode.Value, new CultureInfo("en-US"));
                                }
                                else
                                {
                                    statsResult.MaxString = statsNode.Value;
                                }
                                break;
                            case "count":
                                statsResult.Count = Convert.ToInt64(statsNode.Value);
                                break;
                            case "missing":
                                statsResult.Empty = Convert.ToInt64(statsNode.Value);
                                break;
                            case "sum":
                                statsResult.Sum = Convert.ToDecimal(statsNode.ValueAsDouble, new CultureInfo("en-US")); 
                                break;
                            case "mean":
                                statsResult.Average = Convert.ToDecimal(statsNode.Value);
                                break;
                        }
                    }
                    result.Statistics.Add(statsResult);
                }
                xMainNav.MoveToNext();
            }

            if (xMainNav.SelectSingleNode("@name").Value == "spellcheck")
            {
                if (!string.IsNullOrEmpty(xMainNav.InnerXml))
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(xMainNav.InnerXml);
                    XmlNodeList parentNode = xmlDoc.GetElementsByTagName("lst");
                    foreach (var element in parentNode)
                    {
                        if (((XmlElement)element).InnerXml.Contains("<str name=\"word\">") && !((XmlElement)element).HasAttributes)
                        {
                            result.ResultOfSpellCheck.Add(new SpellcheckResult()
                            {
                                Word = ((XmlElement)element).FirstChild.InnerText,
                                Freq = ((XmlElement)element).LastChild.InnerText
                            });
                        }
                    }

                    
                }

                xMainNav.MoveToNext();
            }

            return result;
        }

        public SolrQueryResult<TDoc> DeserializeDirect(Stream stream, SolrQueryDescription<TDoc> queryDesc)
        {

            var docType = typeof(TDoc);
            var result = new SolrQueryResult<TDoc>(queryDesc);

            var xPathDoc = new XPathDocument(stream);
            var xMainNav = xPathDoc.CreateNavigator();
            xMainNav.MoveToFirstChild(); // response
            if (xMainNav.Name != "response")
            {
                return result;
            }

            // Infos
            result.QueryTime = xMainNav.SelectSingleNode("lst[@name='responseHeader']/int[@name='QTime']").ValueAsInt;
            xMainNav = xMainNav.SelectSingleNode("result");
            result.NumFound = xMainNav.SelectSingleNode("@numFound").ValueAsInt;
            result.StartIndex = xMainNav.SelectSingleNode("@start").ValueAsInt;

            // Dokumente
            var xSolrDoc = xMainNav.Select("doc");

            Dictionary<string, string> dynamicFieldMap = new Dictionary<string, string>();
            foreach (var dynamicFieldKey in queryDesc.DynamicFieldKeys)
            {
                dynamicFieldMap.Add(dynamicFieldKey.Value, dynamicFieldKey.Key);
            }

            while (xSolrDoc.MoveNext())
            {
                var xField = xSolrDoc.Current.SelectChildren(XPathNodeType.Element);

                bool added = false;

                if (KnownTypes.ContainsKey(docType))
                {
                    try
                    {
                        var newDoc = (TDoc)KnownTypes[docType].ConvertXml(xField, dynamicFieldMap);
                        if (newDoc != null)
                        {
                            result.Documents.Add(newDoc);
                            added = true;
                        }
                    }
                    catch
                    {
                        added = false;
                    }
                }

                if (!added)
                {
                    var doc = Activator.CreateInstance(docType);

                    while (xField.MoveNext())
                    {
                        var fieldNode = xField.Current;
                        var fieldNodeName = fieldNode.SelectSingleNode("@name").Value;
                        PropertyInfo field = null;
                        if (queryDesc.DynamicFieldKeys.Any(x => x.Value == fieldNodeName))
                        {
                            fieldNodeName = queryDesc.DynamicFieldKeys.First(x => x.Value == fieldNodeName).Key;
                        }
                        if (_documentFields.ContainsKey(fieldNodeName))
                        {
                            field = _documentFields[fieldNodeName];
                        }
                        else
                        {
                            continue;
                        }
                        if (field.PropertyType.IsGenericType && field.PropertyType.GetInterface("IEnumerable") != null)
                        {
                            var genericArguments = field.PropertyType.GetGenericArguments();
                            var argumentType = genericArguments[0];
                            var listType = typeof(List<>);
                            var list = field.GetValue(doc, null);
                            if (list == null)
                            {
                                list = Activator.CreateInstance(listType.MakeGenericType(argumentType));
                                field.SetValue(doc, list, null);
                            }
                            if (fieldNode.Name == "arr")
                            {
                                var xArr = fieldNode.SelectChildren(XPathNodeType.Element);
                                while (xArr.MoveNext())
                                {
                                    var arrNode = xArr.Current;
                                    var value = GetNodeValue(arrNode);
                                    field.PropertyType.InvokeMember("Add", BindingFlags.InvokeMethod, null, list,
                                        new object[] { Convert.ChangeType(value, argumentType) });
                                }
                            }
                            else
                            {
                                var value = GetNodeValue(fieldNode);
                                field.PropertyType.InvokeMember("Add", BindingFlags.InvokeMethod, null, list,
                                    new object[] { Convert.ChangeType(value, argumentType) });
                            }
                        }
                        else
                        {
                            var type = field.PropertyType;
                            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                            {
                                type = type.GetGenericArguments()[0];
                            }
                            var value = GetNodeValue(fieldNode);
                            if (!string.IsNullOrEmpty(value))
                            {
                                field.SetValue(doc, Convert.ChangeType(value, type), null);
                            }
                        }

                    }
                    result.Documents.Add((TDoc)doc);
                }
            }
            xMainNav.MoveToNext();

            if (xMainNav.SelectSingleNode("@name").Value == "facet_counts")
            {
                // FacetQueries
                var xFacetQueryNodes = xMainNav.Select("lst[@name='facet_queries']/int");
                while (xFacetQueryNodes.MoveNext())
                {
                    var facetNode = xFacetQueryNodes.Current;
                    var facetQuery = facetNode.SelectSingleNode("@name").Value;
                    result.FacetQueries.Add(facetQuery, facetNode.ValueAsInt);
                }

                // FacetFields
                var xFacetFieldNodes = xMainNav.Select("lst[@name='facet_fields']/lst");
                while (xFacetFieldNodes.MoveNext())
                {
                    var facetNode = xFacetFieldNodes.Current;
                    var fieldName = facetNode.SelectSingleNode("@name").Value;
                    var facetItem = queryDesc.FacetFields.SingleOrDefault(x => x.FieldName == fieldName);
                    if (facetItem == null)
                    {
                        continue;
                    }
                    var facetResult = new SolrFacetFieldResult(facetItem);
                    var xCountNodes = facetNode.SelectChildren(XPathNodeType.Element);
                    while (xCountNodes.MoveNext())
                    {
                        var countNode = xCountNodes.Current;
                        facetResult.Counts.Add(countNode.SelectSingleNode("@name").Value, countNode.ValueAsInt);
                    }
                    result.FacetFields.Add(facetResult);
                }
                xMainNav.MoveToNext();
            }
           
            if (xMainNav.SelectSingleNode("@name").Value == "highlighting")
            {
                var xHighlighNodes = xMainNav.Select("lst");
                while (xHighlighNodes.MoveNext())
                {
                    var idNode = xHighlighNodes.Current;
                    var id = idNode.SelectSingleNode("@name").Value;
                    var fieldList = new Dictionary<string, ICollection<string>>();
                    result.Highlights.Add(id, fieldList);
                    var xFieldNodes = idNode.Select("arr");
                    while (xFieldNodes.MoveNext())
                    {
                        var fieldNode = xFieldNodes.Current;
                        var fieldName = fieldNode.SelectSingleNode("@name").Value;
                        var valueList = new LinkedList<string>();
                        fieldList.Add(fieldName, valueList);
                        var xValueNodes = fieldNode.Select("str");
                        while (xValueNodes.MoveNext())
                        {
                            var valueNode = xValueNodes.Current;
                            var value = valueNode.Value;
                            valueList.AddLast(value);
                        }
                    }
                }
                xMainNav.MoveToNext();
            }

            return result;
        }

        private void AppendSerializedField(StreamWriter target, string fieldName, Type dataType, object dataObject)
        {
            if (dataType.IsGenericType && dataType.GetInterface("IEnumerable") != null)
            {
                foreach (var val in (IEnumerable)dataObject)
                {
                    if (val == null)
                    {
                        continue;
                    }
                    var attributeName = fieldName;
                    var genVal = val;
                    var genType = val.GetType();
                    // Dictionary
                    if (genType.Name == "KeyValuePair`2")
                    {
                        attributeName = attributeName.Replace("*", val.GetType().GetProperty("Key").GetValue(val, null).ToString());
                        genVal = val.GetType().GetProperty("Value").GetValue(val, null);
                        genType = genVal.GetType();
                    }

                    AppendSerializedField(target, attributeName, genType, genVal);
                }
            }
            else if (dataType == typeof(decimal) || dataType == typeof(Nullable<decimal>))
            {
                var text = ((decimal)dataObject).ToString("0.###", new CultureInfo("en-US"));
                var element = CreateFieldElement(fieldName, text);
                target.WriteLine("\t\t" + element.ToString());
            }
            else if (dataType == typeof(DateTime) || dataType == typeof(Nullable<DateTime>))
            {
                var text = ((DateTime)dataObject).ToUniversalTime().ToString("u", DateTimeFormatInfo.InvariantInfo).Replace(" ", "T");
                var element = CreateFieldElement(fieldName, text);
                target.WriteLine("\t\t" + element.ToString());
            }
            else
            {
                var element = CreateFieldElement(fieldName, dataObject.ToString());
                try
                {
                    target.WriteLine("\t\t" + element.ToString());
                }
                catch (Exception ex)
                {
                    element.Value = RemoveControlChars(element.Value);
                    target.WriteLine("\t\t" + element.ToString());
                }
            }
        }

        private string GetNodeValue(XPathNavigator node)
        {
            var value = node.Value;
            if (node.Name == "double")
            {
                value = value.Replace(".", ",");
            }
            return value;
        }

        private XElement CreateFieldElement(string attributeName, string value)
        {
            var element = new XElement("field");
            element.Add(new XAttribute("name", attributeName));
            element.Add(new XCData(value));
            return element;
        }

        private string RemoveControlChars(string value)
        {
            if (value == null)
            {
                return null;
            }
            var newValue = string.Empty;
            foreach (var c in value)
            {
                if (c < 0x20 && !(c == 0x09 || c == 0x0A || c == 0x0D))
                {
                    continue;
                }
                newValue += c;
            }
            return newValue;
        }


        private TDoc MapDocResult(XPathNavigator docNavigator, SolrQueryDescription<TDoc> queryDesc)
        {
            var doc = Activator.CreateInstance(typeof(TDoc));
            var xField = docNavigator.SelectChildren(XPathNodeType.Element);
            while (xField.MoveNext())
            {
                var fieldNode = xField.Current;
                var fieldNodeName = fieldNode.SelectSingleNode("@name").Value;
                PropertyInfo field = null;
                if (queryDesc.DynamicFieldKeys.Any(x => x.Value == fieldNodeName))
                {
                    fieldNodeName = queryDesc.DynamicFieldKeys.First(x => x.Value == fieldNodeName).Key;
                }
                if (_documentFields.ContainsKey(fieldNodeName))
                {
                    field = _documentFields[fieldNodeName];
                }
                else
                {
                    continue;
                }
                if (field.PropertyType.IsGenericType && field.PropertyType.GetInterface("IEnumerable") != null)
                {
                    var genericArguments = field.PropertyType.GetGenericArguments();
                    var argumentType = genericArguments[0];
                    var listType = typeof(LinkedList<>);
                    var list = field.GetValue(doc, null);
                    if (list == null)
                    {
                        list = Activator.CreateInstance(listType.MakeGenericType(argumentType));
                        field.SetValue(doc, list, null);
                    }
                    if (fieldNode.Name == "arr")
                    {
                        var xArr = fieldNode.SelectChildren(XPathNodeType.Element);
                        while (xArr.MoveNext())
                        {
                            var arrNode = xArr.Current;
                            var value = arrNode.Value;
                            field.PropertyType.InvokeMember("Add", BindingFlags.InvokeMethod, null, list, new object[] { Convert.ChangeType(value, argumentType) });
                        }
                    }
                    else
                    {
                        var value = fieldNode.Value;
                        field.PropertyType.InvokeMember("Add", BindingFlags.InvokeMethod, null, list, new object[] { Convert.ChangeType(value, argumentType) });
                    }
                }
                else
                {
                    var type = field.PropertyType;
                    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        type = type.GetGenericArguments()[0];
                    }
                    var value = fieldNode.Value;
                    if (!string.IsNullOrEmpty(value))
                    {
                        field.SetValue(doc, Convert.ChangeType(value, type, new CultureInfo("en-US")), null);
                    }
                }

            }

            return (TDoc)doc;
        }
    }
}
