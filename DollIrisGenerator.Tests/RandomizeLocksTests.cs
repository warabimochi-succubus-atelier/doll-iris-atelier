using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using DollIrisGenerator.Models;
using DollIrisGenerator.Services;
using DollIrisGenerator.ViewModels;
using Xunit;

namespace DollIrisGenerator.Tests
{
    /// <summary>「属性ランダム生成」 with locked attribute groups.</summary>
    public sealed class RandomizeLocksTests : IDisposable
    {
        private readonly string _dir = Directory.CreateTempSubdirectory("dolliris_locks_").FullName;

        public void Dispose() => Directory.Delete(_dir, recursive: true);

        /// <summary>Every value a lock group promises to keep, as one comparable string.</summary>
        private static string Snapshot(IrisParameters p, RandomizeLocks group) => group switch
        {
            RandomizeLocks.Colors => string.Join("|", p.Name, p.BaseColor, p.InnerColor, p.OuterColor, p.BottomHighlightColor,
                p.InnerGradientRadius, p.OuterGradientRadius, p.BottomHighlightIntensity,
                p.LimbalRingColor, p.PupilColor, p.RayColor, p.ParticleColor, p.LensRingColor,
                p.OddEyeBaseColor, p.OddEyeInnerColor, p.OddEyeOuterColor, p.OddEyeRayColor, p.OddEyeBottomHighlightColor,
                p.AuroraColor, p.AuroraOpacity, p.AuroraBlend, p.NebulaColor, p.NebulaOpacity, p.NebulaDensity, p.NebulaScale, p.NebulaSwirl,
                p.LightAngle, p.LightStrength, p.FiberNoise, p.FiberNoiseScale),
            RandomizeLocks.Seed => p.Seed.ToString(),
            RandomizeLocks.Pupil => string.Join("|", p.PupilShape, p.PupilSizeRatio, p.PupilOpacity, p.PupilSoftness, p.PupilColor,
                p.PupilFacetIntensity, p.PupilFacetCount, p.PupilCornerRounding),
            RandomizeLocks.Rays => string.Join("|", p.RayCount, p.RayThickness, p.RayLengthRatio, p.RayIntensity,
                p.RayRandomness, p.RayInnerRadiusRatio, p.RayColor,
                p.RayTaper, p.RayWidthVariation, p.RayCurve, p.RayFineLayer),
            RandomizeLocks.LimbalRing => string.Join("|", p.LimbalRingThickness, p.LimbalRingOpacity, p.LimbalRingSoftness, p.LimbalRingColor),
            RandomizeLocks.Particles => string.Join("|", p.ParticleCount, p.ParticleMinSize, p.ParticleMaxSize,
                p.ParticleOpacity, p.ParticleRadiusRatio, p.ParticleColor,
                p.ParticleGlow, p.ParticleFlareRatio, p.ParticleFlareLength, p.GlitterBlend),
            RandomizeLocks.Symbols => WithoutIds(JsonSerializer.Serialize(p.Symbols)),
            RandomizeLocks.Highlights => WithoutIds(JsonSerializer.Serialize(p.Highlights)),
            RandomizeLocks.Lens => string.Join("|", p.LensPatternEnabled, p.LensRingCount, p.LensRingIntensity,
                p.LensRingWidth, p.LensRefractionGlow, p.LensRingColor),
            RandomizeLocks.OddEye => string.Join("|", p.IsOddEye, p.OddEyeSeedOffset, p.OddEyeBaseColor, p.OddEyeInnerColor,
                p.OddEyeOuterColor, p.OddEyeRayColor, p.OddEyeBottomHighlightColor),
            _ => throw new ArgumentOutOfRangeException(nameof(group))
        };

        // Highlight / symbol ids are new GUIDs for every generated item; compare what is drawn
        private static string WithoutIds(string json) =>
            System.Text.RegularExpressions.Regex.Replace(json, "\"Id\":\"[^\"]*\",?", "");

