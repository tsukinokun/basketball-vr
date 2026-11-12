using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

//実寸のバスケットボールアリーナ（コートライン・観客席・観客・壁・天井・LEDボード・スコアボード）を組み立てる
//Tools > Basketball > Build Arena で「Arena」を作り直す。メッシュ・素材・テクスチャは Assets/Arena に保存する
//VR（Quest）で軽く動くように、色は1枚のパレットテクスチャで塗り分けて素材の数を減らし、影も落とさない
public static class ArenaBuilder
{
    const string Dir = "Assets/Arena";
    const string MeshDir = "Assets/Arena/Meshes";

    //コートの寸法（FIBA）。プレイヤーの正面のリング中心が z=4.2
    const float RimZ = 4.2f;
    const float Baseline = RimZ + 1.575f; //手前（リング側）のエンドライン
    const float CourtLength = 28.0f;
    const float CourtHalfWidth = 7.5f;
    const float FarBaseline = Baseline - CourtLength;
    const float CenterZ = Baseline - CourtLength * 0.5f;
    const float RunOffSide = 2.0f; //サイドラインの外の余白
    const float RunOffEnd = 3.0f; //エンドラインの外の余白
    const float LineWidth = 0.05f;

    //観客席
    const int Rows = 14;
    const float RowDepth = 0.8f;
    const float RowRise = 0.45f;
    const float FirstRow = 0.5f;
    const float SeatPitch = 0.55f;
    const float SectionLength = 6.5f; //観客をこの長さごとのブロックに分けて跳ねさせる

    //建物
    const float WallX = 21.5f;
    const float WallNear = 21.0f;
    const float WallFar = -37.0f;
    const float CeilingY = 16.0f;
    const float ConcourseY = 7.2f; //観客席の一番上の高さ（ここにLEDボード）

    //パレット（色の番号）
    enum P { Stands, Wall, Ceiling, Truss, Apron, Paint, Orange, White, Navy, Red, Black, Gray, Yellow, Skin1, Skin2, Skin3, Skin4, Corner, Gold, Count }
    static readonly Color[] Palette = {
        new Color(0.16f, 0.17f, 0.20f), //Stands
        new Color(0.08f, 0.10f, 0.18f), //Wall
        new Color(0.06f, 0.06f, 0.08f), //Ceiling
        new Color(0.38f, 0.40f, 0.44f), //Truss
        new Color(0.08f, 0.16f, 0.38f), //Apron
        new Color(0.85f, 0.38f, 0.10f), //Paint
        new Color(0.96f, 0.47f, 0.10f), //Orange
        new Color(0.92f, 0.92f, 0.92f), //White
        new Color(0.12f, 0.18f, 0.45f), //Navy
        new Color(0.78f, 0.13f, 0.13f), //Red
        new Color(0.09f, 0.09f, 0.10f), //Black
        new Color(0.50f, 0.50f, 0.53f), //Gray
        new Color(0.96f, 0.80f, 0.16f), //Yellow
        new Color(0.98f, 0.80f, 0.66f), //Skin1
        new Color(0.82f, 0.60f, 0.44f), //Skin2
        new Color(0.47f, 0.32f, 0.22f), //Skin3
        new Color(0.93f, 0.72f, 0.56f), //Skin4
        new Color(0.11f, 0.11f, 0.13f), //Corner
        new Color(0.90f, 0.70f, 0.22f), //Gold
    };

    static System.Random Rng;
    static Material MatPalette, MatLines, MatLight, MatRibbon;
    static Transform Root;

    [MenuItem("Tools/Basketball/Build Arena")]
    public static void Build()
    {
        Rng = new System.Random(42); //毎回同じ観客の並びになるように
        Directory.CreateDirectory(MeshDir);
        MakeMaterials();

        GameObject Old = GameObject.Find("Arena");
        if (Old != null) Undo.DestroyObjectImmediate(Old);
        GameObject RootGo = new GameObject("Arena");
        Undo.RegisterCreatedObjectUndo(RootGo, "Build Arena");
        Root = RootGo.transform;

        ResizeFloor();
        BuildCourt();
        List<Transform> Sections = new List<Transform>();
        List<Bounds> Areas = new List<Bounds>();
        //両サイド（コートの長さ方向）と、両エンド（リングの後ろ）に観客席
        float SideLen = CourtLength + RunOffEnd * 2.0f;
        float EndLen = (CourtHalfWidth + RunOffSide) * 2.0f;
        BuildStands("Stands_Right", new Vector3(CourtHalfWidth + RunOffSide, 0, CenterZ), 90.0f, SideLen, Sections, Areas);
        BuildStands("Stands_Left", new Vector3(-CourtHalfWidth - RunOffSide, 0, CenterZ), -90.0f, SideLen, Sections, Areas);
        BuildStands("Stands_Near", new Vector3(0, 0, Baseline + RunOffEnd), 0.0f, EndLen, Sections, Areas);
        BuildStands("Stands_Far", new Vector3(0, 0, FarBaseline - RunOffEnd), 180.0f, EndLen, Sections, Areas);
        BuildBuilding();
        List<Renderer> Ribbons = BuildRibbons();
        List<Graphic> ScreenBacks = BuildScoreboard(Ribbons);
        WireUp(Sections, Areas, Ribbons, ScreenBacks);

        EditorSceneManager.MarkSceneDirty(RootGo.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[ArenaBuilder] built arena: sections=" + Sections.Count);
    }

    //---- 素材 ----
    static void MakeMaterials()
    {
        //パレット（1色1ピクセル）
        Texture2D Pal = new Texture2D(Palette.Length, 1, TextureFormat.RGBA32, false);
        for (int i = 0; i < Palette.Length; i++) Pal.SetPixel(i, 0, Palette[i]);
        Pal.Apply();
        Texture2D PalTex = SaveTexture(Pal, "ArenaPalette", FilterMode.Point, TextureWrapMode.Clamp, false);

        //LEDボードの模様（右へ流れる矢印と帯）
        const int W = 512, H = 64;
        Texture2D Rib = new Texture2D(W, H, TextureFormat.RGBA32, false);
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                Color C = new Color(0.02f, 0.03f, 0.08f);
                int Cx = x % 128;
                float V = Mathf.Abs(y - H * 0.5f) / (H * 0.5f); //中央0〜端1
                //シェブロン（>>>）
                int Chev = (Cx - (int)(V * 24) + 128) % 32;
                if (Cx < 64 && Chev < 10) C = Palette[(int)P.Orange];
                //文字っぽいブロック
                if (Cx >= 72 && Cx < 124 && y > 14 && y < 50 && ((Cx / 6) % 2 == 0 || y < 20 || y > 44) && ((x / 128) % 2 == 0)) C = Palette[(int)P.White];
                if (Cx >= 72 && Cx < 124 && y > 14 && y < 50 && (x / 128) % 2 == 1 && ((Cx + y) / 5) % 3 == 0) C = Palette[(int)P.Yellow];
                //上下の縁
                if (y < 4 || y >= H - 4) C = Palette[(int)P.Navy];
                Rib.SetPixel(x, y, C);
            }
        }
        Rib.Apply();
        Texture2D RibTex = SaveTexture(Rib, "ArenaRibbon", FilterMode.Bilinear, TextureWrapMode.Repeat, true);

