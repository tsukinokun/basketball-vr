using System;
using System.IO;
using UnityEditor;
using UnityEngine;

//バスケットボールの会場っぽい効果音をその場で合成してWAVに書き出す（外部素材を使わない）
//Tools > Basketball > Generate Sounds で Assets/Sounds/Generated に作り直す
public static class SfxSynth
{
    const int Rate = 44100;
    const string Dir = "Assets/Sounds/Generated";
    static System.Random Rng;

    [MenuItem("Tools/Basketball/Generate Sounds")]
    public static void GenerateAll()
    {
        Directory.CreateDirectory(Dir);
        Rng = new System.Random(1234); //毎回同じ音になるように
        Save("SE_RimClang", RimClang());
        Save("SE_Backboard", Backboard());
        Save("SE_Whistle", Whistle());
        Save("SE_Cheer", Cheer());
        Save("SE_Applause", Applause(4.5, 1.0));
        Save("AMB_Crowd", CrowdLoop());
        AssetDatabase.Refresh();
        foreach (string Name in new[] { "SE_RimClang", "SE_Backboard", "SE_Whistle", "SE_Cheer", "SE_Applause", "AMB_Crowd" })
        {
            AudioImporter Imp = (AudioImporter)AssetImporter.GetAtPath(Dir + "/" + Name + ".wav");
            Imp.forceToMono = true;
            AudioImporterSampleSettings S = Imp.defaultSampleSettings;
            S.loadType = AudioClipLoadType.DecompressOnLoad;
            S.compressionFormat = AudioCompressionFormat.Vorbis;
            S.quality = 0.7f;
            Imp.defaultSampleSettings = S;
            Imp.SaveAndReimport();
        }
        Debug.Log("[SfxSynth] generated sounds in " + Dir);
    }

    //リムに当たった金属音：響きの違う複数の音（非整数倍音）が速さを変えて減衰する
    static float[] RimClang()
    {
        double Len = 1.1;
        float[] B = new float[(int)(Len * Rate)];
        double[] F = { 587, 1391, 2263, 3187, 4410, 5630 };
        double[] A = { 1.0, 0.75, 0.5, 0.35, 0.22, 0.12 };
        double[] D = { 3.5, 5.5, 8.0, 11.0, 15.0, 20.0 };
        for (int i = 0; i < B.Length; i++)
        {
            double t = (double)i / Rate;
            double S = 0;
            for (int k = 0; k < F.Length; k++)
            {
                //少しずらした2つの音でうなり（金属のゆらぎ）を作る
                S += A[k] * Math.Exp(-D[k] * t) * (Math.Sin(2 * Math.PI * F[k] * t) + 0.6 * Math.Sin(2 * Math.PI * F[k] * 1.0045 * t + 1.3));
            }
            S += 0.6 * Math.Exp(-30 * t) * Math.Sin(2 * Math.PI * 150 * t); //当たった瞬間の鈍い音
            B[i] = (float)S;
        }
        //当たった瞬間の「カン」という硬い成分
        Biquad Hp = Biquad.HighPass(2500, 0.7);
        for (int i = 0; i < (int)(0.012 * Rate); i++)
        {
            double t = (double)i / Rate;
            B[i] += (float)(1.2 * Hp.Process(Noise()) * Math.Exp(-t * 350));
        }
        return Normalize(B, 0.9f);
    }

    //バックボードに当たった音：低い「ドン」と、板のビビり
    static float[] Backboard()
    {
        double Len = 0.6;
        float[] B = new float[(int)(Len * Rate)];
        Biquad Lp = Biquad.LowPass(900, 0.8);
        double[] F = { 410, 777, 1163, 1690 };
        for (int i = 0; i < B.Length; i++)
        {
            double t = (double)i / Rate;
            double S = 1.0 * Math.Exp(-16 * t) * Math.Sin(2 * Math.PI * (88 + 30 * Math.Exp(-40 * t)) * t);
            S += 0.5 * Math.Exp(-28 * t) * Math.Sin(2 * Math.PI * 176 * t);
            S += 0.9 * Lp.Process(Noise()) * Math.Exp(-45 * t);
            for (int k = 0; k < F.Length; k++)
            {
                S += 0.08 * Math.Exp(-(14 + 4 * k) * t) * Math.Sin(2 * Math.PI * F[k] * t + k);
            }
            B[i] = (float)S;
        }
        return Normalize(B, 0.9f);
    }

