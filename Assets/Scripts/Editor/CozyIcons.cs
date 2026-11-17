using UnityEngine;
using static CozySpriteFactory;

// Tab 메뉴 디자인 시안의 아이콘을 스프라이트로 그린다. (좌표는 시안 SVG와 같은 단위)
public static class CozyIcons
{
    private static readonly Color Ghost = C("#D8C3A0");
    private static readonly Color GhostLine = C("#B89C74");

    // ----- 장비 아이콘 (40 × 40) -----

    public static Sprite Hat(string name, Color color, bool ghost = false)
    {
        return Icon(name, 40, p =>
        {
            Color fill = ghost ? Ghost : color;
            Color line = ghost ? GhostLine : Ink;
            p.Outlined(Sdf.Polygon(11, 25, 11.8f, 17, 14.5f, 12.5f, 20, 11, 25.5f, 12.5f, 28.2f, 17, 29, 25), fill, line, 2.2f);

            if (!ghost)
                p.Fill(Sdf.Capsule(12.4f, 20.5f, 27.6f, 20.5f, 1.4f), C("#C0533A"));

            p.Outlined(Sdf.Ellipse(20, 26, 16, 5), fill, line, 2.2f);
        });
    }

    public static Sprite Shirt(string name, Color color, bool ghost = false)
    {
        return Icon(name, 40, p =>
        {
            p.Outlined(Sdf.Polygon(14, 9, 8, 12.5f, 5, 19, 10.5f, 21, 12.5f, 18, 12.5f, 32,
                    27.5f, 32, 27.5f, 18, 29.5f, 21, 35, 19, 32, 12.5f, 26, 9, 20, 12.5f),
                ghost ? Ghost : color, ghost ? GhostLine : Ink, 2.2f);
        });
    }

    public static Sprite Pants(string name, Color color, bool ghost = false)
    {
        return Icon(name, 40, p =>
        {
            p.Outlined(Sdf.Polygon(11, 8, 29, 8, 31, 33, 23.5f, 33, 20, 17, 16.5f, 33, 9, 33),
                ghost ? Ghost : color, ghost ? GhostLine : Ink, 2.2f);
        });
    }

    public static Sprite Axe(string name, bool ghost = false)
    {
        return Icon(name, 40, p =>
        {
            Color line = ghost ? GhostLine : Ink;
            p.Outlined(Sdf.Capsule(13, 34, 26, 9, 2f), ghost ? Ghost : C("#8B5A3C"), line, 2f);
            p.Outlined(Sdf.Polygon(21, 8, 27, 6, 32, 8.5f, 33.5f, 15, 25.5f, 16.5f), ghost ? Ghost : C("#C9D3DA"), line, 2.2f);
        });
    }

    public static Sprite Pick(string name)
    {
        return Icon(name, 40, p =>
        {
            p.Outlined(Sdf.Capsule(13, 34, 25, 11, 2f), C("#8B5A3C"), 2f);
            p.Outlined(Sdf.Polygon(8, 13.5f, 14, 8.5f, 22, 6.5f, 30, 8.5f, 36, 13.5f, 22, 10.5f), C("#AEB8C0"), 2.2f);
        });
    }

    // ----- 탭·버튼 기호 -----

    // 플레이어 탭: 사람
    public static Sprite Person()
    {
        return Icon("glyph_person", 20, p =>
        {
            p.Fill(Sdf.Circle(10, 7, 3.8f), Ink);
            p.Fill(Sdf.RoundBox(10, 16.5f, 6.5f, 3.6f, 3.4f), Ink);
        });
    }

    // 건물 탭: 집
    public static Sprite House()
    {
        return Icon("glyph_house", 20, p =>
        {
            p.Fill(Sdf.Polygon(2.5f, 10, 10, 3, 17.5f, 10), Ink);
            p.Fill(Sdf.RoundBox(10, 13, 5.5f, 4.5f, 0.8f), Ink);
            p.Fill(Sdf.RoundBox(10, 15, 1.6f, 2.6f, 0.5f), C("#BFDDA6"));
        });
    }

    // 필드 탭: 지도 핀
    public static Sprite Pin()
    {
        return Icon("glyph_pin", 20, p =>
        {
            p.Fill(Sdf.Union(Sdf.Circle(10, 8.5f, 5.6f), Sdf.Polygon(5.1f, 10.5f, 14.9f, 10.5f, 10, 18.2f)), Ink);
            p.Fill(Sdf.Circle(10, 8.5f, 2.2f), C("#A9D3EA"));
        });
    }

