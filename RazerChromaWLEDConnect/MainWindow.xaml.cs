// Licensed to the Chroma Control Contributors under one or more agreements.
// The Chroma Control Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ChromaBroadcast;
using System;
using System.Collections.Generic;
using System.Management;
using System.Threading;
using System.Windows;
//using System.Windows.Controls;
using System.Windows.Media;
using HidApiAdapter;
using RazerChromaWLEDConnect.Base;
using RazerChromaWLEDConnect.WLED;
using System.Diagnostics;
using Wpf.Ui.Controls;

namespace RazerChromaWLEDConnect
{
    public partial class MainWindow : FluentWindow
    {
        protected TextBlock _labelRazerState;
        protected AppSettings appSettings;

        protected HidDevice keyboardLenovo;

        private ManagementEventWatcher managementEventWatcher;
        private bool _isExplicitClose = false; 
        private readonly Dictionary<string, string> powerValues = new Dictionary<string, string>
        {
            {"4", "Entering Suspend"},
            {"7", "Resume from Suspend"},
            {"10", "Power Status Change"},
            {"11", "OEM Event"},
            {"18", "Resume Automatic"}
        };

        public MainWindow()
        {
            InitializeComponent();

            // Get Settings
            this.appSettings = AppSettings.Load();
            _labelRazerState = RazerState;
            ContextMenuItemRunAtBoot.IsChecked = this.appSettings.RunAtBoot;
            BroadcastEnabled.IsChecked = this.appSettings.Sync;
            Init();
        } 
        
        public void Init()
        {
            try 
            {
                if (string.IsNullOrEmpty(this.appSettings.RazerAppId)) return;
                RzChromaBroadcastAPI.Init(Guid.Parse(this.appSettings.RazerAppId));
                RzChromaBroadcastAPI.RegisterEventNotification(OnChromaBroadcastEvent);
                InitPowerEvents();
            } 
            catch (Exception) {}
        }

        public void addControls()
        {
            // Stub
        }

        private void UpdateLabelRazerState(string Text)
        {
            Dispatcher.Invoke(() =>
            {
                _labelRazerState.Text = Text;
            });
        }

        RzResult OnChromaBroadcastEvent(RzChromaBroadcastType type, RzChromaBroadcastStatus? status, RzChromaBroadcastEffect? effect)
        {
            // The Razer Chroma Event listener
            // This method gets triggered whenever Razer broadcasts something. This could be a color change or the disconnection of Chroma
            if (type == RzChromaBroadcastType.BroadcastEffect)
            {
                Dispatcher.Invoke(() =>
                {
                    if (ContextMenuItemSync.IsChecked != BroadcastEnabled.IsChecked) ContextMenuItemSync.IsChecked = (bool)BroadcastEnabled.IsChecked;

                    if (BroadcastEnabled.IsChecked == true)
                    {

                        int[] color1 = { effect.Value.ChromaLink2.R, effect.Value.ChromaLink2.G, effect.Value.ChromaLink2.B };
                        int[] color2 = { effect.Value.ChromaLink3.R, effect.Value.ChromaLink3.G, effect.Value.ChromaLink3.B };
                        int[] color3 = { effect.Value.ChromaLink4.R, effect.Value.ChromaLink4.G, effect.Value.ChromaLink4.B };
                        int[] color4 = { effect.Value.ChromaLink5.R, effect.Value.ChromaLink5.G, effect.Value.ChromaLink5.B };

                        foreach (RGBBase ledInstance in this.appSettings.Instances)
                        {
                            // TODO Has to be a better way to do this
                            if (ledInstance is LenovoKeyboard)
                            {
                                LenovoKeyboard l = (LenovoKeyboard)ledInstance;
                                l.sendColors(color1, color2, color3, color4);
                            } else
                            {
                                WLEDModule l = (WLEDModule)ledInstance;
                                l.sendColors(color1, color2, color3, color4);
                            }
                        }

                        if (this.WindowState != WindowState.Minimized)
                        {
                            CL2.Fill = new SolidColorBrush(Color.FromRgb(effect.Value.ChromaLink2.R, effect.Value.ChromaLink2.G, effect.Value.ChromaLink2.B));
                            CL3.Fill = new SolidColorBrush(Color.FromRgb(effect.Value.ChromaLink3.R, effect.Value.ChromaLink3.G, effect.Value.ChromaLink3.B));
                            CL4.Fill = new SolidColorBrush(Color.FromRgb(effect.Value.ChromaLink4.R, effect.Value.ChromaLink4.G, effect.Value.ChromaLink4.B));
                            CL5.Fill = new SolidColorBrush(Color.FromRgb(effect.Value.ChromaLink5.R, effect.Value.ChromaLink5.G, effect.Value.ChromaLink5.B));
                        }
                    }
                });
            }
            else if (type == RzChromaBroadcastType.BroadcastStatus)
            {
                if (status == RzChromaBroadcastStatus.Live)
                {
                    UpdateLabelRazerState("Connected");
                }
                else if (status == RzChromaBroadcastStatus.NotLive)
                {
                    UpdateLabelRazerState("Disconnected");
                }
            }

            return RzResult.Success;
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isExplicitClose) return;

