using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArcadeShine.Common;
using ArcadeShine.Common.DataModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using LibVLCSharp.Shared;

namespace ArcadeShine;

public partial class MainWindow : Window
{
    private readonly LibVLC _libVlc = new LibVLC();
    
    private int _currentSystemIndex = 0;
    
    private int _currentGameIndex = 0;

    private List<ArcadeShineGame> _currentCategoryGames;

    public Dictionary<InputActionEnum, Key> InputActionMap = new Dictionary<InputActionEnum, Key>();
    
    private Animation _animationCurrentSystemDisappearToLeftSide;
    private Animation _animationNextSystemLogoAppearFromRightSide;
    private Animation _animationCurrentSystemDisappearToRightSide;
    private Animation _animationPreviousSystemAppearFromLeftSide;
    
    private Task _nextCategoryAnimationTask;
    private Task _previousCategoryAnimationTask;
    
    private Animation _animationCurrentGameDisappearToUpSide;
    private Animation _animationNextGameLogoAppearFromBottomSide;
    private Animation _animationCurrentGameDisappearToBottomSide;
    private Animation _animationPreviousGameAppearFromUpSide;
    
    private Task _nextGameAnimationTask;
    private Task _previousGameAnimationTask;
    private Media _currentVideoMedia;
    
    private Animation _globalFadeOutAnimation;
    private Animation _globalFadeInAnimation;
    
    public MainWindow()
    {
        InitializeComponent();
        
        MapInputs();
        UpdateCategory();
        UpdateGame();
        
        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
    }

    private void MapInputs()
    {
        InputActionMap.Add(InputActionEnum.NavigateUpAction, (Key)Enum.Parse(typeof(Key), App.ArcadeShineFrontendSettings.UpKey));
        InputActionMap.Add(InputActionEnum.NavigateDownAction, (Key)Enum.Parse(typeof(Key), App.ArcadeShineFrontendSettings.DownKey));
        InputActionMap.Add(InputActionEnum.NavigateLeftAction, (Key)Enum.Parse(typeof(Key), App.ArcadeShineFrontendSettings.LeftKey));
        InputActionMap.Add(InputActionEnum.NavigateRightAction, (Key)Enum.Parse(typeof(Key), App.ArcadeShineFrontendSettings.RightKey));
        InputActionMap.Add(InputActionEnum.SelectAction, (Key)Enum.Parse(typeof(Key), App.ArcadeShineFrontendSettings.EnterKey));
        InputActionMap.Add(InputActionEnum.BackAction, (Key)Enum.Parse(typeof(Key), App.ArcadeShineFrontendSettings.BackKey));
        InputActionMap.Add(InputActionEnum.ExitAction, (Key)Enum.Parse(typeof(Key), App.ArcadeShineFrontendSettings.ExitKey));
    }
    
    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (InputActionMap.ContainsValue(e.Key))
        {
            InputActionEnum? action = null;
            foreach (var kvp in InputActionMap)
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
                        break;
                    case InputActionEnum.NavigateDownAction:
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
                        break;
                    case InputActionEnum.NavigateLeftAction:
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
                            _previousCategoryAnimationTask.ContinueWith(_ => UpdateGame());
                        }
                        break;
                    case InputActionEnum.NavigateRightAction:
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
                            _nextCategoryAnimationTask.ContinueWith(_ => UpdateGame());
                        }
                        break;
                    case InputActionEnum.SelectAction:
                        LaunchCurrentSelectedGame();
                        break;
                    case InputActionEnum.BackAction:
                        break;
                    case InputActionEnum.ExitAction:
                        break;
                }
            }
        }
    }

    private async void LaunchCurrentSelectedGame()
    {
        PauseVideo();
        Dispatcher.UIThread.Invoke(() =>
        {
            VideoView.IsVisible = false;
            FadePanel.Opacity = 1.0;
        });
        await _globalFadeOutAnimation.RunAsync(FadePanel);
        
        // Create a new process
        Process process = new Process();
        // Set the process start info
        process.StartInfo.FileName = App.ArcadeShineSystemList[_currentSystemIndex].SystemExecutable; // specify the command to run
        var arguments = App.ArcadeShineSystemList[_currentSystemIndex].SystemExecutableArguments.Replace("{GAME_FILE}", _currentCategoryGames[_currentGameIndex].GameRomFile);
        process.StartInfo.Arguments = arguments; // specify the arguments
        // Set additional process start info as necessary
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        // Start the process
        process.Start();
        // Wait for the process to exit
        process.WaitForExit();
        
        await _globalFadeInAnimation.RunAsync(FadePanel);
        Dispatcher.UIThread.Invoke(() =>
        {
            FadePanel.Opacity = 0.0;
            VideoView.IsVisible = true;
        });
        ThreadPool.QueueUserWorkItem(_ => VideoView.MediaPlayer.Play(_currentVideoMedia));
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

        Bitmap previousGameLogo = new Bitmap(_currentCategoryGames[previousIndex].GameLogo);
        Bitmap currentGameLogo = new Bitmap(_currentCategoryGames[_currentGameIndex].GameLogo);
        Bitmap nextGameLogo = new Bitmap(_currentCategoryGames[nextIndex].GameLogo);
        Bitmap gameBackground = new Bitmap(_currentCategoryGames[_currentGameIndex].GameBackgroundPicture);
        Dispatcher.UIThread.Invoke(() =>
        {
            CurrentGameLogoImage.Source = currentGameLogo;
            NextGameLogoImage.Source = nextGameLogo;
            PreviousGameLogoImage.Source = previousGameLogo;
            GameBackground.Source = gameBackground;
            GameDescriptionTextBlock.Text = _currentCategoryGames[_currentGameIndex].GameDescription;
            if(_currentCategoryGames[_currentGameIndex].GameGenres != null && _currentCategoryGames[_currentGameIndex].GameGenres.Count > 0)
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

    public void Play()
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
        ThreadPool.QueueUserWorkItem(_ => VideoView.MediaPlayer.Play(_currentVideoMedia));
    }

    private void PlayCurrentGameVideo()
    {
        _currentVideoMedia = new Media(_libVlc, _currentCategoryGames[_currentGameIndex].GameVideo);
        Dispatcher.UIThread.Invoke(() =>
            VideoView.Margin = _currentCategoryGames[_currentGameIndex].GameVideoAspectRatio == "16:9"
                ? Thickness.Parse("84 40")
                : Thickness.Parse("208 40"));
        ThreadPool.QueueUserWorkItem(_ => VideoView.MediaPlayer?.Play(_currentVideoMedia));
    }

    private void CancelVideoPlay()
    {
        VideoView.MediaPlayer?.Stop();
    }
    
    private void PauseVideo()
    {
        VideoView.MediaPlayer?.Pause();
    }
   
    public void Dispose()
    {
        CancelVideoPlay();
        VideoView.MediaPlayer?.Dispose();
        _libVlc?.Dispose();
    }
}