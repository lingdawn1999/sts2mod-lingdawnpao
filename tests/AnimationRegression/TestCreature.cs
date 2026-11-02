using MegaCrit.Sts2.Core.Nodes.Combat;

// Keep the actual patched trigger method while omitting unrelated UI setup.
public partial class TestCreature : NCreature
{
    public override void _Ready() { }
    public override void _ExitTree() { }
}
