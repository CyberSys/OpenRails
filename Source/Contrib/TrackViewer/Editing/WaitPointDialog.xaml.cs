﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Text.RegularExpressions;

namespace ORTS.TrackViewer.Editing
{
    /// <summary>
    /// Interaction logic for WaitPointDialog.xaml
    /// This is a small dialog to edit the details for waitpoint
    /// </summary>
    public partial class WaitPointDialog : Window
    {
        /// <summary>
        /// Create the dialog to edit the metadata of the waitpoint dialog.
        /// </summary>
        /// <param name="mouseX">Current X-location of the mouse to determine popup location</param>
        /// <param name="mouseY">Current Y-location of the mouse to determine popu location</param>
        /// <param name="currentWaitTimeS">Current value of the wait time (only valid if wait until is zero)</param>
        /// <param name="currentWaitUntil">Current value of the wait-until time</param>
        public WaitPointDialog(int mouseX, int mouseY, int currentWaitTimeS, int currentWaitUntil)
        {
            InitializeComponent();
            this.Left = mouseX;
            this.Top = mouseY;
            waitTimeHours.Focus();
            if (currentWaitUntil > 0)
            {
                int totalMinutes = currentWaitUntil / 60;
                int hours = totalMinutes / 60;
                int minutes = totalMinutes - 60 * hours;
                waitTimeHours.Text = hours.ToString();
                waitTimeMinutes.Text = minutes.ToString();
                selectUntil.IsChecked = true;
            }
            else
            {
                int totalMinutes = currentWaitTimeS / 60;
                int hours = totalMinutes / 60;
                int minutes = totalMinutes - 60 * hours;
                waitTimeHours.Text = hours.ToString();
                waitTimeMinutes.Text = minutes.ToString();
                selectWait.IsChecked = true;
            }
        }

        private void buttonOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        ///<summary>Return whether the 'until' check-box has been selected or not</summary>
        public bool UntilSelected()
        {
            return (bool)selectUntil.IsChecked;
        }

        ///<summary>Return the selected wait time in seconds</summary>
        public int GetWaitTime()
        {
            return 60 * (Convert.ToInt32(waitTimeHours.Text, System.Globalization.CultureInfo.InvariantCulture) * 60
                       + Convert.ToInt32(waitTimeMinutes.Text, System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Make sure we only allow digits to be typed
        /// </summary>
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        /// <summary>
        /// Handle an enter/return press on the textbox. If return is pressed, just do the same as the search button.
        /// </summary>
        private void OnKeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                buttonOK_Click(sender, e);
            }
        }
 
    }
}