        /// <summary>A design whose values the randomizer never produces, so any change is visible.</summary>
        private static IrisParameters Distinctive()
        {
            var p = new IrisParameters
            {
                Name = "お気に入りの色",
                SizeMm = 13.0,
                Shape = IrisShape.HorizontalOval,
                AspectRatio = 1.4,
                TiltAngle = 12.0,
                Seed = 4242,
                BaseColor = "#123456",
                InnerColor = "linear-gradient(90deg, #ABCDEF 0%, #FEDCBA 100%)",
                OuterColor = "#0A0B0C",
                BottomHighlightColor = "#C0FFEE",
                InnerGradientRadius = 0.9,
                OuterGradientRadius = 1.2,
                BottomHighlightIntensity = 0.05,
                LimbalRingThickness = 0.29,
                LimbalRingColor = "#111111",
                LimbalRingOpacity = 0.1,
                LimbalRingSoftness = 0.9,
                PupilShape = PupilShape.Star,
                PupilSizeRatio = 0.7,
                PupilColor = "#222222",
                PupilOpacity = 0.3,
                PupilSoftness = 0.45,
                RayCount = 150,
                RayThickness = 7.0,
                RayLengthRatio = 0.15,
                RayIntensity = 0.02,
                RayRandomness = 0.99,
                RayColor = "#333333",
                RayInnerRadiusRatio = 0.75,
                RayTaper = 0.11,
                RayWidthVariation = 0.97,
                RayCurve = -0.93,
                RayFineLayer = 0.03,
                AuroraColor = "linear-gradient(10deg, #010203 0%, #040506 100%)",
                AuroraOpacity = 0.99,
                AuroraBlend = LayerBlend.Overlay,
                LightAngle = 10.0,
                LightStrength = 0.97,
                FiberNoise = 0.97,
                FiberNoiseScale = 1.97,
                NebulaColor = "linear-gradient(20deg, #0A0B0C 0%, #0C0D0E 100%)",
                NebulaOpacity = 0.03,
                NebulaDensity = 0.97,
                NebulaScale = 2.97,
                NebulaSwirl = -0.97,
                PupilFacetIntensity = 0.03,
                PupilFacetCount = 23,
                PupilCornerRounding = 0.9,
                LensPatternEnabled = true,
                LensRingCount = 20,
                LensRingIntensity = 0.99,
                LensRingWidth = 4.5,
                LensRingColor = "#444444",
                LensRefractionGlow = 0.99,
                ParticleCount = 400,
                ParticleMinSize = 9.0,
                ParticleMaxSize = 14.0,
                ParticleOpacity = 0.1,
                ParticleColor = "#555555",
                ParticleRadiusRatio = 0.15,
                ParticleGlow = 0.02,
                ParticleFlareRatio = 0.99,
                ParticleFlareLength = 7.9,
                GlitterBlend = LayerBlend.SoftLight,
                IsOddEye = true,
                OddEyeBaseColor = "#666666",
                OddEyeInnerColor = "#777777",
                OddEyeOuterColor = "#888888",
                OddEyeRayColor = "#999999",
                OddEyeBottomHighlightColor = "#AAAAAA",
                OddEyeSeedOffset = 7
            };
            p.Highlights.Clear();
            p.Highlights.Add(new HighlightItem { Shape = HighlightShape.Star4, PositionX = 0.11, PositionY = 0.22, Size = 0.33, Color = "#ABCDEF", Opacity = 0.44, Enabled = true, Blend = LayerBlend.ColorDodge });
            p.Symbols.Clear();
            p.Symbols.Add(new DecorativeSymbolItem { Type = SymbolType.Moon, Count = 5, Size = 0.4, PositionX = -0.3, PositionY = 0.3, Color = "#FEDCBA", Opacity = 0.5, Enabled = true });
            return p;
        }

        private static readonly RandomizeLocks[] Groups = RandomizeLocksInfo.Groups.Select(g => g.Lock).ToArray();

        public static IEnumerable<object[]> EachGroup() => Groups.Select(g => new object[] { g });

