﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using PhyloTree.Extensions;
using PhyloTree.Formats;
using System.Diagnostics.CodeAnalysis;
using System.Security;
using System.Threading;

[assembly: SuppressMessage("Globalization", "CA1303")]

/// <summary>
/// Contains classes and methods to read, write and manipulate phylogenetic trees.
/// </summary>
namespace PhyloTree
{
    /// <summary>
    /// Represents a split induced by a branch in a tree.
    /// </summary>
    internal class Split
    {
        /// <summary>
        /// The name of the split. It consists of a series of comma-separated tip names, optionally followed by a vertical bar | and by another series of comma-separated tip names.
        /// E.g. "A,B|C,D"
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The length of the split (representing either the age of the node that induced it or the length of the branch).
        /// </summary>
        public double Length { get; }

        /// <summary>
        /// The support value for the split.
        /// </summary>
        public double Support { get; }

        /// <summary>
        /// Determines whether the <see cref="Length"/> property contains the age of the node that induced the split or the length of the branch.
        /// </summary>
        public LengthTypes LengthType { get; }


        /// <summary>
        /// Determines whether the <see cref="Length"/> of the split contains the age of the node that induced the split or the length of the branch.
        /// </summary>
        public enum LengthTypes
        {
            /// <summary>
            /// The <see cref="Split.Length"/> of the split contains the length of the branch that induced it.
            /// </summary>
            Length,
            /// <summary>
            /// The <see cref="Split.Length"/> of the split contains the age of the node that induced it.
            /// </summary>
            Age
        }

        /// <summary>
        /// Creates a <see cref="Split"/> object.
        /// </summary>
        /// <param name="name">The name of the split.</param>
        /// <param name="length">The length of the split.</param>
        /// <param name="lengthType">Determines whether <paramref name="length"/> contains the age of the node that induced the split or the length of the branch.</param>
        /// <param name="support">The support value of the split.</param>
        public Split(string name, double length, LengthTypes lengthType, double support)
        {
            this.Name = name;
            this.Length = length;
            this.Support = support;
            this.LengthType = lengthType;
        }

