namespace BM
{
    partial class BriefMaker
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
                wcfReceiverHost.Close();
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(BriefMaker));
            this.cbLogErrorChecking = new System.Windows.Forms.CheckBox();
            this.btnRebuildDBfromStreams = new System.Windows.Forms.Button();
            this.panel1 = new System.Windows.Forms.Panel();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabelLastBrfID = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabel2 = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabelMarketOpen = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabel3 = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabelEventCt = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.richTextBox1 = new System.Windows.Forms.RichTextBox();
            this.panel1.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // cbLogErrorChecking
            // 
            this.cbLogErrorChecking.AutoSize = true;
            this.cbLogErrorChecking.Location = new System.Drawing.Point(12, 11);
            this.cbLogErrorChecking.Name = "cbLogErrorChecking";
            this.cbLogErrorChecking.Size = new System.Drawing.Size(241, 17);
            this.cbLogErrorChecking.TabIndex = 22;
            this.cbLogErrorChecking.Tag = "";
            this.cbLogErrorChecking.Text = "Display \'Out of Expected Range Values\'(beta)";
            this.toolTip1.SetToolTip(this.cbLogErrorChecking, "Performs extra checking to make sure that values are within an expected range.");
            this.cbLogErrorChecking.UseVisualStyleBackColor = true;
            // 
            // btnRebuildDBfromStreams
            // 
            this.btnRebuildDBfromStreams.Location = new System.Drawing.Point(269, 3);
            this.btnRebuildDBfromStreams.Name = "btnRebuildDBfromStreams";
            this.btnRebuildDBfromStreams.Size = new System.Drawing.Size(119, 31);
            this.btnRebuildDBfromStreams.TabIndex = 21;
            this.btnRebuildDBfromStreams.Text = "Rebuild from Streams";
            this.toolTip1.SetToolTip(this.btnRebuildDBfromStreams, "This will delete everything from the Briefs table.");
            this.btnRebuildDBfromStreams.UseVisualStyleBackColor = true;
            this.btnRebuildDBfromStreams.Click += new System.EventHandler(this.btnRebuildDBfromStreamMoments_Click);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.progressBar1);
            this.panel1.Controls.Add(this.statusStrip1);
            this.panel1.Controls.Add(this.cbLogErrorChecking);
            this.panel1.Controls.Add(this.btnRebuildDBfromStreams);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panel1.Location = new System.Drawing.Point(0, 538);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(814, 59);
            this.panel1.TabIndex = 23;
            // 
            // progressBar1
            // 
            this.progressBar1.Location = new System.Drawing.Point(403, 6);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(176, 23);
            this.progressBar1.TabIndex = 24;
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabel1,
            this.toolStripStatusLabelLastBrfID,
            this.toolStripStatusLabel2,
            this.toolStripStatusLabelMarketOpen,
            this.toolStripStatusLabel3,
            this.toolStripStatusLabelEventCt});
            this.statusStrip1.Location = new System.Drawing.Point(0, 37);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(814, 22);
            this.statusStrip1.TabIndex = 24;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // toolStripStatusLabel1
            // 
            this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            this.toolStripStatusLabel1.Size = new System.Drawing.Size(48, 17);
            this.toolStripStatusLabel1.Text = "Brief ID:";
            // 
            // toolStripStatusLabelLastBrfID
            // 
            this.toolStripStatusLabelLastBrfID.AutoSize = false;
            this.toolStripStatusLabelLastBrfID.Name = "toolStripStatusLabelLastBrfID";
            this.toolStripStatusLabelLastBrfID.Size = new System.Drawing.Size(100, 17);
            this.toolStripStatusLabelLastBrfID.Text = "---";
            this.toolStripStatusLabelLastBrfID.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // toolStripStatusLabel2
            // 
            this.toolStripStatusLabel2.Name = "toolStripStatusLabel2";
            this.toolStripStatusLabel2.Size = new System.Drawing.Size(79, 17);
            this.toolStripStatusLabel2.Text = "Market Open:";
            // 
            // toolStripStatusLabelMarketOpen
            // 
            this.toolStripStatusLabelMarketOpen.AutoSize = false;
            this.toolStripStatusLabelMarketOpen.Name = "toolStripStatusLabelMarketOpen";
            this.toolStripStatusLabelMarketOpen.Size = new System.Drawing.Size(44, 17);
            this.toolStripStatusLabelMarketOpen.Text = "---";
            this.toolStripStatusLabelMarketOpen.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // toolStripStatusLabel3
            // 
            this.toolStripStatusLabel3.Name = "toolStripStatusLabel3";
            this.toolStripStatusLabel3.Size = new System.Drawing.Size(44, 17);
            this.toolStripStatusLabel3.Text = "Events:";
            // 
            // toolStripStatusLabelEventCt
            // 
            this.toolStripStatusLabelEventCt.AutoSize = false;
            this.toolStripStatusLabelEventCt.Name = "toolStripStatusLabelEventCt";
            this.toolStripStatusLabelEventCt.Size = new System.Drawing.Size(100, 17);
            this.toolStripStatusLabelEventCt.Text = "---";
            this.toolStripStatusLabelEventCt.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // richTextBox1
            // 
            this.richTextBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.richTextBox1.Location = new System.Drawing.Point(0, 0);
            this.richTextBox1.Name = "richTextBox1";
            this.richTextBox1.Size = new System.Drawing.Size(814, 538);
            this.richTextBox1.TabIndex = 24;
            this.richTextBox1.Text = "";
            // 
            // BriefMaker
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(814, 597);
            this.Controls.Add(this.richTextBox1);
            this.Controls.Add(this.panel1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "BriefMaker";
            this.Text = "BriefMaker";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.BriefMaker_FormClosing);
            this.Load += new System.EventHandler(this.BriefMaker_Load);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnRebuildDBfromStreams;
        private System.Windows.Forms.CheckBox cbLogErrorChecking;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelLastBrfID;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel2;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelMarketOpen;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel3;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelEventCt;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.RichTextBox richTextBox1;
    }
}

