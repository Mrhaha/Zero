using UnityEngine;

[CreateAssetMenu(menuName = "Dice/DiceSkin", fileName = "DiceSkin")]
public class DiceSkin : ScriptableObject
{
    public string skinName = "Custom";

    [Header("Texture Settings")]
    public int size = 256;
    public int border = 8;
    public int cornerRadius = 0; // 0 = sharp
    public bool roundedCorners = false;

    [Header("Colors")]
    public Color background = Color.white;
    public Color background2 = Color.white; // for gradients/stripes
    public Color borderColor = Color.black;
    public Color pipColor = Color.black;

    public enum BackgroundPattern { Solid, LinearGradient, RadialGradient, Stripes }
    public BackgroundPattern pattern = BackgroundPattern.Solid;

    public enum PipStyle { SolidCircle, HollowCircle, Square }
    public PipStyle pipStyle = PipStyle.SolidCircle;
    public int pipRadius = 14; // base pip radius
    public int stripeCount = 6;

    // Optional: assign sprites directly to bypass generation
    public Sprite[] faceSpritesOverride; // length 6 if set

    public Sprite[] GenerateSprites()
    {
        if (faceSpritesOverride != null && faceSpritesOverride.Length >= 6)
        {
            return faceSpritesOverride;
        }

        int w = Mathf.Max(32, size);
        int h = w;
        var sprites = new Sprite[6];
        for (int face = 1; face <= 6; face++)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;

            // Clear
            Fill(tex, new Color(0, 0, 0, 0));

            // Body with optional rounded corners and border
            DrawBody(tex, w, h, border, roundedCorners ? cornerRadius : 0, background, borderColor, pattern, background2, stripeCount);

            // Pips
            DrawPips(tex, face, pipStyle, pipRadius, pipColor, border, roundedCorners ? cornerRadius : 0);

            tex.Apply();
            sprites[face - 1] = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }
        return sprites;
    }

    private void Fill(Texture2D tex, Color col)
    {
        var cols = new Color[tex.width * tex.height];
        for (int i = 0; i < cols.Length; i++) cols[i] = col;
        tex.SetPixels(cols);
    }

    private void DrawBody(Texture2D tex, int w, int h, int b, int r, Color bg, Color bd, BackgroundPattern pat, Color bg2, int stripes)
    {
        int innerX0 = b, innerY0 = b, innerX1 = w - b - 1, innerY1 = h - b - 1;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool inBody = InRoundedRect(x, y, innerX0, innerY0, innerX1, innerY1, r);
                bool inBorder = InRoundedRect(x, y, innerX0 - 1, innerY0 - 1, innerX1 + 1, innerY1 + 1, Mathf.Max(0, r - 1));
                if (inBorder && !inBody)
                {
                    tex.SetPixel(x, y, bd);
                }
                else if (inBody)
                {
                    Color c = bg;
                    switch (pat)
                    {
                        case BackgroundPattern.Solid:
                            c = bg; break;
                        case BackgroundPattern.LinearGradient:
                            float t = Mathf.InverseLerp(innerY0, innerY1, y);
                            c = Color.Lerp(bg, bg2, t); break;
                        case BackgroundPattern.RadialGradient:
                            float dx = (x - w * 0.5f) / (w * 0.5f);
                            float dy = (y - h * 0.5f) / (h * 0.5f);
                            float rr = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                            c = Color.Lerp(bg, bg2, rr); break;
                        case BackgroundPattern.Stripes:
                            int k = Mathf.Abs(((x - innerX0) * stripes) / Mathf.Max(1, (innerX1 - innerX0 + 1)));
                            c = (k % 2 == 0) ? bg : bg2; break;
                    }
                    tex.SetPixel(x, y, c);
                }
            }
        }
    }

    private bool InRoundedRect(int x, int y, int x0, int y0, int x1, int y1, int radius)
    {
        if (radius <= 0) return (x >= x0 && x <= x1 && y >= y0 && y <= y1);
        int cx0 = x0 + radius, cy0 = y0 + radius;
        int cx1 = x1 - radius, cy1 = y1 - radius;
        if (x >= cx0 && x <= cx1 && y >= y0 && y <= y1) return true;
        if (y >= cy0 && y <= cy1 && x >= x0 && x <= x1) return true;
        // corners
        if (DistanceSq(x, y, cx0, cy0) <= radius * radius) return true;
        if (DistanceSq(x, y, cx1, cy0) <= radius * radius) return true;
        if (DistanceSq(x, y, cx0, cy1) <= radius * radius) return true;
        if (DistanceSq(x, y, cx1, cy1) <= radius * radius) return true;
        return false;
    }

    private int DistanceSq(int x, int y, int cx, int cy) { int dx = x - cx, dy = y - cy; return dx * dx + dy * dy; }

    private void DrawPips(Texture2D tex, int face, PipStyle style, int baseRadius, Color color, int b, int r)
    {
        int w = tex.width, h = tex.height;
        int cx = w / 2, cy = h / 2;
        int innerX0 = b + r, innerY0 = b + r, innerX1 = w - b - r, innerY1 = h - b - r;
        int inner = Mathf.Min(innerX1 - innerX0, innerY1 - innerY0);
        int off = Mathf.Max(1, inner / 4);
        int rad = Mathf.Max(2, baseRadius);

        Vector2Int C = new Vector2Int(cx, cy);
        Vector2Int NW = new Vector2Int(cx - off, cy + off);
        Vector2Int NE = new Vector2Int(cx + off, cy + off);
        Vector2Int SW = new Vector2Int(cx - off, cy - off);
        Vector2Int SE = new Vector2Int(cx + off, cy - off);
        Vector2Int Wc = new Vector2Int(cx - off, cy);
        Vector2Int Ec = new Vector2Int(cx + off, cy);

        void DotAt(Vector2Int p)
        {
            switch (style)
            {
                case PipStyle.SolidCircle: DrawFilledCircle(tex, p.x, p.y, rad, color); break;
                case PipStyle.HollowCircle: DrawHollowCircle(tex, p.x, p.y, rad, Mathf.Max(1, rad / 3), color); break;
                case PipStyle.Square: DrawFilledRect(tex, p.x - rad, p.y - rad, p.x + rad, p.y + rad, color); break;
            }
        }

        switch (face)
        {
            case 1: DotAt(C); break;
            case 2: DotAt(NW); DotAt(SE); break;
            case 3: DotAt(NW); DotAt(C); DotAt(SE); break;
            case 4: DotAt(NW); DotAt(NE); DotAt(SW); DotAt(SE); break;
            case 5: DotAt(NW); DotAt(NE); DotAt(C); DotAt(SW); DotAt(SE); break;
            case 6: DotAt(NW); DotAt(NE); DotAt(Wc); DotAt(Ec); DotAt(SW); DotAt(SE); break;
        }
    }

    private void DrawFilledCircle(Texture2D tex, int cx, int cy, int r, Color col)
    {
        int w = tex.width, h = tex.height; int r2 = r * r;
        for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
                if (x * x + y * y <= r2)
                {
                    int px = cx + x, py = cy + y;
                    if (px >= 0 && px < w && py >= 0 && py < h) tex.SetPixel(px, py, col);
                }
    }

    private void DrawHollowCircle(Texture2D tex, int cx, int cy, int r, int thickness, Color col)
    {
        int w = tex.width, h = tex.height; int r2 = r * r; int rIn2 = (r - thickness) * (r - thickness);
        for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
            {
                int d2 = x * x + y * y;
                if (d2 <= r2 && d2 >= rIn2)
                {
                    int px = cx + x, py = cy + y;
                    if (px >= 0 && px < w && py >= 0 && py < h) tex.SetPixel(px, py, col);
                }
            }
    }

    private void DrawFilledRect(Texture2D tex, int x0, int y0, int x1, int y1, Color col)
    {
        int w = tex.width, h = tex.height;
        for (int y = Mathf.Max(0, y0); y <= Mathf.Min(h - 1, y1); y++)
            for (int x = Mathf.Max(0, x0); x <= Mathf.Min(w - 1, x1); x++)
                tex.SetPixel(x, y, col);
    }

    // Built-in presets (runtime, no asset needed)
    public static DiceSkin CreatePresetClassic()
    {
        var s = CreateInstance<DiceSkin>();
        s.skinName = "Classic";
        s.size = 256; s.border = 8; s.roundedCorners = true; s.cornerRadius = 18;
        s.background = Color.white; s.borderColor = Color.black; s.pipColor = Color.black;
        s.pattern = BackgroundPattern.Solid; s.pipStyle = PipStyle.SolidCircle; s.pipRadius = 14;
        return s;
    }

    public static DiceSkin CreatePresetDark()
    {
        var s = CreateInstance<DiceSkin>();
        s.skinName = "Dark";
        s.size = 256; s.border = 8; s.roundedCorners = true; s.cornerRadius = 18;
        s.background = new Color(0.08f, 0.08f, 0.1f);
        s.background2 = new Color(0.12f, 0.12f, 0.16f);
        s.borderColor = new Color(0.3f, 0.3f, 0.35f);
        s.pipColor = Color.white;
        s.pattern = BackgroundPattern.LinearGradient; s.pipStyle = PipStyle.SolidCircle; s.pipRadius = 14;
        return s;
    }

    public static DiceSkin CreatePresetCandy()
    {
        var s = CreateInstance<DiceSkin>();
        s.skinName = "Candy";
        s.size = 256; s.border = 8; s.roundedCorners = true; s.cornerRadius = 22;
        s.background = new Color(1.0f, 0.8f, 0.9f);
        s.background2 = new Color(0.9f, 0.95f, 1.0f);
        s.borderColor = new Color(0.85f, 0.5f, 0.7f);
        s.pipColor = new Color(0.4f, 0.1f, 0.3f);
        s.pattern = BackgroundPattern.RadialGradient; s.pipStyle = PipStyle.HollowCircle; s.pipRadius = 16;
        return s;
    }

    public static DiceSkin CreatePresetNeon()
    {
        var s = CreateInstance<DiceSkin>();
        s.skinName = "Neon";
        s.size = 256; s.border = 8; s.roundedCorners = false; s.cornerRadius = 0;
        s.background = new Color(0.05f, 0.05f, 0.08f);
        s.background2 = new Color(0.08f, 0.05f, 0.12f);
        s.borderColor = new Color(0.15f, 0.15f, 0.2f);
        s.pipColor = new Color(0.2f, 1f, 0.9f);
        s.pattern = BackgroundPattern.Stripes; s.stripeCount = 8; s.pipStyle = PipStyle.SolidCircle; s.pipRadius = 12;
        return s;
    }
}

