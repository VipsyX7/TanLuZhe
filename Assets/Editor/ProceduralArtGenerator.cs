// ProceduralArtGenerator.cs
// Pure-C# procedural pixel-art generator for the TanLuZhe 2D URP platformer.
//
// Generates 24 PNG sprite assets into Assets/Art/Generated/ and configures the
// TextureImporter settings for each one. No external packages, no System.Drawing,
// no binary assets checked in: every pixel is drawn in code with Texture2D and
// written with EncodeToPNG().
//
// All drawing uses straight (un-premultiplied) alpha. Everything is deterministic:
// the only "randomness" is a hash based value noise function (VNoise) with a fixed seed.
//
// Menu: TanLuZhe/1. Generate Art Assets

using System.IO;
using UnityEditor;
using UnityEngine;

public static class ProceduralArtGenerator
{
    // Output folder, relative to the project root.
    private const string RelDir = "Assets/Art/Generated";

    // Fixed seed for all hash based noise. Never use unseeded Random.
    private const int Seed = 20240517;

    // Every sprite in this project is authored at 32 pixels per unit.
    private const float Ppu = 32f;

    // ------------------------------------------------------------------
    // Entry points
    // ------------------------------------------------------------------

    [MenuItem("TanLuZhe/1. Generate Art Assets")]
    public static void GenerateAllMenu()
    {
        GenerateAll();
    }

    public static void GenerateAll()
    {
        int count = 0;

        EnsureOutputDir();
        Log("[ProceduralArtGenerator] Generating sprites into " + RelDir + " ...");

        count += Save("player.png", BuildPlayer(), FilterMode.Point, TextureWrapMode.Clamp, Vector4.zero);
        count += Save("enemy.png", BuildEnemy(false), FilterMode.Point, TextureWrapMode.Clamp, Vector4.zero);
        count += Save("enemy_hurt.png", BuildEnemy(true), FilterMode.Point, TextureWrapMode.Clamp, Vector4.zero);
        count += Save("ground_tile.png", BuildGroundTile(), FilterMode.Point, TextureWrapMode.Repeat, new Vector4(8f, 8f, 8f, 8f));
        count += Save("platform_tile.png", BuildPlatformTile(), FilterMode.Point, TextureWrapMode.Clamp, new Vector4(8f, 4f, 8f, 4f));
        count += Save("hook_head.png", BuildHookHead(), FilterMode.Point, TextureWrapMode.Clamp, Vector4.zero);
        count += Save("rope_segment.png", BuildRopeSegment(), FilterMode.Point, TextureWrapMode.Clamp, Vector4.zero);
        count += Save("spark.png", BuildSpark(), FilterMode.Point, TextureWrapMode.Clamp, Vector4.zero);
        count += Save("coin.png", BuildCoin(), FilterMode.Point, TextureWrapMode.Clamp, Vector4.zero);
        count += Save("spike.png", BuildSpike(), FilterMode.Point, TextureWrapMode.Clamp, Vector4.zero);
        count += Save("goal.png", BuildGoal(), FilterMode.Point, TextureWrapMode.Clamp, Vector4.zero);
        count += Save("checkpoint.png", BuildCheckpoint(), FilterMode.Point, TextureWrapMode.Clamp, Vector4.zero);
        count += Save("bg_gradient.png", BuildBgGradient(), FilterMode.Bilinear, TextureWrapMode.Clamp, Vector4.zero);
        count += Save("bg_hill.png", BuildBgHill(), FilterMode.Point, TextureWrapMode.Clamp, Vector4.zero);
        count += Save("bg_cloud.png", BuildBgCloud(), FilterMode.Bilinear, TextureWrapMode.Clamp, Vector4.zero);
        count += Save("dust.png", BuildDust(), FilterMode.Point, TextureWrapMode.Clamp, Vector4.zero);
        count += Save("white.png", BuildWhite(), FilterMode.Point, TextureWrapMode.Clamp, Vector4.zero);

        // Weapons, projectiles and VFX.
        count += Save("weapon_sword.png", BuildWeaponSword(), FilterMode.Point, TextureWrapMode.Clamp, Vector4.zero);
        count += Save("weapon_spear.png", BuildWeaponSpear(), FilterMode.Point, TextureWrapMode.Clamp, Vector4.zero);
        count += Save("weapon_hammer.png", BuildWeaponHammer(), FilterMode.Point, TextureWrapMode.Clamp, Vector4.zero);
        count += Save("weapon_bow.png", BuildWeaponBow(), FilterMode.Point, TextureWrapMode.Clamp, Vector4.zero);
        count += Save("weapon_wand.png", BuildWeaponWand(), FilterMode.Point, TextureWrapMode.Clamp, Vector4.zero);
        count += Save("bolt.png", BuildBolt(), FilterMode.Point, TextureWrapMode.Clamp, Vector4.zero);
        count += Save("slash.png", BuildSlash(), FilterMode.Point, TextureWrapMode.Clamp, Vector4.zero);

        AssetDatabase.Refresh();

        Debug.Log($"[ProceduralArtGenerator] Generated {count} sprites into Assets/Art/Generated/");
    }

    // ------------------------------------------------------------------
    // Asset pipeline
    // ------------------------------------------------------------------

    private static void EnsureOutputDir()
    {
        // Directory.CreateDirectory creates the whole chain, but create both
        // levels explicitly so the intent is obvious.
        string artAbs = Path.Combine(Application.dataPath, "Art");
        if (!Directory.Exists(artAbs))
        {
            Directory.CreateDirectory(artAbs);
        }

        string genAbs = Path.Combine(Application.dataPath, "Art", "Generated");
        if (!Directory.Exists(genAbs))
        {
            Directory.CreateDirectory(genAbs);
        }

        AssetDatabase.Refresh();
        Log("[ProceduralArtGenerator] Output directory ready: " + RelDir);
    }

