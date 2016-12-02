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
using System.Runtime.InteropServices;
using System.Media;

//playmusic

namespace Robotis_vsido_connect
{
    public partial class Form1 : Form
    {
        ComboBox comb;      //comポート一覧
        SerialPort serialport;    //しりあるポート
        List<byte> command_list = new List<byte>();

        //上位システム
        string ipOrHost = "127.0.0.1";
        int port = 50377;
        System.Net.Sockets.TcpClient tcp = null;
        string resMsg = null;
        string[] SplittedMes = null;
        Thread TcpReadThread = null;
        NetworkStream ns = null;

        //robotismini-wifi
        string RobotisHost = "192.168.4.1";
        int RobotisPort = 55555;
        Client Cl;

        //他  
        System.Text.Encoding enc = null;
        Thread motion_thread = null;
        Thread MotionThread = null;
        string fullpath = "";
        int timer_counter = 0;
        SoundPlayer soundplayer = null;

        /// <summary>
        /// モーションファイル(.csv)
        /// </summary>
        string motion1file = "dash.csv";
        string motion2file = "byebye.csv";
        string motion3file = "circle.csv";
        string motion4file = "guru.csv";
        string defaultmotion = "default.csv";
        string kickmotion = "kick.csv";
        string stepmotion = "boringstep.csv";
        string dancefile = "dance_all.csv";


        //フラグ
        bool isAction = false;
        bool loopflag = false;
        bool stopflag = false;
        bool tcpflag = false;
        bool boring_flag = false;
        bool iswifi = true;
        bool danceflag = false;


        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            comb = comboBox1;
            comb.SelectedIndex = 13;

            textBox1.Text = Path.GetFileName(motion1file);
            textBox2.Text = Path.GetFileName(motion2file);
            textBox3.Text = Path.GetFileName(motion3file);
            textBox4.Text = Path.GetFileName(motion4file);

            label19.Text = "";

            textBox7.Enabled = true;
            groupBox1.Enabled = false;
            groupBox2.Enabled = false;

            motion_thread = new Thread(FileAnalyze);
            motion_thread.IsBackground = true;
            motion_thread.Priority = System.Threading.ThreadPriority.BelowNormal;

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
                if (!iswifi)
                {
                    serialport.Open();
                }
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
                if (iswifi)
                {
                    try
                    {
                        Cl = new Client(RobotisHost, RobotisPort);
                        Cl.Send(serial_byte);
                    }
                    catch
                    {
                        MessageBox.Show("接続できません", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    serialport.Write(serial_byte, 0, serial_byte.Length);

                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            groupBox1.Enabled = true;


        }

        //切断?
        private void button2_Click(object sender, EventArgs e)
        {
            if (!iswifi)
            {
                serialport.Close();
            }
            else {
                Cl.ClientClose();
            }
            label13.Text = "status: 未接続";
            groupBox1.Enabled = false;
            groupBox2.Enabled = false;
            boringTimer.Enabled = false;
        }

        //受信開始ボタン
        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                tcp = new System.Net.Sockets.TcpClient(ipOrHost, port);
                label8.Text = "Client:" + ipOrHost + " :" + port;
                ns = tcp.GetStream();
                ns.ReadTimeout = Timeout.Infinite;
                ns.WriteTimeout = Timeout.Infinite;
                enc = System.Text.Encoding.UTF8;

                tcpflag = true;

                TcpReadThread = new Thread(TcpRead);
                TcpReadThread.IsBackground = true;
                TcpReadThread.Priority = System.Threading.ThreadPriority.BelowNormal;
                TcpReadThread.Start();

                MotionThread = new Thread(TcpFunc);
                MotionThread.IsBackground = true;
                MotionThread.Priority = System.Threading.ThreadPriority.BelowNormal;
                MotionThread.Start();
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }

            textBox7.Enabled = true;
            groupBox2.Enabled = true;

            //タイマーの作成
            boringTimer.Interval = 1000;
            boringTimer.Tick += new EventHandler(timer_tick);

        }
        private void timer_tick(object Sender, EventArgs e)
        {
            timer_counter++;
            if (timer_counter > int.Parse(textBox7.Text))
            {
                RandomMotion();
                timer_counter = 0;
            }
            label19.Text = timer_counter.ToString() + "s";
        }


        //上位システムから受信する関数
        void TcpRead()
        {
            while (true)
            {
                System.IO.MemoryStream ms = new System.IO.MemoryStream();
                byte[] resBytes = new byte[2048];
                int resSize = 0;
                int s;
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
                s = Cl.read();
            }
        }
        //上位システムへ送る
        void toServerSend(string sendMsg)
        {
            System.Text.Encoding enc = System.Text.Encoding.UTF8;
            byte[] sendBytes = enc.GetBytes(sendMsg + '\n');
            ns.Write(sendBytes, 0, sendBytes.Length);

        }