        [Theory]
        [MemberData(nameof(EachGroup))]
        public void LockedGroup_NeverChanges_AndUnlockedGroupsStillDo(RandomizeLocks locked)
        {
            var changed = new HashSet<RandomizeLocks>();
            for (int i = 0; i < 40; i++)
            {
                var p = Distinctive();
                string before = Snapshot(p, locked);
                var others = Groups.Where(g => g != locked).ToDictionary(g => g, g => Snapshot(p, g));

                IrisRandomizer.RandomizeAttributes(p, locked, new Random(i));

                Assert.Equal(before, Snapshot(p, locked));
                Assert.Equal(13.0, p.SizeMm);
                Assert.Equal(IrisShape.HorizontalOval, p.Shape);
                Assert.Equal(1.4, p.AspectRatio);
                Assert.Equal(12.0, p.TiltAngle);
                foreach (var (group, snapshot) in others)
                {
                    if (Snapshot(p, group) != snapshot) changed.Add(group);
                }
            }

            // Groups that share a color with the locked one (色 vs. each part) still change their other values
            Assert.Equal(Groups.Where(g => g != locked).OrderBy(g => g), changed.OrderBy(g => g));
        }

        [Fact]
        public void ColorsLocked_KeepsThePaletteButRandomizesEverythingElse()
        {
            var p = Distinctive();
            string colors = Snapshot(p, RandomizeLocks.Colors);
            string pupil = Snapshot(p, RandomizeLocks.Pupil);
            string rays = Snapshot(p, RandomizeLocks.Rays);

            IrisRandomizer.RandomizeAttributes(p, RandomizeLocks.Colors, new Random(3));

            Assert.Equal(colors, Snapshot(p, RandomizeLocks.Colors));
            Assert.Equal("お気に入りの色", p.Name);
            Assert.Equal("linear-gradient(90deg, #ABCDEF 0%, #FEDCBA 100%)", p.InnerColor);
            Assert.NotEqual(pupil, Snapshot(p, RandomizeLocks.Pupil));
            Assert.NotEqual(rays, Snapshot(p, RandomizeLocks.Rays));
            Assert.NotEqual(4242, p.Seed);
            // New symbols take the kept glitter color
            Assert.All(p.Symbols, s => Assert.Equal("#555555", s.Color));
        }

        [Fact]
        public void PartLock_KeepsThatPartsColor_EvenWhenColorsAreFree()
        {
            for (int i = 0; i < 20; i++)
            {
                var p = Distinctive();
                IrisRandomizer.RandomizeAttributes(p, RandomizeLocks.Rays | RandomizeLocks.Pupil, new Random(i));
                Assert.Equal("#333333", p.RayColor);
                Assert.Equal("#222222", p.PupilColor);
                Assert.NotEqual("#123456", p.BaseColor);
            }
        }

        [Fact]
        public void NoLocks_ChangesEveryGroup()
        {
            var p = Distinctive();
            var before = Groups.ToDictionary(g => g, g => Snapshot(p, g));
            IrisRandomizer.RandomizeAttributes(p, new Random(5)); // the old signature: nothing locked
            Assert.All(Groups, g => Assert.NotEqual(before[g], Snapshot(p, g)));
        }

        /// <summary>Settings 「属性ランダム生成」 never changes: the size and outline, and how the pair of eyes is built.</summary>
        private static readonly HashSet<string> AlwaysKept = new()
        {
            nameof(IrisParameters.SizeMm), nameof(IrisParameters.Shape), nameof(IrisParameters.AspectRatio),
            nameof(IrisParameters.TiltAngle), nameof(IrisParameters.MirrorHighlightsWithIris)
        };

        private static string ValueOf(System.Reflection.PropertyInfo prop, IrisParameters p) =>
            WithoutIds(JsonSerializer.Serialize(prop.GetValue(p)));