    //審判のホイッスル：中の玉が回って音が細かく震える
    static float[] Whistle()
    {
        double Len = 0.85;
        float[] B = new float[(int)(Len * Rate)];
        Biquad Breath = Biquad.BandPass(3000, 2.5);
        double Phase = 0;
        for (int i = 0; i < B.Length; i++)
        {
            double t = (double)i / Rate;
            double Trill = Math.Sin(2 * Math.PI * 38 * t); //玉の震え
            double Freq = 2850 + 90 * Trill + 60 * Math.Min(t / 0.08, 1.0);
            Phase += 2 * Math.PI * Freq / Rate;
            double Tone = Math.Sin(Phase) + 0.25 * Math.Sin(2 * Phase) + 0.08 * Math.Sin(3 * Phase);
            double Am = 0.7 + 0.3 * Trill;
            double Env = Math.Min(t / 0.025, 1.0) * Math.Min((Len - t) / 0.09, 1.0);
            B[i] = (float)(Env * (Tone * Am + 0.35 * Breath.Process(Noise())));
        }
        return Normalize(B, 0.85f);
    }

    //ゴール時の歓声：たくさんの声（のこぎり波を声の響きで絞ったもの）と叫び声・拍手を重ねる
    static float[] Cheer()
    {
        double Len = 3.6;
        int N = (int)(Len * Rate);
        float[] B = new float[N];
        int Voices = 46;
        for (int v = 0; v < Voices; v++)
        {
            double Start = Rand(0.0, 0.35);
            double Hold = Rand(1.0, 2.3);
            double Pitch = Rand(140, 330);
            double Gain = Rand(0.3, 1.0);
            double Vib = Rand(4, 7);
            Biquad F1 = Biquad.BandPass(Rand(650, 950), 4);
            Biquad F2 = Biquad.BandPass(Rand(1100, 1700), 5);
            Biquad Br = Biquad.BandPass(Rand(900, 2200), 1.5);
            double Phase = 0;
            for (int i = (int)(Start * Rate); i < N; i++)
            {
                double t = (double)i / Rate - Start;
                if (t > Hold + 1.2) break;
                //「イェーイ」と上がってから少し下がる抑揚
                double Shape = 1.0 + 0.25 * Math.Min(t / 0.25, 1.0) - 0.12 * Math.Max(t - 0.6, 0.0);
                double F = Pitch * Shape * (1 + 0.02 * Math.Sin(2 * Math.PI * Vib * t));
                Phase += F / Rate;
                double Saw = 2 * (Phase - Math.Floor(Phase)) - 1;
                double Env = Math.Min(t / 0.08, 1.0) * (t < Hold ? 1.0 : Math.Exp(-(t - Hold) * 4));
                double S = 1.2 * F1.Process(Saw) + 0.8 * F2.Process(Saw) + 0.5 * Br.Process(Noise());
                B[i] += (float)(Gain * Env * S);
            }
        }
        //口笛の「ヒュー」
        for (int w = 0; w < 4; w++)
        {
            double Start = Rand(0.2, 1.2);
            double L = Rand(0.4, 0.8);
            double F0 = Rand(900, 1400), F1 = F0 * Rand(1.4, 1.8);
            double Phase = 0;
            for (int i = (int)(Start * Rate); i < (int)((Start + L) * Rate) && i < N; i++)
            {
                double t = (double)i / Rate - Start;
                double F = F0 + (F1 - F0) * Math.Sin(Math.PI * 0.5 * Math.Min(t / (L * 0.6), 1.0));
                Phase += 2 * Math.PI * F / Rate;
                double Env = Math.Sin(Math.PI * t / L);
                B[i] += (float)(0.5 * Env * Math.Sin(Phase));
            }
        }
        //全体のざわめき（ゴーッという音）
        Biquad Roar = Biquad.BandPass(800, 0.6);
        for (int i = 0; i < N; i++)
        {
            double t = (double)i / Rate;
            double Env = Math.Min(t / 0.2, 1.0) * Math.Exp(-Math.Max(t - 1.4, 0.0) * 1.5);
            B[i] += (float)(2.5 * Env * Roar.Process(Noise()));
        }
        Normalize(B, 1.0f);
        //後半は拍手に移る
        float[] Clap = Applause(Len, 0.5);
        for (int i = 0; i < N; i++)
        {
            double t = (double)i / Rate;
            B[i] += Clap[i] * (float)(0.6 * Math.Min(Math.Max(t - 0.4, 0.0) / 0.8, 1.0));
        }
        LowPass(B, 7000);
        return Normalize(B, 0.9f);
    }