        //上位システム動作
        void TcpFunc()
        {

            while (true)
            {

                if (resMsg != null && !isAction)
                {
                    timer_counter = 0;
                    SplittedMes = resMsg.Split(';');
                    if (SplittedMes[0] == "0001")  //うつよっ　ばーーん
                    {
                        resMsg = null;
                        timer_counter = 0;
                        string file = motion1file;
                        if (file != "")
                        {
                            motion_thread = new Thread(FileAnalyze);
                            motion_thread.IsBackground = true;
                            motion_thread.Priority = System.Threading.ThreadPriority.BelowNormal;
                            motion_thread.Start(file);
                        }
                    }
                    else if (SplittedMes[0] == "0002") //やっほー
                    {
                        resMsg = null;
                        timer_counter = 0;
                        string file = motion2file;
                        if (file != "")
                        {
                            motion_thread = new Thread(FileAnalyze);
                            motion_thread.IsBackground = true;
                            motion_thread.Priority = System.Threading.ThreadPriority.BelowNormal;
                            motion_thread.Start(file);
                        }
                    }
                    else if (SplittedMes[0] == "0003") //えっ　なんだろー
                    {
                        resMsg = null;
                        timer_counter = 0;
                        string file = motion3file;
                        if (file != "")
                        {
                            motion_thread = new Thread(FileAnalyze);
                            motion_thread.IsBackground = true;
                            motion_thread.Priority = System.Threading.ThreadPriority.BelowNormal;
                            motion_thread.Start(file);
                        }
                    }
                    else if (SplittedMes[0] == "0004") //ぐるぐる
                    {
                        resMsg = null;
                        timer_counter = 0;
                        string file = motion4file;
                        if (file != "")
                        {
                            motion_thread = new Thread(FileAnalyze);
                            motion_thread.IsBackground = true;
                            motion_thread.Priority = System.Threading.ThreadPriority.BelowNormal;
                            motion_thread.Start(file);
                        }
                    }
                    else if (SplittedMes[0] == "0000")
                    {
                        resMsg = null;
                        string file = defaultmotion;
                        if (file != "")
                        {
                            motion_thread = new Thread(FileAnalyze);
                            motion_thread.IsBackground = true;
                            motion_thread.Priority = System.Threading.ThreadPriority.BelowNormal;
                            motion_thread.Start(file);
                        }
                    }
                    resMsg = null;
                }
            }
        }

        void RandomMotion()
        {
            Random rnd_res = new System.Random();
            double r_res = rnd_res.NextDouble();
            //70%の確立で
            if (r_res < 0.7)
            {
                string file = stepmotion;
               
                if (file != "")
                {
                    motion_thread = new Thread(FileAnalyze);
                    motion_thread.IsBackground = true;
                    motion_thread.Priority = System.Threading.ThreadPriority.BelowNormal;
                    motion_thread.Start(file);
                }
            }
            //30%
            else
            {
                string file = kickmotion;
                if (file != "")
                {
                    motion_thread = new Thread(FileAnalyze);
                    motion_thread.IsBackground = true;
                    motion_thread.Priority = System.Threading.ThreadPriority.BelowNormal;
                    motion_thread.Start(file);
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
                textBox6.Text = Path.GetFileName(file);
                fullpath = file;
                motion_thread = new Thread(FileAnalyze);
                motion_thread.IsBackground = true;
                motion_thread.Priority = System.Threading.ThreadPriority.BelowNormal;
                motion_thread.Start(file);
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
                        try
                        {
                            // ストリームの末尾まで繰り返す
                            while (!sr.EndOfStream)
                            {
                                if (tcpflag)
                                {
                                    //上位システム接続時にbusyを送る
                                    toServerSend("busy");
                                }
                                // ファイルから一行読み込む
                                var line = sr.ReadLine();
                                // 読み込んだ一行をカンマ毎に分けて配列に格納する
                                var values = line.Split(',');
                                foreach (var value in values)
                                {
                                    if (value == "ff")
                                    {
                                        command_list = new List<byte>();
                                    }
                                    cnt++;
                                    if (cnt == 4)
                                    {
                                        //コマンドから各モーションの動作時間を算出
                                         sleeptime = Int32.Parse(value, System.Globalization.NumberStyles.HexNumber) * 10;
                                    }
                                    command_list.Add(Convert.ToByte(value, 16));
                                }

                                //コマンドで設定した間隔でv-sido connectに送る
                                byte[] command = command_list.ToArray();
                                if (iswifi)
                                {
                                    Cl.Send(command);
                                }
                                else
                                {
                                    serialport.Write(command, 0, command.Length);
                                }
                                System.Threading.Thread.Sleep(sleeptime); //動作間隔
                                cnt = 0;
                                //中断フラグが立てば終わる
                                if (stopflag)
                                {
                                    stopflag = false;
                                    isAction = false;
                                    if (tcpflag)
                                    {
                                        toServerSend("ready");
                                    }
                                    StopSound();
                                    danceflag = false;
                                    return;
                                }
                            }
                            //ループのチェックボックスが入っていなければ終わる
                            if (!loopflag)
                            {
                               
                                isAction = false;
                                if (tcpflag)
                                {
                                    toServerSend("ready");
                                }
                                StopSound();
                                danceflag = false;
                                return;
                            }
                            PlaySound("Rob_music2.wav");
                        }
                        catch (System.Exception error)
                        {
                            MessageBox.Show(error.Message);
                            isAction = false;
                                   if (tcpflag)
                                    {
                                        toServerSend("ready");
                                    }
                            return;
                        }
                    }
                }
                catch (System.Exception ee)
                {
                    if (tcpflag)
                    {
                        toServerSend("ready");
                    }
                    MessageBox.Show(ee.Message); 
                    isAction = false;
                    return;
                }
            }
        }

