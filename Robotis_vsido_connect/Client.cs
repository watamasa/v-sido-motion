using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/*ネットワーク宣言*/
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Robotis_vsido_connect
{
    class Client
    {
        TcpClient client=null;
        /*クラス全体でストリームライターが使えるようにする*/
        private StreamWriter sw=null;
        private StreamReader sr=null;

        public Client(string ipAddress, int portNum)
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(ipAddress), portNum);
            client = new TcpClient();

            /*サーバーに接続できたかの判定*/
            try
            {
                client.Connect(ep);     //接続の開始　Connect(繋ぐ)
                Console.WriteLine("接続された");	//接続された時の表示
                NetworkStream ns = client.GetStream();
                sr = new StreamReader(ns);  //読み込み
                sw = new StreamWriter(ns);   //文字コードを指定して送信
                sw.AutoFlush = true;    //一行書き込んだら送信する

                /*マルチスレットを立ち上げる*/
                Task.Factory.StartNew(() => Recive());
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);   //接続できなかった場合の処理
            }
        }

        /*データの送信*/
        public void Send(byte[] data)
        {
            char[] num;

            if (sw != null)
            {
                num = Conversion(data);
                //Console.WriteLine(num);
                sw.Write(num);
            }
        }

        /*送信するbyte列の分解*/
        private char[] Conversion(byte[] data)
        {
            char[] num = new char[data.Length*2];
            int i,j;

            for (i = 0, j = 0; i < data.Length; i++, j += 2)
            {
                num[j] = (char)((data[i] >> 4)+0x10);
                num[j + 1] = (char)((data[i] & 0x0f) + 0x20);
            }

            return num;
        }

        //string str = string.Empty;
        int str;
        Boolean readflg = false;

        /*受信処理*/
        private void Recive()
        {
          /*  do
            {
                str = sr.Read();
                if (str == 0)
                {
                    break;
                }
                Console.Write(str);
                readflg = true;
            } while (true);
        */
            }

        /*データの受信*/
        public int read()
        {
            if(readflg)
            {
                readflg = false;
                return str;
            }
            /*受信がなければnullを返す*/
            return 0;
        }

        /*ソケットのクローズ*/
        public void ClientClose()
        {
            try
            {
               client.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);	//閉じることができなかった時の表示
            }
        }
    }
}
