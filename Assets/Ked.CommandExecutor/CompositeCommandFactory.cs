using UnityEngine;

public sealed class CompositeCommandFactory : INodeCommandFactory
{
    private readonly INodeCommandFactory[] _factories;

    public CompositeCommandFactory(params INodeCommandFactory[] factories)
    {
        _factories = factories;
    }

    public bool TryCreate(CommandSpecBase spec, out ISequenceCommand command)
    {
        for (int i = 0; i < _factories.Length; i++)
        {
            if (_factories[i].TryCreate(spec, out command))
                return true;
        }

        command = null;
        Debug.LogWarning(
            $"[CompositeCommandFactory] No factory handled spec: {spec.GetType().Name} " +
            $"(spec='{spec}')");
        return false;
    }
}