using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LTWM
{
    public class WindowTree
    {
        public Node? head = null;

        public class Node
        {
            public bool wasVisitedLastTick = true;

            public IntPtr? handle = 0;

            public float leftRatio = 0.5f;
            public Win32.Rect rect = new Win32.Rect();

            public NodeType type = NodeType.None;
            public Node? left = null;
            public Node? right = null;

            public Node Parent;
            public string Name = "";

            public Node() { }

            public Node(IntPtr handle, NodeType type, Node left, Node right)
            {
                this.handle = handle;
                this.type = type;
                this.left = left;
                this.right = right;
            }
        }

        public enum NodeType
        {
            None, VerticalSplit, HorizontalSplit, Window,
        }

        public Node? FindWindow(IntPtr handle)
        {
            return FindNode(head, handle);
        }

        private Node? FindNode(Node node, IntPtr handle)
        {
            if (node.handle == handle)
            {
                return node;
            }
            else
            {
                if (node.left != null)
                {
                    var left = FindNode(node.left, handle);
                    if (left != null)
                    {
                        return left;
                    }
                }

                if (node.right != null)
                {
                    var right = FindNode(node.right, handle);
                    if (right != null)
                    {
                        return right;
                    }
                }

                return null;
            }
        }

        public Node? FindWindowLeftOf(Node node)
        {
            if (node.Parent == null) return null;

            if (node.Parent.type == NodeType.HorizontalSplit)
            {
                if (node.Parent.right == node)
                {
                    return node.Parent.left;
                }

                if (node.Parent.left == node)
                {
                    if (node.Parent.Parent.left == null) return null;

                    if (node.Parent.Parent.left.type == NodeType.Window)
                    {
                        return node.Parent.Parent.left;
                    }

                    if (node.Parent.Parent.left.type == NodeType.HorizontalSplit)
                    {
                        return node.Parent.Parent.left.right;
                    }
                }
            }

            return null;
        }

        public Node? FindWindowRightOf(Node node)
        {
            if (node.Parent == null) return null;

            if (node.Parent.type == NodeType.HorizontalSplit)
            {
                if (node.Parent.left == node)
                {
                    if (node.Parent.right != null)
                    {
                        if (node.Parent.right.type == NodeType.Window) return node.Parent.right;
                        if (node.Parent.right.type == NodeType.HorizontalSplit) return node.Parent.right.left;
                    }

                    if (node.Parent.Parent.left == null) return null;
                    if (node.Parent.Parent.left.type == NodeType.Window) return node.Parent.Parent.left;
                    if (node.Parent.Parent.left.type == NodeType.HorizontalSplit) return node.Parent.Parent.left.right;
                }

                if (node.Parent.right == node)
                {
                    if (node.Parent.Parent.right == null) return null;
                    if (node.Parent.Parent.right.type == NodeType.Window) return node.Parent.Parent.right;
                    if (node.Parent.Parent.right.type == NodeType.HorizontalSplit) return node.Parent.Parent.right.left;
                }
            }

            return null;
        }


        private NodeType lastNodeType = NodeType.VerticalSplit;
        public void addWindow(IntPtr handle, Node? sibling)
        {
            if (sibling == null)
            {
                head = new Node();
                head.handle = handle;
                head.type = NodeType.Window;
                head.Name = App.GetWindowName(handle);
                return;
            }

            var node = new Node();
            node.handle = sibling.handle;
            node.type = NodeType.Window;
            node.Parent = sibling;
            node.Name = sibling.Name;
            sibling.left = node;

            var new_node = new Node();
            new_node.handle = handle;
            new_node.type = NodeType.Window;
            new_node.Parent = sibling;
            new_node.Name = App.GetWindowName(handle);
            sibling.right = new_node;


            sibling.handle = null;

            NodeType type = lastNodeType == NodeType.VerticalSplit ? NodeType.HorizontalSplit : NodeType.VerticalSplit;

            sibling.type = type;
            lastNodeType = type;
        }

        public void RemoveWindow(IntPtr handle)
        {
            var node = FindWindow(handle);
            if (node == null) return;


        }

        public Node? FindClosestWindow(Win32.Rect win_rect)
        {
            if (head == null) return null;

            return isInsideRectRecursive(head, win_rect).Item1;
        }

        private (Node?, bool) isInsideRectRecursive(Node node, Win32.Rect rect)
        {
            var (left, left_res) = node.left != null ? isInsideRectRecursive(node.left, rect) : (null, false);
            var (right, right_res) = node.right != null ? isInsideRectRecursive(node.right, rect) : (null, false);

            if (left_res) return (left, true);
            if (right_res) return (right, true);

            if (node.type == NodeType.Window)
            {
                if (node.rect.left < rect.left && node.rect.top < rect.top && node.rect.right > rect.left && node.rect.bottom > rect.top)
                {
                    return (node, true);
                }
            }

            return (null, false);
        }

        public enum ResizedOrMoved
        {
            None,
            Resized,
            Moved,
        }

        public ResizedOrMoved WasResizedOrMoved(Node node, Win32.Rect rect)
        {
            int[] arr = new int[4];

            arr[0] = Convert.ToInt32(node.rect.left != rect.left);
            arr[1] = Convert.ToInt32(node.rect.top != rect.top);
            arr[2] = Convert.ToInt32(node.rect.right != rect.right);
            arr[3] = Convert.ToInt32(node.rect.bottom != rect.bottom);

            int sum = arr.Sum();

            if (sum == 1 || sum == 2) return ResizedOrMoved.Resized;
            if (sum == 3 || sum == 4) return ResizedOrMoved.Moved;

            return 0;
        }

        public Node FindParentHorizontalSplit(Node node)
        {
            //if (node.Parent == null) return null;
            if (node.Parent.type == NodeType.HorizontalSplit) return node.Parent;
            return FindParentHorizontalSplit(node.Parent);
        }

        public override string ToString()
        {
            return ToStringRecursive(head);
        }

        private string ToStringRecursive(Node? node)
        {
            if (node == null) return "";
            return $"{node.Name} {node.type} {ToStringRecursive(node.left)} {ToStringRecursive(node.right)}";
        }


        public void RemoveUnvisitedNodes()
        {
            RemoveUnvisitedNodesRecursive(head);
        }

        private void RemoveUnvisitedNodesRecursive(Node? node)
        {
            if (node == null) return;

            if (node.left != null) RemoveUnvisitedNodesRecursive(node.left);
            if (node.right != null) RemoveUnvisitedNodesRecursive(node.right);

            if (node.type == NodeType.Window)
            {
                if (!node.wasVisitedLastTick)
                {
                    var grandparent = node.Parent.Parent;

                    var isLeft = node == node.Parent.left;
                    var toKeep = isLeft ? node.Parent.right : node.Parent.left;

                    if (grandparent.right == node.Parent)
                    {
                        grandparent.right = toKeep;
                    }
                    else
                    {
                        grandparent.left = toKeep;
                    }

                    if (isLeft)
                    {
                        node.Parent.left = null;
                    }
                    else
                    {
                        node.Parent.right = null;
                    }

                    toKeep = null;
                }
                else
                {
                    node.wasVisitedLastTick = false;
                }
            }
        }
    }
}
