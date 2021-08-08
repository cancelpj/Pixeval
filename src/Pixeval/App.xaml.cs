﻿using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Mako;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Pixeval.Events;
using Pixeval.Interop;
using Pixeval.Util;
using WinRT;

namespace Pixeval
{
    public partial class App
    {
        public static MainWindow Window = null!;

        public static Frame AppWindowRootFrame => Window.PixevalAppRootFrame;

        public static MakoClient MakoClient { get; set; } = null!; // The null-state of MakoClient is transient

        public static AppSetting AppSetting { get; set; } = null!;

        public static string? Uid => MakoClient.Session.Id;

        public App()
        {
            InitializeComponent();
            RegisterUnhandledExceptionHandler();
            AppSetting = AppContext.LoadConfiguration() ?? AppSetting.CreateDefault();
            RequestedTheme = AppSetting.Theme switch
            {
                ApplicationTheme.Dark  => Microsoft.UI.Xaml.ApplicationTheme.Dark,
                ApplicationTheme.Light => Microsoft.UI.Xaml.ApplicationTheme.Light,
                _                      => RequestedTheme
            };
        }

        public static IntPtr GetMainWindowHandle()
        {
            return Window.As<IWindowNative>().WindowHandle;
        }

        public static void RootFrameNavigate(Type type, object parameter, NavigationTransitionInfo infoOverride)
        {
            AppWindowRootFrame.Navigate(type, parameter, infoOverride);
        }

        public static void RootFrameNavigate(Type type, object parameter)
        {
            AppWindowRootFrame.Navigate(type, parameter);
        }

        public static void RootFrameNavigate(Type type)
        {
            AppWindowRootFrame.Navigate(type);
        }

        private void RegisterUnhandledExceptionHandler()
        {
            UnhandledException += (_, args) =>
            {
                args.Handled = true;
                UncaughtExceptionHandler(args.Exception);
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                args.SetObserved();
                UncaughtExceptionHandler(args.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += (_, _) =>
            {
                Debugger.Break();
            };

            static async void UncaughtExceptionHandler(Exception e)
            {
                await MessageDialogBuilder.CreateAcknowledgement(Window, MiscResources.ExceptionEncountered, e.ToString()).ShowAsync();
                await ExitWithPushedNotification();
            }
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            await AppContext.WriteLogoIcoIfNotExist();
            Window = new MainWindow();
            Window.SetWindowSize(800, 600);
            Window.Activate();
        }

        /// <summary>
        /// Exit the notification after pushing an <see cref="ApplicationExitingEvent"/>
        /// to the <see cref="EventChannel"/>
        /// </summary>
        /// <returns></returns>
        public static async Task ExitWithPushedNotification()
        {
            await EventChannel.Default.PublishAsync(new ApplicationExitingEvent());
            await Task.Delay(200); // well...just wait a second to let those subscribers handle the event
        }
    }
}