            e.Cancel = true;

            var uiMessageBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = "Quit",
                Content = "Are you sure you want to quit the application?",
                PrimaryButtonText = "Yes",
                CloseButtonText = "No"
            };

            var messageBoxResult = await uiMessageBox.ShowDialogAsync();

            if (messageBoxResult == Wpf.Ui.Controls.MessageBoxResult.Primary)
            {
                this.Quit();
            }
        }

        public void InitPowerEvents()
        {
            var q = new WqlEventQuery();
            var scope = new ManagementScope("root\\CIMV2");

            q.EventClassName = "Win32_PowerManagementEvent";
            managementEventWatcher = new ManagementEventWatcher(scope, q);
            managementEventWatcher.EventArrived += PowerEventArrive;
            managementEventWatcher.Start();
        }

        private void PowerEventArrive(object sender, EventArrivedEventArgs e)
        {
            foreach (PropertyData pd in e.NewEvent.Properties)
            {
                if (pd == null || pd.Value == null) continue;
                var name = powerValues.ContainsKey(pd.Value.ToString()) ? powerValues[pd.Value.ToString()] : pd.Value.ToString();
                if (name == "Entering Suspend")
                {
                    for (int i = 0; i < this.appSettings.Instances.Count; i++)
                    {
                        this.appSettings.Instances[i].turnOff();
                    }
                }
                else if (name == "Resume from Suspend")
                {
                    for (int i = 0; i < this.appSettings.Instances.Count; i++)
                    {
                        this.appSettings.Instances[i].turnOn();
                    }
                }
            }
        }

        public void Quit()
        {
            _isExplicitClose = true;
            RzChromaBroadcastAPI.UnRegisterEventNotification();
            RzChromaBroadcastAPI.UnInit();
            if (managementEventWatcher != null) managementEventWatcher.Stop();

            for (int i = 0; i < this.appSettings.Instances.Count; i++)
            {
                this.appSettings.Instances[i].unload();
            }

            Thread.Sleep(1000);
            Application.Current.Shutdown();
        }

        private void quitApplication(object sender, RoutedEventArgs e)
        {
            this.Quit();
        }

        public void ShowWindow()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Focus();
        }

        private void showApplication(object sender, RoutedEventArgs e)
        {
            ShowWindow();
        }

        private void ShowSettings()
        {
            SettingsWindow sw = new SettingsWindow(this, this.appSettings);
            sw.Show();
        }

        private void settingsShowWindow(object sender, RoutedEventArgs e)
        {
            ShowSettings();
        }

        private void Sync(bool syncWithRazer)
        {
            this.appSettings.Sync = syncWithRazer;
            this.appSettings.Save();

            if (syncWithRazer)
            {
                for (int i = 0; i < this.appSettings.Instances.Count; i++)
                {
                    this.appSettings.Instances[i].load();
                }
            } 
            else
            {
                for (int i = 0; i < this.appSettings.Instances.Count; i++)
                {
                    this.appSettings.Instances[i].unload();
                }
            }
            
        }

        private void syncWithRazer(object sender, RoutedEventArgs e)
        {
            this.Sync(true);
        }
        private void syncWithRazerUnCheck(object sender, RoutedEventArgs e)
        {
            this.Sync(false);
        }

        private void contextMenuSyncWithRazerCheck(object sender, RoutedEventArgs e)
        {
            this.Sync(true);
            BroadcastEnabled.IsChecked = true;
        }

        private void contextMenuSyncWithRazerUnCheck(object sender, RoutedEventArgs e)
        {
            this.Sync(false);
            BroadcastEnabled.IsChecked = false;
        }

        private void StateChange(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                Hide();
            }
        }

        private void contextMenuRunAtBootCheck(object sender, RoutedEventArgs e)
        {
            this.appSettings.RunAtBoot = true;
            this.appSettings.Save();
        }

        private void contextMenuRunAtBootUnCheck(object sender, RoutedEventArgs e)
        {
            this.appSettings.RunAtBoot = false;
            this.appSettings.Save();
        }

        private void Show(object sender, RoutedEventArgs e)
        {
            this.ShowWindow();
        }

        private void about(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://github.com/fu-raz/Razer-Chroma-WLED-Connect-App") { UseShellExecute = true });
        }
    }
}
