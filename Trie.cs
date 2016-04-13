/*
 * Tim Davis #1332245
 * Project Assignment #4
 * Trie.cs
 * Contains Trie class and Node class
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebRole
{
    // Trie class
    public class Trie
    {
        private Node root;

        // Trie constructor
        public Trie()
        {
            this.root = new Node('.');
        }

        // CREATING THE TRIE
        // Trie add, adds a title to the trie structure
        public void Add(string title)
        {
            title = title.ToLower() + Node.Eow;
            Node currentNode = root;
            foreach (char let in title)
            {
                currentNode = currentNode.GetOrAddChild(let);
            }
        }

        // SEARCHING THE TRIE
        // finds a list of maxCount titles and returns it
        // uses FindWordsHelper recursive method
        public List<string> FindWords(string prefix, int maxCount)
        {
            prefix = prefix.ToLower();
            Node startNode = root;
            foreach (char let in prefix)
            {
                startNode = startNode.GetOrAddChild(let);
            }

            List<string> prefixedWords = new List<string>();

            FindWordsHelper(startNode, prefixedWords, prefix, maxCount);

            return prefixedWords;
        }

        // recursive trie traversal helper
        private void FindWordsHelper(Node node, List<string> words, string prefix, int maxCount)
        {
            foreach (Node n in node.children)
            {
                // check if list of words is full
                if (words.Count() >= maxCount)
                {
                    break;
                }

                // list isn't full, keep searching
                if (n.letter == Node.Eow)
                {
                    words.Add(prefix);
                }
                else
                {
                    FindWordsHelper(n, words, prefix + n.letter, maxCount);
                }
            }
        }


        // Node class, contained in Trie class
        public class Node
        {
            public const char Eow = '$';

            public char letter;
            public List<Node> children;

            public Node(char let)
            {
                this.letter = let;
                this.children = new List<Node>();
            }

            // Get child if it exists, otherwise create child
            // Return: child
            public Node GetOrAddChild(char let)
            {
                // GetChild:
                // first check children for let
                foreach (Node c in children)
                {
                    if (c.letter == let)
                    {
                        return c;
                    }
                }

                // AddChild:
                // children doesn't contain let child, create it
                Node child = new Node(let);
                children.Add(child);
                return child;
            }
        }
    }
}
