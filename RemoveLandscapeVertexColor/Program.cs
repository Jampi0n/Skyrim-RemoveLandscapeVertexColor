using System;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using System.Threading.Tasks;
using System.Threading;

namespace RemoveLandscapeVertexColor {
    public class Program {
        public static async Task<int> Main(string[] args) {
            return await SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetTypicalOpen(GameRelease.SkyrimSE, "RemoveLandscapeVertexColor.esp")
                .Run(args);
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state) {
            object myLock = new();
            var contextArray = state.LoadOrder.PriorityOrder.Landscape().WinningContextOverrides(state.LinkCache).ToArray();
            int progressCounter = 0;
            var patchMod = state.PatchMod;

            Parallel.For(0,  contextArray.Length, new ParallelOptions() {
                MaxDegreeOfParallelism = 8,
            }, (int i) => {
                Console.WriteLine("Patch: " + Interlocked.Increment(ref progressCounter) + "/" + contextArray.Length);
                var landscapeGetter = contextArray[i].Record;
                if((landscapeGetter.DATA!.Value[0] & 2) == 2) {
                    ILandscape landscape;
                    lock(myLock) {
                        landscape = contextArray[i].GetOrAddAsOverride(patchMod);
                    }
                    var data = landscape.DATA!.Value.ToArray();
                    data[0] &= 253;
                    landscape.DATA = new Noggog.MemorySlice<byte>(data);
                    landscape.VertexColors = null;
                }
            });
        }
    }
}
