using System.Collections.Generic;

namespace WagahighChoices.Toa.X11
{
    public class QueryTreeResult
    {
        public uint Root { get; }
        public uint Parent { get; }
        public IReadOnlyList<uint> Children { get; }

        public QueryTreeResult(uint root, uint parent, IReadOnlyList<uint> children)
        {
            this.Root = root;
            this.Parent = parent;
            this.Children = children;
        }
    }
}