    //拍手：たくさんの短い「パン」を重ねる
    static float[] Applause(double Len, double Density)
    {
        int N = (int)(Len * Rate);
        float[] B = new float[N];
        double t0 = 0;
        while (t0 < Len - 0.05)
        {
            double Fade = Math.Max(0.0, 1.0 - Math.Max(t0 - (Len - 1.8), 0.0) / 1.8);
            double PerSec = 260 * Density * (0.35 + 0.65 * Fade);
            t0 += -Math.Log(1.0 - Rng.NextDouble()) / PerSec;
            double Gain = Rand(0.3, 1.0) * Fade;
            Biquad Bp = Biquad.BandPass(Rand(700, 2600), Rand(1.0, 2.5));
            int Start = (int)(t0 * Rate);
            int L = (int)(Rand(0.006, 0.014) * Rate);
            for (int i = 0; i < L * 3 && Start + i < N; i++)
            {
                double Env = Math.Exp(-(double)i / L * 2.5);
                B[Start + i] += (float)(Gain * Env * Bp.Process(Noise()));
            }
        }
        return Normalize(B, 0.9f);
    }

    //観客のざわめき（つなぎ目なくループする8秒）
    static float[] CrowdLoop()
    {
        double Loop = 8.0, Cross = 1.0;
        int N = (int)((Loop + Cross) * Rate);
        float[] B = new float[N];
        int Voices = 40;
        for (int v = 0; v < Voices; v++)
        {
            double Pitch = Rand(95, 260);
            double Gain = Rand(0.2, 1.0);
            double Phase = 0;
            double SylEnd = 0, SylLen = 0.2, SylStart = 0;
            bool Talking = true;
            Biquad F1 = Biquad.BandPass(500, 3), F2 = Biquad.BandPass(1500, 4);
            for (int i = 0; i < N; i++)
            {
                double t = (double)i / Rate;
                if (t >= SylEnd)
                {
                    //しゃべる音節・間を交互に決める（音節ごとに母音の響きを変える）
                    Talking = Rng.NextDouble() < (Talking ? 0.8 : 0.6);
                    SylStart = t;
                    SylLen = Talking ? Rand(0.09, 0.25) : Rand(0.1, 0.6);
                    SylEnd = t + SylLen;
                    F1 = Biquad.BandPass(Rand(300, 850), 3);
                    F2 = Biquad.BandPass(Rand(900, 2300), 4);
                }
                Phase += Pitch * (1 + 0.05 * Math.Sin(t * 3 + v)) / Rate;
                double Saw = 2 * (Phase - Math.Floor(Phase)) - 1;
                double Env = Talking ? Math.Sin(Math.PI * (t - SylStart) / SylLen) : 0.0;
                B[i] += (float)(Gain * Env * (F1.Process(Saw) + 0.6 * F2.Process(Saw)));
            }
        }
        Biquad Hum = Biquad.LowPass(500, 0.7);
        for (int i = 0; i < N; i++) B[i] += (float)(0.6 * Hum.Process(Noise()));
        LowPass(B, 3500); //遠くで聞こえる感じに
        //最後の1秒を最初の1秒に重ねてつなぎ目を消す
        int L = (int)(Loop * Rate), C = (int)(Cross * Rate);
        float[] Out = new float[L];
        for (int i = 0; i < L; i++)
        {
            if (i < C)
            {
                float A = (float)i / C;
                Out[i] = B[i] * Mathf.Sqrt(A) + B[L + i] * Mathf.Sqrt(1 - A);
            }
            else Out[i] = B[i];
        }
        return Normalize(Out, 0.7f);
    }

