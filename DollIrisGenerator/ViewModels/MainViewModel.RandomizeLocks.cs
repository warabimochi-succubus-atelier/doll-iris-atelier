using System.Windows.Input;
using DollIrisGenerator.Models;

namespace DollIrisGenerator.ViewModels
{
    // Locks for 「属性ランダム生成」: groups the user ticked stay as they are when attributes are randomized.
    // They are an app setting, not part of the design, so they are not in undo history and are saved with the
    // window settings (MainWindow restores them at start and saves them on exit).
    public partial class MainViewModel
    {
        private RandomizeLocks _randomizeLocks = RandomizeLocks.None;
        private ICommand? _clearRandomizeLocksCommand;

        public RandomizeLocks RandomizeLocks
        {
            get => _randomizeLocks;
            set
            {
                var sanitized = RandomizeLocksInfo.Sanitize(value);
                if (_randomizeLocks == sanitized) return;
                _randomizeLocks = sanitized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LockColors));
                OnPropertyChanged(nameof(LockSeed));
                OnPropertyChanged(nameof(LockPupil));
                OnPropertyChanged(nameof(LockRays));
                OnPropertyChanged(nameof(LockLimbalRing));
                OnPropertyChanged(nameof(LockParticles));
                OnPropertyChanged(nameof(LockSymbols));
                OnPropertyChanged(nameof(LockHighlights));
                OnPropertyChanged(nameof(LockLens));
                OnPropertyChanged(nameof(LockOddEye));
                OnPropertyChanged(nameof(HasRandomizeLocks));
                OnPropertyChanged(nameof(RandomizeLocksSummary));
            }
        }

        public bool HasRandomizeLocks => _randomizeLocks != RandomizeLocks.None;

        /// <summary>
        /// 「🔒 ロック中: 色・瞳孔」 or 「ロックなし …」 for the line under the randomize button. The check boxes themselves sit in
        /// each settings box across several tabs, so this line is the one place that shows every lock at once.
        /// </summary>
        public string RandomizeLocksSummary => HasRandomizeLocks
            ? $"🔒 ロック中: {RandomizeLocksInfo.Describe(_randomizeLocks)}"
            : "ロックなし (各設定枠の「🔒 ランダム生成で変えない」で固定できます)";

        public bool LockColors { get => IsLocked(RandomizeLocks.Colors); set => SetLock(RandomizeLocks.Colors, value); }
        public bool LockSeed { get => IsLocked(RandomizeLocks.Seed); set => SetLock(RandomizeLocks.Seed, value); }
        public bool LockPupil { get => IsLocked(RandomizeLocks.Pupil); set => SetLock(RandomizeLocks.Pupil, value); }
        public bool LockRays { get => IsLocked(RandomizeLocks.Rays); set => SetLock(RandomizeLocks.Rays, value); }
        public bool LockLimbalRing { get => IsLocked(RandomizeLocks.LimbalRing); set => SetLock(RandomizeLocks.LimbalRing, value); }
        public bool LockParticles { get => IsLocked(RandomizeLocks.Particles); set => SetLock(RandomizeLocks.Particles, value); }
        public bool LockSymbols { get => IsLocked(RandomizeLocks.Symbols); set => SetLock(RandomizeLocks.Symbols, value); }
        public bool LockHighlights { get => IsLocked(RandomizeLocks.Highlights); set => SetLock(RandomizeLocks.Highlights, value); }
        public bool LockLens { get => IsLocked(RandomizeLocks.Lens); set => SetLock(RandomizeLocks.Lens, value); }
        public bool LockOddEye { get => IsLocked(RandomizeLocks.OddEye); set => SetLock(RandomizeLocks.OddEye, value); }

        public ICommand ClearRandomizeLocksCommand =>
            _clearRandomizeLocksCommand ??= new RelayCommand(() => RandomizeLocks = RandomizeLocks.None, () => HasRandomizeLocks);

        private bool IsLocked(RandomizeLocks flag) => (_randomizeLocks & flag) != 0;

        private void SetLock(RandomizeLocks flag, bool locked) =>
            RandomizeLocks = locked ? _randomizeLocks | flag : _randomizeLocks & ~flag;
    }
}
