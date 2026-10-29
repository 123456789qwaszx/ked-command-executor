using System.Collections.Generic;
using UnityEngine;

// 이 샘플의 "무대". 커맨드가 다룰 도형을 만들고 이름으로 찾아 준다.
// 실행기는 이 클래스를 모른다 — 팩토리가 대상을 찾아 커맨드 생성자로 넘긴다.
public sealed class DemoStage
{
    public sealed class Shape
    {
        public Transform Transform { get; }
        public SpriteRenderer Renderer { get; }

        private readonly Vector3 _home;
        private readonly Color _color;

        public Shape(SpriteRenderer renderer)
        {
            Renderer = renderer;
            Transform = renderer.transform;
            _home = Transform.position;
            _color = renderer.color;
        }

        public void ResetToHome()
        {
            Transform.position = _home;
            Renderer.color = _color;
        }
    }

    public const float AreaY = 2.4f;
    public const float MoveTargetX = 1.5f;

    private static Sprite _square;

    private readonly Dictionary<string, Shape> _shapes = new();

    public DemoStage(Transform root)
    {
        CreateQuad(root, "Execution Area", new Vector3(0f, AreaY, 0.1f), new Vector2(13f, 3.4f),
            new Color(0.15f, 0.17f, 0.21f), sortingOrder: -1);

        Add(root, "A", new Vector3(-4.5f, AreaY, 0f), new Color(0.98f, 0.62f, 0.22f));
        Add(root, "B", new Vector3(3.5f, AreaY, 0f), new Color(0.30f, 0.74f, 0.90f));
    }

    public IEnumerable<KeyValuePair<string, Shape>> Shapes => _shapes;

    public bool TryGet(string id, out Shape shape)
    {
        shape = null;
        return id != null && _shapes.TryGetValue(id, out shape);
    }

    public void ResetAll()
    {
        foreach (Shape shape in _shapes.Values)
            shape.ResetToHome();
    }

    private void Add(Transform root, string id, Vector3 position, Color color)
    {
        SpriteRenderer renderer = CreateQuad(root, id, position, Vector2.one * 1.1f, color, sortingOrder: 0);
        _shapes.Add(id, new Shape(renderer));
    }

    private static SpriteRenderer CreateQuad(
        Transform root, string name, Vector3 position, Vector2 size, Color color, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, worldPositionStays: false);
        go.transform.position = position;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = Square();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    // 에셋 없이 1×1 유닛 사각형 스프라이트를 만든다 (whiteTexture는 4×4).
    private static Sprite Square()
    {
        if (_square == null)
            _square = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);

        return _square;
    }
}