    //---- 共通 ----
    static double Noise() { return Rng.NextDouble() * 2 - 1; }
    static double Rand(double Min, double Max) { return Min + (Max - Min) * Rng.NextDouble(); }

    static void LowPass(float[] B, double Freq)
    {
        Biquad F = Biquad.LowPass(Freq, 0.7);
        for (int i = 0; i < B.Length; i++) B[i] = (float)F.Process(B[i]);
    }

    static float[] Normalize(float[] B, float Peak)
    {
        float Max = 1e-6f;
        foreach (float S in B) Max = Mathf.Max(Max, Mathf.Abs(S));
        for (int i = 0; i < B.Length; i++) B[i] *= Peak / Max;
        return B;
    }

    static void Save(string Name, float[] B)
    {
        //両端を少しだけフェードしてプチッというノイズを防ぐ（ループ素材以外）
        if (!Name.StartsWith("AMB"))
        {
            int F = Mathf.Min(64, B.Length / 2);
            for (int i = 0; i < F; i++) B[B.Length - 1 - i] *= (float)i / F;
        }
        using (BinaryWriter W = new BinaryWriter(File.Create(Dir + "/" + Name + ".wav")))
        {
            int Bytes = B.Length * 2;
            W.Write(new[] { 'R', 'I', 'F', 'F' });
            W.Write(36 + Bytes);
            W.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            W.Write(16);
            W.Write((short)1); //PCM
            W.Write((short)1); //モノラル
            W.Write(Rate);
            W.Write(Rate * 2);
            W.Write((short)2);
            W.Write((short)16);
            W.Write(new[] { 'd', 'a', 't', 'a' });
            W.Write(Bytes);
            foreach (float S in B) W.Write((short)Mathf.Clamp(S * 32767f, -32768f, 32767f));
        }
    }

    //2次のIIRフィルタ（RBJ Audio EQ Cookbook）
    class Biquad
    {
        double B0, B1, B2, A1, A2, X1, X2, Y1, Y2;

        static Biquad Make(double b0, double b1, double b2, double a0, double a1, double a2)
        {
            return new Biquad { B0 = b0 / a0, B1 = b1 / a0, B2 = b2 / a0, A1 = a1 / a0, A2 = a2 / a0 };
        }

        public static Biquad BandPass(double F, double Q)
        {
            double W = 2 * Math.PI * F / Rate, Al = Math.Sin(W) / (2 * Q), C = Math.Cos(W);
            return Make(Al, 0, -Al, 1 + Al, -2 * C, 1 - Al);
        }

        public static Biquad LowPass(double F, double Q)
        {
            double W = 2 * Math.PI * F / Rate, Al = Math.Sin(W) / (2 * Q), C = Math.Cos(W);
            return Make((1 - C) / 2, 1 - C, (1 - C) / 2, 1 + Al, -2 * C, 1 - Al);
        }

        public static Biquad HighPass(double F, double Q)
        {
            double W = 2 * Math.PI * F / Rate, Al = Math.Sin(W) / (2 * Q), C = Math.Cos(W);
            return Make((1 + C) / 2, -(1 + C), (1 + C) / 2, 1 + Al, -2 * C, 1 - Al);
        }

        public double Process(double X)
        {
            double Y = B0 * X + B1 * X1 + B2 * X2 - A1 * Y1 - A2 * Y2;
            X2 = X1; X1 = X; Y2 = Y1; Y1 = Y;
            return Y;
        }
    }
}
