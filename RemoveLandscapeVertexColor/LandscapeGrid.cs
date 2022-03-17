using System;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.FormKeys.SkyrimLE;
using System.Data;

namespace RemoveLandscapeVertexColor {

    public class VertexAlphaLayer {
        public readonly IFormLink<ILandscapeTextureGetter> texture;
        public readonly float opacity;

        public VertexAlphaLayer(IFormLink<ILandscapeTextureGetter> texture, float opacity) {
            this.texture = texture;
            this.opacity = opacity;
        }
    }
    public class LandscapeVertex {
        private static readonly Dictionary<IFormLink<ILandscapeTextureGetter>, bool> isSnowDict = new();

        public IFormLink<ILandscapeTextureGetter>? baseTexture;
        public List<VertexAlphaLayer> layers = new();
        public readonly int x;
        public readonly int y;
        public readonly LandscapeGrid grid;

        public LandscapeVertex(LandscapeGrid grid, int x, int y) {
            this.grid = grid;
            this.x = x;
            this.y = y;
        }

        public void SetColor(byte r, byte g, byte b) {
            grid.vertexColorArray[y, x] = new Noggog.P3UInt8(r, g, b);
        }

        public Noggog.P3UInt8 GetColor() {
            return grid.vertexColorArray[y, x];
        }

        public static bool IsSnow(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, IFormLink<ILandscapeTextureGetter>? textureLink) {
            if(textureLink == null || textureLink.IsNull) {
                return false;
            }
            if(!isSnowDict.ContainsKey(textureLink)) {
                lock(Program.myLock) {
                    if(isSnowDict.ContainsKey(textureLink)) {
                        return isSnowDict.GetValueOrDefault(textureLink, false);
                    }
                    var isSnow = false;
                    if(textureLink.TryResolve(state.LinkCache, out var texture)) {
                        if(texture.Flags.HasValue) {
                            isSnow = texture.Flags!.Value == LandscapeTexture.Flag.IsSnow;
                        }
                    }
                    isSnowDict.Add(textureLink, isSnow);
                    return isSnow;
                }
            }
            return isSnowDict.GetValueOrDefault(textureLink, false);
        }

        public void Patch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state) {
            float snow = 0f;
            float notSnow = 0f;
            var threshold = 0.2f;

            foreach(var layer in layers) {
                if(IsSnow(state, layer.texture)) {
                    snow += layer.opacity;
                } else {
                    notSnow += layer.opacity;
                }
                if(snow > threshold) {
                    break;
                }
            }
            if(snow <= threshold) {
                float missing = 1 - snow - notSnow;
                if(missing > 0f && IsSnow(state, this.baseTexture)) {
                    snow += missing;
                }
            }
            RGBFormula formula;
            if(snow > threshold) {
                formula = Program.settings.advanced.snow;
            } else {
                formula = Program.settings.advanced.standard;
            }
            var color = GetColor();
            SetColor(formula.Red(color), formula.Green(color), formula.Blue(color));
        }
    }
    public class LandscapeGrid {
        private readonly ILandscape record;
        private readonly LandscapeVertex[,] grid;
        public Noggog.IArray2d<Noggog.P3UInt8> vertexColorArray;

        public static Tuple<int, int> QuadrantOffset(Quadrant quadrant) {
            return quadrant switch {
                Quadrant.BottomLeft => new Tuple<int, int>(0, 0),
                Quadrant.BottomRight => new Tuple<int, int>(0, 16),
                Quadrant.TopLeft => new Tuple<int, int>(16, 0),
                Quadrant.TopRight => new Tuple<int, int>(16, 16),
                _ => new Tuple<int, int>(0, 0),
            };
        }

        public static Tuple<int, int> IntOffset(int pos) {
            int x = pos / 17;
            int y = pos - 17 * x;
            return new Tuple<int, int>(x, y);
        }

        public void Patch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state) {
            for(int x = 0; x < 33; ++x) {
                for(int y = 0; y < 33; ++y) {
                    grid[x, y].Patch(state);
                }
            }
            Write();

        }

        public LandscapeGrid(ILandscape record) {
            this.record = record;
            this.grid = new LandscapeVertex[33, 33];
            for(int x = 0; x < 33; ++x) {
                for(int y = 0; y < 33; ++y) {
                    grid[x, y] = new LandscapeVertex(this, x, y);
                }
            }
            foreach(var layer in this.record.Layers) {
                var quadrant = layer.Header!.Quadrant;
                quadrant = (Quadrant)(byte)quadrant;
                var offset = QuadrantOffset(quadrant);

                if(layer is AlphaLayer alphaLayer) {
                    if(alphaLayer.AlphaLayerData != null) {
                        var data = alphaLayer.AlphaLayerData!.Value;
                        for(int i = 0; i < data.Length; i += 8) {
                            var pos = IntOffset(BitConverter.ToInt16(data.Slice(i, 2)));
                            var opacity = BitConverter.ToSingle(data.Slice(i + 4, 4));
                            grid[pos.Item1 + offset.Item1, pos.Item2 + offset.Item2].layers.Add(new VertexAlphaLayer(layer.Header.Texture, opacity));
                        }
                    }
                } else {
                    for(int x = 0; x <= 16; ++x) {
                        for(int y = 0; y <= 16; ++y) {
                            grid[x + offset.Item1, y + offset.Item2].baseTexture = layer.Header.Texture;
                        }
                    }
                }
            }
            vertexColorArray = record.VertexColors!;
        }

        public void Write() {
            
        }
    }
}
