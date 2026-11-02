using System.Reflection;
using MegaCrit.Sts2.Core.Runs;

public class ActRunProxy : DispatchProxy
{
    public int ActIndex;
    protected override object? Invoke(MethodInfo? method, object?[]? args) =>
        method!.Name == "get_CurrentActIndex" ? ActIndex : method.Invoke(NullRunState.Instance, args);
}
