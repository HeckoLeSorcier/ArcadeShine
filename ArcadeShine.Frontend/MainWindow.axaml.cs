using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
using Avalonia.Styling;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using SDL2;

namespace ArcadeShine.Frontend;

public partial class MainWindow : Window
{
    private readonly LibVLC _libVlc = new LibVLC();

    private int _currentSystemIndex;

    private int _currentGameIndex;

    private List<ArcadeShineGame> _currentCategoryGames = null!;

    private readonly Dictionary<InputActionEnum, Key> _inputActionMap = new Dictionary<InputActionEnum, Key>();
    
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
    
    private bool _isInGame;
    
#if WINDOWS
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);
#endif
    
    private IntPtr _gameControllerHandle;
    private System.Timers.Timer gamepadPollTimer;
    
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
        ConfirmExitFrontendButton.Click += ConfirmExitFrontendButton_OnClick;
        CancelExitFrontendButton.Click += CancelExitFrontendButton_OnClick;

        if (SDL.SDL_Init(SDL.SDL_INIT_GAMECONTROLLER) != 0)
        {
            throw new Exception("SDL2.SDL.SDL_Init(SDL2.SDL.SDL_INIT_GAMECONTROLLER) failed");
        }

        for (int i = 0 ; i < SDL.SDL_NumJoysticks() ; i++)
        {
            if (SDL.SDL_IsGameController(i) == SDL.SDL_bool.SDL_TRUE)
            {
                _gameControllerHandle = SDL.SDL_GameControllerOpen(i);
            }
        }
        gamepadPollTimer = new System.Timers.Timer(16); // 60Hz polling
        gamepadPollTimer.Elapsed += (s, e) =>
        {
            ProcessLastGamepadButtonsPressed();
        };
        gamepadPollTimer.Start();

        if (App.ArcadeShineFrontendSettings.AllowInactivityMode)
            _autoSelectRandomGameTimer = DispatcherTimer.RunOnce(SelectRandomGame,
                TimeSpan.FromSeconds(App.ArcadeShineFrontendSettings.SecondsBeforeRandomGameSelectionInactivityMode));
    }
    
    private void ProcessLastGamepadButtonsPressed()
    {
        SDL.SDL_PollEvent(out var sdlEvent);
        switch (sdlEvent.type)
        {
            case SDL.SDL_EventType.SDL_CONTROLLERBUTTONDOWN:
                Dispatcher.UIThread.Invoke(() =>
                {
                    if (sdlEvent.cbutton.button == (byte)SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_UP)
                    {
                        OnNavigateUpInputAction();
                    }
                    if (sdlEvent.cbutton.button == (byte)SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_DOWN)
                    {
                        OnNavigateDownInputAction();
                    }
                    if (sdlEvent.cbutton.button == (byte)SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_LEFT)
                    {
                        OnNavigateLeftInputAction();
                    }
                    if (sdlEvent.cbutton.button == (byte)SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_DPAD_RIGHT)
                    {
                        OnNavigateRightInputAction();
                    }
                    if (sdlEvent.cbutton.button == (byte)SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_A)
                    {
                        OnSelectInputAction();
                    }
                    if (sdlEvent.cbutton.button == (byte)SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_B)
                    {
                        OnBackInputAction();
                    }
                    if (sdlEvent.cbutton.button == (byte)SDL.SDL_GameControllerButton.SDL_CONTROLLER_BUTTON_BACK)
                    {
                        OnExitInputAction();
                    }
                });
                break;
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
                VideoView.IsVisible = true;
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
            VideoView.IsVisible = true;
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
            if (App.ArcadeShineFrontendSettings.AllowInactivityMode)
            {
                _autoSelectRandomGameTimer.Dispose();
                _autoSelectRandomGameTimer = DispatcherTimer.RunOnce(SelectRandomGame,
                    TimeSpan.FromSeconds(App.ArcadeShineFrontendSettings.SecondsBeforeRandomGameSelectionInactivityMode));
                if (_isRandomizingGameSelection)
                {
                    _cancelRandomGameSelection = true;
                }
            }
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
            Dispatcher.UIThread.Invoke(() =>
            {
                VideoView.IsVisible = false;
                FadePanel.Opacity = 1.0;
            });
            await _globalFadeOutAnimation.RunAsync(FadePanel);
            Dispatcher.UIThread.Invoke(() =>
            {
                LoadingPanel.IsVisible = true;
            });
        
            await Task.Delay(TimeSpan.FromSeconds(0.5));
        
            if (App.ArcadeShineSystemList[_currentSystemIndex].SystemExecutable.Contains("steam.exe"))
            {
                
            }
            
            // Create a new process
            Process process = new Process();
            // Set the process start info
            process.StartInfo.FileName = App.ArcadeShineSystemList[_currentSystemIndex].SystemExecutable; // specify the command to run
            string gameFile;
            string steamGameProcessName = string.Empty;
            bool isSteamGame = false;
            if (App.ArcadeShineSystemList[_currentSystemIndex].SystemExecutable.Contains("steam.exe"))
            {
                isSteamGame = true;
                var steamGameRomFiles = _currentCategoryGames[_currentGameIndex].GameRomFile.Split('|');
                gameFile = steamGameRomFiles[0];
                steamGameProcessName = steamGameRomFiles[1].Replace(".exe", "");
            }
            else
            {
                gameFile = _currentCategoryGames[_currentGameIndex].GameRomFile;
            }
            var arguments = App.ArcadeShineSystemList[_currentSystemIndex].SystemExecutableArguments
                .Replace("{GAME_FILE}", gameFile);
            process.StartInfo.Arguments = arguments; // specify the arguments
            // Set additional process start info as necessary
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            // Start the process
            process.Start();
            // Wait for the process to exit
            await process.WaitForExitAsync();

            if (isSteamGame)
            {
                Process? steamGameProcess;
                do
                {
                    steamGameProcess = Process.GetProcessesByName(steamGameProcessName).FirstOrDefault();
                } while (steamGameProcess == null);

                await steamGameProcess.WaitForExitAsync();
            }
            
            Dispatcher.UIThread.Invoke(() =>
            {
                LoadingPanel.IsVisible = false;
            });
            await _globalFadeInAnimation.RunAsync(FadePanel);
            Dispatcher.UIThread.Invoke(() =>
            {
                FadePanel.Opacity = 0.0;
                VideoView.IsVisible = true;
            });
            _isInGame = false;
            ThreadPool.QueueUserWorkItem(_ => VideoView.MediaPlayer?.Play(_currentVideoMedia));
            _autoSelectRandomGameTimer = DispatcherTimer.RunOnce(SelectRandomGame,
                TimeSpan.FromSeconds(App.ArcadeShineFrontendSettings.SecondsBeforeRandomGameSelectionInactivityMode));
        }
        catch (Exception)
        {
            // ignored
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
            GameTitleIndexCountTextBlock.Text = $"{Lang.Resources.TitleNumber} {_currentGameIndex + 1} / {_currentCategoryGames.Count}  -  {Lang.Resources.TitleCount} {App.ArcadeShineGameList.Count}";
            CurrentGameLogoImage.Source = currentGameLogo;
            NextGameLogoImage.Source = nextGameLogo;
            PreviousGameLogoImage.Source = previousGameLogo;
            GameBackground.Source = gameBackground;
            GameDescriptionTextBlock.Text = _currentCategoryGames[_currentGameIndex].GameDescription;
            if(_currentCategoryGames[_currentGameIndex].GameGenres.Count > 0)
                GameGenresTextBlock.Text = _currentCategoryGames[_currentGameIndex].GameGenres.Aggregate((a, b) => $"{a}, {b}");
            GameDeveloperTextBlock.Text = _currentCategoryGames[_currentGameIndex].GameDeveloper;
            GameYearTextBlock.Text = _currentCategoryGames[_currentGameIndex].GameReleaseYear;
            GameNameTextBlock.Text = _currentCategoryGames[_currentGameIndex].GameName;
            if(!Design.IsDesignMode)
                Task.Delay(TimeSpan.FromSeconds(1)).ContinueWith(_ => Play());
        });
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        GenerateAnimations();
#if WINDOWS
        if(App.ArcadeShineFrontendSettings.AllowWindowsToManageScreenSleep)
            SetThreadExecutionState(ES_CONTINUOUS);
        else
            SetThreadExecutionState(ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);
#endif
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

        if (VideoView.MediaPlayer is { IsPlaying: true })
        {
            VideoView.MediaPlayer.Stop();
        }
        VideoView.MediaPlayer = new MediaPlayer(_libVlc);
        VideoView.MediaPlayer.CropGeometry = _currentCategoryGames[_currentGameIndex].GameVideoAspectRatio;
        VideoView.MediaPlayer.EnableHardwareDecoding = true;
        VideoView.MediaPlayer.EndReached += MediaPlayerOnEndReached;
        PlayCurrentGameVideo();
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
                ? Thickness.Parse("84 40")
                : Thickness.Parse("208 40"));
        ThreadPool.QueueUserWorkItem(_ =>
        {
            if(!_isInGame)
                VideoView.MediaPlayer?.Play(_currentVideoMedia);
        });
    }

    private void CancelVideoPlay()
    {
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
        });
        ExitConfirmationPanel.IsVisible = true;
    }
    
    private void CloseExitConfirmationPopup()
    {
        ExitConfirmationPanel.IsVisible = false;
        Dispatcher.UIThread.Invoke(() =>
        {
            VideoView.IsVisible = true;
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
}