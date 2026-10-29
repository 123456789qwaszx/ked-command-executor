// Ked.Presentation.Core.StageCommand 흉내 — 이름 + 문자열 인자. 생략된 인자는 null이고, 생략값은 파서의 표가 갖는다.
public readonly struct StageCommand
{
    public readonly string Name;
    private readonly string[] _args;

    public StageCommand(string name, string[] args)
    {
        Name = name;
        _args = args ?? System.Array.Empty<string>();
    }

    public int Count => _args.Length;

    public string Arg(int index, string fallback = null)
        => index < _args.Length && !string.IsNullOrEmpty(_args[index]) ? _args[index] : fallback;

    // 어느 자리든 "wait"가 있으면 그 명령을 기다린다(CommandSpecBase.wait).
    public bool HasWaitToken()
    {
        for (int i = 0; i < _args.Length; i++)
        {
            if (_args[i] == "wait")
                return true;
        }

        return false;
    }
}
