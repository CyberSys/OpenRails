﻿namespace ORTS
{
    partial class OptionsForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(OptionsForm));
            this.label1 = new System.Windows.Forms.Label();
            this.numericWorldObjectDensity = new System.Windows.Forms.NumericUpDown();
            this.buttonOK = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.numericSoundDetailLevel = new System.Windows.Forms.NumericUpDown();
            this.comboBoxWindowSize = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.checkBoxTrainLights = new System.Windows.Forms.CheckBox();
            this.numericBrakePipeChargingRatePSIpS = new System.Windows.Forms.NumericUpDown();
            this.label4 = new System.Windows.Forms.Label();
            this.checkBoxPrecipitation = new System.Windows.Forms.CheckBox();
            this.checkBoxGraduatedRelease = new System.Windows.Forms.CheckBox();
            this.checkBoxWire = new System.Windows.Forms.CheckBox();
            this.checkBoxShadows = new System.Windows.Forms.CheckBox();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.checkBoxWindowGlass = new System.Windows.Forms.CheckBox();
            this.checkBoxBINSound = new System.Windows.Forms.CheckBox();
            this.checkBoxAlerter = new System.Windows.Forms.CheckBox();
            this.checkBoxSuppressConfirmations = new System.Windows.Forms.CheckBox();
            this.checkDispatcher = new System.Windows.Forms.CheckBox();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.label6 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.buttonExport = new System.Windows.Forms.Button();
            this.buttonDefaultKeys = new System.Windows.Forms.Button();
            this.buttonCheckKeys = new System.Windows.Forms.Button();
            this.panelKeys = new System.Windows.Forms.Panel();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.buttonDebug = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.numericWorldObjectDensity)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericSoundDetailLevel)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericBrakePipeChargingRatePSIpS)).BeginInit();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(73, 18);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(107, 13);
            this.label1.TabIndex = 1;
            this.label1.Text = "World Object Density";
            // 
            // numericWorldObjectDensity
            // 
            this.numericWorldObjectDensity.Location = new System.Drawing.Point(15, 16);
            this.numericWorldObjectDensity.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.numericWorldObjectDensity.Name = "numericWorldObjectDensity";
            this.numericWorldObjectDensity.Size = new System.Drawing.Size(52, 20);
            this.numericWorldObjectDensity.TabIndex = 0;
            // 
            // buttonOK
            // 
            this.buttonOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonOK.Location = new System.Drawing.Point(289, 447);
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.Size = new System.Drawing.Size(75, 23);
            this.buttonOK.TabIndex = 15;
            this.buttonOK.Text = "OK";
            this.buttonOK.UseVisualStyleBackColor = true;
            this.buttonOK.Click += new System.EventHandler(this.buttonOK_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(73, 44);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(97, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Sound Detail Level";
            // 
            // numericSoundDetailLevel
            // 
            this.numericSoundDetailLevel.Location = new System.Drawing.Point(15, 42);
            this.numericSoundDetailLevel.Maximum = new decimal(new int[] {
            5,
            0,
            0,
            0});
            this.numericSoundDetailLevel.Name = "numericSoundDetailLevel";
            this.numericSoundDetailLevel.Size = new System.Drawing.Size(52, 20);
            this.numericSoundDetailLevel.TabIndex = 2;
            // 
            // comboBoxWindowSize
            // 
            this.comboBoxWindowSize.FormattingEnabled = true;
            this.comboBoxWindowSize.Location = new System.Drawing.Point(15, 68);
            this.comboBoxWindowSize.Name = "comboBoxWindowSize";
            this.comboBoxWindowSize.Size = new System.Drawing.Size(121, 21);
            this.comboBoxWindowSize.TabIndex = 4;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(142, 71);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(67, 13);
            this.label3.TabIndex = 5;
            this.label3.Text = "Window size";
            // 
            // checkBoxTrainLights
            // 
            this.checkBoxTrainLights.AutoSize = true;
            this.checkBoxTrainLights.Location = new System.Drawing.Point(15, 117);
            this.checkBoxTrainLights.Name = "checkBoxTrainLights";
            this.checkBoxTrainLights.Size = new System.Drawing.Size(81, 17);
            this.checkBoxTrainLights.TabIndex = 6;
            this.checkBoxTrainLights.Text = "Train Lights";
            this.checkBoxTrainLights.UseVisualStyleBackColor = true;
            // 
            // numericBrakePipeChargingRatePSIpS
            // 
            this.numericBrakePipeChargingRatePSIpS.Location = new System.Drawing.Point(15, 210);
            this.numericBrakePipeChargingRatePSIpS.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.numericBrakePipeChargingRatePSIpS.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericBrakePipeChargingRatePSIpS.Name = "numericBrakePipeChargingRatePSIpS";
            this.numericBrakePipeChargingRatePSIpS.Size = new System.Drawing.Size(52, 20);
            this.numericBrakePipeChargingRatePSIpS.TabIndex = 10;
            this.numericBrakePipeChargingRatePSIpS.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(73, 212);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(166, 13);
            this.label4.TabIndex = 11;
            this.label4.Text = "Brake Pipe Charging Rate (PSI/s)";
            // 
            // checkBoxPrecipitation
            // 
            this.checkBoxPrecipitation.AutoSize = true;
            this.checkBoxPrecipitation.Location = new System.Drawing.Point(15, 141);
            this.checkBoxPrecipitation.Name = "checkBoxPrecipitation";
            this.checkBoxPrecipitation.Size = new System.Drawing.Size(84, 17);
            this.checkBoxPrecipitation.TabIndex = 7;
            this.checkBoxPrecipitation.Text = "Precipitation";
            this.checkBoxPrecipitation.UseVisualStyleBackColor = true;
            // 
            // checkBoxGraduatedRelease
            // 
            this.checkBoxGraduatedRelease.AutoSize = true;
            this.checkBoxGraduatedRelease.Location = new System.Drawing.Point(15, 187);
            this.checkBoxGraduatedRelease.Name = "checkBoxGraduatedRelease";
            this.checkBoxGraduatedRelease.Size = new System.Drawing.Size(169, 17);
            this.checkBoxGraduatedRelease.TabIndex = 9;
            this.checkBoxGraduatedRelease.Text = "Graduated Release Air Brakes";
            this.checkBoxGraduatedRelease.UseVisualStyleBackColor = true;
            // 
            // checkBoxWire
            // 
            this.checkBoxWire.AutoSize = true;
            this.checkBoxWire.Location = new System.Drawing.Point(15, 164);
            this.checkBoxWire.Name = "checkBoxWire";
            this.checkBoxWire.Size = new System.Drawing.Size(98, 17);
            this.checkBoxWire.TabIndex = 8;
            this.checkBoxWire.Text = "Overhead Wire";
            this.checkBoxWire.UseVisualStyleBackColor = true;
            // 
            // checkBoxShadows
            // 
            this.checkBoxShadows.AutoSize = true;
            this.checkBoxShadows.Location = new System.Drawing.Point(15, 236);
            this.checkBoxShadows.Name = "checkBoxShadows";
            this.checkBoxShadows.Size = new System.Drawing.Size(114, 17);
            this.checkBoxShadows.TabIndex = 12;
            this.checkBoxShadows.Text = "Dynamic Shadows";
            this.checkBoxShadows.UseVisualStyleBackColor = true;
            // 
            // buttonCancel
            // 
            this.buttonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonCancel.Location = new System.Drawing.Point(370, 447);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(75, 23);
            this.buttonCancel.TabIndex = 16;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            // 
            // checkBoxWindowGlass
            // 
            this.checkBoxWindowGlass.AutoSize = true;
            this.checkBoxWindowGlass.Location = new System.Drawing.Point(15, 259);
            this.checkBoxWindowGlass.Name = "checkBoxWindowGlass";
            this.checkBoxWindowGlass.Size = new System.Drawing.Size(171, 17);
            this.checkBoxWindowGlass.TabIndex = 13;
            this.checkBoxWindowGlass.Text = "Use glass on in-game windows";
            this.checkBoxWindowGlass.UseVisualStyleBackColor = true;
            // 
            // checkBoxBINSound
            // 
            this.checkBoxBINSound.AutoSize = true;
            this.checkBoxBINSound.Location = new System.Drawing.Point(15, 282);
            this.checkBoxBINSound.Name = "checkBoxBINSound";
            this.checkBoxBINSound.Size = new System.Drawing.Size(185, 17);
            this.checkBoxBINSound.TabIndex = 14;
            this.checkBoxBINSound.Text = "Use MSTS BIN compatible sound";
            this.checkBoxBINSound.UseVisualStyleBackColor = true;
            // 
            // checkBoxAlerter
            // 
            this.checkBoxAlerter.AutoSize = true;
            this.checkBoxAlerter.Location = new System.Drawing.Point(15, 95);
            this.checkBoxAlerter.Name = "checkBoxAlerter";
            this.checkBoxAlerter.Size = new System.Drawing.Size(56, 17);
            this.checkBoxAlerter.TabIndex = 17;
            this.checkBoxAlerter.Text = "Alerter";
            this.checkBoxAlerter.UseVisualStyleBackColor = true;
            // 
            // checkBoxSuppressConfirmations
            // 
            this.checkBoxSuppressConfirmations.AutoSize = true;
            this.checkBoxSuppressConfirmations.Location = new System.Drawing.Point(15, 305);
            this.checkBoxSuppressConfirmations.Name = "checkBoxSuppressConfirmations";
            this.checkBoxSuppressConfirmations.Size = new System.Drawing.Size(170, 17);
            this.checkBoxSuppressConfirmations.TabIndex = 18;
            this.checkBoxSuppressConfirmations.Text = "Suppress control confirmations";
            this.checkBoxSuppressConfirmations.UseVisualStyleBackColor = true;
            // 
            // checkDispatcher
            // 
            this.checkDispatcher.AutoSize = true;
            this.checkDispatcher.Location = new System.Drawing.Point(15, 328);
            this.checkDispatcher.Name = "checkDispatcher";
            this.checkDispatcher.Size = new System.Drawing.Size(145, 17);
            this.checkDispatcher.TabIndex = 19;
            this.checkDispatcher.Text = "View Disptacher Window";
            this.checkDispatcher.UseVisualStyleBackColor = true;
            // 
            // tabControl1
            // 
            this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Location = new System.Drawing.Point(-1, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(475, 435);
            this.tabControl1.TabIndex = 20;
            // 
            // tabPage1
            // 
            this.tabPage1.BackColor = System.Drawing.SystemColors.Control;
            this.tabPage1.Controls.Add(this.label1);
            this.tabPage1.Controls.Add(this.checkDispatcher);
            this.tabPage1.Controls.Add(this.label2);
            this.tabPage1.Controls.Add(this.checkBoxSuppressConfirmations);
            this.tabPage1.Controls.Add(this.numericWorldObjectDensity);
            this.tabPage1.Controls.Add(this.checkBoxAlerter);
            this.tabPage1.Controls.Add(this.numericSoundDetailLevel);
            this.tabPage1.Controls.Add(this.checkBoxBINSound);
            this.tabPage1.Controls.Add(this.comboBoxWindowSize);
            this.tabPage1.Controls.Add(this.checkBoxWindowGlass);
            this.tabPage1.Controls.Add(this.label3);
            this.tabPage1.Controls.Add(this.checkBoxTrainLights);
            this.tabPage1.Controls.Add(this.checkBoxShadows);
            this.tabPage1.Controls.Add(this.numericBrakePipeChargingRatePSIpS);
            this.tabPage1.Controls.Add(this.checkBoxGraduatedRelease);
            this.tabPage1.Controls.Add(this.label4);
            this.tabPage1.Controls.Add(this.checkBoxWire);
            this.tabPage1.Controls.Add(this.checkBoxPrecipitation);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(467, 409);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "General";
            // 
            // tabPage2
            // 
            this.tabPage2.AutoScroll = true;
            this.tabPage2.BackColor = System.Drawing.SystemColors.Control;
            this.tabPage2.Controls.Add(this.label6);
            this.tabPage2.Controls.Add(this.label5);
            this.tabPage2.Controls.Add(this.buttonExport);
            this.tabPage2.Controls.Add(this.buttonDefaultKeys);
            this.tabPage2.Controls.Add(this.buttonDebug);
            this.tabPage2.Controls.Add(this.buttonCheckKeys);
            this.tabPage2.Controls.Add(this.panelKeys);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(467, 409);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Keyboard";
            // 
            // label6
            // 
            this.label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.Location = new System.Drawing.Point(223, 12);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(100, 23);
            this.label6.TabIndex = 2;
            this.label6.Text = "Key";
            this.label6.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // label5
            // 
            this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(45, 12);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(100, 23);
            this.label5.TabIndex = 2;
            this.label5.Text = "Function";
            this.label5.TextAlign = System.Drawing.ContentAlignment.BottomRight;
            // 
            // buttonExport
            // 
            this.buttonExport.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonExport.Location = new System.Drawing.Point(381, 366);
            this.buttonExport.Name = "buttonExport";
            this.buttonExport.Size = new System.Drawing.Size(75, 23);
            this.buttonExport.TabIndex = 1;
            this.buttonExport.Text = "Export";
            this.toolTip1.SetToolTip(this.buttonExport, "Generate a listing of your keyboard assignments.  \r\nThe output is placed on your " +
                    "desktop.");
            this.buttonExport.UseVisualStyleBackColor = true;
            this.buttonExport.Click += new System.EventHandler(this.buttonExport_Click);
            // 
            // buttonDefaultKeys
            // 
            this.buttonDefaultKeys.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonDefaultKeys.Location = new System.Drawing.Point(90, 366);
            this.buttonDefaultKeys.Name = "buttonDefaultKeys";
            this.buttonDefaultKeys.Size = new System.Drawing.Size(75, 23);
            this.buttonDefaultKeys.TabIndex = 1;
            this.buttonDefaultKeys.Text = "Defaults";
            this.toolTip1.SetToolTip(this.buttonDefaultKeys, "Load the factory default key assignments.");
            this.buttonDefaultKeys.UseVisualStyleBackColor = true;
            this.buttonDefaultKeys.Click += new System.EventHandler(this.buttonDefaultKeys_Click);
            // 
            // buttonCheckKeys
            // 
            this.buttonCheckKeys.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonCheckKeys.Location = new System.Drawing.Point(9, 366);
            this.buttonCheckKeys.Name = "buttonCheckKeys";
            this.buttonCheckKeys.Size = new System.Drawing.Size(75, 23);
            this.buttonCheckKeys.TabIndex = 1;
            this.buttonCheckKeys.Text = "Check";
            this.toolTip1.SetToolTip(this.buttonCheckKeys, "Check for incorrect key assignments.");
            this.buttonCheckKeys.UseVisualStyleBackColor = true;
            this.buttonCheckKeys.Click += new System.EventHandler(this.buttonCheckKeys_Click);
            // 
            // panelKeys
            // 
            this.panelKeys.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.panelKeys.AutoScroll = true;
            this.panelKeys.BackColor = System.Drawing.SystemColors.ControlLight;
            this.panelKeys.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.panelKeys.Location = new System.Drawing.Point(8, 38);
            this.panelKeys.Name = "panelKeys";
            this.panelKeys.Size = new System.Drawing.Size(448, 310);
            this.panelKeys.TabIndex = 0;
            // 
            // buttonDebug
            // 
            this.buttonDebug.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonDebug.Location = new System.Drawing.Point(226, 366);
            this.buttonDebug.Name = "buttonDebug";
            this.buttonDebug.Size = new System.Drawing.Size(75, 23);
            this.buttonDebug.TabIndex = 1;
            this.buttonDebug.Text = "Debug";
            this.toolTip1.SetToolTip(this.buttonDebug, "Run a more complete check.");
            this.buttonDebug.UseVisualStyleBackColor = true;
            this.buttonDebug.Click += new System.EventHandler(this.buttonDebug_Click);
            // 
            // OptionsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(476, 482);
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonOK);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "OptionsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Options";
            ((System.ComponentModel.ISupportInitialize)(this.numericWorldObjectDensity)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericSoundDetailLevel)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericBrakePipeChargingRatePSIpS)).EndInit();
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            this.tabPage2.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NumericUpDown numericWorldObjectDensity;
        private System.Windows.Forms.Button buttonOK;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.NumericUpDown numericSoundDetailLevel;
        private System.Windows.Forms.ComboBox comboBoxWindowSize;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.CheckBox checkBoxTrainLights;
        private System.Windows.Forms.NumericUpDown numericBrakePipeChargingRatePSIpS;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.CheckBox checkBoxPrecipitation;
        private System.Windows.Forms.CheckBox checkBoxGraduatedRelease;
        private System.Windows.Forms.CheckBox checkBoxWire;
        private System.Windows.Forms.CheckBox checkBoxShadows;
        private System.Windows.Forms.Button buttonCancel;
		private System.Windows.Forms.CheckBox checkBoxWindowGlass;
        private System.Windows.Forms.CheckBox checkBoxBINSound;
        private System.Windows.Forms.CheckBox checkBoxAlerter;
        private System.Windows.Forms.CheckBox checkBoxSuppressConfirmations;
		private System.Windows.Forms.CheckBox checkDispatcher;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.Button buttonDefaultKeys;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.Button buttonCheckKeys;
        private System.Windows.Forms.Panel panelKeys;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Button buttonExport;
        private System.Windows.Forms.Button buttonDebug;
    }
}