    public static Sprite Close()
    {
        return Icon("glyph_close", 16, p =>
        {
            p.Fill(Sdf.Capsule(4.2f, 4.2f, 11.8f, 11.8f, 1.7f), Color.white);
            p.Fill(Sdf.Capsule(11.8f, 4.2f, 4.2f, 11.8f, 1.7f), Color.white);
        });
    }

    public static Sprite Check()
    {
        return Icon("glyph_check", 14, p =>
        {
            p.Fill(Sdf.Capsule(3, 7.5f, 6, 10.5f, 1.5f), Color.white);
            p.Fill(Sdf.Capsule(6, 10.5f, 11, 4, 1.5f), Color.white);
        });
    }

    public static Sprite Lock()
    {
        return Icon("glyph_lock", 14, p =>
        {
            Color c = C("#FFF8EC");
            p.Fill(Sdf.Union(
                Sdf.Capsule(4.3f, 7, 4.3f, 5, 0.9f),
                Sdf.Capsule(9.7f, 7, 9.7f, 5, 0.9f),
                Sdf.Capsule(4.3f, 5, 5.5f, 3, 0.9f),
                Sdf.Capsule(5.5f, 3, 8.5f, 3, 0.9f),
                Sdf.Capsule(8.5f, 3, 9.7f, 5, 0.9f)), c);
            p.Fill(Sdf.RoundBox(7, 9.5f, 4.6f, 3.4f, 1f), c);
        });
    }

    // ----- 지도 아이콘 (40 × 40) -----

    public static Sprite Village()
    {
        return Icon("map_village", 40, p =>
        {
            p.Outlined(Sdf.RoundBox(20, 25.5f, 9, 6.5f, 0.5f), C("#FFE9C7"), 2f);
            p.Outlined(Sdf.Polygon(7, 21, 20, 9, 33, 21), C("#E07A5F"), 2f);
            p.Outlined(Sdf.RoundBox(20, 28.5f, 2.5f, 3.5f, 0.5f), C("#A86B45"), 1.6f);
        });
    }

    public static Sprite Forest()
    {
        return Icon("map_forest", 40, p =>
        {
            p.Outlined(Sdf.RoundBox(14, 29.5f, 2, 3.5f, 0.3f), C("#8B5A3C"), 1.8f);
            p.Outlined(Sdf.Circle(14, 19, 8), C("#7FB561"), 2f);
            p.Outlined(Sdf.RoundBox(27, 31, 2, 3, 0.3f), C("#8B5A3C"), 1.8f);
            p.Outlined(Sdf.Polygon(18, 29, 27, 8, 36, 29), C("#5E9B45"), 2f);
        });
    }

    public static Sprite Hill()
    {
        return Icon("map_hill", 40, p =>
        {
            p.Outlined(Sdf.Polygon(4, 32, 15, 13, 22, 23, 27, 16, 36, 32), C("#B9C2C8"), 2f);
            p.Fill(Sdf.Polygon(12.6f, 17.5f, 15, 13.6f, 17.4f, 17), Color.white);
        });
    }

    public static Sprite Beach()
    {
        return Icon("map_beach", 40, p =>
        {
            p.Outlined(Sdf.Circle(28, 12, 5), C("#F6C744"), 1.8f);
            Color sea = C("#3E7FA8");
            p.Fill(Sdf.Union(
                Sdf.Capsule(5, 24, 10, 21.5f, 1.3f), Sdf.Capsule(10, 21.5f, 15, 24, 1.3f),
                Sdf.Capsule(15, 24, 20, 21.5f, 1.3f), Sdf.Capsule(20, 21.5f, 25, 24, 1.3f),
                Sdf.Capsule(25, 24, 30, 21.5f, 1.3f), Sdf.Capsule(30, 21.5f, 35, 24, 1.3f)), sea);
            p.Fill(Sdf.Union(
                Sdf.Capsule(5, 31, 10, 28.5f, 1.3f), Sdf.Capsule(10, 28.5f, 15, 31, 1.3f),
                Sdf.Capsule(15, 31, 20, 28.5f, 1.3f), Sdf.Capsule(20, 28.5f, 25, 31, 1.3f),
                Sdf.Capsule(25, 31, 30, 28.5f, 1.3f), Sdf.Capsule(30, 28.5f, 35, 31, 1.3f)), sea);
        });
    }
}
