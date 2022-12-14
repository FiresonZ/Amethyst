﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Amethyst.Utils;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.Windows.ApplicationModel.Resources;
using Microsoft.Windows.AppNotifications;

namespace Amethyst.Classes;

public static class Shared
{
    public static class Events
    {
        public static ManualResetEvent
            ReloadMainWindowEvent,
            ReloadGeneralPageEvent,
            ReloadSettingsPageEvent,
            ReloadDevicesPageEvent,
            ReloadInfoPageEvent;

        public static readonly Semaphore
            SemSignalStartMain = new(0, 1);

        public static void RequestInterfaceReload(bool all = true)
        {
            if (!General.GeneralTabSetupFinished)
                return; // Not ready yet oof

            if (all) ReloadMainWindowEvent?.Set();

            ReloadGeneralPageEvent?.Set();
            ReloadSettingsPageEvent?.Set();
            ReloadDevicesPageEvent?.Set();
            ReloadInfoPageEvent?.Set();

            // Reload other stuff, like statuses
            TrackingDevices.UpdateTrackingDevicesInterface();
            TrackingDevices.HandleDeviceRefresh(false);
        }
    }

    public static class Main
    {
        // Vector of std.pair holding the Navigation Tag and the relative Navigation Page.
        public static List<(string Tag, Type Page)> Pages;

        // Main Window
        public static Mutex ApplicationMultiInstanceMutex;

        public static Window Window;
        public static AppWindow AppWindow;
        public static IntPtr AppWindowId;
        public static DispatcherQueue DispatcherQueue;

        public static AppNotificationManager NotificationManager;
        public static ResourceManager ResourceManager;
        public static ResourceContext ResourceContext;

        public static Grid
            InterfaceBlockerGrid, NavigationBlockerGrid;

        public static NavigationView MainNavigationView;
        public static Frame MainContentFrame;
        public static TextBlock AppTitleLabel;

        public static SolidColorBrush
            AttentionBrush, NeutralBrush;