        //ループ
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (loopflag)
            {
                loopflag = false;
            }
            else
            {
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
            var values = textBox5.Text.Split(' ');
            foreach (var value in values)
            {
             if (value == "ff")
             { 
                command_list = new List<byte>();
             }
             command_list.Add(Convert.ToByte(value, 16));
             } 
             byte[] command = command_list.ToArray();
            
            if (iswifi)
            {
                Cl.Send(command);
            }
            else
            {
                serialport.Write(command, 0, command.Length);
            }
            System.Threading.Thread.Sleep(Convert.ToInt32(command[3]) * 10); //送信間隔
        }

        //棒立ちボタン    
        private void button7_Click(object sender, EventArgs e)
        {
            string file = defaultmotion;
            if (file != "")
            {

                motion_thread = new Thread(FileAnalyze);
                motion_thread.IsBackground = true;
                motion_thread.Priority = System.Threading.ThreadPriority.BelowNormal;
                motion_thread.Start(file);
            }
        }

        //実行ボタン
        private void DoButton_Click(object sender, EventArgs e)
        {

            if (textBox6.Text == null && !isAction)
            {
                return;
            }
            string file = fullpath;
            motion_thread = new Thread(FileAnalyze);
            motion_thread.IsBackground = true;
            motion_thread.Priority = System.Threading.ThreadPriority.BelowNormal;
            motion_thread.Start(file);
        }
        //wifiのラジオボタン
        private void wifibutton_click(object sender, EventArgs e)
        {
            wifibutton.Checked = true;
            wirebutton.Checked = false;
            label1.Enabled = false;
            comboBox1.Enabled = false;
            iswifi = true;
        }
        //有線のラジオボタン
        private void wirebutton_click(object sender, EventArgs e)
        {
            wifibutton.Checked = false;
            wirebutton.Checked = true;
            label1.Enabled = true;
            comboBox1.Enabled = true;
            iswifi = false;
        }


        private void dance_Button_Click(object sender, EventArgs e)
        {
            danceflag = true;
            PlaySound("Rob_music2.wav");
            string file = dancefile;
            motion_thread = new Thread(FileAnalyze);
            motion_thread.IsBackground = true;
            motion_thread.Priority = System.Threading.ThreadPriority.BelowNormal;
            motion_thread.Start(file);
        }


//BGM関係
        private void PlaySound(string waveFile)
        {
            if (soundplayer != null)
            {
                StopSound();
            }
            soundplayer = new SoundPlayer(waveFile);
            soundplayer.Play();
            //soundplayer.PlaySync();
            //          soundplayer.PlayLooping();
        }
        private void StopSound()
        {
            if (soundplayer != null)
            {
                soundplayer.Stop();
                soundplayer.Dispose();
                soundplayer = null;
                
            }
        }




//上位システム
        //テキストボックスをダブルクリックされたらダイアログを開く
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


        //たいくつ 
        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            if (boring_flag)
            {
                boringTimer.Enabled = false;
                radioButton1.Text = "有効";
                boring_flag = false;
            }
            else
            {
                boringTimer.Enabled = true;
                radioButton1.Text = "無効";
                boring_flag = true;
            }
        }

    }

}
