using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Tab 메뉴 디자인에 쓰는 둥근 패널·버튼·아이콘 스프라이트를 코드로 그려 PNG로 저장한다.
// 모든 좌표는 UI 단위(캔버스 1단위)이며, 선명하게 보이도록 2배 해상도로 그린다.
public static class CozySpriteFactory
{
    public const string Folder = "Assets/UI/Cozy/Generated";

    // UI 1단위당 픽셀 수
    private const int Scale = 2;

    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    // ---------------------------------------------------------------
    // 색
    // ---------------------------------------------------------------
    public static readonly Color Ink = C("#4A2F1F");
    public static readonly Color Wood = C("#7A4F33");
    public static readonly Color WoodDark = C("#5E3B25");
    public static readonly Color Cream = C("#FFF8EC");
    public static readonly Color Sand = C("#F6E4C6");
    public static readonly Color SandLine = C("#E0C39A");
    public static readonly Color SlotLine = C("#D9BF95");
    public static readonly Color Tan = C("#C9A77A");
    public static readonly Color TabOff = C("#EBD3AE");
    public static readonly Color Green = C("#3F7A2E");
    public static readonly Color GreenDark = C("#2C5A1F");
    public static readonly Color Orange = C("#E8833A");
    public static readonly Color Peach = C("#FBD3B1");
    public static readonly Color Coral = C("#E07A5F");

