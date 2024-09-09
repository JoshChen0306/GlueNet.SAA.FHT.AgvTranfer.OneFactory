using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using svrPair;

namespace cTest
{
    public partial class frmMain : Form
    {
        cPair saaService = new cPair();
        public frmMain()
        {
            InitializeComponent();
        }

        private void btnAuto_Click(object sender, EventArgs e)
        {
            if (btnAuto.Text == "AUTO")
            {
                btnAuto.Text = "STOP";//wipService開關
                btnAuto.BackColor = Color.DarkGreen;//wipService開關背景
                tlp2.Visible = false;
                saaService.BgnPair();
                this.Height = 111;  //this.Width = 262;
            }
            else
            {
                btnAuto.Text = "AUTO";
                btnAuto.BackColor = Color.White;
                tlp2.Visible = true;
                saaService.EndPair();                
                this.Height = 199;  //this.Width = 262;
            }
            lblOnLine.BackColor = (btnAuto.Text == "STOP") ? Color.DarkGreen : Color.DarkGray;
            lblOffLine.BackColor = (btnAuto.Text == "AUTO") ? Color.DarkRed : Color.DarkGray;
        }

        private void btnBlockDVisibleT_Click(object sender, EventArgs e)
        {
            saaService.SettingBlockUseFlag("D", "Y");
            MessageBox.Show("生產區 D 啟用");
        }

        private void btnBlockDVisibleF_Click(object sender, EventArgs e)
        {
            saaService.SettingBlockUseFlag("D", "N");
            MessageBox.Show("生產區 D 停用");
        }

        private void btnBlockEVisibleT_Click(object sender, EventArgs e)
        {
            saaService.SettingBlockUseFlag("E", "Y");
            MessageBox.Show("下料區 E 啟用");
        }

        private void btnBlockEVisibleF_Click(object sender, EventArgs e)
        {
            saaService.SettingBlockUseFlag("E", "N");
            MessageBox.Show("下料區 E 停用");
        }
    }
}
