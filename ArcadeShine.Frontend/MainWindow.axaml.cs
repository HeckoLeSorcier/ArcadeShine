using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ArcadeShine.Common;
using ArcadeShine.Common.DataModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using SDL2;

namespace ArcadeShine.Frontend;

public partial class MainWindow : Window
{
    private readonly LibVLC _libVlc = new ("--mouse-hide-timeout=0");

    private int _currentSystemIndex;

    private int _currentGameIndex;

    private List<ArcadeShineGame> _currentCategoryGames = null!;

    private readonly Dictionary<InputActionEnum, Key> _inputActionMap = new ();
    
    private Animation _animationCurrentSystemDisappearToLeftSide = null!;
    private Animation _animationNextSystemLogoAppearFromRightSide = null!;
    private Animation _animationCurrentSystemDisappearToRightSide = null!;
    private Animation _animationPreviousSystemAppearFromLeftSide = null!;
    
    private Task _nextCategoryAnimationTask = null!;
    private Task _previousCategoryAnimationTask = null!;
    
    private Animation _animationCurrentGameDisappearToUpSide = null!;
    private Animation _animationNextGameLogoAppearFromBottomSide = null!;
    private Animation _animationCurrentGameDisappearToBottomSide = null!;
    private Animation _animationPreviousGameAppearFromUpSide = null!;
    
    private Task _nextGameAnimationTask = null!;
    private Task _previousGameAnimationTask = null!;
    private Media _currentVideoMedia = null!;
    
    private Animation _globalFadeOutAnimation = null!;
    private Animation _globalFadeInAnimation = null!;
    
    private IDisposable _autoSelectRandomGameTimer = null!;
    private bool _isRandomizingGameSelection;
    private bool _cancelRandomGameSelection;

    private IDisposable _screenSleepTimer = null!;
    private bool _isScreenSleeping;
    private bool _isCancellingScreenSleeping;
    