    public static Color C(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color color);
        return color;
    }

    public static void ClearCache()
    {
        cache.Clear();
    }

    // AssetDatabase에 폴더가 없으면 차례로 만든다. (예: Assets/UI/Cozy/Generated)
    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // ---------------------------------------------------------------
    // 9-slice 패널
    // ---------------------------------------------------------------

    // 둥근 사각형 패널. 아래쪽 그림자와 테두리를 포함하며 Sliced 이미지로 늘려 쓴다.
    public static Sprite Panel(string name, Color fill, Color border, float borderWidth,
        float radius, float shadow = 0f, Color shadowColor = default, bool flatBottom = false)
    {
        if (cache.TryGetValue(name, out Sprite cached) && cached != null)
            return cached;

        float w = radius * 2f + 4f;
        float body = flatBottom ? radius + 4f : radius * 2f + 4f;
        float h = body + shadow;

        Painter p = new Painter(w, h, Scale);

        if (flatBottom)
        {
            float bottom = body + radius + 2f;
            p.Fill(Sdf.RoundBox(w / 2f, bottom / 2f, w / 2f, bottom / 2f, radius), border);

            if (borderWidth > 0f)
            {
                p.Fill(Sdf.RoundBox(w / 2f, (borderWidth + bottom) / 2f, w / 2f - borderWidth,
                    (bottom - borderWidth) / 2f, Mathf.Max(radius - borderWidth, 0f)), fill);
            }
        }
        else
        {
            if (shadow > 0f)
                p.Fill(Sdf.RoundBox(w / 2f, body / 2f + shadow, w / 2f, body / 2f, radius), shadowColor);

            p.Fill(Sdf.RoundBox(w / 2f, body / 2f, w / 2f, body / 2f, radius),
                borderWidth > 0f ? border : fill);

            if (borderWidth > 0f)
            {
                p.Fill(Sdf.RoundBox(w / 2f, body / 2f, w / 2f - borderWidth, body / 2f - borderWidth,
                    Mathf.Max(radius - borderWidth, 0f)), fill);
            }
        }

        float side = radius + 2f;
        Vector4 slice = flatBottom
            ? new Vector4(side, 2f, side, side)
            : new Vector4(side, side + shadow, side, side);

        return Save(name, p, slice * Scale);
    }

    // 선택 테두리(주황 선 + 바깥쪽 연한 빛). 칸보다 3단위 크게 놓는다.
    public static Sprite Ring(string name, float radius)
    {
        if (cache.TryGetValue(name, out Sprite cached) && cached != null)
            return cached;

        const float glow = 3f;
        const float line = 2.5f;
        float outer = radius + glow;
        float w = outer * 2f + 4f;

        Painter p = new Painter(w, w, Scale);
        Func<float, float, float> box = Sdf.RoundBox(w / 2f, w / 2f, w / 2f - glow, w / 2f - glow, radius);

        p.Band(box, 0f, glow, Peach);
        p.Band(box, -line, 0f, Orange);

        float side = outer + 2f;
        return Save(name, p, new Vector4(side, side, side, side) * Scale);
    }

    // ---------------------------------------------------------------
    // 단순(늘리지 않는) 모양
    // ---------------------------------------------------------------

    // 원형 배지/핀. 테두리와 아래 그림자를 포함한다.
    public static Sprite Circle(string name, float diameter, Color fill, Color border,
        float borderWidth, float shadow = 0f, Color shadowColor = default)
    {
        if (cache.TryGetValue(name, out Sprite cached) && cached != null)
            return cached;

        float r = diameter / 2f;
        Painter p = new Painter(diameter, diameter + shadow, Scale);

        if (shadow > 0f)
            p.Fill(Sdf.Circle(r, r + shadow, r), shadowColor);

        p.Fill(Sdf.Circle(r, r, r), borderWidth > 0f ? border : fill);

        if (borderWidth > 0f)
            p.Fill(Sdf.Circle(r, r, r - borderWidth), fill);

        return Save(name, p, Vector4.zero);
    }

    // 타원 (캐릭터 받침, 무대 배경, 지도 장식)
    public static Sprite Ellipse(string name, float width, float height, Color fill,
        Color border = default, float borderWidth = 0f)
    {
        if (cache.TryGetValue(name, out Sprite cached) && cached != null)
            return cached;

        Painter p = new Painter(width, height, Scale);
        float rx = width / 2f;
        float ry = height / 2f;

        p.Fill(Sdf.Ellipse(rx, ry, rx, ry), borderWidth > 0f ? border : fill);

        if (borderWidth > 0f)
            p.Fill(Sdf.Ellipse(rx, ry, rx - borderWidth, ry - borderWidth), fill);

        return Save(name, p, Vector4.zero);
    }

    // 직접 그리는 아이콘. draw 안에서 size × size 단위 좌표로 그린다.
    public static Sprite Icon(string name, float size, Action<Painter> draw)
    {
        if (cache.TryGetValue(name, out Sprite cached) && cached != null)
            return cached;

        Painter p = new Painter(size, size, Scale);
        draw(p);
        return Save(name, p, Vector4.zero);
    }

    // ---------------------------------------------------------------
    // 저장
    // ---------------------------------------------------------------

    private static Sprite Save(string name, Painter painter, Vector4 border)
    {
        string assetPath = Folder + "/" + name + ".png";
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);

        EnsureFolder(Folder);

        Texture2D texture = painter.ToTexture();
        File.WriteAllBytes(fullPath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f * Scale;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spriteBorder = border;
        importer.SaveAndReimport();

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        cache[name] = sprite;
        return sprite;
    }

    // ---------------------------------------------------------------
    // 그리기 도구
    // ---------------------------------------------------------------

    // 부호 있는 거리 함수(SDF)로 안티에일리어싱된 도형을 그린다. 좌표는 위에서 아래로 증가한다.
    public class Painter
    {
        private readonly int width;
        private readonly int height;
        private readonly float scale;
        private readonly Color[] pixels;

        public Painter(float unitWidth, float unitHeight, float pixelsPerUnit)
        {
            scale = pixelsPerUnit;
            width = Mathf.Max(1, Mathf.CeilToInt(unitWidth * pixelsPerUnit));
            height = Mathf.Max(1, Mathf.CeilToInt(unitHeight * pixelsPerUnit));
            pixels = new Color[width * height];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(1f, 1f, 1f, 0f);
        }

        // 도형 내부를 채운다.
        public void Fill(Func<float, float, float> shape, Color color)
        {
            Band(shape, float.NegativeInfinity, 0f, color);
        }

        // 도형 경계에서 inner ~ outer(단위) 사이의 띠를 칠한다.
        public void Band(Func<float, float, float> shape, float inner, float outer, Color color)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float d = shape((x + 0.5f) / scale, (y + 0.5f) / scale) * scale;
                    float a = Mathf.Clamp01(0.5f - (d - outer * scale));

                    if (!float.IsNegativeInfinity(inner))
                        a *= Mathf.Clamp01(0.5f + (d - inner * scale));

                    a *= color.a;

                    if (a > 0f)
                        Blend(y * width + x, color, a);
                }
            }
        }

        // 굵은 외곽선이 있는 도형 (아이콘 스타일)
        public void Outlined(Func<float, float, float> shape, Color fill, float line = 2.2f)
        {
            Outlined(shape, fill, Ink, line);
        }

        public void Outlined(Func<float, float, float> shape, Color fill, Color lineColor, float line)
        {
            float half = line / 2f;
            Fill((x, y) => shape(x, y) - half, lineColor);
            Fill((x, y) => shape(x, y) + half, fill);
        }

        private void Blend(int index, Color src, float a)
        {
            Color dst = pixels[index];
            float outA = a + dst.a * (1f - a);

            if (outA <= 0f)
                return;

            float r = (src.r * a + dst.r * dst.a * (1f - a)) / outA;
            float g = (src.g * a + dst.g * dst.a * (1f - a)) / outA;
            float b = (src.b * a + dst.b * dst.a * (1f - a)) / outA;
            pixels[index] = new Color(r, g, b, outA);
        }

        public Texture2D ToTexture()
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] flipped = new Color[pixels.Length];

            for (int y = 0; y < height; y++)
                Array.Copy(pixels, y * width, flipped, (height - 1 - y) * width, width);

            texture.SetPixels(flipped);
            texture.Apply();
            return texture;
        }
    }

    // 기본 도형의 거리 함수
    public static class Sdf
    {
        public static Func<float, float, float> RoundBox(float cx, float cy, float hx, float hy, float r)
        {
            r = Mathf.Min(r, Mathf.Min(hx, hy));

            return (x, y) =>
            {
                float qx = Mathf.Abs(x - cx) - (hx - r);
                float qy = Mathf.Abs(y - cy) - (hy - r);
                float ox = Mathf.Max(qx, 0f);
                float oy = Mathf.Max(qy, 0f);
                return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
            };
        }

        public static Func<float, float, float> Circle(float cx, float cy, float r)
        {
            return (x, y) =>
            {
                float dx = x - cx;
                float dy = y - cy;
                return Mathf.Sqrt(dx * dx + dy * dy) - r;
            };
        }

        public static Func<float, float, float> Ellipse(float cx, float cy, float rx, float ry)
        {
            return (x, y) =>
            {
                float nx = (x - cx) / rx;
                float ny = (y - cy) / ry;
                return (Mathf.Sqrt(nx * nx + ny * ny) - 1f) * Mathf.Min(rx, ry);
            };
        }

        public static Func<float, float, float> Capsule(float ax, float ay, float bx, float by, float r)
        {
            return (x, y) =>
            {
                float pax = x - ax;
                float pay = y - ay;
                float bax = bx - ax;
                float bay = by - ay;
                float t = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
                float dx = pax - bax * t;
                float dy = pay - bay * t;
                return Mathf.Sqrt(dx * dx + dy * dy) - r;
            };
        }

        // 다각형 (꼭짓점은 x, y 순서로 나열)
        public static Func<float, float, float> Polygon(params float[] xy)
        {
            int n = xy.Length / 2;

            return (x, y) =>
            {
                float d = float.MaxValue;
                float s = 1f;

                for (int i = 0, j = n - 1; i < n; j = i, i++)
                {
                    float vix = xy[i * 2], viy = xy[i * 2 + 1];
                    float vjx = xy[j * 2], vjy = xy[j * 2 + 1];
                    float ex = vjx - vix, ey = vjy - viy;
                    float wx = x - vix, wy = y - viy;
                    float t = Mathf.Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
                    float bx = wx - ex * t, by = wy - ey * t;
                    d = Mathf.Min(d, bx * bx + by * by);

                    bool c1 = y >= viy;
                    bool c2 = y < vjy;
                    bool c3 = ex * wy > ey * wx;

                    if ((c1 && c2 && c3) || (!c1 && !c2 && !c3))
                        s = -s;
                }

                return s * Mathf.Sqrt(d);
            };
        }

        public static Func<float, float, float> Union(params Func<float, float, float>[] shapes)
        {
            return (x, y) =>
            {
                float d = float.MaxValue;

                foreach (Func<float, float, float> shape in shapes)
                    d = Mathf.Min(d, shape(x, y));

                return d;
            };
        }
    }
}
