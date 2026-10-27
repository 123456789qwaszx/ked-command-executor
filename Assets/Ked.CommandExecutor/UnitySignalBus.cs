using System;

public sealed class UnitySignalBus : ISignalBus
{
    public event Action<string> OnSignal;

    public void Raise(string key) => OnSignal?.Invoke(key);
}