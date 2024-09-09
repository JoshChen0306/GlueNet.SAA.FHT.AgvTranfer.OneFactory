namespace cTest
{
    partial class frmMain
    {
        /// <summary>
        /// 設計工具所需的變數。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清除任何使用中的資源。
        /// </summary>
        /// <param name="disposing">如果應該處置 Managed 資源則為 true，否則為 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form 設計工具產生的程式碼

        /// <summary>
        /// 此為設計工具支援所需的方法 - 請勿使用程式碼編輯器修改
        /// 這個方法的內容。
        /// </summary>
        private void InitializeComponent()
        {
            this.tableLayoutPanel9 = new System.Windows.Forms.TableLayoutPanel();
            this.panel9 = new System.Windows.Forms.Panel();
            this.btnAuto = new System.Windows.Forms.Button();
            this.lblOffLine = new System.Windows.Forms.Label();
            this.lblOnLine = new System.Windows.Forms.Label();
            this.btnBlockDVisibleT = new System.Windows.Forms.Button();
            this.btnBlockDVisibleF = new System.Windows.Forms.Button();
            this.btnBlockEVisibleF = new System.Windows.Forms.Button();
            this.btnBlockEVisibleT = new System.Windows.Forms.Button();
            this.tlp2 = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanel9.SuspendLayout();
            this.panel9.SuspendLayout();
            this.tlp2.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel9
            // 
            this.tableLayoutPanel9.CellBorderStyle = System.Windows.Forms.TableLayoutPanelCellBorderStyle.Single;
            this.tableLayoutPanel9.ColumnCount = 3;
            this.tableLayoutPanel9.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 30.04292F));
            this.tableLayoutPanel9.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 31.75966F));
            this.tableLayoutPanel9.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 38.19743F));
            this.tableLayoutPanel9.Controls.Add(this.panel9, 2, 0);
            this.tableLayoutPanel9.Controls.Add(this.lblOffLine, 1, 0);
            this.tableLayoutPanel9.Controls.Add(this.lblOnLine, 0, 0);
            this.tableLayoutPanel9.Location = new System.Drawing.Point(7, 12);
            this.tableLayoutPanel9.Margin = new System.Windows.Forms.Padding(4);
            this.tableLayoutPanel9.Name = "tableLayoutPanel9";
            this.tableLayoutPanel9.RowCount = 1;
            this.tableLayoutPanel9.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel9.Size = new System.Drawing.Size(234, 52);
            this.tableLayoutPanel9.TabIndex = 7;
            // 
            // panel9
            // 
            this.panel9.BackColor = System.Drawing.Color.DarkOrange;
            this.panel9.Controls.Add(this.btnAuto);
            this.panel9.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel9.Location = new System.Drawing.Point(145, 1);
            this.panel9.Margin = new System.Windows.Forms.Padding(0);
            this.panel9.Name = "panel9";
            this.panel9.Size = new System.Drawing.Size(88, 50);
            this.panel9.TabIndex = 2;
            // 
            // btnAuto
            // 
            this.btnAuto.BackColor = System.Drawing.Color.White;
            this.btnAuto.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnAuto.Font = new System.Drawing.Font("微軟正黑體", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.btnAuto.Location = new System.Drawing.Point(0, 0);
            this.btnAuto.Margin = new System.Windows.Forms.Padding(50);
            this.btnAuto.Name = "btnAuto";
            this.btnAuto.Size = new System.Drawing.Size(88, 50);
            this.btnAuto.TabIndex = 0;
            this.btnAuto.Text = "AUTO";
            this.btnAuto.UseVisualStyleBackColor = false;
            this.btnAuto.Click += new System.EventHandler(this.btnAuto_Click);
            // 
            // lblOffLine
            // 
            this.lblOffLine.AutoSize = true;
            this.lblOffLine.BackColor = System.Drawing.Color.DarkRed;
            this.lblOffLine.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblOffLine.Font = new System.Drawing.Font("微軟正黑體", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.lblOffLine.ForeColor = System.Drawing.Color.Yellow;
            this.lblOffLine.Location = new System.Drawing.Point(73, 3);
            this.lblOffLine.Margin = new System.Windows.Forms.Padding(2);
            this.lblOffLine.Name = "lblOffLine";
            this.lblOffLine.Size = new System.Drawing.Size(69, 46);
            this.lblOffLine.TabIndex = 0;
            this.lblOffLine.Text = "停 止\r\n服 務";
            this.lblOffLine.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblOnLine
            // 
            this.lblOnLine.AutoSize = true;
            this.lblOnLine.BackColor = System.Drawing.Color.DimGray;
            this.lblOnLine.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblOnLine.Font = new System.Drawing.Font("微軟正黑體", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.lblOnLine.ForeColor = System.Drawing.Color.Yellow;
            this.lblOnLine.Location = new System.Drawing.Point(3, 3);
            this.lblOnLine.Margin = new System.Windows.Forms.Padding(2);
            this.lblOnLine.Name = "lblOnLine";
            this.lblOnLine.Size = new System.Drawing.Size(65, 46);
            this.lblOnLine.TabIndex = 1;
            this.lblOnLine.Text = "啟 動\r\n服 務";
            this.lblOnLine.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // btnBlockDVisibleT
            // 
            this.btnBlockDVisibleT.BackColor = System.Drawing.Color.ForestGreen;
            this.btnBlockDVisibleT.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnBlockDVisibleT.Location = new System.Drawing.Point(3, 3);
            this.btnBlockDVisibleT.Name = "btnBlockDVisibleT";
            this.btnBlockDVisibleT.Size = new System.Drawing.Size(110, 37);
            this.btnBlockDVisibleT.TabIndex = 2;
            this.btnBlockDVisibleT.Text = "生產D區啟用";
            this.btnBlockDVisibleT.UseVisualStyleBackColor = false;
            this.btnBlockDVisibleT.Click += new System.EventHandler(this.btnBlockDVisibleT_Click);
            // 
            // btnBlockDVisibleF
            // 
            this.btnBlockDVisibleF.BackColor = System.Drawing.Color.Pink;
            this.btnBlockDVisibleF.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnBlockDVisibleF.Location = new System.Drawing.Point(119, 3);
            this.btnBlockDVisibleF.Name = "btnBlockDVisibleF";
            this.btnBlockDVisibleF.Size = new System.Drawing.Size(111, 37);
            this.btnBlockDVisibleF.TabIndex = 4;
            this.btnBlockDVisibleF.Text = "生產D區停用";
            this.btnBlockDVisibleF.UseVisualStyleBackColor = false;
            this.btnBlockDVisibleF.Click += new System.EventHandler(this.btnBlockDVisibleF_Click);
            // 
            // btnBlockEVisibleF
            // 
            this.btnBlockEVisibleF.BackColor = System.Drawing.Color.Pink;
            this.btnBlockEVisibleF.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnBlockEVisibleF.Location = new System.Drawing.Point(119, 46);
            this.btnBlockEVisibleF.Name = "btnBlockEVisibleF";
            this.btnBlockEVisibleF.Size = new System.Drawing.Size(111, 38);
            this.btnBlockEVisibleF.TabIndex = 6;
            this.btnBlockEVisibleF.Text = "下料E區停用";
            this.btnBlockEVisibleF.UseVisualStyleBackColor = false;
            this.btnBlockEVisibleF.Click += new System.EventHandler(this.btnBlockEVisibleF_Click);
            // 
            // btnBlockEVisibleT
            // 
            this.btnBlockEVisibleT.BackColor = System.Drawing.Color.ForestGreen;
            this.btnBlockEVisibleT.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnBlockEVisibleT.Location = new System.Drawing.Point(3, 46);
            this.btnBlockEVisibleT.Name = "btnBlockEVisibleT";
            this.btnBlockEVisibleT.Size = new System.Drawing.Size(110, 38);
            this.btnBlockEVisibleT.TabIndex = 5;
            this.btnBlockEVisibleT.Text = "下料E區啟用";
            this.btnBlockEVisibleT.UseVisualStyleBackColor = false;
            this.btnBlockEVisibleT.Click += new System.EventHandler(this.btnBlockEVisibleT_Click);
            // 
            // tlp2
            // 
            this.tlp2.ColumnCount = 2;
            this.tlp2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlp2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlp2.Controls.Add(this.btnBlockEVisibleF, 1, 1);
            this.tlp2.Controls.Add(this.btnBlockDVisibleT, 0, 0);
            this.tlp2.Controls.Add(this.btnBlockEVisibleT, 0, 1);
            this.tlp2.Controls.Add(this.btnBlockDVisibleF, 1, 0);
            this.tlp2.Font = new System.Drawing.Font("微軟正黑體", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.tlp2.Location = new System.Drawing.Point(7, 71);
            this.tlp2.Name = "tlp2";
            this.tlp2.RowCount = 2;
            this.tlp2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlp2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlp2.Size = new System.Drawing.Size(233, 87);
            this.tlp2.TabIndex = 9;
            this.tlp2.Visible = false;
            // 
            // frmMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(246, 72);
            this.Controls.Add(this.tlp2);
            this.Controls.Add(this.tableLayoutPanel9);
            this.Name = "frmMain";
            this.tableLayoutPanel9.ResumeLayout(false);
            this.tableLayoutPanel9.PerformLayout();
            this.panel9.ResumeLayout(false);
            this.tlp2.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel9;
        private System.Windows.Forms.Panel panel9;
        private System.Windows.Forms.Button btnAuto;
        private System.Windows.Forms.Label lblOffLine;
        private System.Windows.Forms.Label lblOnLine;
        private System.Windows.Forms.Button btnBlockDVisibleT;
        private System.Windows.Forms.Button btnBlockDVisibleF;
        private System.Windows.Forms.Button btnBlockEVisibleF;
        private System.Windows.Forms.Button btnBlockEVisibleT;
        private System.Windows.Forms.TableLayoutPanel tlp2;
    }
}

