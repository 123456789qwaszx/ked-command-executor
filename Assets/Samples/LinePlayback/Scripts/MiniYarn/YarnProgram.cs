using System;
using System.Collections.Generic;
using UnityEngine;

namespace MiniYarn
{
    // 컴파일된 Yarn 프로그램 흉내. .yarn 문법의 아주 작은 부분집합을 읽는다:
    //
    //   title: NodeName
    //   ---
    //   <<command arg arg>>               명령
    //   Name: text #tag #line:id          대사 (#line이 없으면 line:<노드>-<순번>을 붙인다)
    //   // comment
    //   ===
    //
    // 선택지(->) · 변수 · jump는 없다 — 이 샘플은 줄과 명령의 생명주기만 본다.
    public sealed class YarnProgram
    {
        public sealed class Node
        {
            public string Name;
            public readonly List<object> Content = new(); // Line | Command
        }

        private readonly Dictionary<string, Node> _nodes = new(StringComparer.Ordinal);

        public bool TryGetNode(string name, out Node node)
        {
            node = null;
            return name != null && _nodes.TryGetValue(name, out node);
        }

        public static YarnProgram Parse(string source, string sourceName)
        {
            var program = new YarnProgram();

            if (string.IsNullOrEmpty(source))
                return program;

            string[] rows = source.Replace("\r\n", "\n").Split('\n');

            Node node = null;
            string title = null;
            bool inBody = false;
            bool warnedOptions = false;

            for (int i = 0; i < rows.Length; i++)
            {
                string row = rows[i].Trim();

                if (!inBody)
                {
                    if (row.StartsWith("title:", StringComparison.Ordinal))
                        title = row.Substring(6).Trim();
                    else if (row == "---")
                    {
                        node = new Node { Name = title ?? $"Node{program._nodes.Count}" };
                        inBody = true;
                    }

                    continue;
                }

                if (row == "===")
                {
                    program._nodes[node.Name] = node;
                    node = null;
                    title = null;
                    inBody = false;
                    continue;
                }

                if (row.Length == 0 || row.StartsWith("//", StringComparison.Ordinal))
                    continue;

                if (row.StartsWith("<<", StringComparison.Ordinal) && row.EndsWith(">>", StringComparison.Ordinal))
                {
                    node.Content.Add(new Command { Text = row.Substring(2, row.Length - 4).Trim() });
                    continue;
                }

                if (row.StartsWith("->", StringComparison.Ordinal))
                {
                    if (!warnedOptions)
                        Debug.LogWarning($"[MiniYarn] {sourceName}:{i + 1} options (->) are not supported by this sample - skipped.");

                    warnedOptions = true;
                    continue;
                }

                node.Content.Add(ParseLine(row, node.Name, node.Content.Count));
            }

            if (inBody && node != null)
                program._nodes[node.Name] = node;

            return program;
        }

        // 끝에 붙은 #태그들을 떼어 낸다. #line:id는 줄 ID로, 나머지는 메타데이터로.
        private static Line ParseLine(string row, string nodeName, int index)
        {
            string id = null;
            var metadata = new List<string>();

            string[] tokens = row.Split(' ');
            int textEnd = tokens.Length;

            while (textEnd > 0 && tokens[textEnd - 1].StartsWith("#", StringComparison.Ordinal))
                textEnd--;

            for (int t = textEnd; t < tokens.Length; t++)
            {
                string tag = tokens[t].Substring(1);

                if (tag.StartsWith("line:", StringComparison.Ordinal))
                    id = tag;
                else if (tag.Length > 0)
                    metadata.Add(tag);
            }

            return new Line
            {
                ID = id ?? $"line:{nodeName}-{index:000}",
                RawText = string.Join(" ", tokens, 0, textEnd),
                Metadata = metadata.ToArray(),
            };
        }
    }
}
