﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SD_FXUI.Utils
{
    /// <summary>
    /// Логика взаимодействия для Settings.xaml
    /// </summary>
    public partial class Settings : Window
    {
        static public bool UseNotif = false;
        static public bool UseNotifImgs = false;
        static public bool UseInternalVAE = false;

        bool IsLoadedWnd = false;
        public Settings()
        {
            InitializeComponent();
            chNotification.IsChecked = UseNotif;
            chNotification_1.IsChecked = UseNotifImgs;
            tsVAE.IsChecked = UseInternalVAE;

            IsLoadedWnd = true;
        }

        private void chNotification_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoadedWnd)
            {
                UseNotif = chNotification.IsChecked.Value;
            }
        }

        private void chNotification_1_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoadedWnd)
            {
                UseNotifImgs = chNotification_1.IsChecked.Value;
            }
        }

        private void chVAE_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoadedWnd)
            {
                UseInternalVAE = tsVAE.IsChecked.Value;
            }

            Helper.Form.InvokeUpdateModelsList();
        }
    }
}
