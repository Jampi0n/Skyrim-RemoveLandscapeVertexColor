using Mutagen.Bethesda.Synthesis.Settings;
using NCalc;
using System.Collections.Generic;
using System;

namespace RemoveLandscapeVertexColor {

    public class RGBFormula {
        [SynthesisTooltip("Formula to modify red value of the vertex. Available variables: R,G,B")]
        public string red = "R";
        [SynthesisTooltip("Formula to modify green value of the vertex. Available variables: R,G,B")]
        public string green = "G";
        [SynthesisTooltip("Formula to modify blue value of the vertex. Available variables: R,G,B")]
        public string blue = "B";

        private Expression? _r;
        private Expression? _g;
        private Expression? _b;

        private byte? constR = null;
        private byte? constG = null;
        private byte? constB = null;

        public void Compile() {
            _r = new("Truncate(" + red + ")");
            _g = new("Truncate(" + green + ")");
            _b = new("Truncate(" + blue + ")");
            try {
                constR = (byte)Math.Max(0, Math.Min(255, (double)_r.Evaluate()));
            } catch(Exception) { }
            try {
                constG = (byte)Math.Max(0, Math.Min(255, (double)_g.Evaluate()));
            } catch(Exception) { }
            try {
                constB = (byte)Math.Max(0, Math.Min(255, (double)_b.Evaluate()));
            } catch(Exception) { }
        }

        public static byte Evaluate(Expression e, Noggog.P3UInt8 colors) {
            object result;
            lock(e) {
                e.Parameters["R"] = (int)colors.X;
                e.Parameters["G"] = (int)colors.Y;
                e.Parameters["B"] = (int)colors.Z;
                result = e.Evaluate();
            }

            return (byte)Math.Max(0, Math.Min(255, (double) result));
        }

        public byte Red(Noggog.P3UInt8 colors) {
            return constR.GetValueOrDefault(Evaluate(_r!, colors));
        }
        public byte Green(Noggog.P3UInt8 colors) {
            return constG.GetValueOrDefault(Evaluate(_g!, colors));
        }
        public byte Blue(Noggog.P3UInt8 colors) {
            return constB.GetValueOrDefault(Evaluate(_b!, colors));
        }
    }
    public class Advanced {
        [SynthesisTooltip("Modify vertex colors on landscape textures classified as snow.")]
        public RGBFormula snow = new() {
            red = "Pow(R/255.0,0.1)*255",
            green = "Pow(G/255.0,0.1)*255",
            blue = "Pow(B/255.0,0.1)*255",
        };
        [SynthesisTooltip("Modify vertex colors on landscape textures not classified as snow.")]
        public RGBFormula standard = new() {
            red = "Pow(R/255.0,0.5)*255",
            green = "Pow(G/255.0,0.5)*255",
            blue = "Pow(B/255.0,0.5)*255",
        };

        public void Compile() {
            standard.Compile();
            snow.Compile();
        }
    }
    public class Settings {
        [SynthesisTooltip("Removes all vertex colors. Other settings will be ignored.")]
        public bool removeAllVertexVertexColors = false;

        [SynthesisTooltip("Modify vertex colors individually.")]
        public Advanced advanced = new();

        public void Compile() {
            advanced.Compile();
        }
    }
}