    // Writes one canvas to disk as a PNG and configures its TextureImporter.
    private static int Save(string fileName, Canvas canvas, FilterMode filter, TextureWrapMode wrap, Vector4 border)
    {
        string rel = RelDir + "/" + fileName;
        string abs = Path.Combine(Application.dataPath, "Art", "Generated", fileName);

        Texture2D tex = ToTexture(canvas);
        File.WriteAllBytes(abs, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        // The file is brand new: let the database see it before importing.
        AssetDatabase.Refresh();
        AssetDatabase.ImportAsset(rel, ImportAssetOptions.ForceUpdate);

        ApplyImporterSettings(rel, filter, wrap, border);

        // Re-import once more now that the importer settings changed.
        AssetDatabase.ImportAsset(rel, ImportAssetOptions.ForceUpdate);

        Log("[ProceduralArtGenerator] Wrote " + rel + "  " + canvas.W + "x" + canvas.H
            + "  ppu=" + Ppu + "  filter=" + filter + "  wrap=" + wrap);

        return 1;
    }

    private static Texture2D ToTexture(Canvas canvas)
    {
        Texture2D tex = new Texture2D(canvas.W, canvas.H, TextureFormat.RGBA32, false);
        tex.SetPixels32(canvas.Px);
        tex.Apply(false, false);
        return tex;
    }

    private static void ApplyImporterSettings(string rel, FilterMode filter, TextureWrapMode wrap, Vector4 border)
    {
        TextureImporter importer = AssetImporter.GetAtPath(rel) as TextureImporter;
        if (importer == null)
        {
            Log("[ProceduralArtGenerator] WARNING: no TextureImporter found for " + rel);
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = Ppu;
        importer.spriteBorder = border;
        importer.filterMode = filter;
        importer.wrapMode = wrap;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        // Sprites are not power of two: never let the importer rescale them.
        importer.npotScale = TextureImporterNPOTScale.None;

        // Full Rectangle meshes: the tiles (ground / platform) are drawn with SpriteRenderer
        // tiling and 9-slicing, which a tight mesh cannot do correctly. The mesh type only lives
        // on the settings block, not directly on the importer.
        TextureImporterSettings textureSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(textureSettings);
        textureSettings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(textureSettings);

        importer.SaveAndReimport();
    }

    private static void Log(string s)
    {
        Debug.Log(s);
    }

    // ------------------------------------------------------------------
    // Canvas + tiny drawing API
    // ------------------------------------------------------------------

    // A transparent RGBA32 pixel buffer. Color32 default (0,0,0,0) is fully
    // transparent, so nothing is ever painted black unless we ask for it.
    private sealed class Canvas
    {
        public readonly int W;
        public readonly int H;
        public readonly Color32[] Px;

        public Canvas(int w, int h)
        {
            W = w;
            H = h;
            Px = new Color32[w * h];
        }
    }

    private static Canvas New(int w, int h)
    {
        return new Canvas(w, h);
    }

    private static byte ToByte(float v)
    {
        int n = Mathf.RoundToInt(v);
        if (n < 0)
        {
            n = 0;
        }
        if (n > 255)
        {
            n = 255;
        }
        return (byte)n;
    }

    // Bounds checked, source-over blend using straight (un-premultiplied) alpha.
    private static void Set(Canvas c, int x, int y, Color32 src)
    {
        if (x < 0 || y < 0 || x >= c.W || y >= c.H)
        {
            return;
        }
        if (src.a == 0)
        {
            return;
        }

        int i = y * c.W + x;
        if (src.a == 255)
        {
            c.Px[i] = src;
            return;
        }

        Color32 dst = c.Px[i];
        float sa = src.a / 255f;
        float da = dst.a / 255f;
        float outA = sa + da * (1f - sa);
        if (outA <= 0.0001f)
        {
            c.Px[i] = new Color32(0, 0, 0, 0);
            return;
        }

        float inv = 1f / outA;
        float r = (src.r * sa + dst.r * da * (1f - sa)) * inv;
        float g = (src.g * sa + dst.g * da * (1f - sa)) * inv;
        float b = (src.b * sa + dst.b * da * (1f - sa)) * inv;
        c.Px[i] = new Color32(ToByte(r), ToByte(g), ToByte(b), ToByte(outA * 255f));
    }

    // Unconditional write (no blending). Used for hard pixel art edges and masks.
    private static void SetRaw(Canvas c, int x, int y, Color32 col)
    {
        if (x < 0 || y < 0 || x >= c.W || y >= c.H)
        {
            return;
        }
        c.Px[y * c.W + x] = col;
    }

    private static void Fill(Canvas c, Color32 col)
    {
        for (int i = 0; i < c.Px.Length; i++)
        {
            c.Px[i] = col;
        }
    }

    // Filled rectangle, x/y is the lower left corner.
    private static void Rect(Canvas c, int x, int y, int w, int h, Color32 col)
    {
        for (int yy = y; yy < y + h; yy++)
        {
            for (int xx = x; xx < x + w; xx++)
            {
                Set(c, xx, yy, col);
            }
        }
    }

    // Filled rounded rectangle, x/y is the lower left corner.
    private static void RoundRect(Canvas c, int x, int y, int w, int h, int r, Color32 col)
    {
        if (w <= 0 || h <= 0)
        {
            return;
        }

        int maxR = Mathf.Min(w, h) / 2;
        if (r < 0)
        {
            r = 0;
        }
        if (r > maxR)
        {
            r = maxR;
        }

        float rf = r;
        for (int yy = 0; yy < h; yy++)
        {
            for (int xx = 0; xx < w; xx++)
            {
                float px = xx + 0.5f;
                float py = yy + 0.5f;
                float fx = Mathf.Clamp(px, rf, w - rf);
                float fy = Mathf.Clamp(py, rf, h - rf);
                float dx = px - fx;
                float dy = py - fy;
                if (dx * dx + dy * dy <= rf * rf + 0.0001f)
                {
                    Set(c, x + xx, y + yy, col);
                }
            }
        }
    }

    // Filled circle, pixel centre sampled (hard edges, good for pixel art).
    private static void Circle(Canvas c, float cx, float cy, float r, Color32 col)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r - 1f));
        int x1 = Mathf.Min(c.W - 1, Mathf.CeilToInt(cx + r + 1f));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - r - 1f));
        int y1 = Mathf.Min(c.H - 1, Mathf.CeilToInt(cy + r + 1f));
        float rr = r * r;

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                float dx = x + 0.5f - cx;
                float dy = y + 0.5f - cy;
                if (dx * dx + dy * dy <= rr)
                {
                    Set(c, x, y, col);
                }
            }
        }
    }

    // Soft edged disc. Composited with max-alpha so overlapping blobs merge into
    // one smooth shape instead of stacking into hard rings.
    private static void SoftCircle(Canvas c, float cx, float cy, float r, Color32 col, float soft)
    {
        if (soft < 0.05f)
        {
            soft = 0.05f;
        }

        int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r - 1f));
        int x1 = Mathf.Min(c.W - 1, Mathf.CeilToInt(cx + r + 1f));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - r - 1f));
        int y1 = Mathf.Min(c.H - 1, Mathf.CeilToInt(cy + r + 1f));

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                float dx = x + 0.5f - cx;
                float dy = y + 0.5f - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > r)
                {
                    continue;
                }

                float a = Mathf.Clamp01((r - d) / soft);
                int na = Mathf.RoundToInt(a * col.a);
                if (na <= 0)
                {
                    continue;
                }

                int i = y * c.W + x;
                if (na < c.Px[i].a)
                {
                    continue;
                }
                c.Px[i] = new Color32(col.r, col.g, col.b, (byte)na);
            }
        }
    }

    // Filled triangle (pixel centres, inclusive edges).
    private static void Triangle(Canvas c, float x0, float y0, float x1, float y1, float x2, float y2, Color32 col)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(x0, Mathf.Min(x1, x2))));
        int maxX = Mathf.Min(c.W - 1, Mathf.CeilToInt(Mathf.Max(x0, Mathf.Max(x1, x2))));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(y0, Mathf.Min(y1, y2))));
        int maxY = Mathf.Min(c.H - 1, Mathf.CeilToInt(Mathf.Max(y0, Mathf.Max(y1, y2))));
        const float Eps = 0.0001f;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;
                float d0 = EdgeSign(px, py, x0, y0, x1, y1);
                float d1 = EdgeSign(px, py, x1, y1, x2, y2);
                float d2 = EdgeSign(px, py, x2, y2, x0, y0);
                bool neg = d0 < -Eps || d1 < -Eps || d2 < -Eps;
                bool pos = d0 > Eps || d1 > Eps || d2 > Eps;
                if (!(neg && pos))
                {
                    Set(c, x, y, col);
                }
            }
        }
    }

    private static float EdgeSign(float px, float py, float ax, float ay, float bx, float by)
    {
        return (px - bx) * (ay - by) - (ax - bx) * (py - by);
    }

    // Integer Bresenham line.
    private static void Line(Canvas c, int x0, int y0, int x1, int y1, Color32 col)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = -Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        int guard = 0;

        while (guard++ < 8192)
        {
            Set(c, x0, y0, col);
            if (x0 == x1 && y0 == y1)
            {
                return;
            }
            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x0 += sx;
            }
            if (e2 <= dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    // Draws a 1px outline around the current silhouette: every fully transparent
    // pixel that has a non-transparent 4-neighbour becomes the outline colour.
    private static void Outline(Canvas c, Color32 col)
    {
        bool[] solid = new bool[c.W * c.H];
        for (int i = 0; i < c.Px.Length; i++)
        {
            solid[i] = c.Px[i].a > 0;
        }

        for (int y = 0; y < c.H; y++)
        {
            for (int x = 0; x < c.W; x++)
            {
                int i = y * c.W + x;
                if (solid[i])
                {
                    continue;
                }

                bool touching = false;
                if (x > 0 && solid[i - 1])
                {
                    touching = true;
                }
                else if (x < c.W - 1 && solid[i + 1])
                {
                    touching = true;
                }
                else if (y > 0 && solid[i - c.W])
                {
                    touching = true;
                }
                else if (y < c.H - 1 && solid[i + c.W])
                {
                    touching = true;
                }

                if (touching)
                {
                    c.Px[i] = col;
                }
            }
        }
    }

    // Copies RGB from opaque neighbours into fully transparent pixels (alpha stays 0).
    // Keeps bilinear filtered sprites from picking up dark halos.
    private static void BleedEdges(Canvas c)
    {
        Color32[] src = (Color32[])c.Px.Clone();

        for (int y = 0; y < c.H; y++)
        {
            for (int x = 0; x < c.W; x++)
            {
                int i = y * c.W + x;
                if (src[i].a != 0)
                {
                    continue;
                }

                Color32 pick = new Color32(0, 0, 0, 0);
                bool found = false;

                if (x > 0 && src[i - 1].a > 0)
                {
                    pick = src[i - 1];
                    found = true;
                }
                else if (x < c.W - 1 && src[i + 1].a > 0)
                {
                    pick = src[i + 1];
                    found = true;
                }
                else if (y > 0 && src[i - c.W].a > 0)
                {
                    pick = src[i - c.W];
                    found = true;
                }
                else if (y < c.H - 1 && src[i + c.W].a > 0)
                {
                    pick = src[i + c.W];
                    found = true;
                }

                if (found)
                {
                    c.Px[i] = new Color32(pick.r, pick.g, pick.b, 0);
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // Deterministic hash noise
    // ------------------------------------------------------------------

    private static int WrapIndex(int v, int period)
    {
        v %= period;
        if (v < 0)
        {
            v += period;
        }
        return v;
    }

    private static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int n = x * 374761393 + y * 668265263 + seed * 1442695040;
            n = (n ^ (n >> 13)) * 1274126177;
            n ^= n >> 16;
            return (n & 0x7FFFFFFF) / 2147483647f;
        }
    }

    // Smooth value noise on a 16 cell lattice that wraps: any frequency that is a
    // multiple of 1/16 produces a tileable pattern, which is what the 9-sliced
    // ground/platform tiles need.
    private static float VNoise(float x, float y, int seed)
    {
        int xi = Mathf.FloorToInt(x);
        int yi = Mathf.FloorToInt(y);
        float xf = x - xi;
        float yf = y - yi;
        float u = xf * xf * (3f - 2f * xf);
        float v = yf * yf * (3f - 2f * yf);

        int x0 = WrapIndex(xi, 16);
        int x1 = WrapIndex(xi + 1, 16);
        int y0 = WrapIndex(yi, 16);
        int y1 = WrapIndex(yi + 1, 16);

        float a = Hash(x0, y0, seed);
        float b = Hash(x1, y0, seed);
        float cc = Hash(x0, y1, seed);
        float d = Hash(x1, y1, seed);

        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(cc, d, u), v);
    }

    // ------------------------------------------------------------------
    // Sprites
    // ------------------------------------------------------------------

    // 24x32 friendly hero, facing right: navy outline, teal body, lighter chest
    // highlight, two white eyes with dark pupils on the right, orange scarf on the
    // back (left) side, rounded boots.
    private static Canvas BuildPlayer()
    {
        Canvas c = New(24, 32);

        Color32 navy = new Color32(20, 26, 54, 255);
        Color32 teal = new Color32(62, 198, 206, 255);
        Color32 tealShade = new Color32(46, 172, 184, 255);
        Color32 tealDark = new Color32(34, 148, 164, 255);
        Color32 cyanHi = new Color32(160, 244, 250, 255);
        Color32 white = new Color32(246, 252, 255, 255);
        Color32 pupil = new Color32(22, 28, 54, 255);
        Color32 orange = new Color32(248, 146, 56, 255);
        Color32 orangeDark = new Color32(188, 96, 26, 255);

        // Silhouette. Head is shifted 1px right of the torso so the shape reads as
        // facing right even in pure black.
        RoundRect(c, 6, 19, 12, 11, 3, teal);    // head      x6..17  y19..29
        RoundRect(c, 5, 10, 12, 9, 2, teal);     // torso     x5..16  y10..18
        Rect(c, 7, 6, 3, 4, tealShade);          // back leg  x7..9   y6..9
        Rect(c, 13, 6, 3, 4, tealShade);         // front leg x13..15 y6..9
        RoundRect(c, 6, 1, 5, 5, 2, tealDark);   // back boot x6..10  y1..5
        RoundRect(c, 12, 1, 5, 5, 2, tealDark);  // front boot x12..16 y1..5
        Rect(c, 5, 17, 13, 2, orange);           // scarf band x5..17 y17..18
        RoundRect(c, 1, 12, 6, 7, 2, orange);    // scarf drape x1..6 y12..18
        Rect(c, 1, 9, 4, 4, orange);             // scarf tail x1..4  y9..12

        Outline(c, navy);

        // Interior detail on top of the outline.
        RoundRect(c, 11, 11, 4, 6, 2, cyanHi);   // chest highlight, facing side
        Rect(c, 5, 10, 12, 1, tealDark);         // waist line
        Rect(c, 5, 17, 13, 1, orangeDark);       // scarf fold
        Rect(c, 1, 9, 4, 1, orangeDark);         // tail tip

        // Eyes: 2x3 white with 1x2 dark pupils on the right edge (looking right).
        Rect(c, 10, 23, 2, 3, white);
        Rect(c, 14, 23, 2, 3, white);
        Rect(c, 11, 23, 1, 2, pupil);
        Rect(c, 15, 23, 1, 2, pupil);

        return c;
    }

    // 26x22 hostile blob, facing right. hurt == true gives the hit-flash variant:
    // same silhouette, bright white/pink body, dark eyes.
    private static Canvas BuildEnemy(bool hurt)
    {
        Canvas c = New(26, 22);

        Color32 body = hurt ? new Color32(255, 228, 242, 255) : new Color32(178, 54, 92, 255);
        Color32 bodyLo = hurt ? new Color32(238, 178, 208, 255) : new Color32(132, 32, 66, 255);
        Color32 edge = hurt ? new Color32(212, 124, 164, 255) : new Color32(94, 22, 52, 255);
        Color32 eye = hurt ? new Color32(126, 44, 82, 255) : new Color32(250, 252, 255, 255);
        Color32 pupil = hurt ? new Color32(74, 22, 48, 255) : new Color32(26, 20, 38, 255);
        Color32 brow = hurt ? new Color32(112, 34, 70, 255) : new Color32(72, 16, 42, 255);

        // Stubby legs.
        Rect(c, 7, 1, 4, 3, bodyLo);             // x7..10  y1..3
        Rect(c, 15, 1, 4, 3, bodyLo);            // x15..18 y1..3

        // Blob body plus darker lower belly.
        RoundRect(c, 2, 3, 22, 15, 5, body);     // x2..23  y3..17
        RoundRect(c, 2, 3, 22, 6, 4, bodyLo);    // y3..8

        // Jagged top edge, taller on the right (facing) side.
        Triangle(c, 3f, 14.5f, 9f, 14.5f, 6f, 19f, body);
        Triangle(c, 8f, 14.5f, 14f, 14.5f, 11f, 20f, body);
        Triangle(c, 13f, 14.5f, 19f, 14.5f, 16f, 20f, body);
        Triangle(c, 18f, 14.5f, 24f, 14.5f, 21f, 21f, body);

        Outline(c, edge);

        // Angry eyes: white blocks with dark pupils pushed to the right edge.
        Rect(c, 8, 9, 4, 4, eye);                // x8..11  y9..12
        Rect(c, 16, 9, 4, 4, eye);               // x16..19 y9..12
        Rect(c, 10, 9, 2, 2, pupil);
        Rect(c, 18, 9, 2, 2, pupil);

        // Slanted brows forming a V (angry), inner ends low.
        Line(c, 7, 16, 12, 13, brow);
        Line(c, 7, 15, 12, 12, brow);
        Line(c, 16, 13, 21, 16, brow);
        Line(c, 16, 12, 21, 15, brow);

        return c;
    }

    // 32x32 stone/dirt block, 9-sliced with an 8px border and set to Repeat.
    // Speckle noise is 16px periodic so the repeatable centre of the slice lines up.
    private static Canvas BuildGroundTile()
    {
        Canvas c = New(32, 32);

        Color32 baseCol = new Color32(106, 120, 142, 255);
        Color32 dark = new Color32(86, 100, 122, 255);
        Color32 darker = new Color32(70, 82, 104, 255);
        Color32 light = new Color32(122, 138, 160, 255);
        Color32 top = new Color32(148, 166, 188, 255);
        Color32 bottom = new Color32(62, 74, 96, 255);

        Fill(c, baseCol);

        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float fine = VNoise(x + 0.5f, y + 0.5f, Seed + 7);
                float coarse = VNoise((x + 0.5f) * 0.5f, (y + 0.5f) * 0.5f, Seed);

                if (fine > 0.74f)
                {
                    Set(c, x, y, darker);
                }
                else if (fine > 0.62f)
                {
                    Set(c, x, y, dark);
                }
                else if (coarse > 0.68f)
                {
                    Set(c, x, y, dark);
                }
                else if (coarse < 0.32f)
                {
                    Set(c, x, y, light);
                }
            }
        }

        // A few embedded stones, kept inside the repeatable 16x16 centre.
        Rect(c, 11, 19, 3, 2, dark);
        Rect(c, 11, 20, 3, 1, light);
        Rect(c, 19, 13, 4, 2, dark);
        Rect(c, 19, 14, 4, 1, light);
        Rect(c, 14, 9, 2, 2, darker);
        Rect(c, 14, 10, 2, 1, light);

        // Mortar / bevel between the two 16px courses.
        Rect(c, 0, 15, 32, 1, darker);
        Rect(c, 0, 16, 32, 1, light);

        // Dark seams at the left/right edges read as mortar between blocks.
        Rect(c, 0, 0, 1, 32, dark);
        Rect(c, 31, 0, 1, 32, dark);

        // 1px lighter top edge, 1px darker bottom edge.
        Rect(c, 0, 31, 32, 1, top);
        Rect(c, 0, 0, 32, 1, bottom);

        return c;
    }

    // 32x16 wooden platform, 9-sliced with an 8,4,8,4 border.
    private static Canvas BuildPlatformTile()
    {
        Canvas c = New(32, 16);

        Color32 wood = new Color32(160, 110, 64, 255);
        Color32 woodLo = new Color32(176, 126, 78, 255);
        Color32 woodMottle = new Color32(142, 96, 56, 255);
        Color32 woodDark = new Color32(124, 80, 44, 255);
        Color32 grain = new Color32(104, 64, 34, 255);
        Color32 top = new Color32(212, 162, 110, 255);
        Color32 bottom = new Color32(96, 60, 32, 255);
        Color32 bolt = new Color32(190, 196, 206, 255);
        Color32 boltDark = new Color32(120, 126, 138, 255);

        Fill(c, wood);

        // Soft mottling, 8px periodic in both axes so the 9-slice centre repeats
        // cleanly (border 8,4,8,4 gives a 16x8 centre).
        for (int y = 1; y < 15; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float n = VNoise((x + 0.5f) * 2f, (y + 0.5f) * 2f, Seed + 3);
                if (n > 0.72f)
                {
                    Set(c, x, y, woodMottle);
                }
                else if (n < 0.3f)
                {
                    Set(c, x, y, woodLo);
                }
            }
        }

        // Two darker horizontal grain lines, broken up a little.
        Rect(c, 0, 9, 32, 1, grain);
        Rect(c, 0, 5, 32, 1, grain);
        Set(c, 13, 9, wood);
        Set(c, 14, 9, wood);
        Set(c, 15, 9, wood);
        Set(c, 22, 5, wood);
        Set(c, 23, 5, wood);

        // Metal bolts in the end caps (inside the 8px horizontal borders).
        Rect(c, 3, 6, 2, 2, bolt);
        Rect(c, 3, 6, 2, 1, boltDark);
        Rect(c, 27, 6, 2, 2, bolt);
        Rect(c, 27, 6, 2, 1, boltDark);

        // 1px lighter top highlight, 1px darker bottom edge.
        Rect(c, 0, 15, 32, 1, top);
        Rect(c, 0, 0, 32, 1, bottom);

        return c;
    }

    // 16x16 grapple hook head pointing RIGHT. The 2px tip sits at x = 15 (the right
    // edge) on the vertical centre line, so the visual tip lands on the hit point.
    private static Canvas BuildHookHead()
    {
        Canvas c = New(16, 16);

        Color32 steel = new Color32(176, 188, 202, 255);
        Color32 steelHi = new Color32(208, 220, 232, 255);
        Color32 outline = new Color32(74, 84, 100, 255);
        Color32 cyan = new Color32(124, 244, 255, 255);
        Color32 cyanHot = new Color32(228, 255, 255, 255);
        Color32 cyanGlow = new Color32(140, 246, 255, 170);

        // Arrow head: widens leftwards, top half lit.
        for (int x = 5; x <= 15; x++)
        {
            float half = 4.5f - 0.4f * (x - 5);
            int k = Mathf.FloorToInt(half - 0.5f + 0.0001f);
            if (k < 0)
            {
                k = 0;
            }
            for (int y = 7 - k; y <= 8 + k; y++)
            {
                if (y >= 8)
                {
                    Set(c, x, y, steelHi);
                }
                else
                {
                    Set(c, x, y, steel);
                }
            }
        }

        // Hub and the two barbs that point back down the rope.
        Circle(c, 5f, 7.5f, 3.6f, steel);
        Triangle(c, 2f, 13f, 8.5f, 10f, 7f, 7f, steel);
        Triangle(c, 2f, 2f, 8.5f, 5f, 7f, 8f, steel);

        Outline(c, outline);

        // Bright cyan core with a short glow streak along the head.
        Circle(c, 5f, 7.5f, 2.2f, cyan);
        Circle(c, 5f, 7.5f, 1f, cyanHot);
        Set(c, 8, 7, cyanGlow);
        Set(c, 9, 7, cyanGlow);
        Set(c, 10, 7, cyanGlow);
        Set(c, 8, 8, cyanGlow);
        Set(c, 9, 8, cyanGlow);
        Set(c, 10, 8, cyanGlow);

        return c;
    }

    // 8x8 rope dash / particle: soft white dot with a 1px darker grey rim.
    private static Canvas BuildRopeSegment()
    {
        Canvas c = New(8, 8);

        Color32 white = new Color32(246, 248, 252, 255);
        Color32 rim = new Color32(126, 132, 146, 235);

        SoftCircle(c, 3.5f, 3.5f, 3.7f, rim, 1f);
        SoftCircle(c, 3.5f, 3.5f, 2.8f, white, 0.8f);

        return c;
    }

    // 8x8 impact spark: soft white dot with a cyan-white hot centre.
    private static Canvas BuildSpark()
    {
        Canvas c = New(8, 8);

        Color32 white = new Color32(252, 254, 255, 255);
        Color32 hot = new Color32(168, 246, 255, 255);

        SoftCircle(c, 3.5f, 3.5f, 3.6f, white, 1.5f);
        SoftCircle(c, 3.5f, 3.5f, 1.7f, hot, 0.7f);

        return c;
    }

    // 16x16 gold coin: yellow disc, darker gold rim, upper-left glint, square notch.
    private static Canvas BuildCoin()
    {
        Canvas c = New(16, 16);

        Color32 gold = new Color32(255, 206, 74, 255);
        Color32 goldRim = new Color32(214, 152, 30, 255);
        Color32 goldDark = new Color32(150, 100, 18, 255);
        Color32 notch = new Color32(178, 122, 22, 255);
        Color32 glint = new Color32(255, 255, 246, 255);

        Circle(c, 7.5f, 7.5f, 7.2f, goldDark);
        Circle(c, 7.5f, 7.5f, 6.5f, goldRim);
        Circle(c, 7.5f, 7.5f, 5.4f, gold);

        // Specular glint upper left (smaller x, larger y).
        Circle(c, 4.8f, 10.8f, 1.7f, glint);
        Set(c, 3, 12, glint);

        // Small darker square notch in the centre.
        Rect(c, 7, 7, 2, 2, notch);

        return c;
    }

    // 32x16: three upward metal spikes on a dark base bar, spanning the full width.
    private static Canvas BuildSpike()
    {
        Canvas c = New(32, 16);

        Color32 baseDark = new Color32(56, 60, 72, 255);
        Color32 baseTop = new Color32(92, 98, 114, 255);
        Color32 metal = new Color32(198, 206, 216, 255);
        Color32 metalLight = new Color32(238, 244, 252, 255);
        Color32 metalDark = new Color32(138, 146, 160, 255);
        Color32 outline = new Color32(52, 56, 68, 255);

        Rect(c, 0, 0, 32, 4, baseDark);
        Rect(c, 0, 3, 32, 1, baseTop);

        float[] apexX = { 5.5f, 15.5f, 25.5f };
        for (int i = 0; i < apexX.Length; i++)
        {
            float ax = apexX[i];
            Triangle(c, ax - 5f, 3.5f, ax + 5f, 3.5f, ax, 14f, metal);
            Triangle(c, ax - 5f, 3.5f, ax, 3.5f, ax, 14f, metalLight);
            Triangle(c, ax, 3.5f, ax + 5f, 3.5f, ax, 14f, metalDark);
        }

        Outline(c, outline);

        return c;
    }

    // 24x32 level exit: dark plinth on the bottom 4px, glowing cyan/violet column
    // above with a bright core, plus a few floating motes.
    private static Canvas BuildGoal()
    {
        Canvas c = New(24, 32);

        Color32 plinth = new Color32(48, 42, 78, 255);
        Color32 plinthHi = new Color32(80, 70, 120, 255);
        Color32 plinthLo = new Color32(28, 24, 48, 255);
        Color32 outer = new Color32(120, 96, 224, 255);
        Color32 mid = new Color32(96, 190, 248, 255);
        Color32 core = new Color32(186, 246, 255, 255);
        Color32 hot = new Color32(232, 255, 255, 245);
        Color32 mote = new Color32(150, 236, 255, 210);

        // Plinth, bottom 4px.
        Rect(c, 3, 0, 18, 4, plinth);
        Rect(c, 3, 3, 18, 1, plinthHi);
        Rect(c, 2, 0, 20, 1, plinthLo);
        Rect(c, 2, 0, 1, 4, plinthLo);
        Rect(c, 21, 0, 1, 4, plinthLo);

        // Energy column: three soft layers tapering towards the top.
        for (int layer = 0; layer < 3; layer++)
        {
            float inset = layer * 1.7f;
            Color32 col = layer == 0 ? outer : (layer == 1 ? mid : core);
            int a = layer == 0 ? 118 : (layer == 1 ? 226 : 255);

            for (int y = 4; y <= 30; y++)
            {
                float t = (y - 4) / 26f;
                float hw = 6.6f - 4.2f * t * t - inset;
                if (hw <= 0.5f)
                {
                    continue;
                }

                float fadeTop = Mathf.Clamp01((31f - y) / 3f);
                for (int x = 0; x < 24; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - 12f);
                    if (dx > hw)
                    {
                        continue;
                    }

                    float edge = Mathf.Clamp01((hw - dx) / 1.1f);
                    int al = ToByte(a * edge * fadeTop);
                    if (al <= 0)
                    {
                        continue;
                    }
                    Set(c, x, y, new Color32(col.r, col.g, col.b, (byte)al));
                }
            }
        }

        // Hot inner core line and the glow pooling on the plinth.
        Rect(c, 11, 5, 2, 22, hot);
        Rect(c, 9, 3, 6, 1, new Color32(150, 226, 255, 190));

        // Floating motes.
        Circle(c, 4.2f, 22.5f, 1.1f, mote);
        Circle(c, 20.2f, 18f, 1f, mote);
        Circle(c, 3.6f, 12.5f, 0.9f, mote);
        Circle(c, 20.6f, 9.5f, 1f, mote);
        Set(c, 19, 27, mote);
        Set(c, 4, 6, mote);

        return c;
    }

    // 16x32 checkpoint: slim dark pole, 2px dark base, bright green flag at the top.
    private static Canvas BuildCheckpoint()
    {
        Canvas c = New(16, 32);

        Color32 poleDark = new Color32(84, 88, 102, 255);
        Color32 poleHi = new Color32(140, 146, 162, 255);
        Color32 baseDark = new Color32(46, 48, 60, 255);
        Color32 green = new Color32(82, 226, 120, 255);
        Color32 greenDark = new Color32(40, 156, 78, 255);
        Color32 greenHi = new Color32(158, 252, 184, 255);
        Color32 outline = new Color32(34, 36, 46, 255);

        Rect(c, 4, 0, 8, 2, baseDark);           // 2px dark base
        Rect(c, 7, 2, 2, 28, poleDark);          // pole  x7..8  y2..29
        Rect(c, 7, 2, 1, 28, poleHi);            // lit left edge of the pole

        // Triangular flag pointing right, at the top of the pole.
        Triangle(c, 9f, 21f, 9f, 29f, 14f, 25f, green);
        Line(c, 9, 21, 14, 25, greenDark);
        Line(c, 9, 29, 14, 26, greenHi);

        Outline(c, outline);

        return c;
    }

    // 32x32 smooth vertical gradient: deep indigo at the top, muted violet/rose at
    // the bottom. Row by row float lerp, no banding steps.
    private static Canvas BuildBgGradient()
    {
        Canvas c = New(32, 32);

        Color bottom = new Color(0.28f, 0.16f, 0.30f, 1f);
        Color top = new Color(0.07f, 0.05f, 0.16f, 1f);

        for (int y = 0; y < 32; y++)
        {
            float t = y / 31f;
            Color32 row = Color.Lerp(bottom, top, t);
            row.a = 255;
            for (int x = 0; x < 32; x++)
            {
                SetRaw(c, x, y, row);
            }
        }

        return c;
    }

    // 128x64 parallax silhouette: lighter distant mountains behind, darker rolling
    // hills in front, each with a slightly lighter ridge line along the top.
    // Opaque inside the hills, fully transparent above.
    private static Canvas BuildBgHill()
    {
        Canvas c = New(128, 64);

        Color32 farFill = new Color32(50, 44, 88, 255);
        Color32 farRidge = new Color32(76, 70, 124, 255);
        Color32 nearFill = new Color32(34, 29, 63, 255);
        Color32 nearRidge = new Color32(66, 60, 110, 255);
        Color32 nearShade = new Color32(27, 23, 52, 255);

        for (int x = 0; x < 128; x++)
        {
            float h = 40f
                      + 9f * Mathf.Sin(x * 0.047f + 0.7f)
                      + 5f * Mathf.Sin(x * 0.113f + 2.3f)
                      + 7f * VNoise(x * 0.05f, 9.5f, Seed + 5);
            int top = Mathf.Clamp(Mathf.RoundToInt(h), 22, 58);

            for (int y = 0; y <= top; y++)
            {
                SetRaw(c, x, y, farFill);
            }
            SetRaw(c, x, top, farRidge);
        }

        for (int x = 0; x < 128; x++)
        {
            float h = 20f
                      + 8f * Mathf.Sin(x * 0.033f + 2.1f)
                      + 5f * Mathf.Sin(x * 0.079f + 0.5f)
                      + 6f * VNoise(x * 0.04f, 3.7f, Seed + 9);
            int top = Mathf.Clamp(Mathf.RoundToInt(h), 8, 46);

            for (int y = 0; y <= top; y++)
            {
                float n = VNoise((x + 0.5f) * 0.12f, (y + 0.5f) * 0.12f, Seed + 13);
                if (n > 0.74f)
                {
                    SetRaw(c, x, y, nearShade);
                }
                else
                {
                    SetRaw(c, x, y, nearFill);
                }
            }
            SetRaw(c, x, top, nearRidge);
        }

        return c;
    }

    // 64x24 soft pale violet cloud built from overlapping soft edged circles.
    private static Canvas BuildBgCloud()
    {
        Canvas c = New(64, 24);

        Color32 cloud = new Color32(198, 188, 232, 255);
        Color32 cloudHi = new Color32(222, 214, 246, 255);

        SoftCircle(c, 14f, 12f, 8f, cloud, 1.2f);
        SoftCircle(c, 25f, 13.5f, 9.5f, cloud, 1.2f);
        SoftCircle(c, 31f, 10.5f, 8.5f, cloud, 1.2f);
        SoftCircle(c, 37f, 12.5f, 9f, cloud, 1.2f);
        SoftCircle(c, 49f, 11f, 7.5f, cloud, 1.2f);
        SoftCircle(c, 20f, 15f, 5.5f, cloudHi, 1f);
        SoftCircle(c, 34f, 15.5f, 5f, cloudHi, 1f);
        SoftCircle(c, 44f, 14f, 4.5f, cloudHi, 1f);

        BleedEdges(c);

        return c;
    }

    // 8x8 landing dust: soft round warm grey dot with alpha falloff.
    private static Canvas BuildDust()
    {
        Canvas c = New(8, 8);

        Color32 warm = new Color32(206, 196, 182, 255);

        SoftCircle(c, 3.5f, 3.5f, 3.7f, warm, 3f);

        return c;
    }

    // 4x4 opaque white square for tinted UI / rect fills and debug boxes.
    private static Canvas BuildWhite()
    {
        Canvas c = New(4, 4);

        Color32 white = new Color32(255, 255, 255, 255);
        Fill(c, white);

        // 1px inset core, kept fully opaque so the sprite stays a full bleed fill.
        Rect(c, 1, 1, 2, 2, white);

        return c;
    }

    // ------------------------------------------------------------------
    // Weapons, projectiles and VFX
    // ------------------------------------------------------------------

    // 32x16 short sword lying horizontally and pointing +X: the tip sits on the
    // right edge at x = 31 on the vertical centre line, the pommel on the left
    // edge. Steel blade (4px at the guard, 2px at the tip) with a bright white
    // highlight along the top edge, a small dark grey crossguard just left of
    // centre, a warm brown grip and a dark round pommel.
    private static Canvas BuildWeaponSword()
    {
        Canvas c = New(32, 16);

        Color32 steel = new Color32(178, 190, 206, 255);
        Color32 steelHi = new Color32(226, 238, 248, 255);
        Color32 steelEdge = new Color32(248, 253, 255, 255);
        Color32 steelLo = new Color32(132, 146, 166, 255);
        Color32 guard = new Color32(96, 102, 120, 255);
        Color32 guardHi = new Color32(138, 146, 164, 255);
        Color32 guardLo = new Color32(70, 76, 92, 255);
        Color32 grip = new Color32(160, 106, 62, 255);
        Color32 gripLo = new Color32(122, 78, 44, 255);
        Color32 pommel = new Color32(92, 80, 74, 255);
        Color32 pommelHi = new Color32(134, 122, 114, 255);
        Color32 outline = new Color32(44, 48, 60, 255);

        // Blade: 4px thick out of the guard, narrowing to 2px at the tip so the
        // point still lands exactly on (31, 8).
        for (int x = 13; x <= 31; x++)
        {
            bool narrow = x >= 28;
            int yTop = narrow ? 8 : 9;
            int yBot = narrow ? 7 : 6;

            for (int y = yBot; y <= yTop; y++)
            {
                Color32 col;
                if (y == yTop)
                {
                    col = steelEdge;      // bright white edge along the top
                }
                else if (y == yTop - 1)
                {
                    col = steelHi;
                }
                else if (y == yBot)
                {
                    col = steelLo;        // shaded lower bevel
                }
                else
                {
                    col = steel;
                }
                Set(c, x, y, col);
            }
        }

        // Crossguard: 2px wide and taller than the blade, just left of centre.
        Rect(c, 11, 5, 2, 6, guard);
        Rect(c, 11, 10, 2, 1, guardHi);
        Rect(c, 11, 5, 2, 1, guardLo);

        // Grip: 2px tall warm brown with darker wrap bands.
        Rect(c, 3, 7, 8, 2, grip);
        Rect(c, 3, 7, 8, 1, gripLo);
        Rect(c, 5, 7, 1, 2, gripLo);
        Rect(c, 7, 7, 1, 2, gripLo);
        Rect(c, 9, 7, 1, 2, gripLo);

        // Round pommel flush with the left edge.
        Circle(c, 2f, 8f, 2.5f, pommel);
        Set(c, 1, 9, pommelHi);
        Set(c, 2, 9, pommelHi);

        Outline(c, outline);

        return c;
    }

    // 40x12 spear pointing +X: a 2px wooden shaft along the centre line, a dark
    // binding ring, and a grey/steel leaf-shaped head in the rightmost 12px with a
    // lit top half and a crisp white top edge. The point lands on (39, 5..6).
    private static Canvas BuildWeaponSpear()
    {
        Canvas c = New(40, 12);

        Color32 wood = new Color32(166, 114, 66, 255);
        Color32 woodHi = new Color32(200, 150, 98, 255);
        Color32 woodLo = new Color32(124, 80, 44, 255);
        Color32 binding = new Color32(78, 70, 64, 255);
        Color32 bindingHi = new Color32(122, 112, 102, 255);
        Color32 steel = new Color32(166, 178, 194, 255);
        Color32 steelHi = new Color32(210, 224, 238, 255);
        Color32 steelWhite = new Color32(244, 250, 255, 255);
        Color32 steelLo = new Color32(126, 138, 156, 255);
        Color32 outline = new Color32(44, 48, 60, 255);

        // Shaft: 2px along the centre line y = 5..6, x = 0..26.
        Rect(c, 0, 5, 27, 2, wood);
        Rect(c, 0, 6, 27, 1, woodHi);
        Set(c, 6, 5, woodLo);
        Set(c, 13, 5, woodLo);
        Set(c, 20, 5, woodLo);

        // Binding ring where the head meets the shaft.
        Rect(c, 26, 4, 2, 4, binding);
        Rect(c, 26, 7, 2, 1, bindingHi);

        // Leaf-shaped head, x = 28..39. halfH is the half height per column.
        float[] halfH = { 1.4f, 2.3f, 2.9f, 3.3f, 3.3f, 3.0f, 2.6f, 2.1f, 1.6f, 1.2f, 0.8f, 0.6f };
        for (int i = 0; i < halfH.Length; i++)
        {
            int x = 28 + i;
            float half = halfH[i];
            int yb = Mathf.CeilToInt(5.5f - half);
            int yt = Mathf.FloorToInt(5.5f + half);

            for (int y = yb; y <= yt; y++)
            {
                float dy = y + 0.5f - 6f;
                Color32 col;
                if (y == yt)
                {
                    col = steelWhite;
                }
                else if (dy > 0.2f)
                {
                    col = steelHi;
                }
                else if (dy < -1f)
                {
                    col = steelLo;
                }
                else
                {
                    col = steel;
                }
                Set(c, x, y, col);
            }
        }

        Outline(c, outline);

        return c;
    }

    // 28x28 war hammer with the head on the RIGHT: a chunky dark steel block with a
    // lit top face and a 1px dark outline, plus a short brown wooden handle running
    // to the left edge and ending in a dark round pommel.
    private static Canvas BuildWeaponHammer()
    {
        Canvas c = New(28, 28);

        Color32 steel = new Color32(118, 126, 144, 255);
        Color32 steelTop = new Color32(158, 170, 188, 255);
        Color32 steelTopHi = new Color32(202, 214, 230, 255);
        Color32 steelLo = new Color32(88, 94, 110, 255);
        Color32 wood = new Color32(166, 114, 66, 255);
        Color32 woodHi = new Color32(198, 148, 96, 255);
        Color32 woodLo = new Color32(124, 80, 44, 255);
        Color32 pommel = new Color32(88, 78, 74, 255);
        Color32 pommelHi = new Color32(126, 116, 110, 255);
        Color32 outline = new Color32(42, 46, 58, 255);

        // Handle: 4px tall, vertically centred, from the pommel into the head.
        Rect(c, 2, 12, 12, 4, wood);        // x2..13  y12..15
        Rect(c, 2, 15, 12, 1, woodHi);      // lit top row
        Rect(c, 2, 12, 12, 1, woodLo);      // shaded bottom row
        Rect(c, 5, 12, 1, 4, woodLo);       // grip wraps
        Rect(c, 8, 12, 1, 4, woodLo);
        Rect(c, 11, 12, 1, 4, woodLo);

        // Round pommel at the far left.
        RoundRect(c, 0, 11, 4, 6, 2, pommel);
        Set(c, 1, 15, pommelHi);
        Set(c, 2, 15, pommelHi);

        // Head: 16x14 block on the right, x = 12..27, y = 7..20.
        Rect(c, 12, 7, 16, 14, steel);
        Rect(c, 12, 16, 16, 5, steelTop);      // lit top face
        Rect(c, 12, 20, 16, 1, steelTopHi);    // 1px highlight along the top
        Rect(c, 12, 15, 16, 1, steelLo);       // seam under the top face
        Rect(c, 12, 7, 16, 1, steelLo);        // shaded bottom row

        Outline(c, outline);

        return c;
    }

    // 24x32 wooden recurve bow seen from the side, opening toward +X. The limb is a
    // warm-brown circular arc bulging to the left (the tips curve back toward the
    // string), a pale 1px string runs vertically down the right side of the limb,
    // and a small leather grip wrap sits at the middle of the limb.
    private static Canvas BuildWeaponBow()
    {
        Canvas c = New(24, 32);

        Color32 wood = new Color32(166, 114, 68, 255);
        Color32 woodHi = new Color32(204, 152, 100, 255);
        Color32 woodLo = new Color32(122, 78, 44, 255);
        Color32 leather = new Color32(106, 72, 52, 255);
        Color32 leatherHi = new Color32(142, 102, 78, 255);
        Color32 stringCol = new Color32(228, 234, 228, 255);
        Color32 outline = new Color32(44, 48, 60, 255);

        // Limb: an annulus of a circle centred right of the sprite, so the concave
        // side of the arc faces +X. dx <= -2.5 keeps the tips inside the canvas and
        // lets them curve back toward the string.
        float cx = 22.5f;
        float cy = 16f;
        float r = 14f;
        const float HalfT = 1.5f;

        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 24; x++)
            {
                float dx = x + 0.5f - cx;
                float dy = y + 0.5f - cy;
                if (dx > -2.5f)
                {
                    continue;
                }

                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float t = d - r;
                if (t > HalfT || t < -HalfT)
                {
                    continue;
                }

                Color32 col = t < -0.5f ? woodHi : (t > 0.5f ? woodLo : wood);
                Set(c, x, y, col);
            }
        }

        // Leather grip wrap at the middle of the limb.
        Rect(c, 6, 13, 6, 6, leather);      // x6..11  y13..18
        Rect(c, 6, 17, 6, 1, leatherHi);
        Rect(c, 6, 14, 6, 1, leatherHi);

        Outline(c, outline);

        // String: thin pale vertical line on the right side of the limb, drawn after
        // the outline so it stays 1px wide. It spans the two limb tips (y1..30).
        for (int y = 1; y <= 30; y++)
        {
            Set(c, 20, y, stringCol);
        }

        return c;
    }

    // 28x12 magic wand pointing +X: a dark violet wooden shaft with a subtle
    // highlight along the left 18px, a pale bone/grey binding ring, and a bright
    // cyan gem at the right tip (hot core plus a slightly darker cyan rim).
    private static Canvas BuildWeaponWand()
    {
        Canvas c = New(28, 12);

        Color32 violet = new Color32(98, 72, 152, 255);
        Color32 violetHi = new Color32(134, 104, 188, 255);
        Color32 violetLo = new Color32(72, 52, 116, 255);
        Color32 bone = new Color32(206, 200, 186, 255);
        Color32 boneLo = new Color32(158, 152, 140, 255);
        Color32 gemRim = new Color32(64, 196, 226, 255);
        Color32 gemCore = new Color32(168, 252, 255, 255);
        Color32 gemHot = new Color32(240, 255, 255, 255);
        Color32 outline = new Color32(40, 36, 56, 255);

        // Shaft: 2px along the centre line y = 5..6, x = 0..21.
        Rect(c, 0, 5, 22, 2, violet);
        Rect(c, 0, 6, 18, 1, violetHi);     // subtle highlight, left 18px only
        Set(c, 4, 5, violetLo);
        Set(c, 9, 5, violetLo);
        Set(c, 14, 5, violetLo);

        // Pale bone binding ring just before the gem.
        Rect(c, 18, 4, 2, 4, bone);
        Rect(c, 18, 4, 2, 1, boneLo);

        // Gem at the tip: darker cyan rim, bright core, hot glint.
        Circle(c, 25f, 6f, 3f, gemRim);
        Circle(c, 25f, 6f, 1.8f, gemCore);
        Set(c, 24, 7, gemHot);
        Set(c, 25, 7, gemHot);

        Outline(c, outline);

        return c;
    }

    // 16x8 glowing energy bolt pointing +X: a bright white-cyan elongated diamond
    // with a soft cyan halo that fades out at the edges, a sharp point on the right
    // edge at (15, 4) and a short trailing tail on the left. Corners stay clear.
    private static Canvas BuildBolt()
    {
        Canvas c = New(16, 8);

        Color32 coreHot = new Color32(255, 255, 255, 255);
        Color32 core = new Color32(214, 252, 255, 255);
        Color32 coreRim = new Color32(142, 238, 255, 255);
        Color32 halo = new Color32(110, 220, 255, 255);

        // Soft halo behind the bolt (max-alpha composited, so it never darkens).
        SoftCircle(c, 7.5f, 4f, 6.6f, new Color32(110, 220, 255, 110), 3f);

        // Half height of the solid core per column: fat near the head, a long taper
        // out to the point and a short tail on the left.
        float[] halfCore = { 1.0f, 1.6f, 2.1f, 2.5f, 2.8f, 2.9f, 2.9f, 2.8f,
                             2.6f, 2.3f, 2.0f, 1.7f, 1.4f, 1.1f, 0.9f, 0.7f };

        for (int x = 0; x < 16; x++)
        {
            float hc = halfCore[x];
            float hg = hc + 2.4f;
            if (hg > 3.45f)
            {
                hg = 3.45f;
            }

            for (int y = 0; y < 8; y++)
            {
                float dy = Mathf.Abs(y + 0.5f - 4f);
                if (dy > hg)
                {
                    continue;
                }

                if (dy <= hc)
                {
                    Color32 col = dy < 0.6f ? coreHot : (dy < hc - 0.4f ? core : coreRim);
                    Set(c, x, y, col);
                }
                else
                {
                    float a = Mathf.Clamp01((hg - dy) / 2.4f);
                    Set(c, x, y, new Color32(halo.r, halo.g, halo.b, ToByte(140f * a)));
                }
            }
        }

        return c;
    }

    // 48x48 melee slash arc: a thick crescent of translucent white-cyan energy that
    // opens toward +X (the concave side faces right). The band follows a circle of
    // radius ~18 centred at (21.5, 24) — just left of the sprite's middle, which is
    // what keeps the whole band inside the canvas — and its alpha tapers to zero at
    // both ends. A brighter thin line runs along the inner (right) edge.
    private static Canvas BuildSlash()
    {
        Canvas c = New(48, 48);

        Color32 band = new Color32(196, 246, 255, 255);
        Color32 bandOuter = new Color32(148, 224, 255, 255);
        Color32 inner = new Color32(244, 255, 255, 255);

        const float Cx = 21.5f;
        const float Cy = 24f;
        const float RIn = 15.5f;
        const float ROut = 21.5f;
        const float A0 = 65f;
        const float A1 = 295f;
        const float Span = A1 - A0;
        const float Taper = 0.16f;

        for (int y = 0; y < 48; y++)
        {
            for (int x = 0; x < 48; x++)
            {
                float dx = x + 0.5f - Cx;
                float dy = y + 0.5f - Cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < RIn - 1f || d > ROut + 1f)
                {
                    continue;
                }

                float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                if (ang < 0f)
                {
                    ang += 360f;
                }
                if (ang < A0 || ang > A1)
                {
                    continue;
                }

                float t = (ang - A0) / Span;
                float ends = Mathf.Clamp01(Mathf.Min(t, 1f - t) / Taper);
                if (ends <= 0f)
                {
                    continue;
                }

                // Brighter thin line along the inner edge.
                if (d <= RIn + 1.6f)
                {
                    float lit = Mathf.Clamp01((RIn + 1.6f - d) / 1f);
                    int la = ToByte(255f * ends * lit);
                    if (la > 0)
                    {
                        Set(c, x, y, new Color32(inner.r, inner.g, inner.b, (byte)la));
                    }
                    continue;
                }

                float u = (d - RIn) / (ROut - RIn);
                float prof = Mathf.Clamp01((1f - u) / 0.55f);
                int alpha = ToByte(238f * prof * ends);
                if (alpha <= 0)
                {
                    continue;
                }

                Color32 col = u > 0.55f ? bandOuter : band;
                Set(c, x, y, new Color32(col.r, col.g, col.b, (byte)alpha));
            }
        }

        return c;
    }
}
