namespace CruiseControlApp
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            pnlTopControls = new Panel();
            btnAddProfile = new Button();
            btnReset = new Button();
            btnHelp = new Button();
            btnCheckUpdate = new Button();
            btnLoadPreset = new Button();
            cbAutoUpdate = new CheckBox();
            pbDownload = new ProgressBar();
            lblDownloadProgress = new Label();
            flpProfiles = new FlowLayoutPanel();
            pnlTopControls.SuspendLayout();
            SuspendLayout();
            // 
            // pnlTopControls
            // 
            pnlTopControls.Controls.Add(btnAddProfile);
            pnlTopControls.Controls.Add(btnReset);
            pnlTopControls.Controls.Add(btnHelp);
            pnlTopControls.Controls.Add(btnCheckUpdate);
            pnlTopControls.Controls.Add(btnLoadPreset);
            pnlTopControls.Controls.Add(cbAutoUpdate);
            pnlTopControls.Controls.Add(pbDownload);
            pnlTopControls.Controls.Add(lblDownloadProgress);
            pnlTopControls.Dock = DockStyle.Top;
            pnlTopControls.Location = new Point(0, 0);
            pnlTopControls.Margin = new Padding(4, 3, 4, 3);
            pnlTopControls.Name = "pnlTopControls";
            pnlTopControls.Size = new Size(810, 46);
            pnlTopControls.TabIndex = 0;
            // 
            // btnAddProfile
            // 
            btnAddProfile.Location = new Point(10, 10);
            btnAddProfile.Margin = new Padding(4, 3, 4, 3);
            btnAddProfile.Name = "btnAddProfile";
            btnAddProfile.Size = new Size(125, 27);
            btnAddProfile.TabIndex = 0;
            btnAddProfile.Text = "Добавить профиль";
            btnAddProfile.UseVisualStyleBackColor = true;
            btnAddProfile.Click += BtnAddProfile_Click;
            // 
            // btnReset
            // 
            btnReset.Location = new Point(140, 10);
            btnReset.Margin = new Padding(4, 3, 4, 3);
            btnReset.Name = "btnReset";
            btnReset.Size = new Size(95, 27);
            btnReset.TabIndex = 1;
            btnReset.Text = "Сбросить всё";
            btnReset.UseVisualStyleBackColor = true;
            btnReset.Click += BtnReset_Click;
            // 
            // btnHelp
            // 
            btnHelp.Location = new Point(240, 10);
            btnHelp.Margin = new Padding(4, 3, 4, 3);
            btnHelp.Name = "btnHelp";
            btnHelp.Size = new Size(75, 27);
            btnHelp.TabIndex = 2;
            btnHelp.Text = "Справка";
            btnHelp.UseVisualStyleBackColor = true;
            btnHelp.Click += BtnHelp_Click;
            // 
            // btnCheckUpdate
            // 
            btnCheckUpdate.Location = new Point(320, 10);
            btnCheckUpdate.Margin = new Padding(4, 3, 4, 3);
            btnCheckUpdate.Name = "btnCheckUpdate";
            btnCheckUpdate.Size = new Size(140, 27);
            btnCheckUpdate.TabIndex = 3;
            btnCheckUpdate.Text = "Проверить обновления";
            btnCheckUpdate.UseVisualStyleBackColor = true;
            btnCheckUpdate.Click += BtnCheckUpdate_Click;
            // 
            // btnLoadPreset
            // 
            btnLoadPreset.Location = new Point(465, 10);
            btnLoadPreset.Margin = new Padding(4, 3, 4, 3);
            btnLoadPreset.Name = "btnLoadPreset";
            btnLoadPreset.Size = new Size(105, 27);
            btnLoadPreset.TabIndex = 4;
            btnLoadPreset.Text = "Скачать пресет";
            btnLoadPreset.UseVisualStyleBackColor = true;
            btnLoadPreset.Click += BtnLoadPreset_Click;
            // 
            // cbAutoUpdate
            // 
            cbAutoUpdate.AutoSize = true;
            cbAutoUpdate.Checked = true;
            cbAutoUpdate.CheckState = CheckState.Checked;
            cbAutoUpdate.Location = new Point(580, 14);
            cbAutoUpdate.Margin = new Padding(4, 3, 4, 3);
            cbAutoUpdate.Name = "cbAutoUpdate";
            cbAutoUpdate.Size = new Size(111, 19);
            cbAutoUpdate.TabIndex = 5;
            cbAutoUpdate.Text = "Автообновление";
            cbAutoUpdate.UseVisualStyleBackColor = true;
            cbAutoUpdate.CheckedChanged += CbAutoUpdate_CheckedChanged;
            // 
            // pbDownload
            // 
            pbDownload.Location = new Point(695, 14);
            pbDownload.Name = "pbDownload";
            pbDownload.Size = new Size(70, 18);
            pbDownload.TabIndex = 6;
            pbDownload.Visible = false;
            // 
            // lblDownloadProgress
            // 
            lblDownloadProgress.AutoSize = true;
            lblDownloadProgress.Location = new Point(770, 15);
            lblDownloadProgress.Name = "lblDownloadProgress";
            lblDownloadProgress.Size = new Size(23, 15);
            lblDownloadProgress.TabIndex = 7;
            lblDownloadProgress.Text = "0%";
            lblDownloadProgress.Visible = false;
            // 
            // flpProfiles
            // 
            flpProfiles.Dock = DockStyle.Fill;
            flpProfiles.FlowDirection = FlowDirection.TopDown;
            flpProfiles.Location = new Point(0, 46);
            flpProfiles.Margin = new Padding(4, 3, 4, 3);
            flpProfiles.Name = "flpProfiles";
            flpProfiles.Padding = new Padding(6);
            flpProfiles.Size = new Size(810, 100);
            flpProfiles.TabIndex = 1;
            flpProfiles.WrapContents = false;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(810, 146);
            Controls.Add(flpProfiles);
            Controls.Add(pnlTopControls);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Margin = new Padding(4, 3, 4, 3);
            MaximizeBox = false;
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Svc_";
            KeyDown += Form1_KeyDown;
            pnlTopControls.ResumeLayout(false);
            pnlTopControls.PerformLayout();
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel pnlTopControls;
        private System.Windows.Forms.Button btnAddProfile;
        private System.Windows.Forms.Button btnReset;
        private System.Windows.Forms.Button btnHelp;
        private System.Windows.Forms.Button btnCheckUpdate;
        private System.Windows.Forms.Button btnLoadPreset;
        private System.Windows.Forms.CheckBox cbAutoUpdate;
        private System.Windows.Forms.ProgressBar pbDownload;
        private System.Windows.Forms.Label lblDownloadProgress;
        private System.Windows.Forms.FlowLayoutPanel flpProfiles;
    }
}