        /// <summary>
        /// Guards against a new design setting being left out of the randomizer: with nothing locked, every saved
        /// setting except <see cref="AlwaysKept"/> changes in some run, and is covered by one lock group's snapshot.
        /// </summary>
        [Fact]
        public void EveryDesignSetting_IsRandomized_AndBelongsToALockGroup()
        {
            var props = typeof(IrisParameters).GetProperties()
                .Where(pr => pr.CanRead && pr.GetIndexParameters().Length == 0
                             && (pr.CanWrite || pr.PropertyType.IsGenericType) // the highlight / symbol lists are get-only
                             && pr.GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonIgnoreAttribute), true).Length == 0)
                .ToList();
            var original = Distinctive();
            var changed = new HashSet<string>();
            for (int i = 0; i < 40; i++)
            {
                var p = Distinctive();
                IrisRandomizer.RandomizeAttributes(p, new Random(i));
                foreach (var prop in props)
                {
                    if (ValueOf(prop, p) != ValueOf(prop, original)) changed.Add(prop.Name);
                }
            }

            var neverChanged = props.Select(pr => pr.Name).Where(n => !changed.Contains(n) && !AlwaysKept.Contains(n)).OrderBy(n => n);
            Assert.Empty(neverChanged);
            Assert.All(AlwaysKept, n => Assert.DoesNotContain(n, changed));

