using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace Robotis_vsido_connect
{
    public partial class Form1 : Form
    {
        ComboBox comb;      //comポート一覧
        SerialPort serialport;    //しりあるポート
        List<byte> command_list = new List<byte>();
        string textstr;

        Thread TcpReadThread = null;
        Thread MotionThread = null;
        NetworkStream ns = null;
        string ipOrHost = "127.0.0.1";
        //	string ipOrHost = "192.168.1.8";
        //	string ipOrHost = "127.0.0.1";

        int port = 50377;

        System.Text.Encoding enc = null;
        System.Net.Sockets.TcpClient tcp = null;
        string resMsg = null;
        string[] SplittedMes = null;
        string CurrentDir = null;
         Thread test =null;

/// <summary>
/// モーションファイル(.csv)の絶対パス指定
/// </summary>
        string motion1file = @"C:\Users\USER\Desktop\left_hand_up.csv";
        string motion2file = @"C:\Users\USER\Desktop\hand_test_fast.csv";
        string motion3file = @"C:\Users\USER\Desktop\command.csv";
        string motion4file = @"C:\Users\USER\Desktop\test.csv";


        bool isAction = false;
        bool loopflag = false;
        bool stopflag = false;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            comb = comboBox1;
            comb.Items.Add("COM1");
            comb.Items.Add("COM2");
            comb.Items.Add("COM3");
            comb.Items.Add("COM4");
            comb.Items.Add("COM5");
            comb.Items.Add("COM6");
            comb.Items.Add("COM7");
            comb.Items.Add("COM8");
            comb.Items.Add("COM9");
            comb.Items.Add("COM10");
            comb.Items.Add("COM11");
            comb.Items.Add("COM12");
            comb.Items.Add("COM13");
            comb.Items.Add("COM14");
            comb.Items.Add("COM16");
            comb.SelectedIndex = 0;

            CurrentDir = System.IO.Directory.GetCurrentDirectory();

            this.Text = "serial";
            textBox1.Text = Path.GetFileName(motion1file);
            textBox2.Text = Path.GetFileName(motion2file);
            textBox3.Text = Path.GetFileName(motion3file);
            textBox4.Text = Path.GetFileName(motion4file);

            test = new Thread(FileAnalyze);
            test.IsBackground = true;
            test.Priority = System.Threading.ThreadPriority.BelowNormal;
        }

        //接続
        private void button1_Click(object sender, EventArgs e)
        {

            serialport = new SerialPort();
            if (comb.SelectedItem == null)
            {
                MessageBox.Show("COMポートを選択してください", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            serialport.PortName = comb.SelectedItem.ToString();

            serialport.BaudRate = 115200;
            serialport.StopBits = StopBits.One;
            serialport.Parity = Parity.None;
            serialport.DataBits = 8;

            try
            {
                serialport.Open();
                label13.Text = "status: 接続";
            }
            catch
            {
                MessageBox.Show("comが開けません", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);

            }
            //接続コマンド送信
            try
            {
                byte[] serial_byte = new byte[5];
                serial_byte[0] = 0xff;
                serial_byte[1] = 0x67;
                serial_byte[2] = 0x05;
                serial_byte[3] = 0xfe;
                serial_byte[4] = 0x63;

                serialport.Write(serial_byte, 0, serial_byte.Length);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //切断?
        private void button2_Click(object sender, EventArgs e)
        {
            serialport.Close();
            label13.Text = "status: 未接続";
        }

        //受信開始ボタン
        private void button3_Click(object sender, EventArgs e)
        {
            try{
            tcp = new System.Net.Sockets.TcpClient(ipOrHost, port);
            label8.Text = "Client:" + ipOrHost + " :" + port; 
            ns = tcp.GetStream();
            ns.ReadTimeout = Timeout.Infinite;
            ns.WriteTimeout = Timeout.Infinite;
            enc = System.Text.Encoding.UTF8;
            TcpReadThread = new Thread(TcpRead);
            TcpReadThread.IsBackground = true;
            TcpReadThread.Priority = System.Threading.ThreadPriority.BelowNormal;
            TcpReadThread.Start();

            MotionThread = new Thread(TcpFunc);
            MotionThread.IsBackground = true;
            MotionThread.Priority = System.Threading.ThreadPriority.BelowNormal;
            MotionThread.Start();
            }catch(Exception err){
            
            }
        }

        //受信関数
        void TcpRead()
        {
            while (true)
            {
                System.IO.MemoryStream ms = new System.IO.MemoryStream();
                byte[] resBytes = new byte[2048];
                int resSize = 0;
                do
                {
                    resSize = ns.Read(resBytes, 0, resBytes.Length);
                    if (resSize == 0)
                    {
                        TcpReadThread.Abort();
                        break;
                    }
                    ms.Write(resBytes, 0, resSize);
                } while (ns.DataAvailable || resBytes[resSize - 1] != '\n');
                resMsg = enc.GetString(ms.GetBuffer(), 0, (int)ms.Length);
                ms.Close();
                resMsg = resMsg.TrimEnd('\n');
               SplittedMes = resMsg.Split(';');
                label15.Text = SplittedMes[1];
            }
        }
    //ハンドモーション動作
        void TcpFunc()
        {

            while (true)
            {

                if (resMsg != null && !isAction)
                {
                    SplittedMes = resMsg.Split(';');
                    if (SplittedMes[0] == "0001")  //うつよっ　ばーーん
                    {
                        resMsg = null;
                        string file = motion1file;
                        if (file != ""){
                            test = new Thread(FileAnalyze);
                            test.IsBackground = true;
                            test.Priority = System.Threading.ThreadPriority.BelowNormal;
                            test.Start(file);
                        }
                    }
                    else if (SplittedMes[0] == "0002") //やっほー
                    {
                        resMsg = null;
                        string file = motion2file;
                        if (file != "")
                        {
                            test = new Thread(FileAnalyze);
                            test.IsBackground = true;
                            test.Priority = System.Threading.ThreadPriority.BelowNormal;
                            test.Start(file);
                        }
                   }
                    else if (SplittedMes[0] == "0003") //えっ　なんだろー
                    {
                        resMsg = null;
                        string file = motion3file;
                        if (file != "")
                        {
                            test = new Thread(FileAnalyze);
                            test.IsBackground = true;
                            test.Priority = System.Threading.ThreadPriority.BelowNormal;
                            test.Start(file);
                        }
                    }
                    else if (SplittedMes[0] == "0004") //ぐるぐる
                    {
                        resMsg = null;
                        string file = motion4file;
                        if (file != "")
                        {
                            test = new Thread(FileAnalyze);
                            test.IsBackground = true;
                            test.Priority = System.Threading.ThreadPriority.BelowNormal;
                            test.Start(file);
                        }
                    }
                    else if (SplittedMes[0] == "0000") { }
                    //初期？
                    isAction = false;
                    resMsg = null;
                }
            }
        }

//ファイルから開く
        private void button4_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.InitialDirectory = @"C:\";
            ofd.Filter = "CSVファイル|*.csv";
            ofd.FilterIndex = 0;
            ofd.Title = "開くファイルを選択してください";
            ofd.CheckFileExists = true;


            if (ofd.ShowDialog() == DialogResult.OK)
            {
                string file = ofd.FileName;
                test = new Thread(FileAnalyze);
                test.IsBackground = true;
                test.Priority = System.Threading.ThreadPriority.BelowNormal;
                test.Start(file);
            }
        }

//csvからモーションコマンド送信
        void FileAnalyze(object filename)
        {
            int cnt = 0;
            int sleeptime = 0;
            isAction = true;
            while (true)
            {
                try
                {
                    // csvファイルを開く
                    using (var sr = new System.IO.StreamReader((string)filename))
                    {
                        // ストリームの末尾まで繰り返す
                        while (!sr.EndOfStream)
                        {
                            // ファイルから一行読み込む
                            var line = sr.ReadLine();
                            // 読み込んだ一行をカンマ毎に分けて配列に格納する
                            var values = line.Split(',');
                            foreach (var value in values)
                            {
                                if (value == "ff"){
                                    command_list = new List<byte>();
                                }
                                cnt++;
                                if (cnt == 4) {
                                    sleeptime = Convert.ToInt32(value) * 10;
                                }
                                command_list.Add(Convert.ToByte(value, 16));
                            }

                            //コマンドで設定した間隔でv-sido connectに送る
                            byte[] command = command_list.ToArray();
                            serialport.Write(command, 0, command.Length);
                            System.Threading.Thread.Sleep(sleeptime); //送信間隔
                            label17.Text = sleeptime.ToString() + "ms";
                            cnt = 0;
                            //中断フラグが立てば終わる
                            if (stopflag)
                            {
                                stopflag = false;
                                isAction = false;
                                return;
                            }
                        }
                        //ループのチェックボックスが入っていなければ終わる
                        if (!loopflag) {
                            isAction = false;
                            return;
                        }
                    }
                }
                catch (System.Exception ee)
                {
                    // ファイルを開くのに失敗したとき
                    MessageBox.Show(ee.ToString(), "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        //ループ
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (loopflag){
                loopflag = false;
            }
            else{
                loopflag = true;
            }
        }

        //中止
        private void button5_Click(object sender, EventArgs e)
        {
            stopflag = true;
        }

        //フリーコマンド
        private void button6_Click(object sender, EventArgs e)
        {
            byte[] command = System.Text.Encoding.ASCII.GetBytes(textBox5.Text);
            serialport.Write(command, 0, command.Length);
            System.Threading.Thread.Sleep(Convert.ToInt32(command[3]) * 10); //送信間隔
        }

         private void textBox1_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.InitialDirectory = @"C:\";
            ofd.Filter = "CSVファイル|*.csv";
            ofd.FilterIndex = 0;
            ofd.Title = "開くファイルを選択してください";
            ofd.CheckFileExists = true;

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                motion1file = ofd.FileName;
                textBox1.Text = Path.GetFileName(motion1file);
            }
        }
         private void textBox2_Click(object sender, EventArgs e)
         {
             OpenFileDialog ofd = new OpenFileDialog();
             ofd.InitialDirectory = @"C:\";
             ofd.Filter = "CSVファイル|*.csv";
             ofd.FilterIndex = 0;
             ofd.Title = "開くファイルを選択してください";
             ofd.CheckFileExists = true;

             if (ofd.ShowDialog() == DialogResult.OK)
             {
                 motion2file = ofd.FileName;
                 textBox2.Text = Path.GetFileName(motion2file);
             }
         }
         private void textBox3_Click(object sender, EventArgs e)
         {
             OpenFileDialog ofd = new OpenFileDialog();
             ofd.InitialDirectory = @"C:\";
             ofd.Filter = "CSVファイル|*.csv";
             ofd.FilterIndex = 0;
             ofd.Title = "開くファイルを選択してください";
             ofd.CheckFileExists = true;

             if (ofd.ShowDialog() == DialogResult.OK)
             {
                 motion3file = ofd.FileName;
                 textBox3.Text = Path.GetFileName(motion3file);
             }
         }
         private void textBox4_Click(object sender, EventArgs e)
         {
             OpenFileDialog ofd = new OpenFileDialog();
             ofd.InitialDirectory = @"C:\";
             ofd.Filter = "CSVファイル|*.csv";
             ofd.FilterIndex = 0;
             ofd.Title = "開くファイルを選択してください";
             ofd.CheckFileExists = true;

             if (ofd.ShowDialog() == DialogResult.OK)
             {
                 motion4file = ofd.FileName;
                 textBox4.Text = Path.GetFileName(motion4file);
             }
         }

    }

}