        /// <summary>
        /// Determines whether two <see cref="Split"/>s are compatible with each other.
        /// </summary>
        /// <param name="s1">The first <see cref="Split"/> to compare.</param>
        /// <param name="s2">The second <see cref="Split"/> to compare.</param>
        /// <returns><c>true</c> if the two <see cref="Split"/>s are compatible with each other, <c>false</c> if they are not.</returns>
        public static bool AreCompatible(Split s1, Split s2)
        {
            if (!s1.Name.Contains("|", StringComparison.OrdinalIgnoreCase) && !s2.Name.Contains("|", StringComparison.OrdinalIgnoreCase))
            {
                string[] leaves1 = s1.Name.Split(',');
                string[] leaves2 = s2.Name.Split(',');

                return !leaves1.ContainsAny(leaves2) || leaves1.ContainsAll(leaves2) || leaves2.ContainsAll(leaves1);
            }
            else
            {
                string[][] leaves1 = (from el in s1.Name.Split('|') select el.Split(',')).ToArray();
                string[][] leaves2 = (from el in s2.Name.Split('|') select el.Split(',')).ToArray();

                if (leaves1.Length == 2 && leaves2.Length == 2)
                {
                    return !leaves1[0].Intersect(leaves2[0]).Any() || !leaves1[0].Intersect(leaves2[1]).Any() || !leaves1[1].Intersect(leaves2[0]).Any() || !leaves1[1].Intersect(leaves2[1]).Any();
                }
                else if (leaves1.Length == 1 && leaves2.Length == 2)
                {
                    return (!leaves1[0].ContainsAny(leaves2[0]) || leaves1[0].ContainsAll(leaves2[0]) || leaves2[0].ContainsAll(leaves1[0])) && (!leaves1[0].ContainsAny(leaves2[1]) || leaves1[0].ContainsAll(leaves2[1]) || leaves2[1].ContainsAll(leaves1[0]));
                }
                else if (leaves1.Length == 2 && leaves2.Length == 1)
                {
                    return (!leaves2[0].ContainsAny(leaves1[0]) || leaves2[0].ContainsAll(leaves1[0]) || leaves1[0].ContainsAll(leaves2[0])) && (!leaves2[0].ContainsAny(leaves1[1]) || leaves2[0].ContainsAll(leaves1[1]) || leaves1[1].ContainsAll(leaves2[0]));
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }

        /// <summary>
        /// Gets the children on the left of the split.
        /// </summary>
        /// <returns>The children on the left of the split.</returns>
        public IEnumerable<string> GetChildrenLeft()
        {
            if (this.Name.Contains('|', StringComparison.OrdinalIgnoreCase))
            {
                return this.Name.Split('|')[0].Split(',');
            }
            else
            {
                return this.Name.Split(',');
            }
        }

        /// <summary>
        /// Gets the children on the right of the split.
        /// </summary>
        /// <returns>The children on the right of the split.</returns>
        public IEnumerable<string> GetChildrenRight()
        {
            if (this.Name.Contains('|', StringComparison.OrdinalIgnoreCase))
            {
                return this.Name.Split('|')[1].Split(',');
            }
            else
            {
                return this.Name.Split(',');
            }
        }

        /// <summary>
        /// Determines whether multiple <see cref="Split"/>s are compatible with each other.
        /// </summary>
        /// <param name="splits">The <see cref="Split"/>s to compare.</param>
        /// <returns><c>true</c> if all the <see cref="Split"/>s are compatible with each other, <c>false</c> if the are not.</returns>
        public bool IsCompatible(IEnumerable<Split> splits)
        {
            foreach (Split split in splits)
            {
                if (!AreCompatible(split, this))
                {
                    return false;
                }
            }
            return true;
        }


        /// <summary>
        /// Builds a rooted or unrooted tree starting from a collection of compatible <see cref="Split"/>s.
        /// </summary>
        /// <param name="splits">The <see cref="Split"/>s to use in building the tree. This method assumes that they are all compatible with each other.</param>
        /// <param name="rooted">Whether to build a rooted or an unrooted tree.</param>
        /// <returns>A <see cref="TreeNode"/> containing the tree represented by the <see cref="Split"/>s.</returns>
        public static TreeNode BuildTree(IEnumerable<Split> splits, bool rooted)
        {
            List<TreeNode> nodes = new List<TreeNode>();

            List<Split> allSplits = splits.ToList();

            HashSet<string> addedTips = new HashSet<string>();

            string[][] splitChildrenLeft = new string[allSplits.Count][];
            string[][] splitChildrenRight = new string[allSplits.Count][];

            bool clockLike = true;

            for (int i = 0; i < allSplits.Count; i++)
            {
                if (allSplits[i].LengthType != Split.LengthTypes.Age)
                {
                    clockLike = false;
                }

                splitChildrenLeft[i] = allSplits[i].GetChildrenLeft().ToArray();
                splitChildrenRight[i] = allSplits[i].GetChildrenRight().ToArray();
                if (splitChildrenLeft[i].Length == 1)
                {
                    if (addedTips.Add(splitChildrenLeft[i][0]))
                    {
                        TreeNode child = new TreeNode(null) { Name = splitChildrenLeft[i][0], Support = allSplits[i].Support, Length = allSplits[i].Length };
                        nodes.Add(child);
                    }
                }
                if (splitChildrenRight[i].Length == 1)
                {
                    if (addedTips.Add(splitChildrenRight[i][0]))
                    {
                        TreeNode child = new TreeNode(null) { Name = splitChildrenRight[i][0], Support = allSplits[i].Support, Length = allSplits[i].Length };
                        nodes.Add(child);
                    }
                }

                if (!rooted && splitChildrenLeft[i].Length > splitChildrenRight[i].Length)
                {
                    string[] temp = splitChildrenLeft[i];
                    splitChildrenLeft[i] = splitChildrenRight[i];
                    splitChildrenRight[i] = temp;
                }
            }

            int maxCoalescence = nodes.Count;

            for (int i = 2; i <= maxCoalescence; i++)
            {
                Coalesce(splitChildrenLeft, nodes, allSplits, i);
            }


            if (clockLike)
            {
                nodes = nodes[0].GetChildrenRecursive();

                for (int i = nodes.Count - 1; i >= 0; i--)
                {
                    if (nodes[i].Parent != null)
                    {
                        nodes[i].Length = nodes[i].Parent.Length - nodes[i].Length;
                    }
                    else
                    {
                        nodes[i].Length = double.NaN;
                    }
                }
            }

            if (nodes[0].Children.Count < 3 && !rooted)
            {
                nodes[0] = nodes[0].GetUnrootedTree();
            }

            return nodes[0];
        }

        /// <summary>
        /// Coalesces nodes as commanded by the supplies list of <see cref="Split"/>s.
        /// </summary>
        /// <param name="splitChildrenLeft">The tips specified on the left side of each split.</param>
        /// <param name="nodes">The list of <see cref="TreeNode"/>s that will be coalesced.</param>
        /// <param name="allSplits">The list of <see cref="Split"/>s describing the tree.</param>
        /// <param name="level">The level at which to coalesce. This method should be invoked multiple times, with <paramref name="level"/> increasing from 2 up to the number of tips in the tree (inclusive).</param>
        private static void Coalesce(string[][] splitChildrenLeft, List<TreeNode> nodes, List<Split> allSplits, int level)
        {
            for (int i = 0; i < allSplits.Count; i++)
            {
                if (splitChildrenLeft[i].Length == level)
                {
                    List<TreeNode> currChildren = new List<TreeNode>();
                    for (int j = 0; j < nodes.Count; j++)
                    {
                        if (splitChildrenLeft[i].ContainsAll(nodes[j].GetLeafNames()))
                        {
                            currChildren.Add(nodes[j]);
                        }
                    }
                    if (currChildren.Count > 1)
                    {
                        TreeNode parent = new TreeNode(null) { Support = allSplits[i].Support, Length = allSplits[i].Length };
                        parent.Children.AddRange(currChildren);

                        foreach (TreeNode node in currChildren)
                        {
                            nodes.Remove(node);
                            node.Parent = parent;
                        }

                        nodes.Add(parent);
                    }
                    else if (currChildren.Count > 0)
                    {
                        currChildren[0].Support = Math.Min(currChildren[0].Support, allSplits[i].Support);

                        if (allSplits[i].LengthType == LengthTypes.Length)
                        {
                            currChildren[0].Length = currChildren[0].Length + allSplits[i].Length;
                        }
                        else
                        {
                            currChildren[0].Length = allSplits[i].Length;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Represents a node in a tree (or a whole tree).
    /// </summary>
    [Serializable]
    public partial class TreeNode
    {
        /// <summary>
        /// The parent node of this node. This will be <c>null</c> for the root node.
        /// </summary>
        public TreeNode Parent { get; set; }

        /// <summary>
        /// The child nodes of this node. This will be empty (but initialised) for leaf nodes.
        /// </summary>
        public List<TreeNode> Children { get; }

        /// <summary>
        /// The attributes of this node. Attributes <see cref="Name"/>, <see cref="Length"/> and <see cref="Support"/> are always included. See the respective properties for default values.
        /// </summary>
        public AttributeDictionary Attributes { get; } = new AttributeDictionary();

        /// <summary>
        /// The length of the branch leading to this node. This is <c>double.NaN</c> for branches whose length is not specified (e.g. the root node).
        /// </summary>
        public double Length
        {
            get
            {
                return Attributes.Length;
            }
            set
            {
                Attributes.Length = value;
            }
        }

        /// <summary>
        /// The support value of this node. This is <c>double.NaN</c> for branches whose support is not specified. The interpretation of the support value depends on how the tree was built.
        /// </summary>
        public double Support
        {
            get
            {
                return Attributes.Support;
            }
            set
            {
                Attributes.Support = value;
            }
        }

        /// <summary>
        /// The name of this node (e.g. the species name for leaf nodes). Default is <c>""</c>.
        /// </summary>
        public string Name
        {
            get
            {
                return Attributes.Name;
            }
            set
            {
                Attributes.Name = value;
            }
        }


        /// <summary>
        /// A univocal identifier for the node.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Creates a new <see cref="TreeNode"/> object.
        /// </summary>
        /// <param name="parent">The parent node of this node. For the root node, this should be <c>null</c>.</param>
        public TreeNode(TreeNode parent)
        {
            Parent = parent;
            Id = Guid.NewGuid().ToString();
            Children = new List<TreeNode>();
        }

        /// <summary>
        /// Checks whether the node belongs to a rooted tree.
        /// </summary>
        /// <returns><c>true</c> if the node belongs to a rooted tree, <c>false</c> otherwise.</returns>
        public bool IsRooted()
        {
            return this.GetRootNode().Children.Count < 3;
        }

        /// <summary>
        /// Get an unrooted version of the tree.
        /// </summary>
        /// <returns>A <see cref="TreeNode"/> containing the unrooted tree, having at least 3 children.</returns>
        public TreeNode GetUnrootedTree()
        {
            //A tree is unrooted if the root node has at least 3 children
            if (this.Children.Count >= 3)
            {
                //If the tree is already unrooted, just return a clone
                return this.Clone();
            }
            else
            {
                //At this point, assume that the root node has 2 children

                //If the second child of the root node is not a leaf node (i.e. it has at least 2 children), we can take the first child of the root node and graft it onto the second child; the second child will now have 3 children and will be the root node of the unrooted tree
                if (this.Children[1].Children.Count >= 2)
                {
                    TreeNode child1 = this.Children[1].Clone();
                    TreeNode child0 = this.Children[0].Clone();
                    child0.Parent = child1;
                    child0.Length += child1.Length;
                    child1.Children.Add(child0);
                    child1.Parent = null;
                    child1.Length = double.NaN;
                    child1.Name = this.Name;
                    return child1;
                }
                else
                {
                    //If the second child of the root node is a leaf node, then the first child must not be a leaf node; thus we do the same as before, but swapping the two children 
                    TreeNode child0 = this.Children[1].Clone();
                    TreeNode child1 = this.Children[0].Clone();
                    child0.Parent = child1;
                    child0.Length += child1.Length;
                    child1.Children.Add(child0);
                    child1.Parent = null;
                    child1.Length = double.NaN;
                    child1.Name = this.Name;
                    return child1;
                }
            }
        }

        /// <summary>
        /// Get a version of the tree that is rooted at the specified point of the branch leading to the <paramref name="outgroup"/>.
        /// </summary>
        /// <param name="outgroup">The outgroup to be used when rooting the tree.</param>
        /// <param name="position">The (relative) position on the branch connecting the outgroup to the rest of the tree on which to place the root.</param>
        /// <returns>A <see cref="TreeNode"/> containing the rooted tree.</returns>
        public TreeNode GetRootedTree(TreeNode outgroup, double position = 0.5)
        {
            if (outgroup != null && outgroup.Parent != null)
            {
                TreeNode subject;

                if (this.Children.Count < 3)
                {
                    subject = this.GetUnrootedTree();
                }
                else
                {
                    subject = this.Clone();
                }

                outgroup = subject.GetNodeFromId(outgroup.Id);

                if (outgroup != null && outgroup.Parent != null)
                {

                    position = outgroup.Length * position;

                    TreeNode tbr = new TreeNode(null);

                    TreeNode outGroup2 = outgroup.Clone();
                    outGroup2.Parent = tbr;
                    outGroup2.Length = position;
                    tbr.Children.Add(outGroup2);

                    TreeNode otherChild = outgroup.Parent.Invert(outgroup);
                    otherChild.Parent = tbr;
                    otherChild.Length = outgroup.Length - position;
                    tbr.Children.Add(otherChild);

                    foreach (KeyValuePair<string, object> attribute in this.Attributes)
                    {
                        tbr.Attributes[attribute.Key] = attribute.Value;
                    }

                    tbr.Name = this.Name;

                    return tbr;
                }
                else
                {
                    return this.Clone();
                }
            }
            else
            {
                return this.Clone();
            }
        }

        internal TreeNode Invert(TreeNode ignoredChild)
        {
            if (this.Children.Count < 2)
            {
                return this.Clone();
            }
            else
            {
                TreeNode nd = new TreeNode(null);
                foreach (TreeNode chd in this.Children)
                {
                    if (chd != ignoredChild)
                    {
                        TreeNode chd2 = chd.Clone();
                        chd2.Parent = nd;
                        nd.Children.Add(chd2);
                    }
                }

                if (this.Parent != null)
                {
                    TreeNode prnt = this.Parent.Invert(this);
                    prnt.Parent = nd;

                    foreach (KeyValuePair<string, object> attribute in this.Attributes)
                    {
                        prnt.Attributes[attribute.Key] = attribute.Value;
                    }

                    prnt.Name = this.Name;
                    prnt.Length = this.Length;
                    prnt.Support = this.Support;

                    nd.Children.Add(prnt);
                }
                else
                {
                    foreach (KeyValuePair<string, object> attribute in this.Attributes)
                    {
                        nd.Attributes[attribute.Key] = attribute.Value;
                    }

                    nd.Name = this.Name;
                    nd.Length = this.Length;
                    nd.Support = this.Support;
                }

                return nd;
            }
        }

        /// <summary>
        /// Recursively clone a <see cref="TreeNode"/> object.
        /// </summary>
        /// <returns>The cloned <see cref="TreeNode"/></returns>
        public TreeNode Clone()
        {
            TreeNode nd = new TreeNode(this.Parent)
            {
                Id = this.Id,
                Name = this.Name
            };

            foreach (TreeNode nd2 in this.Children)
            {
                TreeNode nd22 = nd2.Clone();
                nd22.Parent = nd;
                nd.Children.Add(nd22);
            }

            nd.Length = this.Length;
            nd.Support = this.Support;

            foreach (KeyValuePair<string, object> kvp in this.Attributes)
            {
                nd.Attributes[kvp.Key] = kvp.Value;
            }

            return nd;
        }

        /// <summary>
        /// Recursively get all the nodes that descend from this node.
        /// </summary>
        /// <returns>A <see cref="List{T}"/> of <see cref="TreeNode"/> objects, containing the nodes that descend from this node.</returns>
        public List<TreeNode> GetChildrenRecursive()
        {
            List<TreeNode> tbr = new List<TreeNode>
            {
                this
            };

            for (int i = 0; i < this.Children.Count; i++)
            {
                tbr.AddRange(this.Children[i].GetChildrenRecursive());
            }
            return tbr;
        }

        /// <summary>
        /// Lazily recursively get all the nodes that descend from this node.
        /// </summary>
        /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="TreeNode"/> objects, containing the nodes that descend from this node.</returns>
        public IEnumerable<TreeNode> GetChildrenRecursiveLazy()
        {
            yield return this;

            for (int i = 0; i < this.Children.Count; i++)
            {
                foreach (TreeNode t in this.Children[i].GetChildrenRecursiveLazy())
                {
                    yield return t;
                }
            }
        }

        /// <summary>
        /// Get all the leaves that descend (directly or indirectly) from this node.
        /// </summary>
        /// <returns>A <see cref="List{T}"/> of <see cref="TreeNode"/> objects, containing the leaves that descend from this node.</returns>
        public List<TreeNode> GetLeaves()
        {
            List<TreeNode> tbr = new List<TreeNode>();

            if (this.Children.Count == 0)
            {
                tbr.Add(this);
            }

            for (int i = 0; i < this.Children.Count; i++)
            {
                tbr.AddRange(this.Children[i].GetLeaves());
            }
            return tbr;
        }

        /// <summary>
        /// Get the names of all the leaves that descend (directly or indirectly) from this node.
        /// </summary>
        /// <returns>A <see cref="List{T}"/> of <see cref="string"/>s, containing the names of the leaves that descend from this node.</returns>
        public List<string> GetLeafNames()
        {
            List<string> tbr = new List<string>();

            if (this.Children.Count == 0 && !string.IsNullOrEmpty(this.Name))
            {
                tbr.Add(this.Name);
            }

            for (int i = 0; i < this.Children.Count; i++)
            {
                tbr.AddRange(this.Children[i].GetLeafNames());
            }
            return tbr;
        }

        /// <summary>
        /// Get the names of all the named nodes that descend (directly or indirectly) from this node.
        /// </summary>
        /// <returns>A <see cref="List{T}"/> of <see cref="string"/>s, containing the names of the named nodes that descend from this node.</returns>
        public List<string> GetNodeNames()
        {
            List<string> tbr = new List<string>();

            if (!string.IsNullOrEmpty(this.Name))
            {
                tbr.Add(this.Name);
            }

            for (int i = 0; i < this.Children.Count; i++)
            {
                tbr.AddRange(this.Children[i].GetNodeNames());
            }
            return tbr;
        }

        /// <summary>
        /// Get the child node with the specified name.
        /// </summary>
        /// <param name="nodeName">The name of the node to search.</param>
        /// <returns>The <see cref="TreeNode"/> object with the specified name, or <c>null</c> if no node with such name exists.</returns>
        public TreeNode GetNodeFromName(string nodeName)
        {
            if (this.Name == nodeName)
            {
                return this;
            }

            for (int i = 0; i < this.Children.Count; i++)
            {
                TreeNode item = this.Children[i].GetNodeFromName(nodeName);
                if (item != null)
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>
        /// Get the child node with the specified Id.
        /// </summary>
        /// <param name="nodeId">The Id of the node to search.</param>
        /// <returns>The <see cref="TreeNode"/> object with the specified Id, or <c>null</c> if no node with such Id exists.</returns>
        public TreeNode GetNodeFromId(string nodeId)
        {
            if (this.Id == nodeId)
            {
                return this;
            }

            for (int i = 0; i < this.Children.Count; i++)
            {
                TreeNode item = this.Children[i].GetNodeFromId(nodeId);
                if (item != null)
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>
        /// Get the sum of the branch lengths from this node up to the root.
        /// </summary>
        /// <returns>The sum of the branch lengths from this node up to the root.</returns>
        public double UpstreamLength()
        {
            double tbr = 0;

            TreeNode nd = this;

            while (nd.Parent != null)
            {
                tbr += nd.Length;
                nd = nd.Parent;
            }

            return tbr;
        }

        /// <summary>
        /// Get the sum of the branch lengths from this node down to the leaves of the tree. If the tree is not clock-like, the length of the longest path is returned.
        /// </summary>
        /// <returns>The sum of the branch lengths from this node down to the leaves of the tree. If the tree is not clock-like, the length of the longest path is returned.</returns>
        public double LongestDownstreamLength()
        {
            if (this.Children.Count == 0)
            {
                return 0;
            }
            else
            {
                double maxLen = 0;
                for (int i = 0; i < this.Children.Count; i++)
                {
                    double chLen = this.Children[i].LongestDownstreamLength() + this.Children[i].Length;

                    maxLen = Math.Max(maxLen, chLen);
                }

                return maxLen;
            }
        }

        /// <summary>
        /// Get the sum of the branch lengths from this node down to the leaves of the tree. If the tree is not clock-like, the length of the shortest path is returned.
        /// </summary>
        /// <returns>The sum of the branch lengths from this node down to the leaves of the tree. If the tree is not clock-like, the length of the shortest path is returned.</returns>
        public double ShortestDownstreamLength()
        {
            if (this.Children.Count == 0)
            {
                return 0;
            }
            else
            {
                double minLen = double.MaxValue;
                for (int i = 0; i < this.Children.Count; i++)
                {
                    double chLen = this.Children[i].ShortestDownstreamLength() + this.Children[i].Length;

                    minLen = Math.Min(minLen, chLen);
                }

                return minLen;
            }
        }

        /// <summary>
        /// Get the node of the tree from which all other nodes descend.
        /// </summary>
        /// <returns>The node of the tree from which all other nodes descend</returns>
        public TreeNode GetRootNode()
        {
            TreeNode parent = this;
            while (parent.Parent != null)
            {
                parent = parent.Parent;
            }
            return parent;
        }

        /// <summary>
        /// Describes the relationship between two nodes.
        /// </summary>
        public enum NodeRelationship
        {
            /// <summary>
            /// The relationship between the nodes is unknown.
            /// </summary>
            Unknown,

            /// <summary>
            /// The first node is an ancestor of the second node.
            /// </summary>
            Ancestor,

            /// <summary>
            /// The first node is a descendant of the second node.
            /// </summary>
            Descendant,

            /// <summary>
            /// The two nodes are relatives (i.e. they share a common ancestor which is neither one of them).
            /// </summary>
            Relatives
        }

        /// <summary>
        /// Get the sum of the branch lengths from this node to the specified node.
        /// </summary>
        /// <param name="otherNode">The node that should be reached</param>
        /// <param name="nodeRelationship">A value indicating how this node is related to <paramref name="otherNode"/>.</param>
        /// <returns>The sum of the branch lengths from this node to the specified node.</returns>
        public double PathLengthTo(TreeNode otherNode, NodeRelationship nodeRelationship = NodeRelationship.Unknown)
        {
            Contract.Requires(otherNode != null);

            TreeNode LCA = null;

            if (this == otherNode)
            {
                return 0;
            }

            if (nodeRelationship == NodeRelationship.Unknown)
            {
                List<TreeNode> myChildren = this.GetChildrenRecursive();
                if (myChildren.Contains(otherNode))
                {
                    nodeRelationship = NodeRelationship.Ancestor;
                }
                else
                {
                    List<TreeNode> otherChildren = otherNode.GetChildrenRecursive();
                    if (otherChildren.Contains(this))
                    {
                        nodeRelationship = NodeRelationship.Descendant;
                    }
                    else
                    {
                        LCA = GetLastCommonAncestor(new TreeNode[] { this, otherNode });

                        if (LCA != null)
                        {
                            nodeRelationship = NodeRelationship.Relatives;
                        }
                        else
                        {
                            throw new InvalidOperationException("The two nodes do not belong to the same tree!");
                        }
                    }
                }
            }

            switch (nodeRelationship)
            {
                case NodeRelationship.Relatives:
                    return LCA.PathLengthTo(this, NodeRelationship.Ancestor) + LCA.PathLengthTo(otherNode, NodeRelationship.Ancestor);
                case NodeRelationship.Ancestor:
                    for (int i = 0; i < this.Children.Count; i++)
                    {
                        if (this.Children[i] == otherNode)
                        {
                            return this.Children[i].Length;
                        }
                        else if (this.Children[i].GetChildrenRecursive().Contains(otherNode))
                        {
                            return this.Children[i].Length + this.Children[i].PathLengthTo(otherNode, NodeRelationship.Ancestor);
                        }
                    }
                    throw new InvalidOperationException("Unexpected code path!");
                case NodeRelationship.Descendant:
                    return otherNode.PathLengthTo(this, NodeRelationship.Ancestor);
                default:
                    throw new InvalidOperationException("The two nodes do not belong to the same tree!");
            }
        }


        /// <summary>
        /// Get the sum of the branch lengths of this node and all its descendants.
        /// </summary>
        /// <returns>The sum of the branch lengths of this node and all its descendants.</returns>
        public double TotalLength()
        {
            double tbr = this.Length;

            for (int i = 0; i < this.Children.Count; i++)
            {
                tbr += this.Children[i].TotalLength();
            }
            return tbr;
        }

        /// <summary>
        /// Sort (in place) the nodes in the tree in an aesthetically pleasing way.
        /// </summary>
        /// <param name="descending">The way the nodes should be sorted.</param>
        public void SortNodes(bool descending)
        {
            for (int i = 0; i < this.Children.Count; i++)
            {
                this.Children[i].SortNodes(descending);
            }

            if (this.Children.Count > 0)
            {
                this.Children.Sort((a, b) =>
                {
                    int val = (a.GetLevels(true)[1] - b.GetLevels(true)[1]) * (descending ? 1 : -1);
                    if (val != 0)
                    {
                        return val;
                    }
                    else
                    {
                        return string.Compare(a.GetLeafNames()[0], b.GetLeafNames()[0], StringComparison.InvariantCulture);
                    }
                });
            }
        }

        /// <summary>
        /// Determine how many levels there are in the tree above and below this node.
        /// </summary>
        /// <param name="ignoreTotal">If this is <c>true</c>, the total number of levels is not computed (this improves performance).</param>
        /// <returns>
        /// An <see cref="int"/> array with 3 elements: the first element is the number of levels above this node, the second element is the number of levels below this node, and the third element is the total number of levels in the tree.
        /// If <paramref name="ignoreTotal"/> is <c>true</c>, the third element is equal to the second.
        /// </returns>
        internal int[] GetLevels(bool ignoreTotal = false)
        {
            int upperCount = 0;
            TreeNode prnt = this.Parent;
            TreeNode lastPrnt = null;
            while (prnt != null)
            {
                lastPrnt = prnt;
                upperCount++;
                prnt = prnt.Parent;
            }

            int lowerCount = 0;
            if (this.Children.Count > 0)
            {
                for (int i = 0; i < this.Children.Count; i++)
                {
                    TreeNode ch = this.Children[i];
                    lowerCount = Math.Max(lowerCount, 1 + ch.GetLevels(true)[1]);
                }
            }

            if (this.Parent != null && !ignoreTotal)
            {
                return new int[] { upperCount, lowerCount, lastPrnt.GetLevels()[2] };
            }
            else
            {
                return new int[] { upperCount, lowerCount, lowerCount };
            }

        }

        /// <summary>
        /// Convert the tree to a Newick string.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return NWKA.WriteTree(this, false, true);
        }

        /// <summary>
        /// Determines whether the tree is clock-like (i.e. all tips are contemporaneous) or not.
        /// </summary>
        /// <param name="tolerance">The (relative) tolerance when comparing branch lengths.</param>
        /// <returns>A boolean value determining whether the tree is clock-like or not</returns>
        public bool IsClockLike(double tolerance = 0.001)
        {
            List<TreeNode> leaves = this.GetLeaves();

            double len = leaves[0].UpstreamLength();

            foreach (TreeNode leaf in leaves)
            {
                if (Math.Abs(leaf.UpstreamLength() / len - 1) > tolerance)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets the last common ancestor of all the specified nodes, or <c>null</c> if the tree doesn't contain all the nodes.
        /// </summary>
        /// <param name="monophyleticConstraint">The collection of nodes whose last common ancestor is to be determined.</param>
        /// <returns>The last common ancestor of all the specified nodes, or <c>null</c> if the tree doesn't contain all the nodes.</returns>
        public static TreeNode GetLastCommonAncestor(IEnumerable<TreeNode> monophyleticConstraint)
        {
            if (monophyleticConstraint.Any())
            {
                TreeNode seed = monophyleticConstraint.ElementAt(0);

                while (seed != null && !seed.GetChildrenRecursive().ContainsAll(monophyleticConstraint))
                {
                    seed = seed.Parent;
                }

                return seed;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the last common ancestor of all the nodes with the specified names, or <c>null</c> if the tree doesn't contain all the named nodes.
        /// </summary>
        /// <param name="monophyleticConstraint">The collection of names representing nodes whose last common ancestor is to be determined.</param>
        /// <returns>The last common ancestor of all the nodes with the specified names, or <c>null</c> if the tree doesn't contain all the named nodes.</returns>
        public TreeNode GetLastCommonAncestor(params string[] monophyleticConstraint)
        {
            return this.GetLastCommonAncestor((IEnumerable<string>)monophyleticConstraint);
        }

        /// <summary>
        /// Gets the last common ancestor of all the nodes with the specified names, or <c>null</c> if the tree doesn't contain all the named nodes.
        /// </summary>
        /// <param name="monophyleticConstraint">The collection of names representing nodes whose last common ancestor is to be determined.</param>
        /// <returns>The last common ancestor of all the nodes with the specified names, or <c>null</c> if the tree doesn't contain all the named nodes.</returns>
        public TreeNode GetLastCommonAncestor(IEnumerable<string> monophyleticConstraint)
        {
            if (monophyleticConstraint.Any())
            {
                TreeNode seed = this.GetNodeFromName(monophyleticConstraint.ElementAt(0));

                while (seed != null && !seed.GetNodeNames().ContainsAll(monophyleticConstraint))
                {
                    seed = seed.Parent;
                }

                return seed;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Checks whether this node is the last common ancestor of all the nodes with the specified names.
        /// </summary>
        /// <param name="monophyleticConstraint">The collection of names representing nodes whose last common ancestor is to be determined.</param>
        /// <returns><c>true</c> if this node is the last common ancestor of all the nodes with the specified names, <c>false</c> otherwise.</returns>
        public bool IsLastCommonAncestor(IEnumerable<string> monophyleticConstraint)
        {
            if (monophyleticConstraint.Any())
            {
                TreeNode seed = this.GetNodeFromName(monophyleticConstraint.ElementAt(0));

                while (seed != null && !seed.GetNodeNames().ContainsAll(monophyleticConstraint))
                {
                    seed = seed.Parent;
                }

                return seed == this;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Transform the tree into a collection of (undirected) splits.
        /// </summary>
        /// <param name="lengthType">Determines whether the <see cref="Split.Length"/> should represent ages or branch lenghts.</param>
        /// <returns>A list of splits induced by the tree. Each split corresponds to a branch in the tree.</returns>
        internal List<Split> GetSplits(Split.LengthTypes lengthType)
        {
            List<Split> tbr = new List<Split>();

            List<TreeNode> nodes = this.GetChildrenRecursive();

            double totalTreeLength = this.LongestDownstreamLength();

            if (this.Children.Count == 2)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    List<string> nodeLeaves = nodes[i].GetLeafNames();
                    nodeLeaves.Sort();

                    tbr.Add(new Split(nodeLeaves.Aggregate((a, b) => a + "," + b), lengthType == Split.LengthTypes.Length ? nodes[i].Length : (totalTreeLength - nodes[i].UpstreamLength()), lengthType, 1));
                }
            }
            else
            {
                List<string> allLeaves = this.GetLeafNames();

                for (int i = 0; i < nodes.Count; i++)
                {
                    List<string> nodeLeaves = nodes[i].GetLeafNames();

                    List<string> diffLeaves = (from el in allLeaves where !nodeLeaves.Contains(el) select el).ToList();

                    nodeLeaves.Sort();
                    diffLeaves.Sort();

                    if (diffLeaves.Count > 0)
                    {
                        List<string> splitTerminals = new List<string>() { nodeLeaves.Aggregate((a, b) => a + "," + b), diffLeaves.Aggregate((a, b) => a + "," + b) };
                        splitTerminals.Sort();

                        tbr.Add(new Split(splitTerminals.Aggregate((a, b) => a + "|" + b), lengthType == Split.LengthTypes.Length ? nodes[i].Length : ((totalTreeLength - nodes[i].UpstreamLength()) + nodes[i].Length), lengthType, 1));
                    }
                    else
                    {
                        tbr.Add(new Split(nodeLeaves.Aggregate((a, b) => a + "," + b), lengthType == Split.LengthTypes.Length ? nodes[i].Length : (totalTreeLength - nodes[i].UpstreamLength()), lengthType, 1));
                    }
                }

            }
            return tbr;
        }

        /// <summary>
        /// Gets the split corresponding to the branch underlying this node. If this is an internal node, <c>side1</c> will contain all the leaves in the tree except those descending from this node, and <c>side2</c>
        /// will contain all the leaves descending from this node. If this is the root <c>side1</c> will be empty and <c>side2</c> will contain all the leaves in the tree. If the tree is rooted (the root node has exactly
        /// 2 children), <c>side1</c> will contain in all cases an additional <see langword="null"/> element.
        /// </summary>
        /// <returns>The leaves on the two sides of the split.</returns>
        public (List<TreeNode> side1, List<TreeNode> side2) GetSplit()
        {
            if (this.Parent == null)
            {
                if (this.Children.Count == 2)
                {
                    return (new List<TreeNode>() { null }, this.GetLeaves());
                }
                else
                {
                    return (new List<TreeNode>(), this.GetLeaves());
                }
            }
            else
            {
                List<TreeNode> side2 = this.GetLeaves();

                TreeNode parent = this.Parent;

                while (parent.Parent != null)
                {
                    parent = parent.Parent;
                }

                List<TreeNode> side1 = parent.GetLeaves();
                side1.RemoveAll(x => side2.Contains(x));

                if (parent.Children.Count == 2)
                {
                    side1.Add(null);
                }

                return (side1, side2);
            }
        }

        /// <summary>
        /// Gets all the splits in the tree.
        /// </summary>
        /// <returns>An <see cref="IEnumerable{T}"/> that enumerates all the splits in the tree.</returns>
        public IEnumerable<(List<TreeNode> side1, List<TreeNode> side2, double branchLength)> GetSplits()
        {
            foreach (TreeNode node in this.GetChildrenRecursive())
            {
                (List<TreeNode> side1, List<TreeNode> side2) = node.GetSplit();

                if (node.Parent == null)
                {
                    yield return (side1, side2, 0);
                }
                else
                {
                    yield return (side1, side2, node.Length);
                }
            }
        }



        /// <summary>
        /// Prunes the current node from the tree.
        /// </summary>
        /// <param name="leaveParent">This value determines what happens to the parent node of the current node if it only has two children (i.e., the current node and another node). If this is <see langword="false"/>, the parent node is also pruned; if it is <see langword="true"/>, the parent node is left untouched.</param>
        /// <remarks>Note that the node is pruned in-place; however, the return value of this method should be used, because pruning the node may have caused the root of the tree to move.</remarks>
        /// <returns>The <see cref="TreeNode"/> corresponding to the root of the tree after the current node has been pruned.</returns>
        public TreeNode Prune(bool leaveParent)
        {
            if (this.Parent == null)
            {
                return new TreeNode(null);
            }

            this.Parent.Children.Remove(this);

            if (!leaveParent)
            {
                if (this.Parent.Children.Count == 1)
                {
                    TreeNode parent = this.Parent;
                    TreeNode otherChild = this.Parent.Children[0];
                    if (parent.Parent != null)
                    {
                        int index = parent.Parent.Children.IndexOf(parent);
                        parent.Parent.Children[index] = otherChild;
                        otherChild.Length += parent.Length;
                        otherChild.Parent = parent.Parent;

                        while (parent.Parent != null)
                        {
                            parent = parent.Parent;
                        }

                        return parent;
                    }
                    else
                    {
                        if (parent.Length > 0)
                        {
                            otherChild.Length += parent.Length;
                        }

                        otherChild.Parent = null;
                        return otherChild;
                    }
                }
                else
                {
                    TreeNode parent = this.Parent;

                    while (parent.Parent != null)
                    {
                        parent = parent.Parent;
                    }

                    return parent;
                }
            }
            else
            {
                TreeNode parent = this.Parent;

                while (parent.Parent != null)
                {
                    parent = parent.Parent;
                }

                return parent;
            }
        }

        /// <summary>
        /// Prunes a node from the tree.
        /// </summary>
        /// <param name="nodeToPrune">The node that should be pruned.</param>
        /// <param name="leaveParent">This value determines what happens to the parent node of the pruned node if it only has two children (i.e., the pruned node and another node). If this is <see langword="false"/>, the parent node is also pruned; if it is <see langword="true"/>, the parent node is left untouched.</param>
        /// <remarks>Note that the node is pruned in-place; however, the return value of this method should be used, because pruning the node may have caused the root of the tree to move.</remarks>
        /// <returns>The <see cref="TreeNode"/> corresponding to the root of the tree after the <paramref name="nodeToPrune"/> has been pruned.</returns>
        public TreeNode Prune(TreeNode nodeToPrune, bool leaveParent)
        {
            if (nodeToPrune == null)
            {
                if (this.Parent != null)
                {
                    TreeNode parent = this.Parent;

                    while (parent.Parent != null)
                    {
                        parent = parent.Parent;
                    }

                    return parent;
                }
                else
                {
                    return this;
                }
            }

            return nodeToPrune.Prune(leaveParent);
        }

        /// <summary>
        /// Creates a lower triangular distance matrix, where each entry is the path length distance between two leaves in the tree. Entries are in the same order as returned by the <see cref="GetLeaves"/> method.
        /// </summary>
        /// <param name="maxDegreeOfParallelism">Maximum number of threads to use, or -1 to let the runtime decide. If this argument is set to 0 (the default), the value used is 1 for trees with 1500 or fewer leaves, or -1 for larger trees.</param>
        /// <param name="progressCallback">A method used to report progress.</param>
        /// <returns>A <see cref="T:double[][]"/> jagged array containing the distance matrix.</returns>
        public double[][] CreateDistanceMatrixDouble(int maxDegreeOfParallelism = 0, Action<double> progressCallback = null)
        {
            List<TreeNode> leaves = this.GetLeaves();
            List<TreeNode> nodes = this.GetChildrenRecursive();

            if (maxDegreeOfParallelism == 0)
            {
                if (leaves.Count <= 1500)
                {
                    maxDegreeOfParallelism = 1;
                }
                else
                {
                    maxDegreeOfParallelism = -1;
                }
            }

            HashSet<int>[] ancestors = new HashSet<int>[nodes.Count];
            HashSet<int>[] descendants = new HashSet<int>[nodes.Count];
            int[] leafIndices = new int[leaves.Count];

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Parent != null)
                {
                    int parentIndex = nodes.IndexOf(nodes[i].Parent);
                    ancestors[i] = new HashSet<int>(ancestors[parentIndex]);
                    ancestors[i].Add(i);
                }
                else
                {
                    ancestors[i] = new HashSet<int>() { i };
                }

                if (nodes[i].Children.Count == 0)
                {
                    leafIndices[leaves.IndexOf(nodes[i])] = i;
                }
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                descendants[i] = new HashSet<int>();
            }

            for (int i = 0; i < leaves.Count; i++)
            {
                foreach (int j in ancestors[leafIndices[i]])
                {
                    descendants[j].Add(i);
                }
            }

            double[][] tbr = new double[leaves.Count][];

            for (int i = 0; i < tbr.Length; i++)
            {
                tbr[i] = new double[i];
            }

            if (maxDegreeOfParallelism == 1)
            {
                for (int i = 1; i < nodes.Count; i++)
                {
                    double length = nodes[i].Length;

                    for (int k = 0; k < leaves.Count; k++)
                    {
                        if (!descendants[i].Contains(k))
                        {
                            foreach (int j in descendants[i])
                            {
                                tbr[Math.Max(j, k)][Math.Min(j, k)] += length;
                            }
                        }
                    }

                    progressCallback?.Invoke((double)i / (nodes.Count - 1));
                }
            }
            else
            {
                int progress = 0;
                object progressLock = new object();

                System.Threading.Tasks.Parallel.For(1, nodes.Count, new System.Threading.Tasks.ParallelOptions() { MaxDegreeOfParallelism = maxDegreeOfParallelism }, i =>
                {
                    double length = nodes[i].Length;

                    for (int k = 0; k < leaves.Count; k++)
                    {
                        if (!descendants[i].Contains(k))
                        {
                            foreach (int j in descendants[i])
                            {
                                Add(ref tbr[Math.Max(j, k)][Math.Min(j, k)], length);
                            }
                        }
                    }

                    if (progressCallback != null)
                    {
                        lock (progressLock)
                        {
                            progress++;
                            progressCallback.Invoke((double)progress / (nodes.Count - 1));
                        }
                    }
                });
            }

            return tbr;
        }

        /// <summary>
        /// Creates a lower triangular distance matrix, where each entry is the path length distance between two leaves in the tree. Entries are in the same order as returned by the <see cref="GetLeaves"/> method.
        /// </summary>
        /// <param name="maxDegreeOfParallelism">Maximum number of threads to use, or -1 to let the runtime decide. If this argument is set to 0 (the default), the value used is 1 for trees with 1500 or fewer leaves, or -1 for larger trees.</param>
        /// <param name="progressCallback">A method used to report progress.</param>
        /// <returns>A <see cref="T:float[][]"/> jagged array containing the distance matrix.</returns>
        public float[][] CreateDistanceMatrixFloat(int maxDegreeOfParallelism = 0, Action<double> progressCallback = null)
        {
            List<TreeNode> leaves = this.GetLeaves();
            List<TreeNode> nodes = this.GetChildrenRecursive();

            if (maxDegreeOfParallelism == 0)
            {
                if (leaves.Count <= 1500)
                {
                    maxDegreeOfParallelism = 1;
                }
                else
                {
                    maxDegreeOfParallelism = -1;
                }
            }

            HashSet<int>[] ancestors = new HashSet<int>[nodes.Count];
            HashSet<int>[] descendants = new HashSet<int>[nodes.Count];
            int[] leafIndices = new int[leaves.Count];

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Parent != null)
                {
                    int parentIndex = nodes.IndexOf(nodes[i].Parent);
                    ancestors[i] = new HashSet<int>(ancestors[parentIndex]);
                    ancestors[i].Add(i);
                }
                else
                {
                    ancestors[i] = new HashSet<int>() { i };
                }

                if (nodes[i].Children.Count == 0)
                {
                    leafIndices[leaves.IndexOf(nodes[i])] = i;
                }
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                descendants[i] = new HashSet<int>();
            }

            for (int i = 0; i < leaves.Count; i++)
            {
                foreach (int j in ancestors[leafIndices[i]])
                {
                    descendants[j].Add(i);
                }
            }

            float[][] tbr = new float[leaves.Count][];

            for (int i = 0; i < tbr.Length; i++)
            {
                tbr[i] = new float[i];
            }

            if (maxDegreeOfParallelism == 1)
            {
                for (int i = 1; i < nodes.Count; i++)
                {
                    float length = (float)nodes[i].Length;

                    for (int k = 0; k < leaves.Count; k++)
                    {
                        if (!descendants[i].Contains(k))
                        {
                            foreach (int j in descendants[i])
                            {
                                tbr[Math.Max(j, k)][Math.Min(j, k)] += length;
                            }
                        }
                    }

                    progressCallback?.Invoke((double)i / (nodes.Count - 1));
                }
            }
            else
            {
                int progress = 0;
                object progressLock = new object();

                System.Threading.Tasks.Parallel.For(1, nodes.Count, new System.Threading.Tasks.ParallelOptions() { MaxDegreeOfParallelism = maxDegreeOfParallelism }, i =>
                {
                    float length = (float)nodes[i].Length;

                    for (int k = 0; k < leaves.Count; k++)
                    {
                        if (!descendants[i].Contains(k))
                        {
                            foreach (int j in descendants[i])
                            {
                                Add(ref tbr[Math.Max(j, k)][Math.Min(j, k)], length);
                            }
                        }
                    }

                    if (progressCallback != null)
                    {
                        lock (progressLock)
                        {
                            progress++;
                            progressCallback.Invoke((double)progress / (nodes.Count - 1));
                        }
                    }
                });
            }

            return tbr;
        }

        /// <summary>
        /// Interlocked add for double, from https://stackoverflow.com/a/16893641.
        /// </summary>
        private static double Add(ref double location1, double value)
        {
            double newCurrentValue = location1; // non-volatile read, so may be stale
            while (true)
            {
                double currentValue = newCurrentValue;
                double newValue = currentValue + value;
                newCurrentValue = Interlocked.CompareExchange(ref location1, newValue, currentValue);
                if (newCurrentValue.Equals(currentValue))
                {
                    return newValue;
                }
            }
        }

        /// <summary>
        /// Interlocked add for float, adapted from https://stackoverflow.com/a/16893641.
        /// </summary>
        private static float Add(ref float location1, float value)
        {
            float newCurrentValue = location1; // non-volatile read, so may be stale
            while (true)
            {
                float currentValue = newCurrentValue;
                float newValue = currentValue + value;
                newCurrentValue = Interlocked.CompareExchange(ref location1, newValue, currentValue);
                if (newCurrentValue.Equals(currentValue))
                {
                    return newValue;
                }
            }
        }

    }
}
