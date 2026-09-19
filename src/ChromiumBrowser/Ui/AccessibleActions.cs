namespace ChromiumBrowser.Ui;

/// <summary>Expose individually painted controls to Windows accessibility clients.</summary>
internal sealed class AccessibleActions : Control.ControlAccessibleObject
{
    internal sealed record Item(string Name, Rectangle Bounds, Action Invoke,
        AccessibleRole Role = AccessibleRole.PushButton, AccessibleStates State = AccessibleStates.None);

    private readonly Control _owner;
    private readonly Func<IReadOnlyList<Item>> _items;

    public AccessibleActions(Control owner, Func<IReadOnlyList<Item>> items) : base(owner)
    {
        _owner = owner;
        _items = items;
    }

    public override int GetChildCount() => _items().Count + _owner.Controls.Count;
    public override AccessibleObject? GetChild(int index)
    {
        int count = _items().Count;
        if (index < 0 || index >= GetChildCount()) return null;
        return index < count ? new Child(this, index) : _owner.Controls[index - count].AccessibilityObject;
    }

    public override AccessibleObject? HitTest(int x, int y)
    {
        for (int i = 0; i < GetChildCount(); i++)
        {
            AccessibleObject child = GetChild(i)!;
            if (child.Bounds.Contains(x, y)) return child;
        }
        return base.HitTest(x, y);
    }

    private sealed class Child(AccessibleActions parent, int index) : AccessibleObject
    {
        private Item? Current => parent._items().ElementAtOrDefault(index);
        public override string? Name { get => Current?.Name; set { } }
        public override AccessibleObject Parent => parent;
        public override Rectangle Bounds => Current is { } item
            ? parent._owner.RectangleToScreen(item.Bounds) : Rectangle.Empty;
        public override AccessibleRole Role => Current?.Role ?? AccessibleRole.None;
        public override AccessibleStates State => Current is { } item
            ? item.State | AccessibleStates.Focusable
                | (item.Bounds.IsEmpty ? AccessibleStates.Offscreen : AccessibleStates.None)
            : AccessibleStates.Unavailable;
        public override string DefaultAction => Current?.Name ?? string.Empty;
        public override void DoDefaultAction()
        {
            if (Current is { } item && !item.State.HasFlag(AccessibleStates.Unavailable)) item.Invoke();
        }
    }
}