    private bool _isInGame;
    
    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    
    [DllImport("user32.dll", EntryPoint = "SetSystemCursor")]
    private static extern bool SetSystemCursor(IntPtr hCursor, uint id);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfo")]
    private static extern bool SystemParametersInfo(uint action, uint param, IntPtr vparam, uint init);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "LoadCursorFromFileW")]
    private static extern IntPtr LoadCursorFromFile(string fileName);

    private const uint SPI_SETCURSORS = 0x0057;
    private const uint SPIF_UPDATEINIFILE = 0x01;
    private const uint SPIF_SENDCHANGE = 0x02;
    // IDs for system cursors
    private const uint OCR_NORMAL = 32512; // Default arrow cursor
    private const uint OCR_WAIT = 32514; // Loading (hourglass/spinner)
    private const uint OCR_APPSTARTING = 32650; // Arrow + spinner (busy while app starting)

    const int SW_RESTORE = 9;
    const int SW_MINIMIZE = 6;
    
    private readonly string? _currentLinuxDesktopEnv;

    private readonly System.Timers.Timer _gamepadPollTimer;
    
    private bool _specialButtonDown = false;

    private Process? _runningGameSystemProcess;
    private Process? _runningLauncherGameProcess;
    private TimeSpan _currentGameDuration;
    
    public MainWindow()
    {
        InitializeComponent();
        
        Cursor = new Cursor(StandardCursorType.None);
        
        MapInputs();

        var gameIsSelected = false;
        var gameNameToSelect = App.ArcadeShineFrontendSettings.PreserveLastSelectedGameOnExit
            ? App.ArcadeShineFrontendSettings.LastSelectedGame
            : App.ArcadeShineFrontendSettings.DefaultSelectedGame;
        if (!string.IsNullOrEmpty(gameNameToSelect))
        {
            var selectedGame =
                App.ArcadeShineGameList.FirstOrDefault(g =>
                    g.GameName == gameNameToSelect);
            if (selectedGame != null)
            {
                var lastGameSystem =
                    App.ArcadeShineSystemList.FirstOrDefault(s => s.SystemIdentifier == selectedGame.GameSystem);
                if (lastGameSystem != null)
                {
                    _currentSystemIndex = App.ArcadeShineSystemList.IndexOf(lastGameSystem);
                    UpdateCategory();
                    _currentGameIndex = _currentCategoryGames.IndexOf(selectedGame);
                    UpdateGame();
                    gameIsSelected = true;
                }
            }
        }

        if (!gameIsSelected)
        {
            UpdateCategory();
            UpdateGame();
        }

        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
        Closed += OnWindowClosed;
        ConfirmExitFrontendButton.Click += ConfirmExitFrontendButton_OnClick;
        CancelExitFrontendButton.Click += CancelExitFrontendButton_OnClick;

        if (OperatingSystem.IsLinux())
        {
            _currentLinuxDesktopEnv = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
        }

        if (OperatingSystem.IsWindows())
        {
            // Attempt to load a Windows cursor (.cur or .ani) from Assets and set it as the system arrow cursor.
            // This changes the system cursor globally until restored; ensure this is desired.
            IntPtr customCursorHandle = IntPtr.Zero;
            string? tempPath = null;
            try
            {
                // Prefer a .cur asset if available
                Uri? assetUri = null;
                if (AssetLoader.Exists(new Uri("avares://ArcadeShine.Frontend/Assets/BlackDOT.cur")))
                    assetUri = new Uri("avares://ArcadeShine.Frontend/Assets/BlackDOT.cur");

                if (assetUri != null)
                {
                    using var stream = AssetLoader.Open(assetUri);
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    var bytes = ms.ToArray();

                    tempPath = Path.Combine(Path.GetTempPath(), "arcadeshine_cursor.cur");
                    File.WriteAllBytes(tempPath, bytes);

                    customCursorHandle = LoadCursorFromFile(tempPath);
                }
            }
            catch
            {
                // Ignore and leave customCursorHandle as zero (no change will be applied)
            }
            finally
            {
                try
                {
                    if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch { /* ignore */ }
            }

            if (customCursorHandle != IntPtr.Zero)
            {
                // Ensure defaults are loaded first, then override OCR_NORMAL with our custom cursor
                SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
                SetSystemCursor(customCursorHandle, OCR_NORMAL);
                // Also override common loading/busy cursors with the same temporary cursor
                SetSystemCursor(customCursorHandle, OCR_WAIT);
                SetSystemCursor(customCursorHandle, OCR_APPSTARTING);
            }
        }

        if (SDL.SDL_Init(SDL.SDL_INIT_GAMECONTROLLER) != 0)
        {
            throw new Exception("SDL2.SDL.SDL_Init(SDL2.SDL.SDL_INIT_GAMECONTROLLER) failed");
        }

        for (var i = 0 ; i < SDL.SDL_NumJoysticks() ; i++)
        {
            if (SDL.SDL_IsGameController(i) == SDL.SDL_bool.SDL_TRUE)
            {
                SDL.SDL_GameControllerOpen(i);
            }
        }
        _gamepadPollTimer = new System.Timers.Timer(16); // 60Hz polling
        _gamepadPollTimer.Elapsed += (s, e) =>
        { 
            ProcessLastGamepadButtonsPressed();
        };
        _gamepadPollTimer.Start();

        if (App.ArcadeShineFrontendSettings.AllowInactivityMode)
            _autoSelectRandomGameTimer = DispatcherTimer.RunOnce(SelectRandomGame,
                TimeSpan.FromSeconds(App.ArcadeShineFrontendSettings.SecondsBeforeRandomGameSelectionInactivityMode));

        if (App.ArcadeShineFrontendSettings.AllowScreenSleep)
            _screenSleepTimer = DispatcherTimer.RunOnce(TriggerScreenSleep,
                TimeSpan.FromSeconds(App.ArcadeShineFrontendSettings.SecondsBeforeShutdownScreen));
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        // Ensure we restore the default system cursor on Windows when the window is closed
        RestoreSystemCursorsToDefault();
    }

    private void RestoreSystemCursorsToDefault()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            // Reload system cursors from defaults and broadcast the change
            SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, SPIF_SENDCHANGE | SPIF_UPDATEINIFILE);
        }
        catch
        {
            // ignore
        }
    }
    
    private void ProcessLastGamepadButtonsPressed()
    {
        SDL.SDL_PollEvent(out var sdlEvent);
        switch (sdlEvent.type)
        {
            case SDL.SDL_EventType.SDL_CONTROLLERAXISMOTION:
            case SDL.SDL_EventType.SDL_CONTROLLERTOUCHPADDOWN:
            case SDL.SDL_EventType.SDL_CONTROLLERTOUCHPADMOTION:
            case SDL.SDL_EventType.SDL_JOYAXISMOTION:
            case SDL.SDL_EventType.SDL_JOYHATMOTION:
            case SDL.SDL_EventType.SDL_JOYBUTTONDOWN:
            case SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN:
            case SDL.SDL_EventType.SDL_MOUSEMOTION:
            case SDL.SDL_EventType.SDL_MOUSEWHEEL:
                if (_isScreenSleeping && !_isCancellingScreenSleeping)
                {
                    _isCancellingScreenSleeping = true;
                    Dispatcher.UIThread.Invoke(CancelScreenSleepTimer);
                    return;
                }
                ResetRandomGameSelectionTimer();
                ResetSleepScreenTimer();
                break;
            
            case SDL.SDL_EventType.SDL_CONTROLLERBUTTONDOWN:
                if (_isScreenSleeping && !_isCancellingScreenSleeping)
                {
                    _isCancellingScreenSleeping = true;
                    Dispatcher.UIThread.Invoke(CancelScreenSleepTimer);
                    return;
                }
                ResetRandomGameSelectionTimer();
                ResetSleepScreenTimer();
                Dispatcher.UIThread.Invoke(() =>
                {
                    if (sdlEvent.cbutton.button == (byte)SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_UP)
                    {
                        if(!_isInGame)
                            OnNavigateUpInputAction();
                    }
                    if (sdlEvent.cbutton.button == (byte)SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_DOWN)
                    {
                        if(!_isInGame)
                            OnNavigateDownInputAction();
                    }
                    if (sdlEvent.cbutton.button == (byte)SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_LEFT)
                    {
                        if(!_isInGame)
                            OnNavigateLeftInputAction();
                    }
                    if (sdlEvent.cbutton.button == (byte)SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_RIGHT)
                    {
                        if(!_isInGame)
                            OnNavigateRightInputAction();
                    }
                    if (sdlEvent.cbutton.button == (byte)SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_A)
                    {
                        if(!_isInGame)
                            OnSelectInputAction();
                    }
                    if (sdlEvent.cbutton.button == (byte)SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_B)
                    {
                        if(!_isInGame)
                            OnBackInputAction();
                    }

                    if (_isInGame)
                    {
                        if (sdlEvent.cbutton.button == (byte)SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSTICK)
                        {
                            _specialButtonDown = true;
                        }
                        else if (_specialButtonDown && sdlEvent.cbutton.button == (byte)SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_RIGHTSTICK)
                        {
                            KillRunningGame();
                        }
                    }
                });
                return;
            
            case SDL.SDL_EventType.SDL_CONTROLLERBUTTONUP:
                if (!_isInGame) return;
                if (sdlEvent.cbutton.button == (byte)SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_LEFTSTICK)
                {
                    _specialButtonDown = false;
                }
                return;
        }
    }

    private async void SelectRandomGame()
    {
        try
        {
            _isRandomizingGameSelection = true;
            Dispatcher.UIThread.Invoke(() =>
            {
                VideoView.IsVisible = false;
                GameInfosVideoOverlay.IsVisible = false;
                FadePanel.Opacity = 1.0;
            });
            await _globalFadeOutAnimation.RunAsync(FadePanel);
            if (_cancelRandomGameSelection)
            {
                CancelRandomGameSelection();
                return;       
            }
            await Task.Delay(TimeSpan.FromSeconds(0.5));
            if (_cancelRandomGameSelection)
            {
                CancelRandomGameSelection();
                return;       
            }
            var currentGameListIndex = App.ArcadeShineGameList.IndexOf(_currentCategoryGames[_currentGameIndex]);
            Random random = new Random();
            var randomGameIndex = random.Next(0, App.ArcadeShineGameList.Count);
            while (currentGameListIndex == randomGameIndex)
            {
                randomGameIndex = random.Next(0, App.ArcadeShineGameList.Count);
            }
            if (_cancelRandomGameSelection)
            {
                CancelRandomGameSelection();
                return;       
            }
            var randomGame = App.ArcadeShineGameList[randomGameIndex];
            var randomGameSystem = App.ArcadeShineSystemList.FirstOrDefault(s => s.SystemIdentifier == randomGame.GameSystem);
            if (randomGameSystem != null)
                _currentSystemIndex = App.ArcadeShineSystemList.IndexOf(randomGameSystem);
            if (_cancelRandomGameSelection)
            {
                CancelRandomGameSelection();
                return;       
            }
            UpdateCategory();
            if (_cancelRandomGameSelection)
            {
                CancelRandomGameSelection();
                return;       
            }
            _currentGameIndex = _currentCategoryGames.IndexOf(randomGame);
            UpdateGame();
            if (_cancelRandomGameSelection)
            {
                CancelRandomGameSelection();
                return;       
            }
            await Task.Delay(TimeSpan.FromSeconds(0.5));
            if (_cancelRandomGameSelection)
            {
                CancelRandomGameSelection();
                return;       
            }
            await _globalFadeInAnimation.RunAsync(FadePanel);
            Dispatcher.UIThread.Invoke(() =>
            {
                FadePanel.Opacity = 0.0;
            });
            _autoSelectRandomGameTimer.Dispose();
            _autoSelectRandomGameTimer = DispatcherTimer.RunOnce(SelectRandomGame,
                TimeSpan.FromSeconds(App.ArcadeShineFrontendSettings.SecondsBeforeRandomGameSelectionInactivityMode));
            _isRandomizingGameSelection = false;
        }
        catch (Exception)
        {
            //ignored
        }
    }

    private void CancelRandomGameSelection()
    {
        _isRandomizingGameSelection = false;
        _cancelRandomGameSelection = false;
        Dispatcher.UIThread.Invoke(() =>
        {
            FadePanel.Opacity = 0.0;
        });
    }

    private void MapInputs()
    {
        _inputActionMap.Add(InputActionEnum.NavigateUpAction, (Key)Enum.Parse(typeof(Key), App.ArcadeShineFrontendSettings.UpKey));
        _inputActionMap.Add(InputActionEnum.NavigateDownAction, (Key)Enum.Parse(typeof(Key), App.ArcadeShineFrontendSettings.DownKey));
        _inputActionMap.Add(InputActionEnum.NavigateLeftAction, (Key)Enum.Parse(typeof(Key), App.ArcadeShineFrontendSettings.LeftKey));
        _inputActionMap.Add(InputActionEnum.NavigateRightAction, (Key)Enum.Parse(typeof(Key), App.ArcadeShineFrontendSettings.RightKey));
        _inputActionMap.Add(InputActionEnum.SelectAction, (Key)Enum.Parse(typeof(Key), App.ArcadeShineFrontendSettings.EnterKey));
        _inputActionMap.Add(InputActionEnum.BackAction, (Key)Enum.Parse(typeof(Key), App.ArcadeShineFrontendSettings.BackKey));
        _inputActionMap.Add(InputActionEnum.ExitAction, (Key)Enum.Parse(typeof(Key), App.ArcadeShineFrontendSettings.ExitKey));
    }
    
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_inputActionMap.ContainsValue(e.Key))
        {
            if (_isScreenSleeping && !_isCancellingScreenSleeping)
            {
                _isCancellingScreenSleeping = true;
                Dispatcher.UIThread.Invoke(CancelScreenSleepTimer);
            }
            ResetRandomGameSelectionTimer();
            ResetSleepScreenTimer();
            InputActionEnum? action = null;
            foreach (var kvp in _inputActionMap)
            {
                if (kvp.Value.Equals(e.Key))
                {
                    action = kvp.Key;
                    break;
                }
            }

            if (action != null)
            {
                switch (action)
                {
                    case InputActionEnum.NavigateUpAction:
                        OnNavigateUpInputAction();
                        break;
                    case InputActionEnum.NavigateDownAction:
                        OnNavigateDownInputAction();
                        break;
                    case InputActionEnum.NavigateLeftAction:
                        OnNavigateLeftInputAction();
                        break;
                    case InputActionEnum.NavigateRightAction:
                        OnNavigateRightInputAction();
                        break;
                    case InputActionEnum.SelectAction:
                        OnSelectInputAction();
                        break;
                    case InputActionEnum.BackAction:
                        OnBackInputAction();
                        break;
                    case InputActionEnum.ExitAction:
                        OnExitInputAction();
                        break;
                }
            }
        }
    }

    private void ResetRandomGameSelectionTimer()
    {
        if (!App.ArcadeShineFrontendSettings.AllowInactivityMode) return;
        
        _autoSelectRandomGameTimer.Dispose();
        _autoSelectRandomGameTimer = DispatcherTimer.RunOnce(SelectRandomGame,
            TimeSpan.FromSeconds(App.ArcadeShineFrontendSettings.SecondsBeforeRandomGameSelectionInactivityMode));
        if (_isRandomizingGameSelection)
        {
            _cancelRandomGameSelection = true;
        }
    }
    
    private void ResetSleepScreenTimer()
    {
        _screenSleepTimer?.Dispose();
        
        if (!App.ArcadeShineFrontendSettings.AllowScreenSleep) return;
        
        _screenSleepTimer = DispatcherTimer.RunOnce(TriggerScreenSleep,
            TimeSpan.FromSeconds(App.ArcadeShineFrontendSettings.SecondsBeforeShutdownScreen));
        
    }

    private void OnExitInputAction()
    {
        if (!ExitConfirmationPanel.IsVisible)
        {
            OpenExitConfirmationPopup();
        }
        else
        {
            CloseExitConfirmationPopup();
        }
    }

    private void OnBackInputAction()
    {
        if (ExitConfirmationPanel.IsVisible)
        {
            CloseExitConfirmationPopup();
        }
    }

    private void OnSelectInputAction()
    {
        if (!ExitConfirmationPanel.IsVisible)
        {
            LaunchCurrentSelectedGame();
        }
        else
        {
            ShutdownArcadeShineFrontend();
        }
    }

    private void OnNavigateRightInputAction()
    {
        if (_nextCategoryAnimationTask is null or { IsCompleted: true })
        {
            if (App.ArcadeShineSystemList.Count == 0)
            {
                _currentSystemIndex = 0;
            }
            else
            {
                if (_currentSystemIndex == App.ArcadeShineSystemList.Count - 1)
                {
                    _currentSystemIndex = 0;
                }
                else
                {
                    _currentSystemIndex++;
                }
            }

            UpdateCategory();
            _nextCategoryAnimationTask = LaunchNextCategoryAnimations();
            _ = _nextCategoryAnimationTask.ContinueWith(_ => UpdateGame());
        }
    }

    private void OnNavigateLeftInputAction()
    {
        if (_previousCategoryAnimationTask is null or { IsCompleted: true })
        {
            if (App.ArcadeShineSystemList.Count == 0)
            {
                _currentSystemIndex = 0;
            }
            else
            {
                if (_currentSystemIndex == 0)
                {
                    _currentSystemIndex = App.ArcadeShineSystemList.Count - 1;
                }
                else
                {
                    _currentSystemIndex--;
                }
            }

            UpdateCategory();
            _previousCategoryAnimationTask = LaunchPreviousCategoryAnimations();
            _ = _previousCategoryAnimationTask.ContinueWith(_ => UpdateGame());
        }
    }

    private void OnNavigateDownInputAction()
    {
        if (_nextGameAnimationTask is null or { IsCompleted: true })
        {
            if (_currentCategoryGames.Count == 0)
            {
                _currentGameIndex = 0;
            }
            else
            {
                if (_currentGameIndex == _currentCategoryGames.Count - 1)
                {
                    _currentGameIndex = 0;
                }
                else
                {
                    _currentGameIndex++;
                }
            }

            UpdateGame();
            _nextGameAnimationTask = LaunchNextGameAnimations();
        }
    }

    private void OnNavigateUpInputAction()
    {
        if (_previousGameAnimationTask is null or { IsCompleted: true })
        {
            if (_currentCategoryGames.Count == 0)
            {
                _currentGameIndex = 0;
            }
            else
            {
                if (_currentGameIndex == 0)
                {
                    _currentGameIndex = _currentCategoryGames.Count - 1;
                }
                else
                {
                    _currentGameIndex--;
                }
            }

            UpdateGame();
            _previousGameAnimationTask = LaunchPreviousGameAnimations();
        }
    }

    private async void LaunchCurrentSelectedGame()
    {
        try
        {
            _isInGame = true;
            PauseVideo();
            CancelVideoPlay();
            _autoSelectRandomGameTimer.Dispose();
            _screenSleepTimer.Dispose();
            Dispatcher.UIThread.Invoke(() =>
            {
                VideoView.IsVisible = false;
                GameInfosVideoOverlay.IsVisible = false;
                FadePanel.Opacity = 1.0;
            });
            await _globalFadeOutAnimation.RunAsync(FadePanel);
            Dispatcher.UIThread.Invoke(() =>
            {
                LoadingPanel.IsVisible = true;
            });
        
            await Task.Delay(TimeSpan.FromSeconds(0.5));
            
            // Create a new process
            _runningGameSystemProcess = new Process();
            // Set the process start info
            _runningGameSystemProcess.StartInfo.FileName = App.ArcadeShineSystemList[_currentSystemIndex].SystemExecutable; // specify the command to run
            string gameProcessArgs;
            string gameProcessNameToWatch = string.Empty;
            bool isLauncherGame = App.ArcadeShineSystemList[_currentSystemIndex].SystemIsGameLauncher;
            if (isLauncherGame)
            {
                gameProcessArgs = _currentCategoryGames[_currentGameIndex].GameProcessArgs;
                gameProcessNameToWatch = _currentCategoryGames[_currentGameIndex].GameProcessNameToWatch;
            }
            else
            {
                gameProcessArgs = _currentCategoryGames[_currentGameIndex].GameProcessArgs;
            }
            var arguments = App.ArcadeShineSystemList[_currentSystemIndex].SystemExecutableArguments
                .Replace("{GAME_ARGS}", gameProcessArgs);
            _runningGameSystemProcess.StartInfo.Arguments = arguments; // specify the arguments
            // Set additional process start info as necessary
            _runningGameSystemProcess.StartInfo.UseShellExecute = false;
            _runningGameSystemProcess.StartInfo.CreateNoWindow = true;
            _runningGameSystemProcess.StartInfo.RedirectStandardOutput = true;
            // Start the process
            _runningGameSystemProcess.Start();

            var startGameTime = DateTime.Now;
            
            if (!isLauncherGame)
            {
                BringProcessWindowToFront(_runningGameSystemProcess);
                await _runningGameSystemProcess.WaitForExitAsync();
            }
            else
            {
                _runningLauncherGameProcess =  null;
                var loadingError = false;
                do
                {
                    ReduceProcessWindow(_runningGameSystemProcess);
                    _runningLauncherGameProcess = Process.GetProcessesByName(gameProcessNameToWatch).FirstOrDefault();
                    if (TimeSpan.FromSeconds(40) < DateTime.Now - startGameTime)
                    {
                        await KillRunningGame();
                        loadingError = true;
                        break;
                    }
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                } while (_runningLauncherGameProcess == null);
                
                if (!loadingError && _runningLauncherGameProcess is { HasExited: false })
                {
                    BringProcessWindowToFront(_runningLauncherGameProcess);

                    await _runningLauncherGameProcess.WaitForExitAsync();
                }
            }
            if(App.ArcadeShineSystemList[_currentSystemIndex].ExitLauncherOnGameExit)
                await KillRunningGame();
            var endGameTime = DateTime.Now;
            _currentGameDuration = endGameTime - startGameTime;
            _currentCategoryGames[_currentGameIndex].GamePlayedTime += _currentGameDuration.TotalSeconds;
            ArcadeShineGameList.Save(App.ArcadeShineFrontendSettings.GameLibraryPath, App.ArcadeShineGameList);
            
            Dispatcher.UIThread.Invoke(() =>
            {
                LoadingPanel.IsVisible = false;
            });
            await _globalFadeInAnimation.RunAsync(FadePanel);
            Dispatcher.UIThread.Invoke(() =>
            {
                FadePanel.Opacity = 0.0;
            });
            _runningGameSystemProcess =  null;
            _runningLauncherGameProcess =  null;
            _isInGame = false;
            ThreadPool.QueueUserWorkItem(_ => VideoView.MediaPlayer?.Play(_currentVideoMedia));
            _currentGameDuration = TimeSpan.Zero;
            ResetRandomGameSelectionTimer();
            ResetSleepScreenTimer();
        }
        catch (Exception)
        {
            // ignored
        }
    }

    private async Task KillRunningGame()
    {
        if (!_isInGame) return;
        try
        {
            if (OperatingSystem.IsLinux())
            {
                await Task.Delay(TimeSpan.FromSeconds(0.5));
                _runningLauncherGameProcess?.Close();
                await Task.Delay(TimeSpan.FromSeconds(0.5));
                _runningLauncherGameProcess?.Kill(true);
                await Task.Delay(TimeSpan.FromSeconds(0.5));
                _runningGameSystemProcess?.Close();
                await Task.Delay(TimeSpan.FromSeconds(0.5));
                _runningGameSystemProcess?.Kill(true);
            }
            else if (OperatingSystem.IsWindows())
            {
                if (_runningLauncherGameProcess is { HasExited: false })
                {
                    await Process.Start(new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/im {_runningLauncherGameProcess.ProcessName}.exe /f /t",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }).WaitForExitAsync();
                }
                if (_runningGameSystemProcess is { HasExited: false })
                {
                    await Process.Start(new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/im {_runningGameSystemProcess.ProcessName}.exe /f /t",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }).WaitForExitAsync();
                }
            }
        }
        catch
        {
            // ignored
        }
    }

    private void ReduceProcessWindow(Process process)
    {
        if (OperatingSystem.IsLinux())
        {
            Process.Start("xdotool", $"search --pid {process.Id} windowminimize");
        }
        else if (OperatingSystem.IsWindows())
        {
            ShowWindow(process.MainWindowHandle, SW_MINIMIZE);
        }
    }
    
    private void BringProcessWindowToFront(Process process)
    {
        if (OperatingSystem.IsLinux())
        {
            Process.Start("xdotool", $"search --pid {process.Id} windowactivate");
        }
        else if (OperatingSystem.IsWindows())
        {
            ShowWindow(process.MainWindowHandle, SW_RESTORE);
            SetForegroundWindow(process.MainWindowHandle);
        }
    }

    private async Task LaunchNextCategoryAnimations()
    {
        var animTask1 = _animationCurrentSystemDisappearToLeftSide.RunAsync(PreviousGameSystemLogoImage);
        var animTask2 = _animationNextSystemLogoAppearFromRightSide.RunAsync(CurrentGameSystemLogoImage);
        await Task.WhenAll(animTask1, animTask2);
    }
    
    private async Task LaunchPreviousCategoryAnimations()
    {
        var animTask1 = _animationCurrentSystemDisappearToRightSide.RunAsync(NextGameSystemLogoImage);
        var animTask2 = _animationPreviousSystemAppearFromLeftSide.RunAsync(CurrentGameSystemLogoImage);
        await Task.WhenAll(animTask1, animTask2);
    }
    
    private async Task LaunchNextGameAnimations()
    {
        var animTask1 = _animationCurrentGameDisappearToUpSide.RunAsync(PreviousGameLogoImage);
        var animTask2 = _animationNextGameLogoAppearFromBottomSide.RunAsync(CurrentGameLogoImage);
        await Task.WhenAll(animTask1, animTask2);
    }
    
    private async Task LaunchPreviousGameAnimations()
    {
        var animTask1 = _animationCurrentGameDisappearToBottomSide.RunAsync(NextGameLogoImage);
        var animTask2 = _animationPreviousGameAppearFromUpSide.RunAsync(CurrentGameLogoImage);
        await Task.WhenAll(animTask1, animTask2);
    }

    private void UpdateCategory()
    {
        _currentCategoryGames = App.ArcadeShineGameList.FindAll(g =>
            g.GameSystem == App.ArcadeShineSystemList[_currentSystemIndex].SystemIdentifier);
        _currentGameIndex = 0;
        var previousIndex = _currentSystemIndex - 1;
        var nextIndex = _currentSystemIndex + 1;
        if (App.ArcadeShineSystemList.Count == 0)
        {
            previousIndex = 0;
            nextIndex = 0;
        }
        else
        {
            if (_currentSystemIndex == 0) previousIndex = App.ArcadeShineSystemList.Count - 1;
            if (_currentSystemIndex == App.ArcadeShineSystemList.Count - 1) nextIndex = 0;
        }

        Bitmap previousGameSystemLogo = new Bitmap(App.ArcadeShineSystemList[previousIndex].SystemLogo);
        Bitmap currentGameSystemLogo = new Bitmap(App.ArcadeShineSystemList[_currentSystemIndex].SystemLogo);
        Bitmap nextGameSystemLogo = new Bitmap(App.ArcadeShineSystemList[nextIndex].SystemLogo);
        Dispatcher.UIThread.Invoke(() =>
        {
            CurrentGameSystemLogoImage.Source = currentGameSystemLogo;
            NextGameSystemLogoImage.Source = nextGameSystemLogo;
            PreviousGameSystemLogoImage.Source = previousGameSystemLogo;
        });
    }

    private void UpdateGame()
    {
        CancelVideoPlay();
        var previousIndex = _currentGameIndex - 1;
        var nextIndex = _currentGameIndex + 1;
        if (_currentCategoryGames.Count == 0)
        {
            previousIndex = 0;
            nextIndex = 0;
        }
        else
        {
            if (_currentGameIndex == 0) previousIndex = _currentCategoryGames.Count - 1;
            if (_currentGameIndex == _currentCategoryGames.Count - 1) nextIndex = 0;
        }

        App.ArcadeShineFrontendSettings.LastSelectedGame = _currentCategoryGames[_currentGameIndex].GameName;
        ArcadeShineFrontendSettings.Save(App.ArcadeShineFrontendSettings);
        Bitmap previousGameLogo = new Bitmap(_currentCategoryGames[previousIndex].GameLogo);
        Bitmap currentGameLogo = new Bitmap(_currentCategoryGames[_currentGameIndex].GameLogo);
        Bitmap nextGameLogo = new Bitmap(_currentCategoryGames[nextIndex].GameLogo);
        Bitmap gameBackground = new Bitmap(_currentCategoryGames[_currentGameIndex].GameBackgroundPicture);
        Dispatcher.UIThread.Invoke(() =>
        {
            GameTitleIndexCountTextBlock.Text =
                $"{Lang.Resources.TitleNumber} {_currentGameIndex + 1} / {_currentCategoryGames.Count}  -  {Lang.Resources.TitleCount} {App.ArcadeShineGameList.Count}";
            CurrentGameLogoImage.Source = currentGameLogo;
            CurrentLoadingGameLogoImage.Source = currentGameLogo;
            NextGameLogoImage.Source = nextGameLogo;
            PreviousGameLogoImage.Source = previousGameLogo;
            GameBackground.Source = gameBackground;
            GameDescriptionTextBlock.Text = _currentCategoryGames[_currentGameIndex].GameDescription;
            if(_currentCategoryGames[_currentGameIndex].GameGenres.Count > 0)
                GameGenresTextBlock.Text = _currentCategoryGames[_currentGameIndex].GameGenres.Aggregate((a, b) => $"{a}, {b}");
            GameDeveloperTextBlock.Text = _currentCategoryGames[_currentGameIndex].GameDeveloper;
            GameYearTextBlock.Text = _currentCategoryGames[_currentGameIndex].GameReleaseYear;
            GameNameTextBlock.Text = _currentCategoryGames[_currentGameIndex].GameName;
            var gamePlayedTime = TimeSpan.FromSeconds(_currentCategoryGames[_currentGameIndex].GamePlayedTime);
            var playedTimeText = Lang.Resources.GamePlayedTimeNever;
            if (gamePlayedTime.TotalSeconds > 0)
            {
                playedTimeText = string.Empty;
                if (Math.Round(gamePlayedTime.TotalHours) > 0)
                {
                    playedTimeText = $"{Math.Round(gamePlayedTime.TotalHours)} {Lang.Resources.GamePlayedTimeHours}";
                    playedTimeText += $" {Math.Round(gamePlayedTime.TotalMinutes % 60)} {Lang.Resources.GamePlayedTimeMinutes}";
                }
                else
                {
                    playedTimeText = $"{Math.Round(gamePlayedTime.TotalMinutes)} {Lang.Resources.GamePlayedTimeMinutes}";
                }
            }
            GamePlayedTimeTextBlock.Text = playedTimeText;
            if(!Design.IsDesignMode)
                Task.Delay(TimeSpan.FromSeconds(1)).ContinueWith(_ => Play());
        });
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        GenerateAnimations();
    }

    private void GenerateAnimations()
    {
        // CATEGORY ANIMATIONS
        //Move and disappear on the left side
        _animationCurrentSystemDisappearToLeftSide = new Animation
        {
            Duration = TimeSpan.FromSeconds(0.3),
            Easing = new QuarticEaseInOut()
        };
        _animationCurrentSystemDisappearToLeftSide.Children.Add(new KeyFrame()
        {
            Cue = new Cue(0.0),
            Setters = { new Setter(OpacityProperty, 1.0), new Setter(TranslateTransform.XProperty, 480.0) }
        });
        _animationCurrentSystemDisappearToLeftSide.Children.Add(new KeyFrame()
        {
            Cue = new Cue(1.0),
            Setters = { new Setter(OpacityProperty, 0.0), new Setter(TranslateTransform.XProperty, 0.0) }
        });
        
        //Move and appear from the left side
        _animationNextSystemLogoAppearFromRightSide = new Animation
        {
            Duration = TimeSpan.FromSeconds(0.3),
            Easing = new QuarticEaseInOut()
        };
        _animationNextSystemLogoAppearFromRightSide.Children.Add(new KeyFrame()
        {
            Cue = new Cue(0.0),
            Setters = { new Setter(OpacityProperty, 0.0), new Setter(TranslateTransform.XProperty, 480.0) }
        });
        _animationNextSystemLogoAppearFromRightSide.Children.Add(new KeyFrame()
        {
            Cue = new Cue(1.0),
            Setters = { new Setter(OpacityProperty, 1.0), new Setter(TranslateTransform.XProperty, 0.0) }
        });
        
        //Move and disappear on the right side
        _animationCurrentSystemDisappearToRightSide = new Animation
        {
            Duration = TimeSpan.FromSeconds(0.3),
            Easing = new QuarticEaseInOut()
        };
        _animationCurrentSystemDisappearToRightSide.Children.Add(new KeyFrame()
        {
            Cue = new Cue(0.0),
            Setters = { new Setter(OpacityProperty, 1.0), new Setter(TranslateTransform.XProperty, -480.0) }
        });
        _animationCurrentSystemDisappearToRightSide.Children.Add(new KeyFrame()
        {
            Cue = new Cue(1.0),
            Setters = { new Setter(OpacityProperty, 0.0), new Setter(TranslateTransform.XProperty, 0.0) }
        });
        
        //Move and appear from the right side
        _animationPreviousSystemAppearFromLeftSide = new Animation
        {
            Duration = TimeSpan.FromSeconds(0.3),
            Easing = new QuarticEaseInOut()
        };
        _animationPreviousSystemAppearFromLeftSide.Children.Add(new KeyFrame()
        {
            Cue = new Cue(0.0),
            Setters = { new Setter(OpacityProperty, 0.0), new Setter(TranslateTransform.XProperty, -480.0) }
        });
        _animationPreviousSystemAppearFromLeftSide.Children.Add(new KeyFrame()
        {
            Cue = new Cue(1.0),
            Setters = { new Setter(OpacityProperty, 1.0), new Setter(TranslateTransform.XProperty, 0.0) }
        });
        
        
        // GAME ANIMATIONS
        //Move and disappear on the up side
        _animationCurrentGameDisappearToUpSide = new Animation
        {
            Duration = TimeSpan.FromSeconds(0.3),
            Easing = new QuarticEaseInOut()
        };
        _animationCurrentGameDisappearToUpSide.Children.Add(new KeyFrame()
        {
            Cue = new Cue(0.0),
            Setters = { new Setter(OpacityProperty, 1.0), new Setter(TranslateTransform.YProperty, 192.0) }
        });
        _animationCurrentGameDisappearToUpSide.Children.Add(new KeyFrame()
        {
            Cue = new Cue(1.0),
            Setters = { new Setter(OpacityProperty, 0.0), new Setter(TranslateTransform.YProperty, 0.0) }
        });
        
        //Move and appear from the bottom side
        _animationNextGameLogoAppearFromBottomSide = new Animation
        {
            Duration = TimeSpan.FromSeconds(0.3),
            Easing = new QuarticEaseInOut()
        };
        _animationNextGameLogoAppearFromBottomSide.Children.Add(new KeyFrame()
        {
            Cue = new Cue(0.0),
            Setters = { new Setter(OpacityProperty, 0.0), new Setter(TranslateTransform.YProperty, 192.0) }
        });
        _animationNextGameLogoAppearFromBottomSide.Children.Add(new KeyFrame()
        {
            Cue = new Cue(1.0),
            Setters = { new Setter(OpacityProperty, 1.0), new Setter(TranslateTransform.YProperty, 0.0) }
        });
        
        //Move and disappear on the bottom side
        _animationCurrentGameDisappearToBottomSide = new Animation
        {
            Duration = TimeSpan.FromSeconds(0.3),
            Easing = new QuarticEaseInOut()
        };
        _animationCurrentGameDisappearToBottomSide.Children.Add(new KeyFrame()
        {
            Cue = new Cue(0.0),
            Setters = { new Setter(OpacityProperty, 1.0), new Setter(TranslateTransform.YProperty, -192.0) }
        });
        _animationCurrentGameDisappearToBottomSide.Children.Add(new KeyFrame()
        {
            Cue = new Cue(1.0),
            Setters = { new Setter(OpacityProperty, 0.0), new Setter(TranslateTransform.YProperty, 0.0) }
        });
        
        //Move and appear from the up side
        _animationPreviousGameAppearFromUpSide = new Animation
        {
            Duration = TimeSpan.FromSeconds(0.3),
            Easing = new QuarticEaseInOut()
        };
        _animationPreviousGameAppearFromUpSide.Children.Add(new KeyFrame()
        {
            Cue = new Cue(0.0),
            Setters = { new Setter(OpacityProperty, 0.0), new Setter(TranslateTransform.YProperty, -192.0) }
        });
        _animationPreviousGameAppearFromUpSide.Children.Add(new KeyFrame()
        {
            Cue = new Cue(1.0),
            Setters = { new Setter(OpacityProperty, 1.0), new Setter(TranslateTransform.YProperty, 0.0) }
        });
        
        //Global Fade Out
        _globalFadeOutAnimation = new Animation
        {
            Duration = TimeSpan.FromSeconds(1.0),
            Easing = new SineEaseOut()
        };
        _globalFadeOutAnimation.Children.Add(new KeyFrame()
        {
            Cue = new Cue(0.0),
            Setters = { new Setter(OpacityProperty, 0.0) }
        });
        _globalFadeOutAnimation.Children.Add(new KeyFrame()
        {
            Cue = new Cue(1.0),
            Setters = { new Setter(OpacityProperty, 1.0) }
        });
        
        //Global Fade In
        _globalFadeInAnimation = new Animation
        {
            Duration = TimeSpan.FromSeconds(1.0),
            Easing = new SineEaseIn()
        };
        _globalFadeInAnimation.Children.Add(new KeyFrame()
        {
            Cue = new Cue(0.0),
            Setters = { new Setter(OpacityProperty, 1.0) }
        });
        _globalFadeInAnimation.Children.Add(new KeyFrame()
        {
            Cue = new Cue(1.0),
            Setters = { new Setter(OpacityProperty, 0.0) }
        });
    }

    private void Play()
    {
        if (Design.IsDesignMode)
        {
            return;
        }

        Dispatcher.UIThread.Invoke(() =>
        {
            if (VideoView.MediaPlayer is { IsPlaying: true })
            {
                VideoView.MediaPlayer.Stop();
                VideoView.IsVisible = false;
                GameInfosVideoOverlay.IsVisible = false;
            }
            
            VideoView.MediaPlayer = new MediaPlayer(_libVlc);
            VideoView.MediaPlayer.EnableMouseInput = false;
            VideoView.Cursor = new Cursor(StandardCursorType.None);
            VideoView.MediaPlayer.CropGeometry = _currentCategoryGames[_currentGameIndex].GameVideoAspectRatio;
            VideoView.MediaPlayer.EnableHardwareDecoding = true;
            VideoView.MediaPlayer.Playing += MediaPlayerOnPlaying;
            VideoView.MediaPlayer.EndReached += MediaPlayerOnEndReached;
            PlayCurrentGameVideo();
        });
    }

    private void MediaPlayerOnPlaying(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            VideoView.IsVisible = true;
            GameInfosVideoOverlay.InvalidateArrange();
            Task.Delay(TimeSpan.FromSeconds(1)).ContinueWith(_ =>
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    GameInfosVideoOverlay.InvalidateArrange();
                    GameInfosVideoOverlay.IsVisible = true;
                });
            });
        });
    }

    private void MediaPlayerOnEndReached(object? sender, EventArgs e)
    {
        ThreadPool.QueueUserWorkItem(_ => VideoView.MediaPlayer?.Play(_currentVideoMedia));
    }

    private void PlayCurrentGameVideo()
    {
        _currentVideoMedia = new Media(_libVlc, _currentCategoryGames[_currentGameIndex].GameVideo);
        Dispatcher.UIThread.Invoke(() =>
            VideoView.Margin = _currentCategoryGames[_currentGameIndex].GameVideoAspectRatio == "16:9"
                ? Thickness.Parse("40 56 0 55")
                : Thickness.Parse("87 0 87 0"));
        ThreadPool.QueueUserWorkItem(_ =>
        {
            if(!_isInGame)
                VideoView.MediaPlayer?.Play(_currentVideoMedia);
        });
    }

    private void CancelVideoPlay()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            VideoView.IsVisible = false;
            GameInfosVideoOverlay.IsVisible = false;
        });
        VideoView.MediaPlayer?.Stop();
    }
    
    private void PauseVideo()
    {
        VideoView.MediaPlayer?.Pause();
    }

    private void OpenExitConfirmationPopup()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            VideoView.IsVisible = false;
            GameInfosVideoOverlay.IsVisible = false;
        });
        ExitConfirmationPanel.IsVisible = true;
    }
    
    private void CloseExitConfirmationPopup()
    {
        ExitConfirmationPanel.IsVisible = false;
        Dispatcher.UIThread.Invoke(() =>
        {
            VideoView.IsVisible = true;
            GameInfosVideoOverlay.IsVisible = true;
        });
    }

    private void ConfirmExitFrontendButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ExitConfirmationPanel.IsVisible)
        {
            ShutdownArcadeShineFrontend();
        }
    }

    private void ShutdownArcadeShineFrontend()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            // Before shutting down, make sure to restore system cursors on Windows
            RestoreSystemCursorsToDefault();
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        });
    }

    private void CancelExitFrontendButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ExitConfirmationPanel.IsVisible)
        {
            CloseExitConfirmationPopup();
        }
    }

    private async void TriggerScreenSleep()
    {
        if (!App.ArcadeShineFrontendSettings.AllowScreenSleep || _isInGame) return;
        _isScreenSleeping = true;
        PauseVideo();
        CancelVideoPlay();
        _autoSelectRandomGameTimer.Dispose();
        _screenSleepTimer.Dispose();
        Dispatcher.UIThread.Invoke(() =>
        {
            VideoView.IsVisible = false;
            GameInfosVideoOverlay.IsVisible = false;
            FadePanel.Opacity = 1.0;
        });
        await _globalFadeOutAnimation.RunAsync(FadePanel);

        if (OperatingSystem.IsLinux())
        {
            switch (_currentLinuxDesktopEnv)
            {
                case "GNOME":
                    Process.Start("busctl",
                        "--user set-property org.gnome.Mutter.DisplayConfig /org/gnome/Mutter/DisplayConfig org.gnome.Mutter.DisplayConfig PowerSaveMode i 3");
                    break;
                case "KDE":
                    Process.Start("dbus-send",
                        "--session --print-reply --dest=org.kde.kglobalaccel /component/org_kde_powerdevil org.kde.kglobalaccel.Component.invokeShortcut string:\"Turn Off Screen\"");
                    break;
            }
        }
    }
    
    private async void CancelScreenSleepTimer()
    {
        if(!App.ArcadeShineFrontendSettings.AllowScreenSleep || !_isScreenSleeping) return;
        
        await _globalFadeInAnimation.RunAsync(FadePanel);
        Dispatcher.UIThread.Invoke(() =>
        {
            FadePanel.Opacity = 0.0;
        });
        _runningGameSystemProcess =  null;
        _runningLauncherGameProcess =  null;
        ThreadPool.QueueUserWorkItem(_ => VideoView.MediaPlayer?.Play(_currentVideoMedia));
        ResetRandomGameSelectionTimer();
        ResetSleepScreenTimer();
        _isScreenSleeping = false;
        _isCancellingScreenSleeping = false;
    }
}