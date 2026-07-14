using System.ComponentModel;
using System.Runtime.CompilerServices;
using MouseToVJoy.Data;

namespace MouseToVJoy.ViewModels
{
    public class PresetSlotViewModel : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private PresetSettings _settings = new();

        public int SlotIndex { get; }

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value) return;
                _name = value;
                OnPropertyChanged();
            }
        }

        public PresetSettings Settings
        {
            get => _settings;
            set
            {
                if (_settings == value) return;
                _settings = value;
                OnPropertyChanged();
            }
        }

        public PresetSlotViewModel(int slotIndex, string name, PresetSettings settings)
        {
            SlotIndex = slotIndex;
            Name = name;
            Settings = settings;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Resolves operator '==' and '!=' errors by enabling safe null comparisons
        public static bool operator ==(PresetSlotViewModel? left, PresetSlotViewModel? right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;
            return left.SlotIndex == right.SlotIndex;
        }

        public static bool operator !=(PresetSlotViewModel? left, PresetSlotViewModel? right)
        {
            return !(left == right);
        }

        public override bool Equals(object? obj)
        {
            if (obj is PresetSlotViewModel other)
            {
                return SlotIndex == other.SlotIndex;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return SlotIndex.GetHashCode();
        }
    }
}