using System;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;

namespace RemoveLandscapeVertexColor {
    public class Program {

        private static readonly List<string> log = new();
        public static async Task<int> Main(string[] args) {
            return await SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetTypicalOpen(GameRelease.SkyrimSE, "RemoveLandscapeVertexColor.esp")
                .Run(args);
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state) {
            object myLock = new();
            var contextArray = state.LoadOrder.PriorityOrder.Landscape().WinningContextOverrides(state.LinkCache).ToArray();
            int skipCounter = 0;
            int removeCounter = 0;
            int failCounter = 0;
            int progressCounter = 0;
            var patchMod = state.PatchMod;

            Parallel.For(0, contextArray.Length, new ParallelOptions() {
                MaxDegreeOfParallelism = 8,
            }, (int i) => {
                Console.WriteLine("Patch: " + Interlocked.Increment(ref progressCounter) + "/" + contextArray.Length);
                var landscapeGetter = contextArray[i].Record;
                bool hasVertexColor = false;
                try {
                    if((landscapeGetter.DATA!.Value[0] & 2) == 2) {
                        hasVertexColor = true;
                    } else {
                        Interlocked.Increment(ref skipCounter);
                    }
                } catch(Exception) {
                    var message = "Could not parse landscape record: " + landscapeGetter.FormKey;
                    lock(myLock) {
                        log.Add(message);
                    }
                    Interlocked.Increment(ref failCounter);
                }
                if(hasVertexColor) {
                    ILandscape landscape;
                    lock(myLock) {
                        landscape = contextArray[i].GetOrAddAsOverride(patchMod);
                    }
                    var data = landscape.DATA!.Value.ToArray();
                    data[0] &= 253;
                    landscape.DATA = new Noggog.MemorySlice<byte>(data);
                    landscape.VertexColors = null;
                    Interlocked.Increment(ref removeCounter);
                }
            });
            Console.WriteLine(removeCounter + " landscape records had their vertex colors removed.");
            Console.WriteLine(skipCounter + " landscape records were skipped, because they had no vertex colors.");
            Console.WriteLine(failCounter + " landscape records were skipped, because there were errors parsing the records.");

            if(log.Count > 0) {
                foreach(var str in log) {
                    Console.WriteLine(str);
                }
            }
        }
    }
}