        Shader Lit = Shader.Find("Universal Render Pipeline/Lit");
        Shader Unlit = Shader.Find("Universal Render Pipeline/Unlit");
        MatPalette = SaveMaterial("ArenaPalette", Lit, PalTex, Color.white);
        MatPalette.SetFloat("_Smoothness", 0.15f);
        MatLines = SaveMaterial("ArenaLines", Lit, null, new Color(0.95f, 0.95f, 0.95f));
        MatLines.SetFloat("_Smoothness", 0.3f);
        MatLight = SaveMaterial("ArenaLight", Unlit, null, new Color(1.0f, 0.98f, 0.92f));
        MatRibbon = SaveMaterial("ArenaRibbon", Unlit, RibTex, Color.white);
    }

    static Texture2D SaveTexture(Texture2D Tex, string Name, FilterMode Filter, TextureWrapMode Wrap, bool Mips)
    {
        string Path = Dir + "/" + Name + ".png";
        File.WriteAllBytes(Path, Tex.EncodeToPNG());
        Object.DestroyImmediate(Tex);
        AssetDatabase.ImportAsset(Path);
        TextureImporter Imp = (TextureImporter)AssetImporter.GetAtPath(Path);
        Imp.filterMode = Filter;
        Imp.wrapMode = Wrap;
        Imp.mipmapEnabled = Mips;
        Imp.npotScale = TextureImporterNPOTScale.None; //パレットの幅を変えると色がずれる
        Imp.sRGBTexture = true;
        Imp.textureCompression = Mips ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;
        Imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(Path);
    }

    static Material SaveMaterial(string Name, Shader Sh, Texture Tex, Color Col)
    {
        string Path = Dir + "/" + Name + ".mat";
        Material M = AssetDatabase.LoadAssetAtPath<Material>(Path);
        if (M == null)
        {
            M = new Material(Sh);
            AssetDatabase.CreateAsset(M, Path);
        }
        M.shader = Sh;
        M.SetTexture("_BaseMap", Tex);
        M.SetColor("_BaseColor", Col);
        M.enableInstancing = true;
        EditorUtility.SetDirty(M);
        return M;
    }

    //---- 床 ----
    static void ResizeFloor()
    {
        //建物の中全体に床を広げる（木目の大きさは元のまま 2.5m で1枚）
        GameObject Floor = GameObject.Find("Floor");
        if (Floor == null) return;
        Undo.RecordObject(Floor.transform, "Build Arena");
        float SizeX = WallX * 2.0f, SizeZ = WallNear - WallFar;
        Floor.transform.position = new Vector3(0, 0, (WallNear + WallFar) * 0.5f);
        Floor.transform.localScale = new Vector3(SizeX / 10.0f, 1, SizeZ / 10.0f);
        Material M = Floor.GetComponent<Renderer>().sharedMaterial;
        Undo.RecordObject(M, "Build Arena");
        M.SetTextureScale("_BaseMap", new Vector2(SizeX / 2.5f, SizeZ / 2.5f));
        if (M.HasProperty("_BumpMap")) M.SetTextureScale("_BumpMap", new Vector2(SizeX / 2.5f, SizeZ / 2.5f));
    }

    //---- コート（ライン・ペイント・外周） ----
    static void BuildCourt()
    {
        //塗り（コートの外周とペイントエリア・センターサークル）
        MB Fill = new MB();
        float Y = 0.004f;
        float X0 = -CourtHalfWidth, X1 = CourtHalfWidth;
        float Ox = CourtHalfWidth + RunOffSide;
        //外周（コートの外側を囲む4枚）
        Fill.FloorRect(-Ox, Ox, Baseline, Baseline + RunOffEnd, Y, P.Apron);
        Fill.FloorRect(-Ox, Ox, FarBaseline - RunOffEnd, FarBaseline, Y, P.Apron);
        Fill.FloorRect(-Ox, X0, FarBaseline, Baseline, Y, P.Apron);
        Fill.FloorRect(X1, Ox, FarBaseline, Baseline, Y, P.Apron);
        //ペイントエリア（幅4.9m、エンドラインからフリースローラインまで5.8m）
        Fill.FloorRect(-2.45f, 2.45f, Baseline - 5.8f, Baseline, Y, P.Navy);
        Fill.FloorRect(-2.45f, 2.45f, FarBaseline, FarBaseline + 5.8f, Y, P.Navy);
        Fill.FloorDisc(new Vector3(0, Y, CenterZ), 1.8f, 48, P.Paint);
        MakeObject("CourtPaint", Fill, MatPalette, Root, true);

        //白線
        MB L = new MB();
        float Ly = 0.008f;
        L.Polyline(new[] { new Vector3(X0, Ly, Baseline), new Vector3(X1, Ly, Baseline), new Vector3(X1, Ly, FarBaseline), new Vector3(X0, Ly, FarBaseline), new Vector3(X0, Ly, Baseline) }, LineWidth);
        L.Polyline(new[] { new Vector3(X0, Ly, CenterZ), new Vector3(X1, Ly, CenterZ) }, LineWidth);
        L.Polyline(Arc(new Vector3(0, Ly, CenterZ), 1.8f, 0, 360, 64), LineWidth);
        foreach (int Side in new[] { 1, -1 })
        {
            //Side=1 がリング側（+Z）、-1 が反対側
            float Base = Side > 0 ? Baseline : FarBaseline;
            float Rim = Base - Side * 1.575f;
            float Ft = Base - Side * 5.8f;
            //ペイントエリアの枠
            L.Polyline(new[] { new Vector3(-2.45f, Ly, Base), new Vector3(-2.45f, Ly, Ft), new Vector3(2.45f, Ly, Ft), new Vector3(2.45f, Ly, Base) }, LineWidth);
            //フリースローサークル
            L.Polyline(Arc(new Vector3(0, Ly, Ft), 1.8f, 0, 360, 48), LineWidth);
            //スリーポイントライン（半径6.75m、両端は6.6mの直線）
            //直線と円弧のつなぎ目は、リング中心からコート側へ Dz の所
            float Dz = Mathf.Sqrt(6.75f * 6.75f - 6.6f * 6.6f);
            float A0 = Mathf.Atan2(-Side * Dz, -6.6f) * Mathf.Rad2Deg;
            float A1 = Mathf.Atan2(-Side * Dz, 6.6f) * Mathf.Rad2Deg;
            List<Vector3> Three = new List<Vector3>();
            Three.Add(new Vector3(-6.6f, Ly, Base));
            Three.AddRange(Arc(new Vector3(0, Ly, Rim), 6.75f, A0, A1, 64, Side));
            Three.Add(new Vector3(6.6f, Ly, Base));
            L.Polyline(Three.ToArray(), LineWidth);
            //制限区域の半円（半径1.25m、コート側）
            L.Polyline(Arc(new Vector3(0, Ly, Rim), 1.25f, 180, 0, 24, Side), LineWidth);
        }
        MakeObject("CourtLines", L, MatLines, Root, true);
    }

    //中心 C、半径 R の円弧（角度は +X から +Z 回り、Side=-1 で逆回り）
    static Vector3[] Arc(Vector3 C, float R, float From, float To, int Seg, int Side = 1)
    {
        float Span = To - From;
        if (Side > 0 && Span < 0) Span += 360;
        if (Side < 0 && Span > 0) Span -= 360;
        if (Mathf.Approximately(Span, 0)) Span = 360;
        Vector3[] Pts = new Vector3[Seg + 1];
        for (int i = 0; i <= Seg; i++)
        {
            float A = (From + Span * i / Seg) * Mathf.Deg2Rad;
            Pts[i] = C + new Vector3(Mathf.Cos(A), 0, Mathf.Sin(A)) * R;
        }
        return Pts;
    }

    //---- 観客席と観客 ----
    //ローカル座標：コートは -Z 側、段は +Z に向かって高くなる、長さ方向は X
    static void BuildStands(string Name, Vector3 Pos, float Yaw, float Length, List<Transform> Sections, List<Bounds> Areas)
    {
        GameObject Go = new GameObject(Name);
        Go.transform.SetParent(Root, false);
        Go.transform.localPosition = Pos;
        Go.transform.localRotation = Quaternion.Euler(0, Yaw, 0);

        //段（1段ずつ床から積んだ箱）
        MB Steps = new MB();
        for (int r = 0; r < Rows; r++)
        {
            float Top = FirstRow + r * RowRise;
            float Z0 = r * RowDepth;
            Steps.Box(new Vector3(0, Top * 0.5f, Z0 + RowDepth * 0.5f), new Vector3(Length, Top, RowDepth), Face.Front | Face.Top | Face.Left | Face.Right, P.Stands);
            //座席の背もたれ（段の奥の細い帯）
            Steps.Box(new Vector3(0, Top + 0.2f, Z0 + RowDepth - 0.06f), new Vector3(Length, 0.4f, 0.08f), Face.Front | Face.Top, (r % 2 == 0) ? P.Navy : P.Red);
        }
        //一番前の手すり壁
        Steps.Box(new Vector3(0, 0.5f, -0.1f), new Vector3(Length, 1.0f, 0.1f), Face.Front | Face.Top, P.Black);
        MakeObject("Steps", Steps, MatPalette, Go.transform, true);

        //観客（ブロックごとに別のメッシュ。ブロックの境目は通路）
        int Count = Mathf.Max(1, Mathf.RoundToInt(Length / SectionLength));
        float SecLen = Length / Count;
        Bounds Area = new Bounds();
        bool HasArea = false;
        for (int s = 0; s < Count; s++)
        {
            MB Crowd = new MB();
            float Xs = -Length * 0.5f + s * SecLen;
            for (int r = 0; r < Rows; r++)
            {
                float Top = FirstRow + r * RowRise;
                float Z = r * RowDepth + 0.42f;
                for (float x = Xs + 0.6f; x < Xs + SecLen - 0.6f; x += SeatPitch)
                {
                    if (Rng.NextDouble() > 0.86) continue; //空席
                    AddPerson(Crowd, new Vector3(x + Rand(-0.05f, 0.05f), Top, Z));
                }
            }
            GameObject Sec = MakeObject("Crowd_" + s, Crowd, MatPalette, Go.transform, false);
            Sections.Add(Sec.transform);
            Bounds B = Sec.GetComponent<Renderer>().bounds;
            if (!HasArea) { Area = B; HasArea = true; } else Area.Encapsulate(B);
        }
        if (HasArea) Areas.Add(Area);
    }

    //座っている人（胴体と頭の箱）。前・上・左右だけ作る（後ろは見えない）
    static void AddPerson(MB M, Vector3 Seat)
    {
        P[] Shirts = { P.Orange, P.Orange, P.Orange, P.White, P.White, P.Navy, P.Red, P.Black, P.Gray, P.Yellow };
        P[] Skins = { P.Skin1, P.Skin2, P.Skin3, P.Skin4 };
        P Shirt = Shirts[Rng.Next(Shirts.Length)];
        P Skin = Skins[Rng.Next(Skins.Length)];
        float H = Rand(0.48f, 0.6f);
        float Lean = Rand(-0.04f, 0.04f);
        Face F = Face.Front | Face.Top | Face.Left | Face.Right;
        M.Box(Seat + new Vector3(Lean, 0.3f + H * 0.5f, 0), new Vector3(Rand(0.38f, 0.46f), H, 0.28f), F, Shirt);
        M.Box(Seat + new Vector3(Lean, 0.3f + H + 0.13f, -0.02f), new Vector3(0.19f, 0.23f, 0.2f), F, Skin);
    }

    //---- 建物（壁・天井・トラス・照明・角のブロック・バナー） ----
    static void BuildBuilding()
    {
        MB M = new MB();
        //壁（内側の面だけ）。下は暗い色、上は紺
        M.QuadFacing(new Vector3(-WallX, 0, WallNear), new Vector3(-WallX, CeilingY, WallNear), new Vector3(WallX, CeilingY, WallNear), new Vector3(WallX, 0, WallNear), Vector3.back, P.Wall);
        M.QuadFacing(new Vector3(-WallX, 0, WallFar), new Vector3(-WallX, CeilingY, WallFar), new Vector3(WallX, CeilingY, WallFar), new Vector3(WallX, 0, WallFar), Vector3.forward, P.Wall);
        M.QuadFacing(new Vector3(WallX, 0, WallFar), new Vector3(WallX, CeilingY, WallFar), new Vector3(WallX, CeilingY, WallNear), new Vector3(WallX, 0, WallNear), Vector3.left, P.Wall);
        M.QuadFacing(new Vector3(-WallX, 0, WallFar), new Vector3(-WallX, CeilingY, WallFar), new Vector3(-WallX, CeilingY, WallNear), new Vector3(-WallX, 0, WallNear), Vector3.right, P.Wall);
        //天井
        M.QuadFacing(new Vector3(-WallX, CeilingY, WallFar), new Vector3(-WallX, CeilingY, WallNear), new Vector3(WallX, CeilingY, WallNear), new Vector3(WallX, CeilingY, WallFar), Vector3.down, P.Ceiling);
        //四隅のブロック（観客席の間）
        float Sx = CourtHalfWidth + RunOffSide;
        float Zn = Baseline + RunOffEnd, Zf = FarBaseline - RunOffEnd;
        foreach (int Sgn in new[] { 1, -1 })
        {
            float Cx = Sgn * (Sx + WallX) * 0.5f, W = WallX - Sx;
            M.Box(new Vector3(Cx, ConcourseY * 0.5f, (Zn + WallNear) * 0.5f), new Vector3(W, ConcourseY, WallNear - Zn), Face.All & ~Face.Bottom, P.Corner);
            M.Box(new Vector3(Cx, ConcourseY * 0.5f, (Zf + WallFar) * 0.5f), new Vector3(W, ConcourseY, Zf - WallFar), Face.All & ~Face.Bottom, P.Corner);
            //選手の入場口（角のブロックの暗い穴）
            M.QuadFacing(new Vector3(Sgn * Sx - Sgn * 0.01f, 0, Zn + 3.0f), new Vector3(Sgn * Sx - Sgn * 0.01f, 3.2f, Zn + 3.0f), new Vector3(Sgn * Sx - Sgn * 0.01f, 3.2f, Zn + 6.5f), new Vector3(Sgn * Sx - Sgn * 0.01f, 0, Zn + 6.5f), new Vector3(-Sgn, 0, 0), P.Black);
        }
        //トラス（天井の梁）
        for (float Z = WallFar + 4.0f; Z < WallNear; Z += 6.5f)
        {
            M.Box(new Vector3(0, CeilingY - 0.9f, Z), new Vector3(WallX * 2.0f, 0.5f, 0.35f), Face.All & ~Face.Top, P.Truss);
            M.Box(new Vector3(0, CeilingY - 0.3f, Z), new Vector3(WallX * 2.0f, 0.15f, 0.25f), Face.All & ~Face.Top, P.Truss);
        }
        M.Box(new Vector3(-6, CeilingY - 1.2f, CenterZ), new Vector3(0.3f, 0.3f, WallNear - WallFar), Face.All & ~Face.Top, P.Truss);
        M.Box(new Vector3(6, CeilingY - 1.2f, CenterZ), new Vector3(0.3f, 0.3f, WallNear - WallFar), Face.All & ~Face.Top, P.Truss);
        //チャンピオンバナー（左右の壁の高い所）
        P[] BannerCols = { P.Orange, P.Navy, P.Red, P.Navy, P.Orange };
        int Bi = 0;
        foreach (int Sgn in new[] { 1, -1 })
        {
            for (float Z = CenterZ - 12.0f; Z <= CenterZ + 12.0f; Z += 6.0f)
            {
                float X = Sgn * (WallX - 0.05f);
                Vector3 N = new Vector3(-Sgn, 0, 0);
                P Col = BannerCols[Bi++ % BannerCols.Length];
                M.QuadFacing(new Vector3(X, 9.5f, Z - 1.1f), new Vector3(X, 14.0f, Z - 1.1f), new Vector3(X, 14.0f, Z + 1.1f), new Vector3(X, 9.5f, Z + 1.1f), N, Col);
                X -= Sgn * 0.02f;
                M.QuadFacing(new Vector3(X, 12.6f, Z - 0.9f), new Vector3(X, 12.9f, Z - 0.9f), new Vector3(X, 12.9f, Z + 0.9f), new Vector3(X, 12.6f, Z + 0.9f), N, P.White);
                M.QuadFacing(new Vector3(X, 10.6f, Z - 0.6f), new Vector3(X, 12.2f, Z - 0.6f), new Vector3(X, 12.2f, Z + 0.6f), new Vector3(X, 10.6f, Z + 0.6f), N, P.Gold);
                M.QuadFacing(new Vector3(X, 9.5f, Z - 1.1f), new Vector3(X, 9.8f, Z - 1.1f), new Vector3(X, 9.8f, Z + 1.1f), new Vector3(X, 9.5f, Z + 1.1f), N, P.White);
            }
        }
        MakeObject("Building", M, MatPalette, Root, true);

        //天井の照明（光る板。実際のライトは使わずに軽くする）
        MB Lights = new MB();
        for (float Z = WallFar + 4.0f + 3.25f; Z < WallNear - 2.0f; Z += 6.5f)
        {
            for (float X = -15.0f; X <= 15.0f; X += 5.0f)
            {
                float S = Mathf.Abs(X) < 8.0f ? 0.9f : 0.6f;
                Lights.QuadFacing(new Vector3(X - S, CeilingY - 0.5f, Z - S), new Vector3(X - S, CeilingY - 0.5f, Z + S), new Vector3(X + S, CeilingY - 0.5f, Z + S), new Vector3(X + S, CeilingY - 0.5f, Z - S), Vector3.down, P.White);
            }
        }
        MakeObject("CeilingLights", Lights, MatLight, Root, true);
    }

    //---- LEDのリボンボード（観客席の上を一周＋リングの後ろの床置き） ----
    static List<Renderer> BuildRibbons()
    {
        List<Renderer> List = new List<Renderer>();
        float Y0 = ConcourseY + 0.1f, Y1 = ConcourseY + 0.9f;
        float Hh = Y1 - Y0;
        //壁4面（内側に少し浮かせる）
        MB M = new MB();
        float D = 0.06f;
        M.QuadUV(new Vector3(WallX - D, Y0, WallNear - D), new Vector3(WallX - D, Y1, WallNear - D), new Vector3(-WallX + D, Y1, WallNear - D), new Vector3(-WallX + D, Y0, WallNear - D), Vector3.back, (WallX * 2) / (Hh * 8));
        M.QuadUV(new Vector3(-WallX + D, Y0, WallFar + D), new Vector3(-WallX + D, Y1, WallFar + D), new Vector3(WallX - D, Y1, WallFar + D), new Vector3(WallX - D, Y0, WallFar + D), Vector3.forward, (WallX * 2) / (Hh * 8));
        M.QuadUV(new Vector3(WallX - D, Y0, WallFar + D), new Vector3(WallX - D, Y1, WallFar + D), new Vector3(WallX - D, Y1, WallNear - D), new Vector3(WallX - D, Y0, WallNear - D), Vector3.left, (WallNear - WallFar) / (Hh * 8));
        M.QuadUV(new Vector3(-WallX + D, Y0, WallNear - D), new Vector3(-WallX + D, Y1, WallNear - D), new Vector3(-WallX + D, Y1, WallFar + D), new Vector3(-WallX + D, Y0, WallFar + D), Vector3.right, (WallNear - WallFar) / (Hh * 8));
        List.Add(MakeObject("RibbonBoard", M, MatRibbon, Root, false).GetComponent<Renderer>());

        //リングの後ろ（エンドラインの外）の床置きLEDボード
        MB F = new MB();
        float Z = Baseline + RunOffEnd - 0.6f;
        F.Box(new Vector3(0, 0.45f, Z + 0.1f), new Vector3(12.0f, 0.9f, 0.2f), Face.Top | Face.Left | Face.Right, P.Black, true);
        F.QuadUV(new Vector3(-6.0f, 0.05f, Z), new Vector3(-6.0f, 0.85f, Z), new Vector3(6.0f, 0.85f, Z), new Vector3(6.0f, 0.05f, Z), Vector3.back, 12.0f / (0.8f * 8));
        GameObject Courtside = MakeObject("CourtsideBoard", F, MatRibbon, Root, false);
        List.Add(Courtside.GetComponent<Renderer>());
        return List;
    }

    //---- スコアボード（リングの奥の壁）と天井から吊るしたビジョン ----
    static List<Graphic> BuildScoreboard(List<Renderer> Ribbons)
    {
        //枠（正面はバックボードと重なって見えないので、リングの左右に1枚ずつ）
        MB M = new MB();
        foreach (float Bx in BoardX) M.Box(new Vector3(Bx, BoardY, WallNear - 0.25f), new Vector3(8.6f, 3.8f, 0.4f), Face.All & ~Face.Back, P.Black);
        //センターハングのビジョン（4面スクリーン）
        Vector3 Jc = new Vector3(0, 11.5f, CenterZ);
        M.Box(Jc, new Vector3(6.2f, 3.6f, 6.2f), Face.Top | Face.Bottom, P.Black);
        M.Box(Jc + Vector3.up * 2.3f, new Vector3(0.2f, 1.0f, 0.2f), Face.All, P.Truss);
        MakeObject("ScoreboardFrame", M, MatPalette, Root, true);
        MB S = new MB();
        S.Box(Jc, new Vector3(6.0f, 3.4f, 6.0f), Face.Front | Face.Back | Face.Left | Face.Right, P.Black, true, 2.0f);
        Ribbons.Add(MakeObject("Jumbotron", S, MatRibbon, Root, false).GetComponent<Renderer>());

        List<Graphic> Backs = new List<Graphic>();
        foreach (float Bx in BoardX) Backs.Add(BuildScoreCanvas(new Vector3(Bx, BoardY, WallNear - 0.47f)));
        return Backs;
    }

    static readonly float[] BoardX = { -8.5f, 8.5f };
    const float BoardY = 10.4f;

    //得点・残り時間の表示（ワールド空間のキャンバス、8m×3.36m）
    static Graphic BuildScoreCanvas(Vector3 Pos)
    {
        GameObject CanvasGo = new GameObject("ScoreboardCanvas", typeof(RectTransform), typeof(Canvas));
        CanvasGo.transform.SetParent(Root, false);
        Canvas Cv = CanvasGo.GetComponent<Canvas>();
        Cv.renderMode = RenderMode.WorldSpace;
        RectTransform Rt = (RectTransform)CanvasGo.transform;
        Rt.sizeDelta = new Vector2(1000, 420);
        Rt.localScale = Vector3.one * 0.008f;
        Rt.position = Pos;
        Rt.rotation = Quaternion.identity; //プレイヤー（-Z側）から読める向き

        Image Back = NewUI<Image>("Back", Rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Back.color = new Color(0.02f, 0.03f, 0.09f);
        Font Ft = AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/Fonts/RussoOne-Regular.ttf");
        NewText("Title", Rt, new Vector2(0, 0.78f), new Vector2(1, 1), "BASKETBALL VR", 64, Palette[(int)P.White], Ft);
        NewText("ScoreLabel", Rt, new Vector2(0.03f, 0.58f), new Vector2(0.47f, 0.78f), "SCORE", 60, Palette[(int)P.Orange], Ft);
        NewText("TimeLabel", Rt, new Vector2(0.53f, 0.58f), new Vector2(0.97f, 0.78f), "TIME", 60, Palette[(int)P.Orange], Ft);
        Text Score = NewText("Score", Rt, new Vector2(0.03f, 0.05f), new Vector2(0.47f, 0.6f), "000", 190, new Color(1.0f, 0.3f, 0.2f), Ft);
        Text Time = NewText("Time", Rt, new Vector2(0.53f, 0.05f), new Vector2(0.97f, 0.6f), "60.00", 150, new Color(1.0f, 0.85f, 0.2f), Ft);
        Image Div = NewUI<Image>("Divider", Rt, new Vector2(0.497f, 0.08f), new Vector2(0.503f, 0.75f), Vector2.zero, Vector2.zero);
        Div.color = new Color(0.2f, 0.25f, 0.4f);

        //GameManager の表示をそのまま写す
        GameManager Gm = Object.FindFirstObjectByType<GameManager>();
        if (Gm != null)
        {
            SerializedObject So = new SerializedObject(Gm);
            AddMirror(Score, So.FindProperty("txtScore").objectReferenceValue as Text);
            AddMirror(Time, So.FindProperty("txtTime").objectReferenceValue as Text);
        }
        return Back;
    }

    static void AddMirror(Text Target, Text Source)
    {
        TextMirror Mir = Target.gameObject.AddComponent<TextMirror>();
        SerializedObject So = new SerializedObject(Mir);
        So.FindProperty("Source").objectReferenceValue = Source;
        So.ApplyModifiedPropertiesWithoutUndo();
    }

    static T NewUI<T>(string Name, RectTransform Parent, Vector2 Min, Vector2 Max, Vector2 OMin, Vector2 OMax) where T : Graphic
    {
        GameObject Go = new GameObject(Name, typeof(RectTransform), typeof(T));
        RectTransform Rt = (RectTransform)Go.transform;
        Rt.SetParent(Parent, false);
        Rt.anchorMin = Min;
        Rt.anchorMax = Max;
        Rt.offsetMin = OMin;
        Rt.offsetMax = OMax;
        T G = Go.GetComponent<T>();
        G.raycastTarget = false;
        return G;
    }

    static Text NewText(string Name, RectTransform Parent, Vector2 Min, Vector2 Max, string Msg, int Size, Color Col, Font Ft)
    {
        Text Tx = NewUI<Text>(Name, Parent, Min, Max, Vector2.zero, Vector2.zero);
        Tx.font = Ft != null ? Ft : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Tx.fontSize = Size;
        Tx.alignment = TextAnchor.MiddleCenter;
        Tx.horizontalOverflow = HorizontalWrapMode.Overflow;
        Tx.verticalOverflow = VerticalWrapMode.Overflow;
        Tx.color = Col;
        Tx.text = Msg;
        return Tx;
    }

    //---- 既存のオブジェクトとつなぐ ----
    static void WireUp(List<Transform> Sections, List<Bounds> Areas, List<Renderer> Ribbons, List<Graphic> ScreenBacks)
    {
        AudioClip Rim = LoadClip("SE_RimClang"), Board = LoadClip("SE_Backboard");

        //アリーナの演出
        ArenaFx Fx = Root.gameObject.AddComponent<ArenaFx>();
        SerializedObject So = new SerializedObject(Fx);
        SetArray(So.FindProperty("CrowdSections"), Sections.ToArray());
        SetArray(So.FindProperty("Ribbons"), Ribbons.ToArray());
        SetArray(So.FindProperty("ScreenBacks"), ScreenBacks.ToArray());
        SerializedProperty AreaProp = So.FindProperty("FlashAreas");
        AreaProp.arraySize = Areas.Count;
        for (int i = 0; i < Areas.Count; i++) AreaProp.GetArrayElementAtIndex(i).boundsValue = Areas[i];
        So.FindProperty("GlowMat").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/GoalSparkMat.mat");
        So.FindProperty("AMB_Crowd").objectReferenceValue = LoadClip("AMB_Crowd");
        So.FindProperty("SE_Cheer").objectReferenceValue = LoadClip("SE_Cheer");
        So.FindProperty("SE_Applause").objectReferenceValue = LoadClip("SE_Applause");
        So.FindProperty("SE_Whistle").objectReferenceValue = LoadClip("SE_Whistle");
        So.ApplyModifiedPropertiesWithoutUndo();

        //リング・ボード・支柱に当たった音と揺れ
        GameObject RimGo = GameObject.Find("Hoop/Rim");
        GameObject NetGo = GameObject.Find("Hoop/Net");
        GameObject BoardGo = GameObject.Find("Hoop/Backboard");
        if (RimGo != null) SetSurface(RimGo, Rim, 1.0f, 1.0f, new[] { RimGo.transform, NetGo != null ? NetGo.transform : null }, Vector3.down, 0.012f);
        if (BoardGo != null) SetSurface(BoardGo, Board, 1.0f, 1.0f, new[] { BoardGo.transform }, Vector3.forward, 0.008f);
        foreach (string N in new[] { "Hoop/Pole", "Hoop/Arm", "Hoop/RimBracket" })
        {
            GameObject G = GameObject.Find(N);
            if (G != null) SetSurface(G, Rim, 0.6f, 0.55f, new Transform[0], Vector3.down, 0.0f);
        }

        //ネットを揺らす・コインの代わりにアリーナの演出・タイトルは拍手
        FxManager Fm = Object.FindFirstObjectByType<FxManager>();
        if (Fm != null && NetGo != null)
        {
            SerializedObject Fs = new SerializedObject(Fm);
            Fs.FindProperty("NetTransform").objectReferenceValue = NetGo.transform;
            Fs.ApplyModifiedProperties();
        }
        GameManager Gm = Object.FindFirstObjectByType<GameManager>();
        if (Gm != null)
        {
            SerializedObject Gs = new SerializedObject(Gm);
            Gs.FindProperty("PatCoin").objectReferenceValue = null;
            Gs.FindProperty("SE_Tada").objectReferenceValue = LoadClip("SE_Applause");
            Gs.ApplyModifiedProperties();
        }
    }

    static void SetSurface(GameObject Go, AudioClip Clip, float Vol, float Pitch, Transform[] Shake, Vector3 Dir, float Amount)
    {
        SurfaceSound S = Go.GetComponent<SurfaceSound>();
        if (S == null) S = Undo.AddComponent<SurfaceSound>(Go);
        SerializedObject So = new SerializedObject(S);
        So.FindProperty("Clip").objectReferenceValue = Clip;
        So.FindProperty("Volume").floatValue = Vol;
        So.FindProperty("Pitch").floatValue = Pitch;
        List<Transform> List = new List<Transform>();
        foreach (Transform T in Shake) if (T != null) List.Add(T);
        SetArray(So.FindProperty("ShakeTargets"), List.ToArray());
        So.FindProperty("ShakeDir").vector3Value = Dir;
        So.FindProperty("ShakeAmount").floatValue = Amount;
        So.ApplyModifiedProperties();
    }

    static void SetArray(SerializedProperty Prop, Object[] Items)
    {
        Prop.arraySize = Items.Length;
        for (int i = 0; i < Items.Length; i++) Prop.GetArrayElementAtIndex(i).objectReferenceValue = Items[i];
    }

    static AudioClip LoadClip(string Name)
    {
        return AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/Generated/" + Name + ".wav");
    }

    //---- メッシュを保存してオブジェクトにする ----
    static GameObject MakeObject(string Name, MB Builder, Material Mat, Transform Parent, bool IsStatic)
    {
        GameObject Go = new GameObject(Name);
        Go.transform.SetParent(Parent, false);
        //保存名は親の名前も付けて重ならないようにする
        string MeshName = (Parent != Root ? Parent.name + "_" : "") + Name;
        Mesh Me = Builder.ToMesh(MeshName);
        string Path = MeshDir + "/" + MeshName + ".asset";
        Mesh Existing = AssetDatabase.LoadAssetAtPath<Mesh>(Path);
        if (Existing != null)
        {
            EditorUtility.CopySerialized(Me, Existing);
            Object.DestroyImmediate(Me);
            Me = Existing;
        }
        else AssetDatabase.CreateAsset(Me, Path);
        Go.AddComponent<MeshFilter>().sharedMesh = Me;
        MeshRenderer R = Go.AddComponent<MeshRenderer>();
        R.sharedMaterial = Mat;
        R.shadowCastingMode = ShadowCastingMode.Off; //天井や壁で床が暗くならないように
        R.lightProbeUsage = LightProbeUsage.Off;
        R.reflectionProbeUsage = ReflectionProbeUsage.Off;
        if (IsStatic) GameObjectUtility.SetStaticEditorFlags(Go, StaticEditorFlags.BatchingStatic);
        return Go;
    }

    static float Rand(float Min, float Max) { return Min + (Max - Min) * (float)Rng.NextDouble(); }

    [System.Flags]
    enum Face { Front = 1, Back = 2, Left = 4, Right = 8, Top = 16, Bottom = 32, All = 63 }

    //メッシュを組み立てる道具
    class MB
    {
        readonly List<Vector3> V = new List<Vector3>();
        readonly List<Vector3> N = new List<Vector3>();
        readonly List<Vector2> UV = new List<Vector2>();
        readonly List<int> T = new List<int>();

        static Vector2 PalUV(P Col) { return new Vector2(((int)Col + 0.5f) / Palette.Length, 0.5f); }

        //4点の面。Facing の向きから見て表になるように並びを直す
        public void Quad(Vector3 A, Vector3 B, Vector3 C, Vector3 D, Vector3 Facing, Vector2 Ua, Vector2 Ub, Vector2 Uc, Vector2 Ud)
        {
            Vector3 Nr = Vector3.Cross(B - A, C - A);
            if (Vector3.Dot(Nr, Facing) < 0)
            {
                Vector3 Tv = B; B = D; D = Tv;
                Vector2 Tu = Ub; Ub = Ud; Ud = Tu;
            }
            int I = V.Count;
            V.Add(A); V.Add(B); V.Add(C); V.Add(D);
            Vector3 Nn = Facing.normalized;
            N.Add(Nn); N.Add(Nn); N.Add(Nn); N.Add(Nn);
            UV.Add(Ua); UV.Add(Ub); UV.Add(Uc); UV.Add(Ud);
            T.Add(I); T.Add(I + 1); T.Add(I + 2);
            T.Add(I); T.Add(I + 2); T.Add(I + 3);
        }

        public void QuadFacing(Vector3 A, Vector3 B, Vector3 C, Vector3 D, Vector3 Facing, P Col)
        {
            Vector2 U = PalUV(Col);
            Quad(A, B, C, D, Facing, U, U, U, U);
        }

        //A=左下, B=左上, C=右上, D=右下 として、横方向に Repeat 回くり返すUV
        public void QuadUV(Vector3 A, Vector3 B, Vector3 C, Vector3 D, Vector3 Facing, float Repeat)
        {
            Quad(A, B, C, D, Facing, new Vector2(0, 0), new Vector2(0, 1), new Vector2(Repeat, 1), new Vector2(Repeat, 0));
        }

        //箱（Uv=true の時はスクリーン用に各面 0〜Repeat のUV）
        public void Box(Vector3 C, Vector3 S, Face Faces, P Col, bool Uv = false, float Repeat = 1.0f)
        {
            Vector3 H = S * 0.5f;
            System.Func<float, float, float, Vector3> Pt = (x, y, z) => C + new Vector3(H.x * x, H.y * y, H.z * z);
            void F(Face Fc, Vector3 A, Vector3 B, Vector3 Cc, Vector3 D, Vector3 Nr)
            {
                if ((Faces & Fc) == 0) return;
                if (Uv) QuadUV(A, B, Cc, D, Nr, Repeat);
                else QuadFacing(A, B, Cc, D, Nr, Col);
            }
            F(Face.Front, Pt(-1, -1, -1), Pt(-1, 1, -1), Pt(1, 1, -1), Pt(1, -1, -1), Vector3.back);
            F(Face.Back, Pt(1, -1, 1), Pt(1, 1, 1), Pt(-1, 1, 1), Pt(-1, -1, 1), Vector3.forward);
            F(Face.Left, Pt(-1, -1, 1), Pt(-1, 1, 1), Pt(-1, 1, -1), Pt(-1, -1, -1), Vector3.left);
            F(Face.Right, Pt(1, -1, -1), Pt(1, 1, -1), Pt(1, 1, 1), Pt(1, -1, 1), Vector3.right);
            F(Face.Top, Pt(-1, 1, -1), Pt(-1, 1, 1), Pt(1, 1, 1), Pt(1, 1, -1), Vector3.up);
            F(Face.Bottom, Pt(-1, -1, 1), Pt(-1, -1, -1), Pt(1, -1, -1), Pt(1, -1, 1), Vector3.down);
        }

        public void FloorRect(float X0, float X1, float Z0, float Z1, float Y, P Col)
        {
            QuadFacing(new Vector3(X0, Y, Z0), new Vector3(X0, Y, Z1), new Vector3(X1, Y, Z1), new Vector3(X1, Y, Z0), Vector3.up, Col);
        }

        public void FloorDisc(Vector3 C, float R, int Seg, P Col)
        {
            for (int i = 0; i < Seg; i++)
            {
                float A0 = Mathf.PI * 2 * i / Seg, A1 = Mathf.PI * 2 * (i + 1) / Seg;
                Vector3 P0 = C + new Vector3(Mathf.Cos(A0), 0, Mathf.Sin(A0)) * R;
                Vector3 P1 = C + new Vector3(Mathf.Cos(A1), 0, Mathf.Sin(A1)) * R;
                QuadFacing(C, P0, P1, C, Vector3.up, Col); //1点つぶした四角形＝三角形
            }
        }

        //床に引く線（点の列を幅 W の帯にする。つなぎ目は少し重ねる）
        public void Polyline(Vector3[] Pts, float W)
        {
            Vector2 U = new Vector2(0.5f, 0.5f);
            for (int i = 0; i + 1 < Pts.Length; i++)
            {
                Vector3 Dir = (Pts[i + 1] - Pts[i]).normalized;
                Vector3 Side = new Vector3(-Dir.z, 0, Dir.x) * (W * 0.5f);
                Vector3 A = Pts[i] - Dir * (W * 0.5f), B = Pts[i + 1] + Dir * (W * 0.5f);
                Quad(A - Side, B - Side, B + Side, A + Side, Vector3.up, U, U, U, U);
            }
        }

        public Mesh ToMesh(string Name)
        {
            Mesh M = new Mesh { name = Name };
            M.indexFormat = V.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            M.SetVertices(V);
            M.SetNormals(N);
            M.SetUVs(0, UV);
            M.SetTriangles(T, 0);
            M.RecalculateBounds();
            M.RecalculateTangents();
            return M;
        }
    }
}
