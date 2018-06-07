﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WinTestJD
{
    public partial class Form1 : Form
    {
        private HeartService heartService;
        public Form1()
        {
            InitializeComponent();
        }

        //启动心跳,白名单更新
        private void button3_Click(object sender, EventArgs e)
        {
            heartService = new HeartService();
            heartService.Start();
        }


        //更新车位总数
        private void button1_Click(object sender, EventArgs e)
        {
            if (heartService == null)
                return;
            heartService.UpdateParkTotalCount();

        }

        //更新剩余车位数
        private void button2_Click(object sender, EventArgs e)
        {
            if (heartService == null)
                return;
            heartService.UpdateParkRemainCount();



        }




    }
}
