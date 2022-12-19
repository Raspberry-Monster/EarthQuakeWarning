﻿using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EarthquakeWaring.App.Extensions;
using EarthquakeWaring.App.Infrastructure.Models.ApiModels;
using EarthquakeWaring.App.Infrastructure.Models.EarthQuakeModels;
using EarthquakeWaring.App.Infrastructure.Models.SettingModels;
using EarthquakeWaring.App.Infrastructure.Models.ViewModels;
using EarthquakeWaring.App.Infrastructure.ServiceAbstraction;
using EarthquakeWaring.App.Services;
using EarthquakeWaring.App.Windows;
using Microsoft.Extensions.DependencyInjection;
using Button = WPFUI.Controls.Button;

namespace EarthquakeWaring.App.Pages;

public partial class EarthQuakesListPage : Page
{
    private readonly ISetting<CurrentPosition> _currentPosition;
    private readonly ISetting<AlertLimit> _alertLimit;
    private readonly IEarthQuakeApi _quakeApi;
    private readonly IEarthQuakeCalculator _calculator;
    private readonly IServiceProvider _service;
    private readonly EarthQuakesListPageViewModel _viewModel;


    public EarthQuakesListPage()
    {
        _service = DI.Services;
        _calculator =_service.GetRequiredService<IEarthQuakeCalculator>();
        _quakeApi = _service.GetRequiredService<IEarthQuakeApi>();
        _alertLimit = _service.GetRequiredService<ISetting<AlertLimit>>();
        _currentPosition = _service.GetRequiredService<ISetting<CurrentPosition> >();
        
        _viewModel = new EarthQuakesListPageViewModel();
        InitializeComponent();
    }

    protected override async void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        _viewModel.ProgressRingVisibility = Visibility.Visible;
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var results = await _quakeApi.GetEarthQuakeList(0, cancellationToken);
        results.ForEach(t => _viewModel.InformationList.Add(ConvertToInformation(t)));
        ListView.ItemsSource = _viewModel.InformationList;
        _viewModel.ProgressRingVisibility = Visibility.Collapsed;
        LoadingRing.Visibility = Visibility.Collapsed;
    }

    private EarthQuakeTrackingInformation ConvertToInformation(HuaniaEarthQuake latestInfo)
    {
        var trackingInformation = new EarthQuakeTrackingInformation
        {
            // Update the tracking information
            Position = latestInfo.Epicenter,
            StartTime = latestInfo.StartAt,
            UpdateTime = latestInfo.UpdateAt,
            Depth = latestInfo.Depth,
            Latitude = latestInfo.Latitude,
            Longitude = latestInfo.Longitude,
            EventId = latestInfo.EventId,
            Sations = latestInfo.Sations,
            Magnitude = latestInfo.Magnitude
        };

        if (_currentPosition.Setting == null) return trackingInformation;
        trackingInformation.Distance = _calculator.GetDistance(_currentPosition.Setting.Latitude,
            _currentPosition.Setting.Longitude, trackingInformation.Latitude, trackingInformation.Longitude);
        trackingInformation.TheoryCountDown =
            (int)_calculator.GetCountDownSeconds(trackingInformation.Depth,
                trackingInformation.Distance);
        trackingInformation.Intensity =
            _calculator.GetIntensity(trackingInformation.Magnitude, trackingInformation.Distance);
        trackingInformation.Stage = EarthQuakeTracker.GetEarthQuakeAlertStage(trackingInformation);
        trackingInformation.DontUseShouldAlert =
            EarthQuakeTracker.ShouldPopupAlert(trackingInformation, _alertLimit.Setting ?? new AlertLimit());
        return trackingInformation;
    }


    private async void SimulateEarthQuake(object sender, RoutedEventArgs e)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var info = (((sender as Button)?.Tag as EarthQuakeTrackingInformation)!);
        var tracker = _service.GetService<IEarthQuakeTracker>();
        tracker!.SimulateTimeSpan = DateTime.Now - info.StartTime;
        tracker!.SimulateUpdates = await _quakeApi.GetEarthQuakeInfo(info.EventId, cancellationTokenSource.Token);
        tracker?.StartTrack(new HuaniaEarthQuake()
        {
            EventId = info.EventId
        }, cancellationTokenSource);
    }

    private void ShowEarthQuakeDetail(object sender, MouseButtonEventArgs e)
    {
        if (((Grid)sender).Tag is EarthQuakeTrackingInformation info)
        {
            // _service.GetService<MainWindow>()?.RootFrame.Navigate(new EarthQuakeDetail(info, _currentPosition.Setting));
            if (!App.MainWindowOpened)
            {
                _service.GetService<MainWindow>()?.Show();
            }

            if (App.MainWindowOpened)
            {
                App.RootFrame?.Navigate(new EarthQuakeDetail(info, _currentPosition.Setting));
            }
        }
    }
}