            // Each randomized setting is kept by at least one lock group on its own
            var keptBySomeLock = new HashSet<string>();
            foreach (var group in Groups)
            {
                var kept = new HashSet<string>(changed);
                for (int i = 0; i < 40; i++)
                {
                    var p = Distinctive();
                    IrisRandomizer.RandomizeAttributes(p, group, new Random(i));
                    kept.RemoveWhere(n => ValueOf(typeof(IrisParameters).GetProperty(n)!, p) != ValueOf(typeof(IrisParameters).GetProperty(n)!, original));
                }
                keptBySomeLock.UnionWith(kept);
            }
            Assert.Empty(changed.Except(keptBySomeLock).OrderBy(n => n));
        }

        [Fact]
        public void AllLocked_ChangesNothing()
        {
            var p = Distinctive();
            string before = JsonSerializer.Serialize(p);
            IrisRandomizer.RandomizeAttributes(p, RandomizeLocks.All, new Random(1));
            Assert.Equal(before, JsonSerializer.Serialize(p));
        }

        // ------------------------------------------------------------------
        // View model
        // ------------------------------------------------------------------

        private MainViewModel NewViewModel() => new(new FakeDialogService(), new FavoritesStore(Path.Combine(_dir, "favorites.json")));

        [Fact]
        public void ViewModel_LockedColorsSurviveTheCommand_AndUndoIsOneStep()
        {
            var vm = NewViewModel();
            vm.CurrentIris.BaseColor = "#123456";
            vm.CurrentIris.RayColor = "#333333";
            vm.CurrentIris.PupilShape = PupilShape.Star;
            vm.CurrentIris.PupilSizeRatio = 0.7;
            vm.LockColors = true;
            var before = vm.CurrentIris.Clone();
            string beforeJson = JsonSerializer.Serialize(before);
            int undoCount = UndoDepth(vm);

            vm.RandomizeAttributesCommand.Execute(null);

            Assert.Equal(Snapshot(before, RandomizeLocks.Colors), Snapshot(vm.CurrentIris, RandomizeLocks.Colors));
            Assert.NotEqual(Snapshot(before, RandomizeLocks.Pupil), Snapshot(vm.CurrentIris, RandomizeLocks.Pupil));
            Assert.Contains("ロック中の項目 (色)", vm.StatusMessage);
            Assert.StartsWith("属性ランダム生成", vm.HistoryItems[0].Details);

            vm.UndoCommand.Execute(null);
            Assert.Equal(beforeJson, JsonSerializer.Serialize(vm.CurrentIris));
            Assert.Equal(undoCount, UndoDepth(vm));
        }

        /// <summary>Undo steps available, counted without changing the view model's state for the caller.</summary>
        private static int UndoDepth(MainViewModel vm)
        {
            var field = typeof(MainViewModel).GetField("_undoStack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            return ((System.Collections.ICollection)field.GetValue(vm)!).Count;
        }

        [Fact]
        public void ViewModel_EverythingLocked_DoesNotAddAnUndoStep()
        {
            var vm = NewViewModel();
            vm.RandomizeLocks = RandomizeLocks.All;
            string before = JsonSerializer.Serialize(vm.CurrentIris);
            bool couldUndo = vm.CanUndo;

            vm.RandomizeAttributesCommand.Execute(null);

            Assert.Equal(before, JsonSerializer.Serialize(vm.CurrentIris));
            Assert.Equal(couldUndo, vm.CanUndo);
            Assert.Contains("すべての属性がロック", vm.StatusMessage);
        }

        [Fact]
        public void ViewModel_CheckBoxesMapToTheFlags_AndClearRemovesThem()
        {
            var vm = NewViewModel();
            var raised = new List<string?>();
            vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            Assert.False(vm.HasRandomizeLocks);
            Assert.False(vm.ClearRandomizeLocksCommand.CanExecute(null));

            vm.LockColors = true;
            vm.LockOddEye = true;
            Assert.Equal(RandomizeLocks.Colors | RandomizeLocks.OddEye, vm.RandomizeLocks);
            Assert.Contains(nameof(MainViewModel.LockColors), raised);
            Assert.Equal("🔒 ロック中: 色・オッドアイ", vm.RandomizeLocksSummary);

            vm.LockColors = false;
            Assert.Equal(RandomizeLocks.OddEye, vm.RandomizeLocks);

            vm.RandomizeLocks = RandomizeLocks.Pupil | (RandomizeLocks)4096; // unknown bit from a newer file
            Assert.Equal(RandomizeLocks.Pupil, vm.RandomizeLocks);
            Assert.True(vm.LockPupil);

            Assert.True(vm.ClearRandomizeLocksCommand.CanExecute(null));
            vm.ClearRandomizeLocksCommand.Execute(null);
            Assert.Equal(RandomizeLocks.None, vm.RandomizeLocks);
            Assert.False(vm.LockPupil);
        }

        [Fact]
        public void ViewModel_LockChangesAreNotDesignUndoSteps()
        {
            var vm = NewViewModel();
            int depth = UndoDepth(vm);
            vm.LockRays = true;
            vm.LockHighlights = true;
            Assert.Equal(depth, UndoDepth(vm));
        }

        // ------------------------------------------------------------------
        // Saved with the window settings
        // ------------------------------------------------------------------

        [Fact]
        public void WindowSettings_RemembersLocksAcrossRestarts()
        {
            string path = Path.Combine(_dir, "windowsettings.json");
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var window = new System.Windows.Window { Width = 1300, Height = 800 };
                    WindowSettingsService.SaveSettings(window, path, randomizeLocks: RandomizeLocks.Colors | RandomizeLocks.Highlights);
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();

            Assert.Contains("\"Colors, Highlights\"", File.ReadAllText(path));
            Assert.Equal(RandomizeLocks.Colors | RandomizeLocks.Highlights, WindowSettingsService.LoadSettings(path).RandomizeLocks);
        }

        [Fact]
        public void WindowSettings_OldOrOddLockValuesLoadSafely()
        {
            string path = Path.Combine(_dir, "windowsettings.json");

            // Files written before this setting existed: nothing locked
            File.WriteAllText(path, "{\"Width\":1440,\"Height\":880,\"WindowState\":\"Normal\"}");
            Assert.Equal(RandomizeLocks.None, WindowSettingsService.LoadSettings(path).RandomizeLocks);

            // Unknown bits are dropped, known ones kept
            File.WriteAllText(path, "{\"Width\":1440,\"Height\":880,\"RandomizeLocks\":4097}");
            Assert.Equal(RandomizeLocks.Colors, WindowSettingsService.LoadSettings(path).RandomizeLocks);

            // An unknown name makes the file unreadable as a whole: fall back to defaults instead of crashing
            File.WriteAllText(path, "{\"Width\":1500,\"Height\":880,\"RandomizeLocks\":\"Colors, Sparkles\"}");
            Assert.Equal(RandomizeLocks.None, WindowSettingsService.LoadSettings(path).RandomizeLocks);
        }
    }
}
