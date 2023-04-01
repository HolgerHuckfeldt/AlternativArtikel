using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Absolute.SolrClient
{
    /// <summary>
    ///     String-Dictionary that automatically escapes inserted strings using the lucene escaping mechanism
    /// </summary>
    public class SolrEscapedDictonary : ISolrEscapedDictonary 
    {
        private IDictionary<string,string> _dictionary;

        public SolrEscapedDictonary()
            :this(new Dictionary<string,string>())
        {
        }

        public SolrEscapedDictonary(IDictionary<string,string> dictionary)
        {
            _dictionary = dictionary;
        } 
        
        public void Add(string key, string value)
        {
            var escapedValue = Escape(value);
            _dictionary.Add(key, escapedValue);
        }

        public bool ContainsKey(string key)
        {
            return _dictionary.ContainsKey(key);
        }

        public ICollection<string> Keys
        {
            get { return _dictionary.Keys; }
        }

        public bool Remove(string key)
        {
            return _dictionary.Remove(key);
        }

        public bool TryGetValue(string key, out string value)
        {
            return _dictionary.TryGetValue(key, out value);
        }

        public ICollection<string> Values
        {
            get { return _dictionary.Values; }
        }

        public string this[string key]
        {
            get
            {
                return _dictionary[key];
            }
            set
            {
               var escapedValue = Escape(value);
               _dictionary[key] = escapedValue;
            }
        }

        public void Add(KeyValuePair<string, string> item)
        {
            var escapedItem = new KeyValuePair<string, string>(item.Key, Escape(item.Value));
            _dictionary.Add(escapedItem);
        }

        public void Clear()
        {
            _dictionary.Clear();
        }

        public bool Contains(KeyValuePair<string, string> item)
        {
            return _dictionary.Contains(item) || _dictionary.Contains(new KeyValuePair<string, string>(item.Key, Escape(item.Value)));
        }

        public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
        {
            _dictionary.CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return _dictionary.Count; }
        }

        public bool IsReadOnly
        {
            get { return _dictionary.IsReadOnly; }
        }

        public bool Remove(KeyValuePair<string, string> item)
        {
            return _dictionary.Remove(item);
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return _dictionary.GetEnumerator();                                                                                                                                                                                                
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _dictionary.GetEnumerator();
        }

        protected virtual string Escape(string input)
        {
            return Escape_Intern(input);
        }

        public void AddUnescaped(string key, string value)
        {
            _dictionary.Add(key, value);
        }

        public static string Escape_Intern(string s)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in s)
            {
                if (c == '\\' || c == '+' || c == '-' || c == '!' || c == '(' || c == ')' || c == ':' || c == '^' || c == '[' || c == ']' || c == '"' || c == '{' || c == '}' || c == '~' || c == '*' || c == '?' || c == '|' || c == '&')
                {
                    sb.Append('\\');
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

    }
}