        // Navigate the main view (w/ animations)
        public static void NavigateToPage(string navItemTag,
            NavigationTransitionInfo transitionInfo)
        {
            Logger.Info($"Navigation requested! Page tag: {navItemTag}");

            var page = Pages.FirstOrDefault(p => p.Tag.Equals(navItemTag)).Page;
            var preNavPageType = MainContentFrame.CurrentSourcePageType;

            // Navigate only if the selected page isn't currently loaded
            if (page is null || preNavPageType == page) return;
            AppSounds.PlayAppSound(AppSounds.AppSoundType.Invoke);

            // Switch bring back the current item to the base state
            if (!string.IsNullOrEmpty(preNavPageType?.Name))
                switch (preNavPageType.FullName)
                {
                    case "Amethyst.Pages.General":
                        NavigationItems.NavViewGeneralButtonIcon.Translation = new Vector3(0, -8, 0);
                        NavigationItems.NavViewGeneralButtonLabel.Opacity = 1.0;

                        NavigationItems.NavViewGeneralButtonIcon.Foreground = NeutralBrush;
                        NavigationItems.NavViewGeneralButtonIcon.Glyph = "\uE80F";
                        break;
                    case "Amethyst.Pages.Settings":
                        NavigationItems.NavViewSettingsButtonIcon.Translation = new Vector3(0, -8, 0);
                        NavigationItems.NavViewSettingsButtonLabel.Opacity = 1.0;

                        NavigationItems.NavViewSettingsButtonIcon.Foreground = NeutralBrush;
                        NavigationItems.NavViewSettingsButtonIcon.Glyph = "\uE713";
                        break;
                    case "Amethyst.Pages.Devices":
                        NavigationItems.NavViewDevicesButtonIcon.Translation = new Vector3(0, -8, 0);
                        NavigationItems.NavViewDevicesButtonLabel.Opacity = 1.0;

                        NavigationItems.NavViewDevicesButtonIcon.Foreground = NeutralBrush;

                        NavigationItems.NavViewDevicesButtonIcon.Glyph = "\uF158";
                        NavigationItems.NavViewDevicesButtonIcon.FontSize = 20;
                        break;
                    case "Amethyst.Pages.Info":
                        NavigationItems.NavViewInfoButtonIcon.Translation = new Vector3(0, -8, 0);
                        NavigationItems.NavViewInfoButtonLabel.Opacity = 1.0;

                        NavigationItems.NavViewInfoButtonIcon.Foreground = NeutralBrush;
                        NavigationItems.NavViewInfoButtonIcon.Glyph = "\uE946";
                        break;
                }

            // Switch the next navview item to the active state
            if (!string.IsNullOrEmpty(page.Name))
                switch (page.FullName)
                {
                    case "Amethyst.Pages.General":
                        NavigationItems.NavViewGeneralButtonIcon.Glyph = "\uEA8A";
                        NavigationItems.NavViewGeneralButtonIcon.Foreground = AttentionBrush;

                        NavigationItems.NavViewGeneralButtonLabel.Opacity = 0.0;
                        NavigationItems.NavViewGeneralButtonIcon.Translation = Vector3.Zero;
                        break;
                    case "Amethyst.Pages.Settings":
                        NavigationItems.NavViewSettingsButtonIcon.Glyph = "\uF8B0";
                        NavigationItems.NavViewSettingsButtonIcon.Foreground = AttentionBrush;

                        NavigationItems.NavViewSettingsButtonLabel.Opacity = 0.0;
                        NavigationItems.NavViewSettingsButtonIcon.Translation = Vector3.Zero;
                        break;
                    case "Amethyst.Pages.Devices":
                        NavigationItems.NavViewDevicesButtonLabel.Opacity = 0.0;
                        NavigationItems.NavViewDevicesButtonIcon.Translation = Vector3.Zero;

                        NavigationItems.NavViewDevicesButtonIcon.Foreground = AttentionBrush;

                        NavigationItems.NavViewDevicesButtonIcon.Glyph = "\uEBD2";
                        NavigationItems.NavViewDevicesButtonIcon.FontSize = 23;
                        break;
                    case "Amethyst.Pages.Info":
                        NavigationItems.NavViewInfoButtonIcon.Glyph = "\uF167";
                        NavigationItems.NavViewInfoButtonIcon.Foreground = AttentionBrush;

                        NavigationItems.NavViewInfoButtonLabel.Opacity = 0.0;
                        NavigationItems.NavViewInfoButtonIcon.Translation = Vector3.Zero;
                        break;
                }

            Interfacing.CurrentPageTag = navItemTag; // Cache the current page tag
            Interfacing.CurrentPageClass = page.FullName; // Cache the current page tag

            MainContentFrame.Navigate(page, null, transitionInfo);
        }

        public static class NavigationItems
        {
            public static FontIcon
                NavViewGeneralButtonIcon,
                NavViewSettingsButtonIcon,
                NavViewDevicesButtonIcon,
                NavViewInfoButtonIcon;

            public static TextBlock
                NavViewGeneralButtonLabel,
                NavViewSettingsButtonLabel,
                NavViewDevicesButtonLabel,
                NavViewInfoButtonLabel;
        }
    }

    public static class General
    {
        // General Page
        public static bool GeneralTabSetupFinished = false;
        public static ToggleButton ToggleTrackersButton;
        public static ToggleSplitButton SkeletonToggleButton;
        public static CheckBox ForceRenderCheckBox;
        public static MenuFlyoutItem OffsetsButton;

        public static Button
            CalibrationButton, ServiceSettingsButton;

        public static TextBlock
            DeviceNameLabel,
            DeviceStatusLabel,
            ErrorWhatText,
            TrackingDeviceErrorLabel,
            ServerStatusLabel,
            ServerErrorLabel,
            ServerErrorWhatText,
            ForceRenderText;

        public static Grid
            ErrorButtonsGrid,
            ErrorWhatGrid,
            ServerErrorWhatGrid;

        public static ToggleButton
            ToggleFreezeButton;

        public static TextBlock
            AdditionalDeviceErrorsHyperlink;

        public static Task AdditionalDeviceErrorsHyperlinkTappedEvent =
            new(() =>
            {
                // Placeholder event // async
            });
    }

    public static class Settings
    {
        // Settings Page
        public static bool SettingsTabSetupFinished = false;

        public static Button RestartButton;
        public static Grid FlipDropDownGrid;
        public static ScrollViewer PageMainScrollViewer;
        public static ToggleSwitch FlipToggle;
        public static Expander FlipDropDown;

