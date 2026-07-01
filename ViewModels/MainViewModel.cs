using MouseToVJoy.Data;
using MouseToVJoy.Helpers;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace MouseToVJoy.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly VJoyModel _vJoyModel;
        private readonly Dispatcher _uiDispatcher;
        private readonly DispatcherTimer _idleTimer;
        private readonly string _presetFilePath;

        private HwndSource? _hwndSource;
        private string _statusMessage = "Disconnected. Press Start to initialize.";
        private string _presetFeedbackMessage = "Preset actions will appear here.";
        private PresetSlotViewModel? _selectedPreset;
        private string _presetNameInput = string.Empty;
        private int _activePresetIndex;
        private bool _isLoadingPresetFile;
        private bool _isRunning;
        private bool _isSteeringActive;
        private bool _pedalModeActive;
        private bool _cursorHidden;
        private readonly MouseHookHandler _mouseHookHandler;
        private IntPtr _mouseHookId = IntPtr.Zero;
        private double _virtualWheelValue = 16384.0;
        private double _virtualThrottleValue = 1.0;
        private double _virtualBrakeValue = 1.0;
        private double _virtualMouseX;
        private double _mousePedalRawCombinedValue;

        private double _brakeStabilityRatio;

        private const int AxisMin = 1;
        private const int AxisCenter = 16384;
        private const int AxisMax = 32768;
        private const int AxisHalfRange = AxisCenter - AxisMin;

        private double _pedalSensitivity = 40;
        private double _mousePedalDeadZone = 0.1;
        private double _wheelSensitivity = 0.1;
        private bool _enableThrottle = true;
        private bool _enableBrake = true;
        private bool _enableKeyboardThrottle;
        private bool _enableKeyboardBrake;
        private string _keyboardThrottleKey = "W";
        private string _keyboardBrakeKey = "S";
        private double _keyboardThrottleLagUpSeconds = 0.20;
        private double _keyboardThrottleLagDownSeconds = 0.15;
        private double _keyboardBrakeLagUpSeconds = 0.08;
        private double _keyboardBrakeLagDownSeconds = 0.20;
        private bool _enableKeyboardResponseCurve;
        private string _keyboardResponseCurvePoints = "0,0;0.25,0.15;0.5,0.5;0.75,0.85;1,1";
        private bool _enableWheelCentering;
        private double _wheelReturnTimeSeconds = 20.95;
        private bool _enableRightClickPedalMode = true;
        private bool _enableCenterLockedCursor;
        private bool _enableFullThrottleHold = true;
        private double _fullThrottleHoldDeadzone = 0.01;
        private bool _enableSteeringDampening = true;
        private double _steeringDampening = 0.20;
        private bool _enableBrakeAssist = true;
        private double _brakeAssistThreshold = 0.70;
        private double _brakeAssistStrength = 0.35;
        private bool _enableBrakeXDeadzone = true;
        private double _brakeXDeadzone = 0.04;
        private bool _enableTimedBrakeXDeadzone = true;
        private double _brakeXDeadzoneDurationSeconds = 0.05;
        private double _brakeXDeadzoneRemainingSeconds;
        private bool _brakeXDeadzoneArmed = true;
        private bool _enableTrailBraking = true;
        private double _trailBrakingRelease = 0.06;
        private double _brakeAssistHold = 0.0;
        private DateTime _lastIdleTickUtc = DateTime.UtcNow;
        private double _keyboardThrottleRawRatio;
        private double _keyboardBrakeRawRatio;
        private bool _enableBrakeResetting;
        private double _brakeReturnTimeSeconds = 5.0;
        private double _lastRawBrakeValue = 0.0;
        private double _brakeIdleTimer = 0.0;
        private const double BrakeIdleDelaySeconds = 0.1; 

        private bool _enableKeyboardThrottleCurve;
        private string _keyboardThrottleCurvePoints = PresetSettings.DefaultResponseCurvePoints;
        private bool _enableKeyboardBrakeCurve;
        private string _keyboardBrakeCurvePoints = PresetSettings.DefaultResponseCurvePoints;

        private bool _enableKeyboardBrakeSteeringAssist;
        private double _keyboardBrakeSteeringAssistStrength = 0.50;

        private bool _enableKeyboardThrottleSteeringAssist;
        private double _keyboardThrottleSteeringAssistStrength = 0.50;
        private double _keyboardThrottleAssistIdleThreshold = 1.0;
        private double _keyboardThrottleAssistDuration = 2.0;

        private double _throttleIdleTimer = 0.0;
        private double _throttleAssistActiveTimer = 0.0;
        private bool _wasThrottlePressed = false;
        private double _activeThrottleReduction = 0.0;

        public bool EnableKeyboardThrottleCurve 
        { get => _enableKeyboardThrottleCurve; set => SetSetting(ref _enableKeyboardThrottleCurve, value); }
        public string KeyboardThrottleCurvePoints 
        { get => _keyboardThrottleCurvePoints; set => SetSetting(ref _keyboardThrottleCurvePoints, string.IsNullOrWhiteSpace(value) ? PresetSettings.DefaultResponseCurvePoints : value.Trim()); }
        public bool EnableKeyboardBrakeCurve 
        { get => _enableKeyboardBrakeCurve; set => SetSetting(ref _enableKeyboardBrakeCurve, value); }
        public string KeyboardBrakeCurvePoints 
        { get => _keyboardBrakeCurvePoints; set => SetSetting(ref _keyboardBrakeCurvePoints, string.IsNullOrWhiteSpace(value) ? PresetSettings.DefaultResponseCurvePoints : value.Trim()); }

        public bool EnableKeyboardBrakeSteeringAssist 
        { get => _enableKeyboardBrakeSteeringAssist; set => SetSetting(ref _enableKeyboardBrakeSteeringAssist, value); }
        public double KeyboardBrakeSteeringAssistStrength 
        { get => _keyboardBrakeSteeringAssistStrength; set => SetSetting(ref _keyboardBrakeSteeringAssistStrength, Math.Clamp(value, 0.0, 2.0)); }

        public bool EnableKeyboardThrottleSteeringAssist 
        { get => _enableKeyboardThrottleSteeringAssist; set => SetSetting(ref _enableKeyboardThrottleSteeringAssist, value); }
        public double KeyboardThrottleSteeringAssistStrength 
        { get => _keyboardThrottleSteeringAssistStrength; set => SetSetting(ref _keyboardThrottleSteeringAssistStrength, Math.Clamp(value, 0.0, 2.0)); }
        public double KeyboardThrottleAssistIdleThreshold 
        { get => _keyboardThrottleAssistIdleThreshold; set => SetSetting(ref _keyboardThrottleAssistIdleThreshold, Math.Clamp(value, 0.1, 10.0)); }
        public double KeyboardThrottleAssistDuration 
        { get => _keyboardThrottleAssistDuration; set => SetSetting(ref _keyboardThrottleAssistDuration, Math.Clamp(value, 0.1, 10.0)); }

        private const int WM_INPUT = 0x00FF;
        private const int WH_MOUSE_LL = 14;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int RIM_TYPEMOUSE = 0;
        private const int RID_INPUT = 0x10000003;
        private const uint RIDEV_INPUTSINK = 0x00000100;
        private const ushort HID_USAGE_PAGE_GENERIC = 0x01;
        private const ushort HID_USAGE_GENERIC_MOUSE = 0x02;

        private const int VK_1 = 0x31;
        private const int VK_2 = 0x32;
        private const int VK_3 = 0x33;
        private const int VK_NUMPAD1 = 0x61;
        private const int VK_NUMPAD2 = 0x62;
        private const int VK_NUMPAD3 = 0x63;
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        public MainViewModel()
        {
            _uiDispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            _vJoyModel = new VJoyModel();
            _presetFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MouseToVJoy",
                "presets.json");

            StartCommand = new RelayCommand(ExecuteStart, CanExecuteStart);
            StopCommand = new RelayCommand(ExecuteStop, CanExecuteStop);
            LoadPresetCommand = new RelayCommand(ExecuteLoadPreset, CanUseSelectedPreset);
            UpdatePresetCommand = new RelayCommand(ExecuteUpdatePreset, CanUseSelectedPreset);
            RenamePresetCommand = new RelayCommand(ExecuteRenamePreset, CanUseSelectedPreset);
            ResetPresetCommand = new RelayCommand(ExecuteResetPreset, CanUseSelectedPreset);
            OpenThrottleCurveEditorCommand = new RelayCommand(ExecuteOpenThrottleCurveEditor);
            OpenBrakeCurveEditorCommand = new RelayCommand(ExecuteOpenBrakeCurveEditor);
            _mouseHookHandler = MouseHookCallback;

            _idleTimer = new DispatcherTimer(DispatcherPriority.Send, _uiDispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(1)
            };
            _idleTimer.Tick += OnIdleTimerTick;

            Presets = new ObservableCollection<PresetSlotViewModel>();
            KeyboardKeyOptions = new ObservableCollection<string>
            {
                "W", "A", "S", "D",
                "Q", "E", "R", "F",
                "Space", "LeftShift", "LeftCtrl",
                "Up", "Down", "Left", "Right"
            };
            LoadPresetFile();
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage == value) return;
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public int VirtualWheelValue
        {
            get => (int)_virtualWheelValue;
            set => SetVirtualWheelValue(value, force: true);
        }

        public int VirtualThrottleValue
        {
            get => (int)_virtualThrottleValue;
            private set
            {
                int clampedValue = Math.Clamp(value, AxisMin, AxisMax);
                if ((int)_virtualThrottleValue == clampedValue) return;
                _virtualThrottleValue = clampedValue;
                OnPropertyChanged();
            }
        }

        public int VirtualBrakeValue
        {
            get => (int)_virtualBrakeValue;
            private set
            {
                int clampedValue = Math.Clamp(value, AxisMin, AxisMax);
                if ((int)_virtualBrakeValue == clampedValue) return;
                _virtualBrakeValue = clampedValue;
                OnPropertyChanged();
            }
        }

        public string PresetFeedbackMessage
        {
            get => _presetFeedbackMessage;
            set
            {
                if (_presetFeedbackMessage == value) return;
                _presetFeedbackMessage = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<PresetSlotViewModel> Presets { get; }

        public PresetSlotViewModel? SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                if (_selectedPreset == value) return;
                _selectedPreset = value;
                PresetNameInput = value?.Name ?? string.Empty;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();

                if (!_isLoadingPresetFile && value != null)
                {
                    ApplySettings(value.Settings);
                    ResetControlsToZero();
                    SendAxesToVJoy();
                    ReportPresetAction($"Selected and applied preset \"{value.Name}\". Press Load to make it auto-load next time.");
                }
            }
        }

        public string PresetNameInput
        {
            get => _presetNameInput;
            set
            {
                if (_presetNameInput == value) return;
                _presetNameInput = value;
                OnPropertyChanged();
            }
        }

        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                if (_isRunning == value) return;
                _isRunning = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public double PedalSensitivity
        {
            get => _pedalSensitivity;
            set => SetSetting(ref _pedalSensitivity, Math.Clamp(value, 1.0, 300.0));
        }

        public double MousePedalDeadZone
        {
            get => _mousePedalDeadZone;
            set => SetSetting(ref _mousePedalDeadZone, Math.Clamp(value, 0.0, 0.5));
        }

        public double WheelSensitivity
        {
            get => _wheelSensitivity;
            set => SetSetting(ref _wheelSensitivity, Math.Clamp(value, 0.01, 3.0));
        }

        public bool EnableBrakeResetting
        {
            get => _enableBrakeResetting;
            set => SetSetting(ref _enableBrakeResetting, value);
        }

        public double BrakeReturnTimeSeconds
        {
            get => _brakeReturnTimeSeconds;
            set => SetSetting(ref _brakeReturnTimeSeconds, Math.Clamp(value, 0.1, 60.0));
        }

        public bool EnableThrottle
        {
            get => _enableThrottle;
            set
            {
                if (!SetSetting(ref _enableThrottle, value)) return;
                if (!value)
                {
                    SetThrottleValue(AxisMin);
                }

                OnPropertyChanged(nameof(ArePedalsEnabled));
                OnPropertyChanged(nameof(IsKeyboardThrottleSettingsEnabled));
                SendAxesToVJoy();
            }
        }

        public bool EnableBrake
        {
            get => _enableBrake;
            set
            {
                if (!SetSetting(ref _enableBrake, value)) return;
                if (!value)
                {
                    SetBrakeValue(AxisMin);
                    _brakeStabilityRatio = 0.0;
                    ResetBrakeXDeadzoneTimer();
                }

                OnPropertyChanged(nameof(ArePedalsEnabled));
                OnPropertyChanged(nameof(IsKeyboardBrakeSettingsEnabled));
                SendAxesToVJoy();
            }
        }

        public bool ArePedalsEnabled => EnableThrottle || EnableBrake;
        public bool IsKeyboardThrottleSettingsEnabled => EnableThrottle && EnableKeyboardThrottle;
        public bool IsKeyboardBrakeSettingsEnabled => EnableBrake && EnableKeyboardBrake;

        public ObservableCollection<string> KeyboardKeyOptions { get; }

        public bool EnableKeyboardThrottle
        {
            get => _enableKeyboardThrottle;
            set
            {
                if (!SetSetting(ref _enableKeyboardThrottle, value)) return;
                if (value)
                {
                    _keyboardThrottleRawRatio = GetAxisRatio(_virtualThrottleValue);
                }

                OnPropertyChanged(nameof(IsKeyboardThrottleSettingsEnabled));
            }
        }

        public bool EnableKeyboardBrake
        {
            get => _enableKeyboardBrake;
            set
            {
                if (!SetSetting(ref _enableKeyboardBrake, value)) return;
                if (value)
                {
                    _keyboardBrakeRawRatio = GetAxisRatio(_virtualBrakeValue);
                }

                OnPropertyChanged(nameof(IsKeyboardBrakeSettingsEnabled));
            }
        }

        public string KeyboardThrottleKey
        {
            get => _keyboardThrottleKey;
            set => SetSetting(ref _keyboardThrottleKey, string.IsNullOrWhiteSpace(value) ? "W" : value);
        }

        public string KeyboardBrakeKey
        {
            get => _keyboardBrakeKey;
            set => SetSetting(ref _keyboardBrakeKey, string.IsNullOrWhiteSpace(value) ? "S" : value);
        }

        public double KeyboardThrottleLagUpSeconds
        {
            get => _keyboardThrottleLagUpSeconds;
            set => SetSetting(ref _keyboardThrottleLagUpSeconds, Math.Clamp(value, 0.0, 5.0));
        }

        public double KeyboardThrottleLagDownSeconds
        {
            get => _keyboardThrottleLagDownSeconds;
            set => SetSetting(ref _keyboardThrottleLagDownSeconds, Math.Clamp(value, 0.0, 5.0));
        }

        public double KeyboardBrakeLagUpSeconds
        {
            get => _keyboardBrakeLagUpSeconds;
            set => SetSetting(ref _keyboardBrakeLagUpSeconds, Math.Clamp(value, 0.0, 5.0));
        }

        public double KeyboardBrakeLagDownSeconds
        {
            get => _keyboardBrakeLagDownSeconds;
            set => SetSetting(ref _keyboardBrakeLagDownSeconds, Math.Clamp(value, 0.0, 5.0));
        }

        public bool EnableKeyboardResponseCurve
        {
            get => _enableKeyboardResponseCurve;
            set => SetSetting(ref _enableKeyboardResponseCurve, value);
        }

        public string KeyboardResponseCurvePoints
        {
            get => _keyboardResponseCurvePoints;
            set => SetSetting(ref _keyboardResponseCurvePoints, string.IsNullOrWhiteSpace(value) ? PresetSettings.DefaultResponseCurvePoints : value.Trim());
        }

        public bool EnableWheelCentering
        {
            get => _enableWheelCentering;
            set => SetSetting(ref _enableWheelCentering, value);
        }

        public double WheelReturnTimeSeconds
        {
            get => _wheelReturnTimeSeconds;
            set => SetSetting(ref _wheelReturnTimeSeconds, Math.Clamp(value, 0.1, 60.0));
        }

        public bool EnableRightClickPedalMode
        {
            get => _enableRightClickPedalMode;
            set => SetSetting(ref _enableRightClickPedalMode, value);
        }

        public bool EnableCenterLockedCursor
        {
            get => _enableCenterLockedCursor;
            set
            {
                if (!SetSetting(ref _enableCenterLockedCursor, value)) return;

                if (!IsRunning || !_isSteeringActive) return;

                if (value)
                {
                    ResetVirtualMouseToCenter();
                    CenterCursor();
                    LockCursorToCenter();
                }
                else
                {
                    UnlockCursor();
                    ShowMouseCursor();
                    SyncVirtualMouseWithCursor();
                }
            }
        }

        public bool EnableFullThrottleHold
        {
            get => _enableFullThrottleHold;
            set => SetSetting(ref _enableFullThrottleHold, value);
        }

        public double FullThrottleHoldDeadzone
        {
            get => _fullThrottleHoldDeadzone;
            set => SetSetting(ref _fullThrottleHoldDeadzone, Math.Clamp(value, 0.0, 0.5));
        }

        public bool EnableSteeringDampening
        {
            get => _enableSteeringDampening;
            set => SetSetting(ref _enableSteeringDampening, value);
        }

        public double SteeringDampening
        {
            get => _steeringDampening;
            set => SetSetting(ref _steeringDampening, Math.Clamp(value, 0.0, 0.95));
        }

        public bool EnableBrakeAssist
        {
            get => _enableBrakeAssist;
            set => SetSetting(ref _enableBrakeAssist, value);
        }

        public double BrakeAssistThreshold
        {
            get => _brakeAssistThreshold;
            set => SetSetting(ref _brakeAssistThreshold, Math.Clamp(value, 0.0, 1.0));
        }

        public double BrakeAssistStrength
        {
            get => _brakeAssistStrength;
            set => SetSetting(ref _brakeAssistStrength, Math.Clamp(value, 0.0, 1.0));
        }

        public bool EnableBrakeXDeadzone
        {
            get => _enableBrakeXDeadzone;
            set => SetSetting(ref _enableBrakeXDeadzone, value);
        }

        public double BrakeXDeadzone
        {
            get => _brakeXDeadzone;
            set => SetSetting(ref _brakeXDeadzone, Math.Clamp(value, 0.0, 0.5));
        }

        public bool EnableTimedBrakeXDeadzone
        {
            get => _enableTimedBrakeXDeadzone;
            set => SetSetting(ref _enableTimedBrakeXDeadzone, value);
        }

        public double BrakeXDeadzoneDurationSeconds
        {
            get => _brakeXDeadzoneDurationSeconds;
            set => SetSetting(ref _brakeXDeadzoneDurationSeconds, Math.Clamp(value, 0.05, 5.0));
        }

        public bool EnableTrailBraking
        {
            get => _enableTrailBraking;
            set => SetSetting(ref _enableTrailBraking, value);
        }

        public double TrailBrakingRelease
        {
            get => _trailBrakingRelease;
            set => SetSetting(ref _trailBrakingRelease, Math.Clamp(value, 0.0, 0.5));
        }

        public double BrakeAssistHold
        {
            get => _brakeAssistHold;
            set => SetSetting(ref _brakeAssistHold, Math.Clamp(value, 0.0, 1.0));
        }

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand LoadPresetCommand { get; }
        public ICommand UpdatePresetCommand { get; }
        public ICommand RenamePresetCommand { get; }
        public ICommand ResetPresetCommand { get; }
        public ICommand OpenResponseCurveEditorCommand { get; }
        public ICommand OpenThrottleCurveEditorCommand { get; }
        public ICommand OpenBrakeCurveEditorCommand { get; }

        public void AttachToWindow(Window window)
        {
            window.SourceInitialized += (_, _) =>
            {
                _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
                _hwndSource?.AddHook(WndProc);
            };

            window.Closed += (_, _) =>
            {
                ExecuteStop(null);
                _hwndSource?.RemoveHook(WndProc);
                _hwndSource = null;
            };
        }

        private void ExecuteStart(object? parameter)
        {
            if (_hwndSource == null)
            {
                StatusMessage = "[ERROR] The window is not ready for Raw Input.";
                return;
            }

            if (!_vJoyModel.Initialize())
            {
                StatusMessage = string.IsNullOrWhiteSpace(_vJoyModel.LastError)
                    ? "[ERROR] Could not initialize vJoy."
                    : $"[ERROR] {_vJoyModel.LastError}";
                return;
            }

            if (!RegisterMouseRawInput(_hwndSource.Handle))
            {
                _vJoyModel.Disconnect();
                StatusMessage = $"[ERROR] Could not enable Raw Input. Win32: {Marshal.GetLastWin32Error()}.";
                return;
            }

            _mouseHookId = SetWindowsHookEx(WH_MOUSE_LL, _mouseHookHandler, IntPtr.Zero, 0);
            if (_mouseHookId == IntPtr.Zero)
            {
                _vJoyModel.Disconnect();
                StatusMessage = $"[ERROR] Could not enable the middle-click hook. Win32: {Marshal.GetLastWin32Error()}.";
                return;
            }

            IsRunning = true;
            _isSteeringActive = false;
            _pedalModeActive = false;
            _brakeStabilityRatio = 0.0;
            ResetBrakeXDeadzoneTimer();
            _lastIdleTickUtc = DateTime.UtcNow;
            _idleTimer.Start();
            ShowMouseCursor();
            ResetControlsToZero();

            StatusMessage = string.IsNullOrWhiteSpace(_vJoyModel.LastWarning)
                ? "[READY] Raw Input enabled. Press Middle Click to toggle driving control."
                : $"[READY] Raw Input enabled. {_vJoyModel.LastWarning}";
        }

        private void ExecuteStop(object? parameter)
        {
            _idleTimer.Stop();
            UnlockCursor();
            ShowMouseCursor();
            _isSteeringActive = false;
            _pedalModeActive = false;
            _brakeStabilityRatio = 0.0;
            ResetBrakeXDeadzoneTimer();

            if (_mouseHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHookId);
                _mouseHookId = IntPtr.Zero;
            }

            _vJoyModel.Disconnect();
            IsRunning = false;
            StatusMessage = "Disconnected. vJoy has been released.";
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_INPUT && TryReadRawMouse(lParam, out RawMouseData mouseData))
            {
                if (IsRunning && _isSteeringActive && (mouseData.LastX != 0 || mouseData.LastY != 0))
                {
                    ApplyMouseDelta(mouseData.LastX, mouseData.LastY);
                    handled = true;
                    return IntPtr.Zero;
                }
            }

            return IntPtr.Zero;
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_MBUTTONDOWN)
            {
                _uiDispatcher.BeginInvoke(ToggleSteering);
            }
            else if (nCode >= 0 && wParam == (IntPtr)WM_RBUTTONDOWN && EnableRightClickPedalMode)
            {
                _uiDispatcher.BeginInvoke(SetPedalModeActive);
                return (IntPtr)1;
            }
            else if (nCode >= 0 && wParam == (IntPtr)WM_RBUTTONUP && EnableRightClickPedalMode)
            {
                _uiDispatcher.BeginInvoke(SetPedalModeInactive);
                return (IntPtr)1;
            }

            return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
        }

        private void ToggleSteering()
        {
            if (!IsRunning) return;

            _isSteeringActive = !_isSteeringActive;

            if (_isSteeringActive)
            {
                if (EnableCenterLockedCursor)
                {
                    ResetVirtualMouseToCenter();
                    CenterCursor();
                    LockCursorToCenter();
                }
                else
                {
                    SyncVirtualMouseWithCursor();
                    UnlockCursor();
                    ShowMouseCursor();
                }

                _pedalModeActive = false;
                StatusMessage = "[ACTIVE] Raw mouse control. Up = throttle, down = brake, left/right = steering.";
            }
            else
            {
                _pedalModeActive = false;
                UnlockCursor();
                ShowMouseCursor();
                StatusMessage = "[PAUSED] Mouse control is free.";
            }
        }

        private void ApplyMouseDelta(int deltaX, int deltaY)
        {
            if (_pedalModeActive)
            {
                SetVirtualWheelValue(AxisCenter, force: true);
                KeepCursorXCentered();
            }
            else
            {
                MoveVirtualMouse(deltaX);
            }

            bool isoWheel = IsKeyDown(VK_1) || IsKeyDown(VK_NUMPAD1);
            bool isoThrottle = IsKeyDown(VK_2) || IsKeyDown(VK_NUMPAD2);
            bool isoBrake = IsKeyDown(VK_3) || IsKeyDown(VK_NUMPAD3);
            bool hasIsolation = isoWheel || isoThrottle || isoBrake;

            UpdateMousePedals(deltaY, isoWheel, isoThrottle, isoBrake);
            UpdateBrakeStability();
            UpdateBrakeXDeadzoneTimer(0.0);

            if (_pedalModeActive)
            {
                SetVirtualWheelValue(AxisCenter, force: true);
            }
            else if (isoWheel || !hasIsolation)
            {
                ApplyWheelTarget(MapMouseXToWheel());
            }

            SendAxesToVJoy();
            MaintainCenterLockedCursor();
        }

        private void SetPedalModeActive()
        {
            if (!IsRunning || !_isSteeringActive) return;

            _pedalModeActive = true;
            SetVirtualWheelValue(AxisCenter, force: true);
            KeepCursorXCentered();
            SendAxesToVJoy();
        }

        private void SetPedalModeInactive()
        {
            if (!_pedalModeActive) return;

            _pedalModeActive = false;
            if (EnableCenterLockedCursor)
            {
                ResetVirtualMouseToCenter();
                CenterCursor();
                LockCursorToCenter();
            }
            else
            {
                SyncVirtualMouseWithCursor();
            }

            ApplyWheelTarget(MapMouseXToWheel());
            SendAxesToVJoy();
        }

        private void UpdateMousePedals(int deltaY, bool isoWheel, bool isoThrottle, bool isoBrake)
        {
            bool mouseThrottleEnabled = EnableThrottle && !EnableKeyboardThrottle;
            bool mouseBrakeEnabled = EnableBrake && !EnableKeyboardBrake;

            if (isoWheel)
            {
                _mousePedalRawCombinedValue = 0.0;
                if (mouseThrottleEnabled) SetThrottleValue(AxisMin);
                if (mouseBrakeEnabled) SetBrakeValue(AxisMin);
                return;
            }

            if (!mouseThrottleEnabled && !mouseBrakeEnabled)
            {
                _mousePedalRawCombinedValue = 0.0;
                return;
            }

            _mousePedalRawCombinedValue += deltaY * PedalSensitivity;
            if (mouseThrottleEnabled)
            {
                _mousePedalRawCombinedValue = ApplyFullThrottleHold(_mousePedalRawCombinedValue);
            }

            _mousePedalRawCombinedValue = ClampMousePedalRawValue(_mousePedalRawCombinedValue, mouseThrottleEnabled, mouseBrakeEnabled);
            double outputCombinedY = ApplyMousePedalDeadZone(_mousePedalRawCombinedValue);

            if (outputCombinedY > 0 && mouseBrakeEnabled)
            {
                SetBrakeValue(AxisMin + outputCombinedY);
                if (mouseThrottleEnabled) SetThrottleValue(AxisMin);
            }
            else if (outputCombinedY < 0 && mouseThrottleEnabled)
            {
                SetThrottleValue(AxisMin - outputCombinedY);
                if (mouseBrakeEnabled) SetBrakeValue(AxisMin);
            }
            else
            {
                if (mouseThrottleEnabled) SetThrottleValue(AxisMin);
                if (mouseBrakeEnabled) SetBrakeValue(AxisMin);
            }

            if (isoThrottle && mouseBrakeEnabled) SetBrakeValue(AxisMin);
            if (isoBrake && mouseThrottleEnabled) SetThrottleValue(AxisMin);
        }

        private double ApplyMousePedalDeadZone(double combinedPedalValue)
        {
            double maxMagnitude = AxisMax - AxisMin;
            double deadZone = maxMagnitude * MousePedalDeadZone;

            if (deadZone <= 0.0) return combinedPedalValue;

            double magnitude = Math.Abs(combinedPedalValue);

            if (magnitude <= deadZone) return 0.0;

            double activeRange = maxMagnitude - deadZone;
            double outputMagnitude = ((magnitude - deadZone) / activeRange) * maxMagnitude;

            return Math.Sign(combinedPedalValue) * outputMagnitude;
        }

        private static double ClampMousePedalRawValue(double value, bool mouseThrottleEnabled, bool mouseBrakeEnabled)
        {
            double maxTravel = AxisMax - AxisMin;
            if (mouseThrottleEnabled && mouseBrakeEnabled) return Math.Clamp(value, -maxTravel, maxTravel);
            if (mouseBrakeEnabled) return Math.Clamp(value, 0.0, maxTravel);
            if (mouseThrottleEnabled) return Math.Clamp(value, -maxTravel, 0.0);
            return 0.0;
        }

        private void ApplyWheelTarget(double targetWheelValue)
        {
            targetWheelValue = ApplyBrakeStabilityAssists(targetWheelValue);

            if (EnableSteeringDampening)
            {
                double smoothing = Math.Clamp(SteeringDampening, 0.0, 0.95);
                targetWheelValue = _virtualWheelValue + ((targetWheelValue - _virtualWheelValue) * (1.0 - smoothing));
            }

            SetVirtualWheelValue(targetWheelValue);
        }

        private double ApplyFullThrottleHold(double combinedPedalValue)
        {
            if (!EnableFullThrottleHold) return combinedPedalValue;
            if (!EnableThrottle) return combinedPedalValue;
            if (_virtualThrottleValue < AxisMax - 1) return combinedPedalValue;

            double maxPedalTravel = AxisMax - AxisMin;
            double holdRange = maxPedalTravel * FullThrottleHoldDeadzone;
            if (combinedPedalValue < 0 && combinedPedalValue <= -maxPedalTravel + holdRange)
            {
                return -maxPedalTravel;
            }

            return combinedPedalValue;
        }

        private double ApplyBrakeStabilityAssists(double targetWheelValue)
        {
            if (!EnableBrake) return targetWheelValue;

            double currentBrakeRatio = GetCurrentBrakeRatio();
            double heldBrakeRatio = GetAssistBrakeRatio();
            if (currentBrakeRatio <= 0.0 && heldBrakeRatio <= 0.0) return targetWheelValue;

            double centerOffset = targetWheelValue - AxisCenter;
            bool deadzoneActive = IsBrakeXDeadzoneActive(currentBrakeRatio);
            bool assistActive = heldBrakeRatio >= BrakeAssistThreshold;

            if (EnableBrakeXDeadzone && deadzoneActive)
            {
                double deadzoneRange = AxisHalfRange * BrakeXDeadzone;
                if (Math.Abs(centerOffset) <= deadzoneRange)
                {
                    targetWheelValue = AxisCenter;
                    centerOffset = 0.0;
                }
            }

            if (EnableBrakeAssist && assistActive)
            {
                double assistRatio = Math.Clamp((heldBrakeRatio - BrakeAssistThreshold) / Math.Max(0.01, 1.0 - BrakeAssistThreshold), 0.0, 1.0);
                double assistStrength = assistRatio * BrakeAssistStrength;
                targetWheelValue = AxisCenter + (centerOffset * (1.0 - assistStrength));
            }

            return targetWheelValue;
        }

        private void UpdateBrakeStability()
        {
            if (!EnableBrake)
            {
                _brakeStabilityRatio = 0.0;
                return;
            }

            double brakeRatio = GetCurrentBrakeRatio();

            if (EnableTrailBraking)
            {
                _brakeStabilityRatio = Math.Max(brakeRatio, _brakeStabilityRatio);
            }
            else
            {
                _brakeStabilityRatio = brakeRatio;
            }
        }

        private double GetAssistBrakeRatio()
        {
            double currentBrakeRatio = GetCurrentBrakeRatio();
            if (!EnableTrailBraking) return currentBrakeRatio;

            double heldRatio = _brakeStabilityRatio * BrakeAssistHold;
            return Math.Max(currentBrakeRatio, heldRatio);
        }

        private double GetCurrentBrakeRatio()
        {
            if (!EnableBrake) return 0.0;
            return Math.Clamp((_virtualBrakeValue - AxisMin) / (AxisMax - AxisMin), 0.0, 1.0);
        }

        private bool IsBrakeXDeadzoneActive(double currentBrakeRatio)
        {
            if (!EnableBrakeXDeadzone || currentBrakeRatio < BrakeAssistThreshold)
            {
                return false;
            }

            return !EnableTimedBrakeXDeadzone || _brakeXDeadzoneRemainingSeconds > 0.0;
        }

        private void UpdateBrakeXDeadzoneTimer(double elapsedSeconds)
        {
            double currentBrakeRatio = GetCurrentBrakeRatio();

            if (!EnableBrakeXDeadzone || !EnableTimedBrakeXDeadzone)
            {
                _brakeXDeadzoneRemainingSeconds = 0.0;
                _brakeXDeadzoneArmed = currentBrakeRatio < BrakeAssistThreshold;
                return;
            }

            if (currentBrakeRatio < BrakeAssistThreshold)
            {
                ResetBrakeXDeadzoneTimer();
                return;
            }

            if (_brakeXDeadzoneArmed)
            {
                _brakeXDeadzoneRemainingSeconds = BrakeXDeadzoneDurationSeconds;
                _brakeXDeadzoneArmed = false;
            }
            else if (_brakeXDeadzoneRemainingSeconds > 0.0)
            {
                _brakeXDeadzoneRemainingSeconds = Math.Max(0.0, _brakeXDeadzoneRemainingSeconds - elapsedSeconds);
            }
        }

        private void ResetBrakeXDeadzoneTimer()
        {
            _brakeXDeadzoneRemainingSeconds = 0.0;
            _brakeXDeadzoneArmed = true;
        }

        private void OnIdleTimerTick(object? sender, EventArgs e)
        {
            if (!IsRunning) return;

            DateTime now = DateTime.UtcNow;
            double elapsedSeconds = Math.Clamp((now - _lastIdleTickUtc).TotalSeconds, 0.001, 0.05);
            _lastIdleTickUtc = now;

            if (!_isSteeringActive)
            {
                ResetControlsToZero();
                SendAxesToVJoy();
                return;
            }

            MaintainCenterLockedCursor();
            UpdateKeyboardPedals(elapsedSeconds);
            UpdateBrakeStability();
            UpdateBrakeXDeadzoneTimer(elapsedSeconds);

            if (EnableTrailBraking)
            {
                _brakeStabilityRatio = Math.Max(GetCurrentBrakeRatio(), _brakeStabilityRatio - (TrailBrakingRelease * elapsedSeconds * 10.0));
            }

            bool isolateWheel = IsKeyDown(VK_1) || IsKeyDown(VK_NUMPAD1);
            if (!isolateWheel && EnableWheelCentering)
            {
                ApplyWheelTarget(ApplyTimedCentering(_virtualWheelValue, AxisCenter, WheelReturnTimeSeconds, elapsedSeconds));
            }



            bool isolateBrake = IsKeyDown(VK_3) || IsKeyDown(VK_NUMPAD3);

            if (_mousePedalRawCombinedValue > _lastRawBrakeValue)
            {
                _brakeIdleTimer = 0.0;
            }
            else
            {
                _brakeIdleTimer += elapsedSeconds;
            }
            _lastRawBrakeValue = _mousePedalRawCombinedValue;
            if (!isolateBrake && EnableBrakeResetting && _mousePedalRawCombinedValue > 0)
            {
                if (_brakeIdleTimer >= BrakeIdleDelaySeconds)
                {
                    double maxTravel = AxisMax - AxisMin;
                    double step = (maxTravel / BrakeReturnTimeSeconds) * elapsedSeconds;

                    _mousePedalRawCombinedValue = Math.Max(0.0, _mousePedalRawCombinedValue - step);
                    _lastRawBrakeValue = _mousePedalRawCombinedValue;

                    double outputCombinedY = ApplyMousePedalDeadZone(_mousePedalRawCombinedValue);
                    SetBrakeValue(AxisMin + outputCombinedY);
                }
            }

            SendAxesToVJoy();
        }

        private void UpdateKeyboardPedals(double elapsedSeconds)
        {
            double steeringFactor = Math.Abs(_virtualWheelValue - AxisCenter) / (double)AxisHalfRange;

            // --- THROTTLE ---
            if (EnableThrottle && EnableKeyboardThrottle)
            {
                bool pressed = IsKeyDown(GetVirtualKey(KeyboardThrottleKey));
                if (!pressed)
                {
                    _throttleIdleTimer += elapsedSeconds;
                }
                else
                {
                    if (!_wasThrottlePressed && _throttleIdleTimer >= KeyboardThrottleAssistIdleThreshold)
                    {
                        _throttleAssistActiveTimer = KeyboardThrottleAssistDuration;
                    }
                    _throttleIdleTimer = 0.0;
                }
                _wasThrottlePressed = pressed;

                double target = pressed ? 1.0 : 0.0;
                double lagSeconds = pressed ? KeyboardThrottleLagUpSeconds : KeyboardThrottleLagDownSeconds;
                _keyboardThrottleRawRatio = MoveRatioToward(_keyboardThrottleRawRatio, target, lagSeconds, elapsedSeconds);

                double outputRatio = EnableKeyboardThrottleCurve ? ApplyResponseCurve(_keyboardThrottleRawRatio, KeyboardThrottleCurvePoints) : _keyboardThrottleRawRatio;

                double targetReduction = 0.0;
                if (EnableKeyboardThrottleSteeringAssist && _throttleAssistActiveTimer > 0)
                {
                    _throttleAssistActiveTimer -= elapsedSeconds;
                    targetReduction = steeringFactor * KeyboardThrottleSteeringAssistStrength;
                }
                else
                {
                    _throttleAssistActiveTimer = 0.0;
                }

                double fadeTime = (targetReduction > _activeThrottleReduction) ? 0.1 : 1.2;
                _activeThrottleReduction = MoveRatioToward(_activeThrottleReduction, targetReduction, fadeTime, elapsedSeconds);

                outputRatio = Math.Max(0.0, outputRatio * (1.0 - _activeThrottleReduction));

                SetThrottleValue(RatioToAxis(outputRatio));
            }
            else
            {
                _keyboardThrottleRawRatio = GetAxisRatio(_virtualThrottleValue);
                _throttleIdleTimer = 0.0;
                _throttleAssistActiveTimer = 0.0;
            }

            // --- BRAKE ---
            if (EnableBrake && EnableKeyboardBrake)
            {
                bool pressed = IsKeyDown(GetVirtualKey(KeyboardBrakeKey));
                double target = pressed ? 1.0 : 0.0;
                double lagSeconds = pressed ? KeyboardBrakeLagUpSeconds : KeyboardBrakeLagDownSeconds;
                _keyboardBrakeRawRatio = MoveRatioToward(_keyboardBrakeRawRatio, target, lagSeconds, elapsedSeconds);

                double outputRatio = EnableKeyboardBrakeCurve ? ApplyResponseCurve(_keyboardBrakeRawRatio, KeyboardBrakeCurvePoints) : _keyboardBrakeRawRatio;

                if (EnableKeyboardBrakeSteeringAssist)
                {
                    double reduction = steeringFactor * KeyboardBrakeSteeringAssistStrength;
                    outputRatio = Math.Max(0.0, outputRatio * (1.0 - reduction));
                }

                SetBrakeValue(RatioToAxis(outputRatio));
            }
            else
            {
                _keyboardBrakeRawRatio = GetAxisRatio(_virtualBrakeValue);
            }
        }

        private void ResetControlsToZero()
        {
            SetVirtualWheelValue(ApplyTimedCentering(_virtualWheelValue, AxisCenter, 0.1, 0.001), force: true);
            SetThrottleValue(AxisMin);
            SetBrakeValue(AxisMin);
            _mousePedalRawCombinedValue = 0.0;
            _brakeStabilityRatio = 0.0;
            _keyboardThrottleRawRatio = 0.0;
            _keyboardBrakeRawRatio = 0.0;
        }

        private void SendAxesToVJoy()
        {
            _vJoyModel.UpdateAxes((int)_virtualWheelValue, (int)_virtualThrottleValue, (int)_virtualBrakeValue);
        }

        private void SyncVirtualMouseWithCursor()
        {
            _virtualMouseX = TryGetCursorPosition(out CursorPoint cursorPoint)
                ? cursorPoint.X
                : GetScreenWidth() / 2.0;
        }

        private void ResetVirtualMouseToCenter()
        {
            _virtualMouseX = GetScreenWidth() / 2.0;
        }

        private void MoveVirtualMouse(int deltaX)
        {
            _virtualMouseX = Math.Clamp(_virtualMouseX + (deltaX * WheelSensitivity), 0, GetScreenWidth() - 1);
        }

        private double MapMouseXToWheel()
        {
            double screenWidth = Math.Max(1, GetScreenWidth() - 1);
            double normalizedX = Math.Clamp(_virtualMouseX / screenWidth, 0.0, 1.0);
            return AxisMin + (normalizedX * (AxisMax - AxisMin));
        }

        private void SetVirtualWheelValue(double value, bool force = false)
        {
            int previousValue = (int)_virtualWheelValue;
            _virtualWheelValue = Math.Clamp(value, AxisMin, AxisMax);

            if (force || (int)_virtualWheelValue != previousValue)
            {
                OnPropertyChanged(nameof(VirtualWheelValue));
            }
        }

        private void SetThrottleValue(double value)
        {
            VirtualThrottleValue = EnableThrottle ? (int)Math.Clamp(value, AxisMin, AxisMax) : AxisMin;
        }

        private void SetBrakeValue(double value)
        {
            VirtualBrakeValue = EnableBrake ? (int)Math.Clamp(value, AxisMin, AxisMax) : AxisMin;
        }

        private bool SetSetting<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private static double ApplyTimedCentering(double currentValue, double targetValue, double durationSeconds, double deltaTimeSeconds)
        {
            double remainingDistance = Math.Abs(targetValue - currentValue);
            if (remainingDistance < 0.5) return targetValue;

            double maxTravel = targetValue == AxisCenter ? AxisCenter : AxisMax;
            double step = (maxTravel / durationSeconds) * deltaTimeSeconds;

            if (step >= remainingDistance) return targetValue;
            return currentValue < targetValue ? currentValue + step : currentValue - step;
        }

        private static double MoveRatioToward(double currentRatio, double targetRatio, double durationSeconds, double deltaTimeSeconds)
        {
            if (durationSeconds <= 0.0) return targetRatio;

            double remainingDistance = Math.Abs(targetRatio - currentRatio);
            if (remainingDistance < 0.0001) return targetRatio;

            double step = deltaTimeSeconds / Math.Max(0.001, durationSeconds);
            if (step >= remainingDistance) return targetRatio;
            return currentRatio < targetRatio ? currentRatio + step : currentRatio - step;
        }

        private double ApplyResponseCurve(double input, string curvePointsText)
        {
            var points = ParseResponseCurvePoints(curvePointsText).ToArray();
            if (points.Length == 0) return input;
            if (input <= points[0].X) return points[0].Y;

            for (int i = 1; i < points.Length; i++)
            {
                if (input > points[i].X) continue;
                double width = Math.Max(0.0001, points[i].X - points[i - 1].X);
                double t = Math.Clamp((input - points[i - 1].X) / width, 0.0, 1.0);
                return points[i - 1].Y + ((points[i].Y - points[i - 1].Y) * t);
            }
            return points[^1].Y;
        }

        private static double GetAxisRatio(double axisValue)
        {
            return Math.Clamp((axisValue - AxisMin) / (AxisMax - AxisMin), 0.0, 1.0);
        }

        private static double RatioToAxis(double ratio)
        {
            return AxisMin + (Math.Clamp(ratio, 0.0, 1.0) * (AxisMax - AxisMin));
        }

        private bool CanExecuteStart(object? parameter) => !IsRunning;
        private bool CanExecuteStop(object? parameter) => IsRunning;

        private bool CanUseSelectedPreset(object? parameter) => SelectedPreset != null;

        private void ExecuteLoadPreset(object? parameter)
        {
            if (SelectedPreset == null) return;

            ApplySettings(SelectedPreset.Settings);
            _activePresetIndex = SelectedPreset.SlotIndex;
            SavePresetFile();
            ReportPresetAction($"Loaded preset \"{SelectedPreset.Name}\". It will auto-load next time.");
        }

        private void ExecuteUpdatePreset(object? parameter)
        {
            if (SelectedPreset == null) return;

            SelectedPreset.Settings = CaptureSettings();
            SavePresetFile();
            ReportPresetAction($"Updated preset \"{SelectedPreset.Name}\" with current settings.");
        }

        private void ExecuteRenamePreset(object? parameter)
        {
            if (SelectedPreset == null) return;

            string newName = string.IsNullOrWhiteSpace(PresetNameInput)
                ? $"Preset {SelectedPreset.SlotIndex + 1}"
                : PresetNameInput.Trim();

            SelectedPreset.Name = newName;
            PresetNameInput = newName;
            SavePresetFile();
            ReportPresetAction($"Renamed slot {SelectedPreset.SlotIndex + 1} to \"{SelectedPreset.Name}\".");
        }

        private void ExecuteResetPreset(object? parameter)
        {
            if (SelectedPreset == null) return;

            SelectedPreset.Name = $"Preset {SelectedPreset.SlotIndex + 1}";
            SelectedPreset.Settings = PresetSettings.CreateDefault();
            PresetNameInput = SelectedPreset.Name;
            ApplySettings(SelectedPreset.Settings);
            ResetControlsToZero();
            _activePresetIndex = SelectedPreset.SlotIndex;

            SavePresetFile();
            ReportPresetAction($"Reset and loaded slot {SelectedPreset.SlotIndex + 1}. It will auto-load next time.");
        }

        private PresetSettings CaptureSettings()
        {
            return new PresetSettings
            {
                PedalSensitivity = PedalSensitivity,
                MousePedalDeadZone = MousePedalDeadZone,
                WheelSensitivity = WheelSensitivity,
                EnableThrottle = EnableThrottle,
                EnableBrake = EnableBrake,
                EnableKeyboardThrottle = EnableKeyboardThrottle,
                EnableKeyboardBrake = EnableKeyboardBrake,
                KeyboardThrottleKey = KeyboardThrottleKey,
                KeyboardBrakeKey = KeyboardBrakeKey,
                KeyboardThrottleLagUpSeconds = KeyboardThrottleLagUpSeconds,
                KeyboardThrottleLagDownSeconds = KeyboardThrottleLagDownSeconds,
                KeyboardBrakeLagUpSeconds = KeyboardBrakeLagUpSeconds,
                KeyboardBrakeLagDownSeconds = KeyboardBrakeLagDownSeconds,
                EnableWheelCentering = EnableWheelCentering,
                WheelReturnTimeSeconds = WheelReturnTimeSeconds,
                EnableBrakeResetting = EnableBrakeResetting,
                BrakeReturnTimeSeconds = BrakeReturnTimeSeconds,
                EnableRightClickPedalMode = EnableRightClickPedalMode,
                EnableCenterLockedCursor = EnableCenterLockedCursor,
                EnableFullThrottleHold = EnableFullThrottleHold,
                FullThrottleHoldDeadzone = FullThrottleHoldDeadzone,
                EnableSteeringDampening = EnableSteeringDampening,
                SteeringDampening = SteeringDampening,
                EnableBrakeAssist = EnableBrakeAssist,
                BrakeAssistThreshold = BrakeAssistThreshold,
                BrakeAssistStrength = BrakeAssistStrength,
                BrakeAssistHold = BrakeAssistHold,
                EnableBrakeXDeadzone = EnableBrakeXDeadzone,
                BrakeXDeadzone = BrakeXDeadzone,
                EnableTimedBrakeXDeadzone = EnableTimedBrakeXDeadzone,
                BrakeXDeadzoneDurationSeconds = BrakeXDeadzoneDurationSeconds,
                EnableTrailBraking = EnableTrailBraking,
                TrailBrakingRelease = TrailBrakingRelease,

                EnableKeyboardThrottleCurve = EnableKeyboardThrottleCurve,
                KeyboardThrottleCurvePoints = KeyboardThrottleCurvePoints,
                EnableKeyboardBrakeCurve = EnableKeyboardBrakeCurve,
                KeyboardBrakeCurvePoints = KeyboardBrakeCurvePoints,
                EnableKeyboardBrakeSteeringAssist = EnableKeyboardBrakeSteeringAssist,
                KeyboardBrakeSteeringAssistStrength = KeyboardBrakeSteeringAssistStrength,
                EnableKeyboardThrottleSteeringAssist = EnableKeyboardThrottleSteeringAssist,
                KeyboardThrottleSteeringAssistStrength = KeyboardThrottleSteeringAssistStrength,
                KeyboardThrottleAssistIdleThreshold = KeyboardThrottleAssistIdleThreshold,
                KeyboardThrottleAssistDuration = KeyboardThrottleAssistDuration
            };
        }

        private void ApplySettings(PresetSettings settings)
        {
            PedalSensitivity = settings.PedalSensitivity;
            MousePedalDeadZone = settings.MousePedalDeadZone;
            WheelSensitivity = settings.WheelSensitivity;
            EnableThrottle = settings.EnableThrottle;
            EnableBrake = settings.EnableBrake;
            EnableKeyboardThrottle = settings.EnableKeyboardThrottle;
            EnableKeyboardBrake = settings.EnableKeyboardBrake;
            KeyboardThrottleKey = settings.KeyboardThrottleKey;
            KeyboardBrakeKey = settings.KeyboardBrakeKey;
            KeyboardThrottleLagUpSeconds = settings.KeyboardThrottleLagUpSeconds;
            KeyboardThrottleLagDownSeconds = settings.KeyboardThrottleLagDownSeconds;
            KeyboardBrakeLagUpSeconds = settings.KeyboardBrakeLagUpSeconds;
            KeyboardBrakeLagDownSeconds = settings.KeyboardBrakeLagDownSeconds;
            EnableWheelCentering = settings.EnableWheelCentering;
            WheelReturnTimeSeconds = settings.WheelReturnTimeSeconds;
            EnableBrakeResetting = settings.EnableBrakeResetting;
            BrakeReturnTimeSeconds = settings.BrakeReturnTimeSeconds;
            EnableRightClickPedalMode = settings.EnableRightClickPedalMode;
            EnableCenterLockedCursor = settings.EnableCenterLockedCursor;
            EnableFullThrottleHold = settings.EnableFullThrottleHold;
            FullThrottleHoldDeadzone = settings.FullThrottleHoldDeadzone;
            EnableSteeringDampening = settings.EnableSteeringDampening;
            SteeringDampening = settings.SteeringDampening;
            EnableBrakeAssist = settings.EnableBrakeAssist;
            BrakeAssistThreshold = settings.BrakeAssistThreshold;
            BrakeAssistStrength = settings.BrakeAssistStrength;
            BrakeAssistHold = settings.BrakeAssistHold;
            EnableBrakeXDeadzone = settings.EnableBrakeXDeadzone;
            BrakeXDeadzone = settings.BrakeXDeadzone;
            EnableTimedBrakeXDeadzone = settings.EnableTimedBrakeXDeadzone;
            BrakeXDeadzoneDurationSeconds = settings.BrakeXDeadzoneDurationSeconds;
            EnableTrailBraking = settings.EnableTrailBraking;
            TrailBrakingRelease = settings.TrailBrakingRelease;

            EnableKeyboardThrottleCurve = settings.EnableKeyboardThrottleCurve;
            KeyboardThrottleCurvePoints = settings.KeyboardThrottleCurvePoints;
            EnableKeyboardBrakeCurve = settings.EnableKeyboardBrakeCurve;
            KeyboardBrakeCurvePoints = settings.KeyboardBrakeCurvePoints;
            EnableKeyboardBrakeSteeringAssist = settings.EnableKeyboardBrakeSteeringAssist;
            KeyboardBrakeSteeringAssistStrength = settings.KeyboardBrakeSteeringAssistStrength;
            EnableKeyboardThrottleSteeringAssist = settings.EnableKeyboardThrottleSteeringAssist;
            KeyboardThrottleSteeringAssistStrength = settings.KeyboardThrottleSteeringAssistStrength;
            KeyboardThrottleAssistIdleThreshold = settings.KeyboardThrottleAssistIdleThreshold;
            KeyboardThrottleAssistDuration = settings.KeyboardThrottleAssistDuration;
        }

        private void LoadPresetFile()
        {
            PresetFileData data = ReadPresetFile();
            Presets.Clear();

            for (int i = 0; i < PresetFileData.PresetSlotCount; i++)
            {
                PresetSlotData source = i < data.Presets.Count
                    ? data.Presets[i]
                    : PresetSlotData.CreateDefault(i);

                Presets.Add(new PresetSlotViewModel(i, source.Name, source.Settings));
            }

            int activeIndex = Math.Clamp(data.ActivePresetIndex, 0, Presets.Count - 1);
            _activePresetIndex = activeIndex;
            _isLoadingPresetFile = true;
            try
            {
                SelectedPreset = Presets[activeIndex];
            }
            finally
            {
                _isLoadingPresetFile = false;
            }

            ApplySettings(SelectedPreset.Settings);
            ReportPresetAction($"Auto-loaded preset \"{SelectedPreset.Name}\".");
            SavePresetFile();
        }

        private PresetFileData ReadPresetFile()
        {
            try
            {
                if (!File.Exists(_presetFilePath))
                {
                    return PresetFileData.CreateDefault();
                }

                string json = File.ReadAllText(_presetFilePath);
                PresetFileData? data = JsonSerializer.Deserialize<PresetFileData>(json);
                return data?.Normalize() ?? PresetFileData.CreateDefault();
            }
            catch
            {
                return PresetFileData.CreateDefault();
            }
        }

        private void SavePresetFile()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_presetFilePath)!);
                PresetFileData data = new()
                {
                    ActivePresetIndex = _activePresetIndex
                };

                foreach (PresetSlotViewModel preset in Presets)
                {
                    data.Presets.Add(new PresetSlotData
                    {
                        Name = preset.Name,
                        Settings = preset.Settings
                    });
                }

                JsonSerializerOptions options = new() { WriteIndented = true };
                File.WriteAllText(_presetFilePath, JsonSerializer.Serialize(data.Normalize(), options));
            }
            catch (Exception ex)
            {
                StatusMessage = $"[ERROR] Could not save presets: {ex.Message}";
            }
        }

        private void ReportPresetAction(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            PresetFeedbackMessage = $"{timestamp} - {message}";
            StatusMessage = message;
        }



        private void ExecuteOpenThrottleCurveEditor(object? parameter)
        {
            ResponseCurveEditorWindow editor = new(KeyboardThrottleCurvePoints) { Owner = Application.Current?.MainWindow };
            if (editor.ShowDialog() == true)
            {
                KeyboardThrottleCurvePoints = editor.CurvePoints;
                EnableKeyboardThrottleCurve = true;
                StatusMessage = "Throttle response curve updated.";
            }
        }

        private void ExecuteOpenBrakeCurveEditor(object? parameter)
        {
            ResponseCurveEditorWindow editor = new(KeyboardBrakeCurvePoints) { Owner = Application.Current?.MainWindow };
            if (editor.ShowDialog() == true)
            {
                KeyboardBrakeCurvePoints = editor.CurvePoints;
                EnableKeyboardBrakeCurve = true;
                StatusMessage = "Brake response curve updated.";
            }
        }



        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            if (_uiDispatcher.CheckAccess())
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
            else
            {
                _uiDispatcher.BeginInvoke(() =>
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
            }
        }

        private static bool RegisterMouseRawInput(IntPtr hwnd)
        {
            RawInputDevice[] devices =
            {
                new()
                {
                    UsagePage = HID_USAGE_PAGE_GENERIC,
                    Usage = HID_USAGE_GENERIC_MOUSE,
                    Flags = RIDEV_INPUTSINK,
                    Target = hwnd
                }
            };

            return RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RawInputDevice>());
        }

        private static bool TryReadRawMouse(IntPtr rawInputHandle, out RawMouseData mouseData)
        {
            mouseData = default;

            uint dataSize = 0;
            uint headerSize = (uint)Marshal.SizeOf<RawInputHeader>();
            uint sizeResult = GetRawInputData(rawInputHandle, RID_INPUT, IntPtr.Zero, ref dataSize, headerSize);
            if (sizeResult == uint.MaxValue || dataSize == 0) return false;

            IntPtr buffer = Marshal.AllocHGlobal((int)dataSize);
            try
            {
                sizeResult = GetRawInputData(rawInputHandle, RID_INPUT, buffer, ref dataSize, headerSize);
                if (sizeResult == uint.MaxValue) return false;

                RawInput raw = Marshal.PtrToStructure<RawInput>(buffer);
                if (raw.Header.Type != RIM_TYPEMOUSE) return false;

                mouseData = new RawMouseData(raw.Mouse.LastX, raw.Mouse.LastY);
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static bool IsKeyDown(int virtualKey)
        {
            return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        private static int GetVirtualKey(string keyName)
        {
            return keyName switch
            {
                "A" => 0x41,
                "D" => 0x44,
                "E" => 0x45,
                "F" => 0x46,
                "Q" => 0x51,
                "R" => 0x52,
                "S" => 0x53,
                "W" => 0x57,
                "Space" => 0x20,
                "LeftShift" => 0xA0,
                "LeftCtrl" => 0xA2,
                "Up" => 0x26,
                "Down" => 0x28,
                "Left" => 0x25,
                "Right" => 0x27,
                _ => 0
            };
        }

        public static Collection<ResponseCurvePoint> ParseResponseCurvePoints(string? pointsText)
        {
            Collection<ResponseCurvePoint> points = new();
            if (string.IsNullOrWhiteSpace(pointsText))
            {
                points.Add(new ResponseCurvePoint(0.0, 0.0));
                points.Add(new ResponseCurvePoint(1.0, 1.0));
                return points;
            }

            foreach (string pointText in pointsText.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] parts = pointText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length != 2) continue;
                if (!double.TryParse(parts[0], out double x) || !double.TryParse(parts[1], out double y)) continue;

                points.Add(new ResponseCurvePoint(Math.Clamp(x, 0.0, 1.0), Math.Clamp(y, 0.0, 1.0)));
            }

            var normalized = points
                .OrderBy(point => point.X)
                .GroupBy(point => point.X)
                .Select(group => group.Last())
                .ToArray();

            points.Clear();
            if (normalized.Length == 0 || normalized[0].X > 0.0)
            {
                points.Add(new ResponseCurvePoint(0.0, 0.0));
            }

            foreach (ResponseCurvePoint point in normalized)
            {
                points.Add(point);
            }

            if (points[points.Count - 1].X < 1.0)
            {
                points.Add(new ResponseCurvePoint(1.0, 1.0));
            }

            return points;
        }

        private static int GetScreenWidth()
        {
            return Math.Max(1, GetSystemMetrics(SM_CXSCREEN));
        }

        private static void CenterCursor()
        {
            int centerX = GetSystemMetrics(SM_CXSCREEN) / 2;
            int centerY = GetSystemMetrics(SM_CYSCREEN) / 2;
            SetCursorPos(centerX, centerY);
        }

        private static bool TryGetCursorPosition(out CursorPoint cursorPoint)
        {
            return GetCursorPos(out cursorPoint);
        }

        private static void KeepCursorXCentered()
        {
            if (!TryGetCursorPosition(out CursorPoint cursorPoint)) return;

            int centerX = GetSystemMetrics(SM_CXSCREEN) / 2;
            if (cursorPoint.X != centerX)
            {
                SetCursorPos(centerX, cursorPoint.Y);
            }
        }

        private void MaintainCenterLockedCursor()
        {
            if (!EnableCenterLockedCursor || !IsRunning || !_isSteeringActive) return;

            CenterCursor();
            LockCursorToCenter();
        }

        private static void LockCursorToCenter()
        {
            int centerX = GetSystemMetrics(SM_CXSCREEN) / 2;
            int centerY = GetSystemMetrics(SM_CYSCREEN) / 2;
            CursorClipRect clipRect = new()
            {
                Left = centerX,
                Top = centerY,
                Right = centerX + 1,
                Bottom = centerY + 1
            };

            ClipCursor(ref clipRect);
        }

        private static void UnlockCursor()
        {
            ClipCursor(IntPtr.Zero);
        }

        private void ShowMouseCursor()
        {
            if (!_cursorHidden) return;

            while (ShowCursor(true) < 0)
            {
            }

            _cursorHidden = false;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] RawInputDevice[] rawInputDevices,
            uint numDevices,
            uint size);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputData(
            IntPtr rawInput,
            uint command,
            IntPtr data,
            ref uint size,
            uint headerSize);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out CursorPoint point);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        [DllImport("user32.dll")]
        private static extern int ShowCursor(bool show);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ClipCursor(IntPtr rect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ClipCursor(ref CursorClipRect rect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, MouseHookHandler callback, IntPtr moduleHandle, uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

        private delegate IntPtr MouseHookHandler(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct RawInputDevice
        {
            public ushort UsagePage;
            public ushort Usage;
            public uint Flags;
            public IntPtr Target;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RawInputHeader
        {
            public uint Type;
            public uint Size;
            public IntPtr Device;
            public IntPtr WParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RawInput
        {
            public RawInputHeader Header;
            public RawMouse Mouse;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RawMouse
        {
            public ushort Flags;
            public ushort ButtonFlags;
            public ushort ButtonData;
            public uint RawButtons;
            public int LastX;
            public int LastY;
            public uint ExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CursorPoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CursorClipRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private readonly record struct RawMouseData(int LastX, int LastY);
    }

    public class PresetSlotViewModel : INotifyPropertyChanged
    {
        private string _name;
        private PresetSettings _settings;

        public PresetSlotViewModel(int slotIndex, string name, PresetSettings settings)
        {
            SlotIndex = slotIndex;
            _name = string.IsNullOrWhiteSpace(name) ? $"Preset {slotIndex + 1}" : name;
            _settings = settings;
        }

        public int SlotIndex { get; }

        public string DisplayName => $"{SlotIndex + 1}. {Name}";

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value) return;
                _name = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public PresetSettings Settings
        {
            get => _settings;
            set
            {
                _settings = value ?? PresetSettings.CreateDefault();
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class PresetSettings
    {
        public const string DefaultResponseCurvePoints = "0,0;0.25,0.15;0.5,0.5;0.75,0.85;1,1";

        public double PedalSensitivity { get; set; } = 40;
        public double MousePedalDeadZone { get; set; } = 0.1;
        public double WheelSensitivity { get; set; } = 0.1;
        public bool EnableThrottle { get; set; } = true;
        public bool EnableBrake { get; set; } = true;
        public bool EnableKeyboardThrottle { get; set; }
        public bool EnableKeyboardBrake { get; set; }
        public string KeyboardThrottleKey { get; set; } = "W";
        public string KeyboardBrakeKey { get; set; } = "S";
        public double KeyboardThrottleLagUpSeconds { get; set; } = 1;
        public double KeyboardThrottleLagDownSeconds { get; set; } = 0.7;
        public double KeyboardBrakeLagUpSeconds { get; set; } = 0.3;
        public double KeyboardBrakeLagDownSeconds { get; set; } = 0.24;
        public bool EnableWheelCentering { get; set; }
        public double WheelReturnTimeSeconds { get; set; } = 20.95;
        public bool EnableBrakeResetting { get; set; }
        public double BrakeReturnTimeSeconds { get; set; } = 5.0;
        public bool EnableRightClickPedalMode { get; set; } = false;
        public bool EnableCenterLockedCursor { get; set; }
        public bool EnableFullThrottleHold { get; set; } = false;
        public double FullThrottleHoldDeadzone { get; set; } = 0.01;
        public bool EnableSteeringDampening { get; set; } = true;
        public double SteeringDampening { get; set; } = 0.70;
        public bool EnableBrakeAssist { get; set; } = true;
        public double BrakeAssistThreshold { get; set; } = 0.70;
        public double BrakeAssistStrength { get; set; } = 0.35;
        public double BrakeAssistHold { get; set; }
        public bool EnableBrakeXDeadzone { get; set; } = false;
        public double BrakeXDeadzone { get; set; } = 0.04;
        public bool EnableTimedBrakeXDeadzone { get; set; } = false;
        public double BrakeXDeadzoneDurationSeconds { get; set; } = 0.05;
        public bool EnableTrailBraking { get; set; } = true;
        public double TrailBrakingRelease { get; set; } = 0.06;
        public bool EnableKeyboardThrottleCurve { get; set; }
        public string KeyboardThrottleCurvePoints { get; set; } = DefaultResponseCurvePoints;
        public bool EnableKeyboardBrakeCurve { get; set; }
        public string KeyboardBrakeCurvePoints { get; set; } = DefaultResponseCurvePoints;

        public bool EnableKeyboardBrakeSteeringAssist { get; set; }
        public double KeyboardBrakeSteeringAssistStrength { get; set; } = 1; // 0.0 - 2.0

        public bool EnableKeyboardThrottleSteeringAssist { get; set; }
        public double KeyboardThrottleSteeringAssistStrength { get; set; } = 1;
        public double KeyboardThrottleAssistIdleThreshold { get; set; } = 0.1; // secunde
        public double KeyboardThrottleAssistDuration { get; set; } = 4.0; // secunde
        public static PresetSettings CreateDefault()
        {
            return new PresetSettings();
        }
    }

    public class PresetSlotData
    {
        public string Name { get; set; } = string.Empty;
        public PresetSettings Settings { get; set; } = PresetSettings.CreateDefault();

        public static PresetSlotData CreateDefault(int slotIndex)
        {
            return new PresetSlotData
            {
                Name = $"Preset {slotIndex + 1}",
                Settings = PresetSettings.CreateDefault()
            };
        }
    }

    public class PresetFileData
    {
        public const int PresetSlotCount = 10;

        public int ActivePresetIndex { get; set; }
        public Collection<PresetSlotData> Presets { get; set; } = new();

        public static PresetFileData CreateDefault()
        {
            PresetFileData data = new();
            for (int i = 0; i < PresetSlotCount; i++)
            {
                data.Presets.Add(PresetSlotData.CreateDefault(i));
            }

            return data;
        }

        public PresetFileData Normalize()
        {
            ActivePresetIndex = Math.Clamp(ActivePresetIndex, 0, PresetSlotCount - 1);

            while (Presets.Count < PresetSlotCount)
            {
                Presets.Add(PresetSlotData.CreateDefault(Presets.Count));
            }

            while (Presets.Count > PresetSlotCount)
            {
                Presets.RemoveAt(Presets.Count - 1);
            }

            for (int i = 0; i < Presets.Count; i++)
            {
                Presets[i] ??= PresetSlotData.CreateDefault(i);
                if (string.IsNullOrWhiteSpace(Presets[i].Name))
                {
                    Presets[i].Name = $"Preset {i + 1}";
                }

                Presets[i].Settings ??= PresetSettings.CreateDefault();
            }

            return this;
        }
    }



    public readonly record struct ResponseCurvePoint(double X, double Y);
}
