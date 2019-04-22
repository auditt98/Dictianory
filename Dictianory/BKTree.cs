using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dictianory
{
    public class BKTree
    {
        private Node _Root;

        public void Add(string word)
        {
            word = word.ToLower();
            if (_Root == null)
            {
                _Root = new Node(word);
                return;
            }

            var curNode = _Root;

            var dist = LevenshteinDistance(curNode.Word, word);
            while (curNode.ContainsKey(dist))
            {
                if (dist == 0) return;

                curNode = curNode[dist];
                dist = LevenshteinDistance(curNode.Word, word);
            }

            curNode.AddChild(dist, word);
        }

        public List<string> Search(string word, int d)
        {
            var rtn = new List<string>();
            word = word.ToLower();

            RecursiveSearch(_Root, rtn, word, d);

            return rtn;
        }

        private void RecursiveSearch(Node node, List<string> rtn, string word, int d)
        {
            var curDist = LevenshteinDistance(node.Word, word);
            var minDist = curDist - d;
            var maxDist = curDist + d;

            if (curDist <= d)
                rtn.Add(node.Word);

            foreach (var key in node.Keys.Cast<int>().Where(key => minDist <= key && key <= maxDist))
            {
                RecursiveSearch(node[key], rtn, word, d);
            }
        }

        public static int LevenshteinDistance(string first, string second)
        {
            if (first.Length == 0) return second.Length;
            if (second.Length == 0) return first.Length;

            var lenFirst = first.Length;
            var lenSecond = second.Length;

            var d = new int[lenFirst + 1, lenSecond + 1];

            for (var i = 0; i <= lenFirst; i++)
                d[i, 0] = i;

            for (var i = 0; i <= lenSecond; i++)
                d[0, i] = i;

            for (var i = 1; i <= lenFirst; i++)
            {
                for (var j = 1; j <= lenSecond; j++)
                {
                    var match = (first[i - 1] == second[j - 1]) ? 0 : 1;

                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + match);
                }
            }
            return d[lenFirst, lenSecond];
        }
    }

    public class Node
    {
        public string Word { get; set; }
        public HybridDictionary Children { get; set; }

        public Node() { }

        public Node(string word)
        {
            this.Word = word.ToLower();
        }

        public Node this[int key]
        {
            get { return (Node)Children[key]; }
        }

        public ICollection Keys
        {
            get
            {
                if (Children == null)
                    return new ArrayList();
                return Children.Keys;
            }
        }

        public bool ContainsKey(int key)
        {
            return Children != null && Children.Contains(key);
        }

        public void AddChild(int key, string word)
        {
            if (this.Children == null)
                Children = new HybridDictionary();
            this.Children[key] = new Node(word);
        }
    }
}
