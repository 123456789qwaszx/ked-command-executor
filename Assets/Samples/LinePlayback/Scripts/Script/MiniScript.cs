using System;
using System.Collections.Generic;

// Yarn의 "컴파일된 노드" 흉내 — 대본 한 덩어리를 명령 줄과 대사 줄의 열로 펴 둔 것.
//
// 실제 Yarn과 같은 규칙을 지킨다: 명령은 실행기로 바로 가지 않는다. 러너가 핸들러(브리지)에 넘기면
// 브리지는 스펙으로 쌓아 두기만 하고, 쌓인 스펙은 **다음 대사 줄에 들어갈 때** 한 스텝으로 재생된다.
// 그래서 "줄 하나 = 스텝 하나"이고, 줄 경계가 곧 스텝 수명의 경계다.
//
// 문법(흉내라서 작다):
//   <<name arg arg ...>>        명령
//   Speaker: text #line:id      대사 (#line이 없으면 lineN을 붙인다)
//   // ...                      주석
public enum MiniInstructionKind { Command, Line }

public sealed class MiniInstruction
{
    public MiniInstructionKind Kind;
    public int SourceLine;

    // Command
    public string Name;
    public string[] Args;

    // Line
    public string LineId;
    public string Speaker;
    public string Text;
}

public static class MiniScriptParser
{
    private const string LineTag = " #line:";

    public static List<MiniInstruction> Parse(string source)
    {
        var list = new List<MiniInstruction>();

        if (string.IsNullOrEmpty(source))
            return list;

        string[] rows = source.Replace("\r\n", "\n").Split('\n');
        int autoId = 0;

        for (int i = 0; i < rows.Length; i++)
        {
            string row = rows[i].Trim();

            if (row.Length == 0 || row.StartsWith("//", StringComparison.Ordinal))
                continue;

            if (row.StartsWith("<<", StringComparison.Ordinal) && row.EndsWith(">>", StringComparison.Ordinal))
            {
                string[] tokens = row.Substring(2, row.Length - 4)
                    .Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

                if (tokens.Length == 0)
                    continue;

                var args = new string[tokens.Length - 1];
                Array.Copy(tokens, 1, args, 0, args.Length);

                list.Add(new MiniInstruction
                {
                    Kind = MiniInstructionKind.Command,
                    SourceLine = i + 1,
                    Name = tokens[0],
                    Args = args,
                });
                continue;
            }

            list.Add(ParseLine(row, i + 1, ref autoId));
        }

        return list;
    }

    private static MiniInstruction ParseLine(string row, int sourceLine, ref int autoId)
    {
        string lineId = null;
        int tag = row.IndexOf(LineTag, StringComparison.Ordinal);

        if (tag >= 0)
        {
            lineId = row.Substring(tag + LineTag.Length).Trim();
            row = row.Substring(0, tag).TrimEnd();
        }

        if (string.IsNullOrEmpty(lineId))
            lineId = $"line{++autoId}";

        string speaker = null;
        string text = row;
        int colon = row.IndexOf(": ", StringComparison.Ordinal);

        if (colon > 0 && colon <= 16)
        {
            speaker = row.Substring(0, colon);
            text = row.Substring(colon + 2);
        }

        return new MiniInstruction
        {
            Kind = MiniInstructionKind.Line,
            SourceLine = sourceLine,
            LineId = lineId,
            Speaker = speaker,
            Text = text,
        };
    }
}
