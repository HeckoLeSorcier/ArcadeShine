using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArcadeShine.Common;
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

    public Dictionary<InputActionEnum, Key> InputActionMap = new Dictionary<InputActionEnum, Key>();
    
    private Animation _animationLeftDisappear;
    private Animation _animationLeftAppear;
    private Animation _animationRightDisappear;
    private Animation _animationRightAppear;
    
    public MainWindow()
    {
        InitializeComponent();
        
        MapInputs();
        
        Bitmap gameBackground = new Bitmap(App.ArcadeShineGameList[0].GameBackgroundPicture);
        GameBackground.Source = gameBackground;
        
        Bitmap gameLogo = new Bitmap(App.ArcadeShineGameList[0].GameLogo);
        GameLogoImage.Source = gameLogo;
        
        ChangeCategory();
        
        GameDescriptionTextBlock.Text = App.ArcadeShineGameList[0].GameDescription;
        if(App.ArcadeShineGameList[0].GameGenres != null && App.ArcadeShineGameList[0].GameGenres.Count > 0)
            GameGenresTextBlock.Text = App.ArcadeShineGameList[0].GameGenres.Aggregate((a, b) => $"{a}, {b}");
        GameDeveloperTextBlock.Text = App.ArcadeShineGameList[0].GameDeveloper;
        GameYearTextBlock.Text = App.ArcadeShineGameList[0].GameReleaseYear;
        GameNameTextBlock.Text = App.ArcadeShineGameList[0].GameName;
        
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

    private Task _nextCategoryAnimationTask;
    private Task _previousCategoryAnimationTask;
    
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
                        break;
                    case InputActionEnum.NavigateDownAction:
                        break;
                    case InputActionEnum.NavigateLeftAction:
                        if (_previousCategoryAnimationTask is null or { IsCompleted: true })
                        {
                            _previousCategoryAnimationTask = LaunchPreviousCategoryAnimations();
                            _previousCategoryAnimationTask.ContinueWith(_ => App.ArcadeShineSystemList.Count==0?_currentSystemIndex=0:_currentSystemIndex==0?App.ArcadeShineSystemList.Count-1:_currentSystemIndex--)
                                .ContinueWith(_ => ChangeCategory());
                        }
                        break;
                    case InputActionEnum.NavigateRightAction:
                        if (_nextCategoryAnimationTask is null or { IsCompleted: true })
                        {
                            _nextCategoryAnimationTask = LaunchNextCategoryAnimations();
                            _nextCategoryAnimationTask.ContinueWith(_ => App.ArcadeShineSystemList.Count==0?_currentSystemIndex=0:_currentSystemIndex==App.ArcadeShineSystemList.Count-1?0:_currentSystemIndex++)
                                .ContinueWith(_ => ChangeCategory());
                        }
                        break;
                    case InputActionEnum.SelectAction:
                        break;
                    case InputActionEnum.BackAction:
                        break;
                    case InputActionEnum.ExitAction:
                        break;
                }
            }
        }
    }

    private async Task LaunchNextCategoryAnimations()
    {
        var animTask1 = _animationLeftDisappear.RunAsync(GameSystemLogoImage);
        var animTask2 = _animationLeftAppear.RunAsync(NextGameSystemLogoImage);
        await Task.WhenAll(animTask1, animTask2);
    }
    
    private async Task LaunchPreviousCategoryAnimations()
    {
        var animTask1 = _animationRightDisappear.RunAsync(GameSystemLogoImage);
        var animTask2 = _animationRightAppear.RunAsync(PreviousGameSystemLogoImage);
        await Task.WhenAll(animTask1, animTask2);
    }

    private void ChangeCategory()
    {
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

        Bitmap nextGameSystemLogo = new Bitmap(App.ArcadeShineSystemList
            .FirstOrDefault(s => s.SystemIdentifier == App.ArcadeShineGameList[nextIndex].GameSystem).SystemLogo);
        Bitmap previousGameSystemLogo = new Bitmap(App.ArcadeShineSystemList
            .FirstOrDefault(s => s.SystemIdentifier == App.ArcadeShineGameList[previousIndex].GameSystem).SystemLogo);
        Bitmap currentGameSystemLogo = new Bitmap(App.ArcadeShineSystemList
            .FirstOrDefault(s => s.SystemIdentifier == App.ArcadeShineGameList[_currentSystemIndex].GameSystem).SystemLogo);
        Dispatcher.UIThread.Invoke(() =>
        {
            GameSystemLogoImage.Source = currentGameSystemLogo;
            NextGameSystemLogoImage.Source = nextGameSystemLogo;
            PreviousGameSystemLogoImage.Source = previousGameSystemLogo;
        });
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        GenerateAnimations();
        if(!Design.IsDesignMode)
            Task.Delay(TimeSpan.FromSeconds(3)).ContinueWith(_ => Play());
    }

    private void GenerateAnimations()
    {
        //Move and disappear on the left side
        _animationLeftDisappear = new Animation
        {
            Duration = TimeSpan.FromSeconds(0.3),
            Easing = new QuarticEaseInOut()
        };
        _animationLeftDisappear.Children.Add(new KeyFrame()
        {
            Cue = new Cue(0.0),
            Setters = { new Setter(OpacityProperty, 1.0), new Setter(TranslateTransform.XProperty, 0.0) }
        });
        _animationLeftDisappear.Children.Add(new KeyFrame()
        {
            Cue = new Cue(1.0),
            Setters = { new Setter(OpacityProperty, 0.0), new Setter(TranslateTransform.XProperty, -480.0) }
        });
        
        //Move and appear from the left side
        _animationLeftAppear = new Animation
        {
            Duration = TimeSpan.FromSeconds(0.3),
            Easing = new QuarticEaseInOut()
        };
        _animationLeftAppear.Children.Add(new KeyFrame()
        {
            Cue = new Cue(0.0),
            Setters = { new Setter(OpacityProperty, 0.0), new Setter(TranslateTransform.XProperty, 0.0) }
        });
        _animationLeftAppear.Children.Add(new KeyFrame()
        {
            Cue = new Cue(1.0),
            Setters = { new Setter(OpacityProperty, 1.0), new Setter(TranslateTransform.XProperty, -480.0) }
        });
        
        //Move and disappear on the right side
        _animationRightDisappear = new Animation
        {
            Duration = TimeSpan.FromSeconds(0.3),
            Easing = new QuarticEaseInOut()
        };
        _animationRightDisappear.Children.Add(new KeyFrame()
        {
            Cue = new Cue(0.0),
            Setters = { new Setter(OpacityProperty, 1.0), new Setter(TranslateTransform.XProperty, 0.0) }
        });
        _animationRightDisappear.Children.Add(new KeyFrame()
        {
            Cue = new Cue(1.0),
            Setters = { new Setter(OpacityProperty, 0.0), new Setter(TranslateTransform.XProperty, 480.0) }
        });
        
        //Move and appear from the right side
        _animationRightAppear = new Animation
        {
            Duration = TimeSpan.FromSeconds(0.3),
            Easing = new QuarticEaseInOut()
        };
        _animationRightAppear.Children.Add(new KeyFrame()
        {
            Cue = new Cue(0.0),
            Setters = { new Setter(OpacityProperty, 0.0), new Setter(TranslateTransform.XProperty, 0.0) }
        });
        _animationRightAppear.Children.Add(new KeyFrame()
        {
            Cue = new Cue(1.0),
            Setters = { new Setter(OpacityProperty, 1.0), new Setter(TranslateTransform.XProperty, 480.0) }
        });
    }

    public void Play()
    {
        if (Design.IsDesignMode)
        {
            return;
        }
        
        VideoView.MediaPlayer = new MediaPlayer(_libVlc);
        VideoView.MediaPlayer.CropGeometry = "16:9";
        VideoView.MediaPlayer.EnableHardwareDecoding = true;
        VideoView.MediaPlayer.EndReached += MediaPlayerOnEndReached;
        PlayCurrentGameVideo();
    }

    private void MediaPlayerOnEndReached(object? sender, EventArgs e)
    {
        VideoView.MediaPlayer.Position = 0f;
        PlayCurrentGameVideo();
    }

    private void PlayCurrentGameVideo()
    {
        using var media = new Media(_libVlc, App.ArcadeShineGameList[0].GameVideo);
        VideoView.MediaPlayer?.Play(media);
    }

    public void Stop()
    {            
        VideoView.MediaPlayer?.Stop();
    }
   
    public void Dispose()
    {
        VideoView.MediaPlayer?.Dispose();
        _libVlc?.Dispose();
    }
}