        public static CheckBox
            ExternalFlipCheckBox,
            CheckOverlapsCheckBox;

        public static TextBlock
            SetErrorFlyoutText,
            ExternalFlipStatusLabel;

        public static StackPanel
            ExternalFlipStatusStackPanel,
            FlipDropDownContainer;
    }

    public static class Devices
    {
        // Devices Page
        public static bool DevicesTabSetupFinished = false, // On-load setup
            DevicesJointsValid = true, // Optionally no signal
            PluginsPageLoadedOnce = false; // Manager flyout

        public static TextBlock
            DeviceNameLabel,
            DeviceStatusLabel,
            ErrorWhatText,
            TrackingDeviceErrorLabel;

        public static Grid
            DeviceErrorGrid,
            TrackingDeviceChangePanel,
            DevicesMainContentGridOuter,
            DevicesMainContentGridInner;

        public static Button
            SetAsOverrideButton,
            SetAsBaseButton,
            DeselectDeviceButton;

        public static TreeView DevicesTreeView;
        public static StackPanel SelectedDeviceSettingsRootLayoutPanel;

        public static async Task ReloadSelectedDevice(bool manual, bool reconnect = false)
        {
            // Update the status here
            TrackingDevices.HandleDeviceRefresh(reconnect);

            // Update GeneralPage status
            TrackingDevices.UpdateTrackingDevicesInterface();

            // Refresh the device MVVM
            TrackingDevices.TrackingDevicesList[AppData.Settings.SelectedTrackingDeviceGuid].OnPropertyChanged();

            if (TrackingDevices.IsBase(AppData.Settings.SelectedTrackingDeviceGuid))
            {
                Logger.Info($"Selected a base ({AppData.Settings.SelectedTrackingDeviceGuid})");
                SetAsOverrideButton.IsEnabled = false;
                SetAsBaseButton.IsEnabled = false;

                DeselectDeviceButton.Visibility = Visibility.Collapsed;
            }
            else if (TrackingDevices.IsOverride(AppData.Settings.SelectedTrackingDeviceGuid))
            {
                Logger.Info($"Selected an override ({AppData.Settings.SelectedTrackingDeviceGuid})");
                SetAsOverrideButton.IsEnabled = false;
                SetAsBaseButton.IsEnabled = true;

                DeselectDeviceButton.Visibility = Visibility.Visible;
                if (DeviceErrorGrid.Visibility != Visibility.Visible)
                    Interfacing.CurrentAppState = "overrides";
            }
            else
            {
                Logger.Info($"Selected a [none] ({AppData.Settings.SelectedTrackingDeviceGuid})");
                SetAsOverrideButton.IsEnabled = true;
                SetAsBaseButton.IsEnabled = true;

                DeselectDeviceButton.Visibility = Visibility.Collapsed;
            }

            // Device stuff reload animation
            if (!manual)
            {
                // Remove the only one child of our outer main content grid
                // (What a bestiality it is to do that!!1)
                DevicesMainContentGridOuter.Children.Clear();
                DevicesMainContentGridInner.Transitions.Add(
                    new EntranceThemeTransition { IsStaggeringEnabled = false });

                // Sleep peacefully pretending that noting happened
                await Task.Delay(10);

                // Re-add the child for it to play our funky transition
                // (Though it's not the same as before...)
                DevicesMainContentGridOuter.Children.Add(DevicesMainContentGridInner);
            }

            // Remove the transition
            await Task.Delay(100);
            DevicesMainContentGridInner.Transitions.Clear();
        }
    }

    public static class TeachingTips
    {
        public static class MainPage
        {
            public static TeachingTip
                InitializerTeachingTip, ReloadTeachingTip;
        }

        public static class GeneralPage
        {
            public static TeachingTip
                ToggleTrackersTeachingTip, StatusTeachingTip;
        }

        public static class SettingsPage
        {
            public static TeachingTip
                ManageTrackersTeachingTip, AutoStartTeachingTip;
        }

        public static class DevicesPage
        {
            public static TeachingTip
                DevicesListTeachingTip, DeviceControlsTeachingTip;
        }

        public static class InfoPage
        {
            public static TeachingTip
                HelpTeachingTip;
        }
    }
}